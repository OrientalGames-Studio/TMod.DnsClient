# Dns Client

A client for resolve the host's dns records

----

## Usage

``` c#

using TMod.DnsClient;

DnsQueryClient queryClient = new DnsQueryClient();

var records = queryClient.QueryAllAsync("Some domain. (e.g: www.baidu.com)");
await foreach(var record in records)
{
   foreach(var rr in record.Answers.Concat(record.Authorities).Concat(record.Additionals))
   {
      Console.WriteLine($"{rr.Name} parses out a record:{rr.GetDnsRecordValue()}({rr.RecordType})")
   }
}

```

or

``` c#
using TMod.DnsClient;
using TMod.DnsClient.Models;

DnsQueryClient queryClient = new DnsQueryClient();
// for IPv4
var record = await queryClient.QueryAsync("Some domain. (e.g: www.baidu.com)", DnsRecordType.A);
foreach(var rr in record.Answers.Concat(record.Authorities).Concat(record.Additionals))
{
   Console.WriteLine($"{rr.Name} parses out a record:{rr.GetDnsRecordValue()}({rr.RecordType})")
}

// for IPv6
record = await queryClient.QueryAsync("Some domain. (e.g: www.baidu.com)", DnsRecordType.AAAA);
foreach(var rr in record.Answers.Concat(record.Authorities).Concat(record.Additionals))
{
   Console.WriteLine($"{rr.Name} parses out a record:{rr.GetDnsRecordValue()}({rr.RecordType})")
}

// for TXT
record = await queryClient.QueryAsync("Some domain. (e.g: www.baidu.com)", DnsRecordType.TXT);
foreach(var rr in record.Answers.Concat(record.Authorities).Concat(record.Additionals))
{
   Console.WriteLine($"{rr.Name} parses out a record:{rr.GetDnsRecordValue()}({rr.RecordType})")
}

// etc ...
```
