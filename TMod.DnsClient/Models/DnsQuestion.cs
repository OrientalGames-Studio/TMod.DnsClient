using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TMod.DnsClient.Models
{
    internal class DnsQuestion
    {
        public string Name { get; set; } = "";
        public DnsRecordType Type { get; set; }
        public DnsRecordDirection Class { get; set; }
    }
}
