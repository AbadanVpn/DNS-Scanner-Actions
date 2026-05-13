# 📡 DNS Scanner Actions

اسکنر DNS مبتنی بر C# برای استفاده در GitHub Actions. این ابزار یک دامنه مشخص را روی لیستی از سرورهای DNS (آیپی‌های ورودی) به صورت همزمان کوئری می‌کند، نتایج موفق و ناموفق را ثبت می‌کند و لاگ اسکن را به عنوان `Artifact` در GitHub Actions ذخیره می‌کند.

---

## ✨ ویژگی‌ها

- **اسکن همزمان** – امکان تنظیم حداکثر کوئری‌های همزمان (پیش‌فرض ۲۰).
- **ورودی دامنه از طریق محیط** – دامنه هدف از متغیر `INPUT_DOMAIN` دریافت می‌شود.
- **بارگذاری آیپی از فایل متنی** – لیست سرورهای DNS از فایل `ips.txt` (هر آیپی در یک خط) خوانده می‌شود.
- **خروجی دوطرفه** – همزمان در کنسول و فایل `scan_output.txt` ذخیره می‌شود.
- **ذخیره لاگ در GitHub Actions** – فایل خروجی به عنوان `Artifact` با نام `scan-log` آپلود می‌شود.
- **قابلیت اجرای دستی** – از طریق `workflow_dispatch` در GitHub Actions قابل اجراست.

---

## 🔧 پیش‌نیازها

- یک مخزن GitHub
- دسترسی به GitHub Actions
- دات‌نت نسخه ۸.۰.x (در workflow به صورت خودکار نصب می‌شود)

---

## 🚀 نحوه استفاده

1. مخزن را **فورک** یا **کلون** کنید.
2. فایل `ips.txt` را در ریشه مخزن با لیست آیپی‌های سرورهای DNS مورد نظر پر کنید.
3. فایل `.github/workflows/scan-dns.yml` را با محتوای زیر ایجاد کنید:

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

4. به برگه **Actions** در مخزن GitHub خود بروید.  
   (در بالای صفحه، کنار `Code` و `Issues`، برگه `Actions` را ببینید.)

5. در سمت چپ، روی **DNS Range Scanner** کلیک کنید. سپس دکمه **Run workflow** (سمت راست) را بزنید.

6. دامنه مورد نظر را وارد کنید (مثلاً `example.com`) و دوباره **Run workflow** را بزنید.

7. پس از اتمام اجرا، روی همان وورکفلو کلیک کنید. در پایین صفحه، بخش **Artifacts** را ببینید و `scan-log.zip` را دانلود کنید. داخل آن `scan_output.txt` قرار دارد.

---

## 🧪 اجرای محلی (برای تست)

```bash
git clone https://github.com/AbadanVpn/DNS-Scanner-Actions.git
cd DNS-Scanner-Actions
export INPUT_DOMAIN="example.com"
dotnet run
```

سپس خروجی در کنسول و فایل `scan_output.txt` ذخیره می‌شود.

---

## 📂 ساختار پروژه

```
.
├── .github/workflows/scan-dns.yml   # تعریف workflow GitHub Actions
├── DnsScanner.csproj                 # فایل پروژه دات‌نت
├── Program.cs                        # کد اصلی اسکنر DNS
├── ips.txt                           # لیست آیپی سرورهای DNS
└── README.md                         # این فایل
```

---

## 🧠 نحوه عملکرد

1. **خواندن آیپی‌ها** – اسکنر فایل `ips.txt` را می‌خواند و آیپی‌های معتبر را استخراج می‌کند.
2. **دریافت دامنه هدف** – دامنه از متغیر محیطی `INPUT_DOMAIN` گرفته می‌شود (پیش‌فرض `google.com`).
3. **اسکن همزمان** – با استفاده از `SemaphoreSlim` حداکثر ۲۰ درخواست همزمان به سرورهای DNS ارسال می‌شود.
4. **ارسال کوئری DNS** – یک درخواست استاندارد DNS از نوع A (IPv4) از طریق UDP روی پورت ۵۳ ساخته و ارسال می‌شود.
5. **پردازش پاسخ** – پاسخ دریافت شده Parse شده و آیپی‌های برگشتی استخراج می‌گردند.
6. **ثبت نتایج** – نتیجه هر سرور (موفق/ناموفق، زمان پاسخ و آیپی‌های برگشتی) هم در کنسول و هم در فایل `scan_output.txt` ذخیره می‌شود.
7. **آپلود لاگ** – در نهایت فایل خروجی به عنوان `Artifact` در GitHub Actions آپلود می‌شود.

---

## ⚙️ سفارشی‌سازی

- **تعداد همزمانی:** در `Program.cs` خط مربوط به `maxConcurrent` را تغییر دهید:

```csharp
int maxConcurrent = 50; // تغییر به تعداد دلخواه
```

- **زمان تایم‌اوت:** در فراخوانی `QueryDnsAsync` مقدار میلی‌ثانیه را تغییر دهید:

```csharp
var result = await QueryDnsAsync(ip, domain, 2000); // ۲ ثانیه تایم‌اوت
```

- **دامنه پیش‌فرض:** در `Program.cs` می‌توانید دامنه پیش‌فرض را تغییر دهید:

```csharp
string domain = Environment.GetEnvironmentVariable("INPUT_DOMAIN") ?? "my-site.com";
```

---

## 📄 مجوز

این پروژه تحت مجوز **MIT** منتشر شده است.

---

## 🤝 مشارکت

پیشنهادها و Pull Requestها با خوشحالی پذیرفته می‌شوند. برای گزارش باگ یا ایده جدید، یک Issue باز کنید.

---

## 🙏 قدردانی

ساخته شده با ❤️ توسط [AbadanVpn](https://github.com/AbadanVpn).

---

**توجه:** این ابزار فقط برای مقاصد آموزشی و تست امنیتی مجاز ساخته شده است. لطفاً از آن بر روی سرورهایی که مالکیت یا اجازه اسکن آنها را دارید استفاده کنید.
