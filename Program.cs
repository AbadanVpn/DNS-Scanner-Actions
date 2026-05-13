using System.Net;
using System.Net.Sockets;
using System.Text;

string domain = Environment.GetEnvironmentVariable("INPUT_DOMAIN") ?? "google.com";
string startIpStr = Environment.GetEnvironmentVariable("INPUT_START_IP") ?? "192.168.1.1";
string endIpStr = Environment.GetEnvironmentVariable("INPUT_END_IP") ?? "192.168.1.254";

if (!IPAddress.TryParse(startIpStr, out IPAddress startIp) || !IPAddress.TryParse(endIpStr, out IPAddress endIp))
{
    Console.WriteLine("Invalid IP range");
    return;
}

uint start = IpToUint(startIp);
uint end = IpToUint(endIp);
if (start > end)
{
    Console.WriteLine("Start IP must be less than or equal End IP");
    return;
}

Console.WriteLine($"Scanning DNS servers from {startIpStr} to {endIpStr} for domain '{domain}'...");
Console.WriteLine("--------------------------------------------------");

List<IPAddress> range = new List<IPAddress>();
for (uint ip = start; ip <= end; ip++)
    range.Add(UintToIp(ip));

int maxConcurrent = 20;
int completed = 0;
object lockObj = new object();
using var semaphore = new SemaphoreSlim(maxConcurrent);
var tasks = range.Select(ip => Task.Run(async () =>
{
    await semaphore.WaitAsync();
    try
    {
        var result = await QueryDnsAsync(ip, domain, 1000);
        lock (lockObj)
        {
            if (result.Success)
                Console.WriteLine($"✓ {ip} - {result.ResponseTimeMs}ms -> {string.Join(", ", result.ResolvedIPs)}");
            else
                Console.WriteLine($"✗ {ip} - {result.Error}");
        }
    }
    finally
    {
        Interlocked.Increment(ref completed);
        semaphore.Release();
    }
})).ToList();

await Task.WhenAll(tasks);
Console.WriteLine("--------------------------------------------------");
Console.WriteLine($"Scan completed. Total IPs scanned: {range.Count}");

static uint IpToUint(IPAddress ip)
{
    byte[] bytes = ip.GetAddressBytes();
    return (uint)(bytes[0] << 24 | bytes[1] << 16 | bytes[2] << 8 | bytes[3]);
}

static IPAddress UintToIp(uint ip)
{
    return new IPAddress(new byte[] { (byte)(ip >> 24), (byte)(ip >> 16), (byte)(ip >> 8), (byte)ip });
}

static async Task<DnsResult> QueryDnsAsync(IPAddress dnsServer, string domain, int timeoutMs)
{
    byte[] query = BuildQuery(domain);
    using var udp = new UdpClient();
    var endpoint = new IPEndPoint(dnsServer, 53);
    var sw = System.Diagnostics.Stopwatch.StartNew();
    try
    {
        var sendTask = udp.SendAsync(query, query.Length, endpoint);
        var delayTask = Task.Delay(timeoutMs);
        if (await Task.WhenAny(sendTask, delayTask) == delayTask)
            throw new TimeoutException();
        var receiveTask = udp.ReceiveAsync();
        if (await Task.WhenAny(receiveTask, Task.Delay(timeoutMs)) == receiveTask)
        {
            var response = receiveTask.Result.Buffer;
            sw.Stop();
            var ips = ParseResponse(response);
            return new DnsResult { Server = dnsServer, Success = ips.Count > 0, ResolvedIPs = ips, ResponseTimeMs = sw.ElapsedMilliseconds };
        }
        throw new TimeoutException();
    }
    catch (Exception ex)
    {
        sw.Stop();
        return new DnsResult { Server = dnsServer, Success = false, Error = ex.Message, ResponseTimeMs = sw.ElapsedMilliseconds };
    }
}

static byte[] BuildQuery(string domain)
{
    using var ms = new MemoryStream();
    using var writer = new BinaryWriter(ms);
    ushort id = (ushort)new Random().Next(1, 65000);
    writer.Write(IPAddress.HostToNetworkOrder((short)id));
    writer.Write((ushort)0x0100);
    writer.Write((ushort)1);
    writer.Write((ushort)0);
    writer.Write((ushort)0);
    writer.Write((ushort)0);
    foreach (string label in domain.Split('.'))
    {
        writer.Write((byte)label.Length);
        writer.Write(Encoding.ASCII.GetBytes(label));
    }
    writer.Write((byte)0);
    writer.Write(IPAddress.HostToNetworkOrder((short)1));
    writer.Write(IPAddress.HostToNetworkOrder((short)1));
    return ms.ToArray();
}

static List<string> ParseResponse(byte[] data)
{
    var addresses = new List<string>();
    if (data.Length < 12) return addresses;
    int ancount = IPAddress.NetworkToHostOrder(BitConverter.ToInt16(data, 6));
    if (ancount == 0) return addresses;
    int pos = 12;
    while (pos < data.Length && data[pos] != 0) pos++;
    pos += 5;
    for (int i = 0; i < ancount && pos + 10 < data.Length; i++)
    {
        if ((data[pos] & 0xC0) == 0xC0) pos += 2;
        else while (data[pos] != 0) pos++; pos++;
        pos += 8;
        int rdlength = IPAddress.NetworkToHostOrder(BitConverter.ToInt16(data, pos));
        pos += 2;
        if (rdlength == 4 && pos + 4 <= data.Length)
        {
            var ip = new IPAddress(data.Skip(pos).Take(4).ToArray());
            addresses.Add(ip.ToString());
        }
        pos += rdlength;
    }
    return addresses;
}

class DnsResult
{
    public IPAddress Server { get; set; }
    public bool Success { get; set; }
    public string Error { get; set; } = "";
    public List<string> ResolvedIPs { get; set; } = new List<string>();
    public long ResponseTimeMs { get; set; }
}
