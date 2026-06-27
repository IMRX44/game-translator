# معماری

## جریان داده (یک بار ترجمه)

```
شورتکات/Live  ──▶  TranslationEngine.TranslateOnceAsync
                        │
            ┌───────────┼──────────────────────────────────────────────┐
            ▼           ▼                                               ▼
   WindowCaptureService  FrameDiffer (آیا صفحه عوض شده؟)        GameProfileStore
   (GDI BitBlt/PrintWindow → BGRA)   اگر نه → خروج (سبک می‌ماند)   (نواحی متن این بازی)
            │
            ▼
   WindowsOcrEngine (Windows.Media.Ocr) ──▶ خطوط متن + باکس (client px)
            │
            ▼
   HybridTranslator  ──▶  TranslationCache (hash→fa، روی دیسک)
            │                 │ miss
            │       ┌─────────┼─────────────┐
            │       ▼         ▼             ▼
            │   Glossary   Onnx NMT     ITranslationProvider (AI)
            │  (آنی)      (اختیاری)     OpenAI/Anthropic/Gemini/Azure
            │                              (batch: همهٔ خطوط در یک درخواست)
            ▼
   TranslationResult (باکس‌ها + هندسهٔ client) ──▶ OverlayWindow.Render
                                                    (شفاف، click-through، RTL)
```

در حالت **Hybrid**، `HybridTranslator` ابتدا نتیجهٔ آفلاینِ آنی را از طریق callback `onInstant`
به اورلی می‌فرستد و سپس نتیجهٔ AI را جایگزین می‌کند — کاربر بلافاصله چیزی می‌بیند و بعد دقیق‌تر می‌شود.

## مرزهای پروژه‌ها

| پروژه | پلتفرم | مسئولیت |
|---|---|---|
| `Core` | net8.0 (هر OS) | مدل‌ها (`RectI`, `OcrTextBlock`)، interfaceها (`ITranslationProvider`, `IOcrEngine`, `ISecretStore`)، `FrameDiffer`, `TranslationCache`, `GameProfileStore`, `GlossaryProvider`, `HybridTranslator`, `AppSettings` |
| `Providers` | net8.0 (هر OS) | پیاده‌سازی HTTPِ ارائه‌دهنده‌های AI + `OnnxNmtProvider` + `ProviderFactory` + پروتکل batch/parse |
| `Platform.Windows` | net8.0-windows | `WindowsOcrEngine`, `WindowCaptureService`, `GlobalHotkey`, `DpapiSecretStore`, P/Invokeها |
| `App` | net8.0-windows (WPF) | `OverlayWindow`, `SettingsWindow`, `RegionEditor`, `TrayIconManager`, `HotkeyHost`, `TranslationEngine`, ترکیب همه |

تمام منطقِ قابل‌تست (frame-diff، cache، glossary، hybrid، parse) در `Core`/`Providers` و مستقل از
ویندوز است، پس روی هر CI/OS تست می‌شود.

## تصمیم‌های کلیدی

- **امن نسبت به انتی‌چیت:** فقط خواندن پیکسل از بیرون (GDI) و یک پنجرهٔ اورلیِ جدا با
  `WS_EX_LAYERED | WS_EX_TRANSPARENT`. هیچ تزریق DLL، هوک DirectX یا تغییر فایل بازی‌ای انجام نمی‌شود.
- **سبک ماندن:** `FrameDiffer` با نمونه‌برداری پرشیِ پیکسلی فقط وقتی صفحه واقعاً تغییر کرد OCR/ترجمه را اجرا می‌کند؛
  `TranslationCache` ترجمه‌های تکراری را از API دور نگه می‌دارد.
- **یک interface برای همهٔ ارائه‌دهنده‌ها:** فقط transport فرق می‌کند؛ ساخت prompt و parse مشترک است
  (`BatchTranslationProtocol`)، پس افزودن ارائه‌دهندهٔ جدید کم‌هزینه است.
- **امنیت کلیدها:** `ISecretStore` در Core تعریف و با DPAPI در Windows پیاده شده؛ کلیدها هرگز plain-text ذخیره نمی‌شوند.
- **DPI:** مانیفست PerMonitorV2؛ اورلی مختصات پیکسلیِ کپچر را با ضریب DPIِ مانیتور به DIP تبدیل می‌کند.

## نقاط توسعه

- **کپچر جدید** (مثلاً Windows Graphics Capture برای Exclusive Fullscreen): فقط خروجیِ `CapturedFrame` را تأمین کنید.
- **OCR جایگزین** (مثلاً Tesseract): `IOcrEngine` را پیاده کنید.
- **ارائه‌دهندهٔ جدید**: `ITranslationProvider` + یک شاخه در `ProviderFactory`.
- **NMT آفلاین واقعی**: `OnnxNmtProvider.RunModel/LoadModel` را پیاده کنید.
