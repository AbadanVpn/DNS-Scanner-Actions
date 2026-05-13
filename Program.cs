using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.IO;

string domain = Environment.GetEnvironmentVariable("INPUT_DOMAIN") ?? "google.com";
string ipsFilePath = "ips.txt";  // فایل حاوی آی‌پی‌ها (هر خط یک آی‌پی)

List<IPAddress> targetIps = new List<IPAddress>();

if (File.Exists(ipsFilePath))
{
    var lines = File.ReadAllLines(ipsFilePath);
    foreach (var line in lines)
    {
        string trimmed = line.Trim();
        if (!string.IsNullOrWhiteSpace(trimmed) && IPAddress.TryParse(trimmed, out IPAddress ip))
            targetIps.Add(ip);
        else if (!string.IsNullOrWhiteSpace(trimmed))
            Console.WriteLine($"⚠️ Invalid IP ignored: {trimmed}");
    }
    if (targetIps.Count == 0)
    {
        Console.WriteLine("No valid IPs found in ips.txt. Exiting.");
        return;
    }
    Console.WriteLine($"Loaded {targetIps.Count} IP(s) from ips.txt:");
    foreach (var ip in targetIps) Console.WriteLine($"  - {ip}");
}
else
{
    Console.WriteLine("File ips.txt not found. Please create it with one IP per line.");
    return;
}

// ادامه اسکن (بدون تغییر از قبل)
string logFile = "scan_output.txt";
using StreamWriter fileWriter = new StreamWriter(logFile);
Console.SetOut(new MultiTextWriter(Console.Out, fileWriter));

Console.WriteLine($"\nScanning DNS servers for domain '{domain}'...");
Console.WriteLine("--------------------------------------------------");

int maxConcurrent = 20;
int completed = 0;
object lockObj = new object();
using var semaphore = new SemaphoreSlim(maxConcurrent);
var tasks = targetIps.Select(ip => Task.Run(async () =>
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
Console.WriteLine($"Scan completed. Total IPs scanned: {targetIps.Count}");
Console.Out.Flush();

// ***********************************************************
// توابع کمکی (همان‌های قبل)
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

public class MultiTextWriter : TextWriter
{
    private readonly TextWriter[] _writers;
    public MultiTextWriter(params TextWriter[] writers) => _writers = writers;
    public override Encoding Encoding => Encoding.UTF8;
    public override void Write(char value)
    {
        foreach (var w in _writers) w.Write(value);
    }
    public override void Write(string value)
    {
        foreach (var w in _writers) w.Write(value);
    }
    public override void Flush()
    {
        foreach (var w in _writers) w.Flush();
    }
}
