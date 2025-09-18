using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

using TMod.DnsClient.Utils;

using static System.Runtime.InteropServices.JavaScript.JSType;

namespace TMod.DnsClient.Models
{
    [DebuggerDisplay("{Name}={GetDnsRecordValue()}({RecordType.ToString()})")]
    public record DnsRecord(string Name
        ,DnsRecordType RecordType
        ,DnsRecordDirection Direction
        ,uint Ttl
        , byte[] RawData
        , byte[] Payload
        , int RawdataOffset
        , int RawdataLen)
    {
        public string GetDnsRecordValue()
        {
            try
            {
                return RecordType switch { 
                    DnsRecordType.A => new IPAddress(new ReadOnlySpan<byte>(Payload, RawdataOffset, 4)).ToString(),
                    DnsRecordType.AAAA => new IPAddress(new ReadOnlySpan<byte>(Payload, RawdataOffset, 16)).ToString(),
                    DnsRecordType.CNAME or DnsRecordType.NS or DnsRecordType.PTR => DnsMessageParser.ParseName(Payload, RawdataOffset, out _),
                    DnsRecordType.MX => $"{BinaryPrimitives.ReadUInt16BigEndian(Payload.AsSpan(RawdataOffset, 2))} {DnsMessageParser.ParseName(Payload, RawdataOffset+2, out _)}",
                    DnsRecordType.TXT => ParseTxt(Payload, RawdataOffset, RawdataLen),
                    DnsRecordType.SOA => ParseSOA(Payload,RawdataOffset,RawdataLen),
                    DnsRecordType.SRV => ParseSRV(Payload, RawdataOffset),
                    DnsRecordType.OPT => $"OPT raw len={RawdataLen}",
                    _ => BitConverter.ToString(Payload,RawdataOffset,RawdataLen)
                };
            }
            catch
            {
                return BitConverter.ToString(RawData);
            }
        }

        private string ParseSRV(byte[] payload, int rawdataOffset)
        {
            ushort pr = BinaryPrimitives.ReadUInt16BigEndian(payload.AsSpan(rawdataOffset, 2));
            ushort weight = BinaryPrimitives.ReadUInt16BigEndian(payload.AsSpan(rawdataOffset + 2, 2));
            ushort port = BinaryPrimitives.ReadUInt16BigEndian(payload.AsSpan(rawdataOffset + 4, 2));
            string target = DnsMessageParser.ParseName(payload, rawdataOffset + 6, out _);
            return $"{pr} {weight} {port} {target}";
        }

        private string ParseSOA(byte[] payload, int rawdataOffset,int rawDataLen)
        {
            int off = rawdataOffset;
            string mname = DnsMessageParser.ParseName(payload, off, out off);
            string rname = DnsMessageParser.ParseName(payload, off, out off);
            if ( off + 20 <= rawdataOffset + rawDataLen )
            {
                uint serial = BinaryPrimitives.ReadUInt32BigEndian(payload.AsSpan(off, 4)); off += 4;
                uint refresh = BinaryPrimitives.ReadUInt32BigEndian(payload.AsSpan(off, 4)); off += 4;
                uint retry = BinaryPrimitives.ReadUInt32BigEndian(payload.AsSpan(off, 4)); off += 4;
                uint expire = BinaryPrimitives.ReadUInt32BigEndian(payload.AsSpan(off, 4)); off += 4;
                uint minimum = BinaryPrimitives.ReadUInt32BigEndian(payload.AsSpan(off, 4));
                return $"{mname} {rname} {serial} {refresh} {retry} {expire} {minimum}";
            }
            return $"{mname} {rname}";
        }

        private string ParseTxt(byte[] payload, int rawdataOffset, int rawdataLen)
        {
            var parts = new List<string>();
            int idx = 0;
            while ( idx < rawdataLen )
            {
                int len = payload[rawdataOffset + idx];
                idx++;
                if ( len > 0 )
                {
                    parts.Add(Encoding.UTF8.GetString(payload, rawdataOffset + idx, len));
                    idx += len;
                }
            }
            return string.Join("", parts);
        }

        private string ParseSRV()
        {
            ushort priority = BinaryPrimitives.ReadUInt16BigEndian(RawData.AsSpan(0,2));
            ushort weight = BinaryPrimitives.ReadUInt16BigEndian(RawData.AsSpan(2,2));
            ushort port = BinaryPrimitives.ReadUInt16BigEndian(RawData.AsSpan(4,2));
            string target = DnsMessageParser.ParseName(RawData, 6, out _);
            return $"{priority} {weight} {port} {target}";
        }

        private string ParseSOA()
        {
            int offset = 0;
            string mname = DnsMessageParser.ParseName(RawData, offset, out offset);
            string rname = DnsMessageParser.ParseName(RawData,offset, out offset);
            uint serial = BinaryPrimitives.ReadUInt32BigEndian(RawData.AsSpan(offset));
            offset+= 4;
            uint refresh = BinaryPrimitives.ReadUInt32BigEndian(RawData.AsSpan(offset));
            offset+= 4;
            uint retry = BinaryPrimitives.ReadUInt32BigEndian(RawData.AsSpan(offset));
            offset+= 4;
            uint expire = BinaryPrimitives.ReadUInt32BigEndian(RawData.AsSpan(offset));
            offset+= 4;
            uint minimum = BinaryPrimitives.ReadUInt32BigEndian(RawData.AsSpan(offset));
            return $"{mname} {rname} {serial} {refresh} {retry} {expire} {minimum}";
        }
    }
}
