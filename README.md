#  [فارسی](https://github.com/AbadanVpn/DNS-Scanner-Actions/blob/main/README%20FA.md)

# 📡 DNS Scanner Actions

A C#-based DNS scanner for use in GitHub Actions. This tool queries a specified domain against a list of DNS servers (IP inputs) concurrently, records successful and failed results, and uploads the scan log as an `Artifact` in GitHub Actions.

---

## ✨ Features

- **Concurrent scanning** – Configurable max concurrent queries (default 20).
- **Domain input via environment** – Target domain taken from `INPUT_DOMAIN` variable.
- **Load IPs from text file** – Reads DNS server list from `ips.txt` (one IP per line).
- **Dual output** – Writes simultaneously to console and `scan_output.txt`.
- **Save log in GitHub Actions** – Uploads output file as an `Artifact` named `scan-log`.
- **Manual trigger** – Can be run via `workflow_dispatch` in GitHub Actions.

---

## 🔧 Prerequisites

- A GitHub repository
- Access to GitHub Actions
- .NET version 8.0.x (automatically installed in the workflow)

---

## 🚀 How to use

1. **Fork** or **clone** the repository.
2. Populate `ips.txt` in the root of the repository with the list of DNS server IPs.
3. Create (or adjust) `.github/workflows/scan-dns.yml` with the following content:

```yaml
name: DNS Range Scanner
on:
  workflow_dispatch:
    inputs:
      domain:
        description: 'Domain to resolve'
        required: true
        default: 'google.com'
jobs:
  scan:
    runs-on: ubuntu-latest
    steps:
      - name: Checkout code
        uses: actions/checkout@v4
      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'
      - name: Build and Run Scanner
        run: |
          dotnet build
          dotnet run
        env:
          INPUT_DOMAIN: ${{ github.event.inputs.domain }}
      - name: Upload log
        uses: actions/upload-artifact@v4
        with:
          name: scan-log
          path: scan_output.txt
```

4. Go to the **Actions** tab in your GitHub repository.  
   (At the top of the page, next to `Code` and `Issues`, you will see the `Actions` tab.)

5. On the left, click **DNS Range Scanner**. Then click the **Run workflow** button (on the right).

6. Enter the desired domain (e.g., `example.com`) and click **Run workflow** again.

7. After the run completes, click on the same workflow. At the bottom of the page, in the **Artifacts** section, download `scan-log.zip`. Inside it you will find `scan_output.txt`.

---

## 🧪 Local testing

To test on your own machine, follow these steps:

```bash
git clone https://github.com/AbadanVpn/DNS-Scanner-Actions.git
cd DNS-Scanner-Actions
export INPUT_DOMAIN="example.com"
dotnet run
```

Output will be shown in the console and saved in `scan_output.txt`.

---

## 📂 Project structure

```
.
├── .github/workflows/scan-dns.yml   # GitHub Actions workflow definition
├── DnsScanner.csproj                 # .NET project file
├── Program.cs                        # Main DNS scanner code
├── ips.txt                           # List of DNS server IPs
└── README.md                         # This file
```

---

## 🧠 How it works

1. **Read IPs** – The scanner reads `ips.txt` and extracts valid IP addresses.
2. **Get target domain** – The domain is taken from the environment variable `INPUT_DOMAIN` (default `google.com`).
3. **Concurrent scanning** – Uses `SemaphoreSlim` to send up to 20 concurrent DNS requests.
4. **Send DNS query** – Builds and sends a standard DNS A record (IPv4) query over UDP on port 53.
5. **Parse response** – Extracts returned IP addresses from the response.
6. **Log results** – For each server, logs success/failure, response time, and returned IPs to both console and `scan_output.txt`.
7. **Upload log** – Finally, uploads the output file as an Artifact in GitHub Actions.

---

## ⚙️ Customisation

- **Concurrency limit** – In `Program.cs`, change the `maxConcurrent` line:

```csharp
int maxConcurrent = 50; // change to desired number
```

- **Timeout** – In the `QueryDnsAsync` call, change the milliseconds value:

```csharp
var result = await QueryDnsAsync(ip, domain, 2000); // 2 seconds timeout
```

- **Default domain** – In `Program.cs`:

```csharp
string domain = Environment.GetEnvironmentVariable("INPUT_DOMAIN") ?? "my-site.com";
```

---

## 📄 License

This project is released under the **MIT** license.

---

## 🤝 Contributing

Suggestions and Pull Requests are gladly accepted. Open an Issue to report bugs or propose new ideas.

---

## 🙏 Acknowledgements

Made with ❤️ by [AbadanVpn](https://github.com/AbadanVpn).

---

**Note:** This tool is built only for educational purposes and authorised security testing. Please use it only on servers you own or have permission to scan.
