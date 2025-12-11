using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

using TMod.DnsClient.Models;
using TMod.DnsClient.Utils;

namespace TMod.DnsClient
{
    public class DnsQueryClient
    {
        [Obsolete()]
        private readonly string? _dnsServer;
        private readonly int _timeout;
        private readonly IEnumerable<string> _dnsServers;

        public DnsQueryClient():this("8.8.8.8")
        {

        }

        public DnsQueryClient(string dnsServer,int timeout = 3000)
        {
            //_dnsServer = dnsServer;
            _timeout = timeout;
            ArgumentNullException.ThrowIfNullOrWhiteSpace(dnsServer, nameof(dnsServer));
            _dnsServers = [dnsServer];
        }

        public DnsQueryClient(IEnumerable<string> dnsServers,int timeout = 3000)
        {
            ArgumentNullException.ThrowIfNull(dnsServers, nameof(dnsServers));
            if (!dnsServers.Any())
            {
                throw new ArgumentException("At least one DNS server must be provided", nameof(dnsServers));
            }
            _dnsServers = dnsServers;
            _timeout = timeout;
        }

        public async Task<DnsMessage> QueryAsync(string domain, DnsRecordType recordType,CancellationToken cancellationToken = default)
        {
            if(recordType == DnsRecordType.ANY)
            {
                throw new ArgumentException("Use QueryAllAsync for ANY query");
            }
            byte[] query = BuildDnsQuery(domain, recordType);
            using var udp = new System.Net.Sockets.UdpClient();
            udp.Client.ReceiveTimeout = 5000;
            UdpReceiveResult response = default;
            CancellationTokenSource timeoutToken = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            foreach ( var dnsServer in _dnsServers ?? [] )
            {
                try
                {
                    timeoutToken.CancelAfter(5000);
                    await udp.SendAsync(new ReadOnlyMemory<byte>(query), dnsServer, 53, timeoutToken.Token);
                    timeoutToken = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    timeoutToken.CancelAfter(5000);
                    response = await udp.ReceiveAsync(timeoutToken.Token);
                    break;
                }
                catch ( Exception )
                {
                    continue;
                }
            }
            if(response == default || response.Buffer.Length == 0)
            {
                throw new Exception("All Dns servers query failed");
            }
            // 在解析前先检查 header 的 flags / counts，判断是否被截断
            byte[] responseBuffer = response.Buffer;
            ushort flags = BinaryPrimitives.ReadUInt16BigEndian(responseBuffer.AsSpan(2, 2));
            const ushort TC_MASK = 0x0200;
            if ( ( flags & TC_MASK ) != 0 )
            {
                // UDP 数据被截断，改用 TCP 重试
                timeoutToken = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutToken.CancelAfter(5000);
                responseBuffer = await QueryTcpAsync(query, timeoutToken.Token);
            }
            return ParseDnsResponse(responseBuffer);
        }

        private async Task<byte[]> QueryTcpAsync(byte[] query, CancellationToken cancellationToken = default)
        {
            using var tcp = new TcpClient();
            await tcp.ConnectAsync(_dnsServer, 53);
            using var ns = tcp.GetStream();
            // 发送长度前缀 + query
            var lenPrefix = new byte[2];
            BinaryPrimitives.WriteUInt16BigEndian(lenPrefix.AsSpan(), ( ushort )query.Length);
            await ns.WriteAsync(lenPrefix);
            await ns.WriteAsync(query);

            // 读取两字节长度，然后读完整应答
            await ReadExactAsync(ns, lenPrefix, 0, 2);
            int respLen = BinaryPrimitives.ReadUInt16BigEndian(lenPrefix);
            var resp = new byte[respLen];
            await ReadExactAsync(ns, resp, 0, respLen);
            return resp;
        }

        private static async Task ReadExactAsync(NetworkStream ns, byte[] buffer, int offset, int count)
        {
            int got = 0;
            while ( got < count )
            {
                int r = await ns.ReadAsync(buffer, offset + got, count - got);
                if ( r <= 0 ) throw new EndOfStreamException("Unexpected EOF while reading TCP DNS response");
                got += r;
            }
        }

