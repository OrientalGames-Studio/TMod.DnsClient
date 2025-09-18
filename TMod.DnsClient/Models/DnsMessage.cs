//-----------------------------------------------------------------------
// <copyright company="纳米维景">
// 版权所有(C) 2025, 纳米维景(上海)医疗科技有限公司
// </copyright>
//-----------------------------------------------------------------------
// <summary>
//     修改日期           版本号       创建人
// </summary>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TMod.DnsClient.Models
{
    public class DnsMessage
    {
        public DnsHeader Header { get; set; }

        internal List<DnsQuestion> Questions { get; set; } = [];

        public List<DnsRecord> Answers { get; set; } = [];

        public List<DnsRecord> Authorities { get; set; } = [];

        public List<DnsRecord> Additionals { get; set; } = [];
    }
}
