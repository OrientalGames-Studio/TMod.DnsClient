using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TMod.DnsClient.Models
{
    public struct DnsHeader
    {
        public ushort Id { get; set; }
        public ushort Flags { get; set; }
        public ushort QdCount { get; set; }
        public ushort AnCount { get; set; }
        public ushort NsCount { get; set; }
        public ushort ArCount { get; set; }
    }
}