        public async IAsyncEnumerable<DnsMessage> QueryAllAsync(string domain,[EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var recordTypes = Enum.GetValues<DnsRecordType>().Where(t => t != DnsRecordType.ANY);
            foreach ( var item in recordTypes )
            {
                yield return await QueryAsync(domain, item, cancellationToken);
            }
        }

        private byte[] BuildDnsQuery(string domain,DnsRecordType recordType,bool useEdns = true,ushort ednsUdpSize = 4096)
        {
            Random random = Random.Shared;
            ushort id = (ushort)random.Next(0, ushort.MaxValue);

            // header: 12 bytes
            var header = new byte[12];
            BinaryPrimitives.WriteUInt16BigEndian(header.AsSpan(0, 2), id);
            BinaryPrimitives.WriteUInt16BigEndian(header.AsSpan(2, 2), 0x0100); // RD = 1
            BinaryPrimitives.WriteUInt16BigEndian(header.AsSpan(4, 2), 1); // QDCOUNT = 1
            BinaryPrimitives.WriteUInt16BigEndian(header.AsSpan(6, 2), 0); // ANCOUNT
            BinaryPrimitives.WriteUInt16BigEndian(header.AsSpan(8, 2), 0); // NSCOUNT
            BinaryPrimitives.WriteUInt16BigEndian(header.AsSpan(10, 2), useEdns ? ( ushort )1 : ( ushort )0); // ARCOUNT

            using var ms = new MemoryStream();
            ms.Write(header, 0, header.Length);

            // question
            foreach ( var label in domain.Split('.', StringSplitOptions.RemoveEmptyEntries) )
            {
                var bytes = Encoding.ASCII.GetBytes(label);
                ms.WriteByte(( byte )bytes.Length);
                ms.Write(bytes, 0, bytes.Length);
            }
            ms.WriteByte(0); // end
            var qtype = new byte[2];
            BinaryPrimitives.WriteUInt16BigEndian(qtype, ( ushort )recordType);
            ms.Write(qtype);
            var qclass = new byte[2];
            BinaryPrimitives.WriteUInt16BigEndian(qclass, ( ushort )DnsRecordDirection.In);
            ms.Write(qclass);

            // OPT (EDNS) as additional if requested
            if ( useEdns )
            {
                // NAME = root (0)
                // TYPE = 41 (OPT)
                // CLASS = UDP payload size (e.g., 4096)
                // TTL = extended RCODE/flags (0)
                // RDLENGTH = 0
                var opt = new byte[11];
                opt[0] = 0;
                BinaryPrimitives.WriteUInt16BigEndian(opt.AsSpan(1, 2), ( ushort )DnsRecordType.OPT);
                BinaryPrimitives.WriteUInt16BigEndian(opt.AsSpan(3, 2), ednsUdpSize);
                BinaryPrimitives.WriteUInt32BigEndian(opt.AsSpan(5, 4), 0);
                BinaryPrimitives.WriteUInt16BigEndian(opt.AsSpan(9, 2), 0);
                ms.Write(opt, 0, opt.Length);
            }

            return ms.ToArray();
        }

        private DnsMessage ParseDnsResponse(byte[] payload)
        {
            DnsMessage result = new();
            int offset = 0;
            if ( payload.Length < 12 ) throw new InvalidDataException("DNS response too short");

            // Header
            DnsHeader header = new DnsHeader();
            header.Id = BinaryPrimitives.ReadUInt16BigEndian(payload.AsSpan(offset));
            offset += 2;
            header.Flags = BinaryPrimitives.ReadUInt16BigEndian(payload.AsSpan(offset));
            offset += 2;
            header.QdCount = BinaryPrimitives.ReadUInt16BigEndian(payload.AsSpan(offset));
            offset += 2;
            header.AnCount = BinaryPrimitives.ReadUInt16BigEndian(payload.AsSpan(offset));
            offset += 2;
            header.NsCount = BinaryPrimitives.ReadUInt16BigEndian(payload.AsSpan(offset));
            offset += 2;
            header.ArCount = BinaryPrimitives.ReadUInt16BigEndian(payload.AsSpan(offset));
            offset += 2;
            result.Header = header;

            // Questions
            for (int i = 0; i < header.QdCount; i++)
            {
                string name = DnsMessageParser.ParseName(payload, offset, out offset);
                DnsRecordType qtype = (DnsRecordType)BinaryPrimitives.ReadUInt16BigEndian(payload.AsSpan(offset));
                offset += 2;
                DnsRecordDirection qclass = (DnsRecordDirection)BinaryPrimitives.ReadUInt16BigEndian(payload.AsSpan(offset));
                offset += 2;
                result.Questions.Add(new DnsQuestion() { Name = name,Type = qtype,Class = qclass});
            }

            int total = result.Header.AnCount + result.Header.NsCount + result.Header.ArCount;

            result.Answers.AddRange(ParseDnsRecords(payload, header.AnCount, ref offset));
            result.Authorities.AddRange(ParseDnsRecords(payload, header.NsCount, ref offset));
            result.Additionals.AddRange(ParseDnsRecords(payload, header.ArCount, ref offset));
            return result;
        }

        private List<DnsRecord> ParseDnsRecords(byte[] payload,int count, ref int offset)
        {
            List<DnsRecord> records = new List<DnsRecord>();
            for (int i = 0; i < count; i++)
            {
                string name = DnsMessageParser.ParseName(payload, offset, out offset);
                DnsRecordType type = (DnsRecordType)BinaryPrimitives.ReadUInt16BigEndian(payload.AsSpan(offset));
                offset += 2;
                DnsRecordDirection direction = (DnsRecordDirection)BinaryPrimitives.ReadUInt16BigEndian(payload.AsSpan(offset));
                offset += 2;
                uint ttl = BinaryPrimitives.ReadUInt32BigEndian(payload.AsSpan(offset));
                offset += 4;
                ushort rdlength = BinaryPrimitives.ReadUInt16BigEndian(payload.AsSpan(offset));
                offset += 2;
                int rdataOffset = offset;
                byte[] rdata = payload.Skip(offset).Take(rdlength).ToArray();
                offset += rdlength;
                records.Add(new DnsRecord(name, type, direction, ttl, rdata,payload,rdataOffset, rdlength));
            }
            return records;
        }
    }
}
