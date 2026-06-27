# بیلد و اجرا روی ویندوز

## پیش‌نیازها
- **Windows 10 build 19041 (نسخهٔ 2004) یا بالاتر**، یا Windows 11.
- **.NET 8 SDK** — https://dotnet.microsoft.com/download/dotnet/8.0
- حداقل یک **پک زبان OCR** نصب‌شده. (Settings → Time & Language → Language → Add a language؛
  زبان‌هایی که آیکن OCR دارند، مثل English، Japanese، …). بدون آن، OCR کار نمی‌کند و اپ هشدار می‌دهد.

> چرا فقط ویندوز؟ پروژهٔ `GameTranslator.Platform.Windows` از `Windows.Media.Ocr`،
> GDI capture و DPAPI استفاده می‌کند و `GameTranslator.App` یک اپ WPF است.
> پروژه‌های `Core`/`Providers`/`Tests` روی هر OS بیلد می‌شوند.

## بیلد
```powershell
dotnet build GameTranslator.sln -c Release
```
اگر فقط می‌خواهید اپ را اجرا کنید:
```powershell
dotnet run --project src/GameTranslator.App -c Release
```

## انتشار تک‌فایل (single-file, self-contained)
```powershell
dotnet publish src/GameTranslator.App -c Release -r win-x64 `
  -p:PublishSingleFile=true -p:SelfContained=true -p:IncludeNativeLibrariesForSelfExtract=true
```
خروجی در `src/GameTranslator.App/bin/Release/net8.0-windows10.0.19041.0/win-x64/publish/GameTranslator.exe`.
پوشهٔ `assets/glossary/fa.json` کنار exe کپی می‌شود.

## اولین اجرا
1. اپ در System Tray ظاهر می‌شود (روی آیکن دابل‌کلیک = تنظیمات).
2. در **تنظیمات**:
   - **پنجرهٔ هدف** را انتخاب کنید (یا خالی بگذارید تا پنجرهٔ فعال استفاده شود).
   - **ارائه‌دهنده** و **API Key** را وارد کنید و **تست اتصال** بزنید.
   - **شورتکات** را تنظیم کنید (پیش‌فرض `Ctrl + Alt + T`).
   - **حالت** (Auto/Hybrid/Online/Offline)، **حالت نمایش** (Inline/Subtitle)، زبان مبدأ/مقصد، فونت و شفافیت.
3. داخل بازی، **شورتکات** را بزنید تا متن صفحه ترجمه و روی آن نمایش داده شود؛ دوباره بزنید تا پاک شود.
4. برای ترجمهٔ پیوسته، از منوی tray **«ترجمهٔ زنده»** را روشن کنید.

## پروفایل به‌ازای هر بازی (نواحی)
از تنظیمات → **«ویرایش نواحی / پروفایل‌ها»**: یک اسکرین‌شات از پنجرهٔ بازی گرفته می‌شود؛ با درگ، نواحی
متن (منو/زیرنویس) را بکشید و ذخیره کنید. از آن پس OCR فقط همان نواحی را برای آن بازی اسکن می‌کند
(سریع‌تر و دقیق‌تر). پروفایل‌ها در `%AppData%\GameTranslator\profiles\*.json` ذخیره می‌شوند.

## مسیر داده‌ها
- تنظیمات: `%AppData%\GameTranslator\settings.json`
- کلیدهای API (رمزنگاری‌شده با DPAPI): `%AppData%\GameTranslator\secrets\*.bin`
- حافظهٔ ترجمه: `%AppData%\GameTranslator\cache.json`
- پروفایل‌ها: `%AppData%\GameTranslator\profiles\`

## موتور ترجمهٔ آفلاین (NMT اختیاری)
واژه‌نامهٔ `assets/glossary/fa.json` به‌صورت آنی اصطلاحات رایج را آفلاین ترجمه می‌کند. برای ترجمهٔ
آفلاینِ جمله‌های کامل می‌توانید یک مدل محلی (مثلاً OPUS-MT انگلیسی→فارسی اکسپورت‌شده به ONNX) اضافه کنید:
1. مدل ONNX را تهیه و مسیرش را در تنظیمات (**مسیر مدل آفلاین ONNX**) وارد کنید.
2. نقاط اتصال در `src/GameTranslator.Providers/Offline/OnnxNmtProvider.cs` (`LoadModel`/`RunModel`) را با
   `Microsoft.ML.OnnxRuntime` + یک توکنایزر SentencePiece پیاده‌سازی کنید و `IsAvailable` را true کنید.
   تا وقتی مدل اضافه نشده، این لایه no-op است و pipeline به glossary/AI تکیه می‌کند.

## رفع اشکال
- **صفحه سیاه کپچر می‌شود**: بازی احتمالاً در حالت **Exclusive Fullscreen** است. آن را روی
  **Borderless / Windowed Fullscreen** بگذارید. (راه‌حل آینده: Windows Graphics Capture — پایین.)
- **OCR چیزی برنمی‌گرداند**: پک زبانِ مبدأ (مثلاً English) را در ویندوز نصب کنید و `زبان مبدأ` را درست بگذارید.
- **اورلی کمی جابه‌جاست**: مطمئن شوید مانیفست DPI فعال است (هست). در مانیتورهای مختلط DPI، اورلی
  با ضریب DPIِ همان مانیتور تراز می‌شود.
- **شورتکات کار نمی‌کند**: ممکن است برنامهٔ دیگری همان ترکیب را گرفته باشد؛ ترکیب دیگری انتخاب کنید.

## توسعهٔ آینده: Windows Graphics Capture
کپچر فعلی با GDI (`PrintWindow`/`BitBlt`) است که برای بازی‌های Windowed/Borderless عالی کار می‌کند و
سبک است. برای پوشش Exclusive Fullscreen می‌توان یک `IOcrEngine`-سازگار جدید با
`Windows.Graphics.Capture` + خواندن CPU از طریق Direct3D11 اضافه کرد؛ نقطهٔ تزریق آن
`WindowCaptureService` است (همان interface خروجی `CapturedFrame`).
