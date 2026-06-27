# GameTranslator — مترجم زندهٔ روی صفحه (Live On-Screen Game Translator)

یک ابزار **اورلیِ ترجمهٔ زنده** برای ویندوز که متن روی صفحهٔ بازی را همان لحظه به فارسی ترجمه و
روی همان متن نمایش می‌دهد — **بدون دست‌زدن به فایل‌های بازی و بدون تزریق به پروسهٔ بازی**، پس برای
بازی‌های آنلاینِ دارای انتی‌چیت هم مناسب است.

> A transparent, click-through overlay that OCRs whatever is on the game screen and shows a
> Persian translation in place. It reads pixels from *outside* the game process (no DLL
> injection, no DirectX hook, no file edits), the same way screenshot tools do — which keeps
> it anti-cheat-friendly.

---

## ✨ قابلیت‌ها

- **یک شورتکات گلوبال** → کل متن صفحه OCR و ترجمه می‌شود؛ زدن دوباره، اورلی را پاک می‌کند.
- **ترجمهٔ زنده (Live)**: با تشخیص تغییر فریم فقط وقتی صفحه عوض شد دوباره ترجمه می‌کند (سبک می‌ماند).
- **OCR آفلاین** با موتور داخلی ویندوز (`Windows.Media.Ocr`) — رایگان، سریع، بدون اینترنت.
- **سه لایهٔ ترجمه + مود ترکیبی هوشمند**:
  1. واژه‌نامهٔ آفلاینِ آنی (`assets/glossary/fa.json`) برای اصطلاحات رایج بازی،
  2. مدل NMT محلی اختیاری (ONNX) برای جمله‌ها،
  3. ارائه‌دهنده‌های هوش مصنوعی برای دقت بالا.
  در حالت **Hybrid**، اول نتیجهٔ آفلاینِ آنی نشان داده می‌شود و وقتی پاسخ AI رسید جایگزین می‌شود.
- **چند ارائه‌دهندهٔ AI**: OpenAI (و هر endpoint سازگار مثل **ArvanCloud / OpenRouter**)، **Anthropic/Claude**،
  **Google Gemini**، **Azure OpenAI** — همه پشت یک interface مشترک، با batching کل خطوط یک فریم در یک درخواست.
- **ذخیرهٔ امن کلیدها** با DPAPI (رمزنگاری‌شده per-user، نه plain-text).
- **حافظهٔ ترجمهٔ پایدار** روی دیسک تا مصرف API بین سشن‌ها کم شود.
- **پروفایل به‌ازای هر بازی**: نواحی متن (منو/زیرنویس) را ذخیره کنید تا OCR فقط همان‌جا اجرا شود.
- **حالت زیرنویس**: فقط نوار پایین صفحه برای بازی‌های داستانی.
- **اپ سبک در System Tray** با UI تیرهٔ راست‌به‌چپ.

---

## 🚀 شروع سریع (روی ویندوز)

```powershell
git clone https://github.com/imrx44/game-translator
cd game-translator
git checkout claude/game-text-translator-lq82lp
dotnet build GameTranslator.sln -c Release
dotnet run --project src/GameTranslator.App
```

نیازمندی‌ها: **Windows 10 (1903+) / 11**، **.NET 8 SDK**، و حداقل یک **پک زبان OCR** ویندوز
(Settings → Time & Language → Language → افزودن زبان با قابلیت OCR، مثلاً English).

سپس از آیکن tray → **تنظیمات**: ارائه‌دهنده و API Key، شورتکات، زبان‌ها و حالت را تنظیم کنید.

> راهنمای کامل بیلد/اجرا و رفع‌اشکال در [`docs/BUILD-WINDOWS.md`](docs/BUILD-WINDOWS.md) است.
> معماری و جریان داده در [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md).

---

## 🧱 ساختار

```
src/
  GameTranslator.Core/             # مستقل از پلتفرم: مدل‌ها، interfaceها، frame-diff, cache, profiles, hybrid translator
  GameTranslator.Providers/        # ارائه‌دهنده‌های ترجمه (OpenAI/Anthropic/Gemini/Azure) + NMT آفلاین
  GameTranslator.Platform.Windows/ # OCR، کپچر، شورتکات گلوبال، DPAPI (فقط ویندوز)
  GameTranslator.App/              # WPF: اورلی، tray، تنظیمات، ویرایش نواحی، ارکستریشن
tests/GameTranslator.Tests/        # تست‌های واحدِ بخش‌های مستقل از پلتفرم (روی هر OS اجرا می‌شوند)
assets/glossary/fa.json            # واژه‌نامهٔ آفلاین
```

`Core`، `Providers` و `Tests` روی هر سیستم‌عاملی بیلد/تست می‌شوند؛ `Platform.Windows` و `App` نیاز به ویندوز دارند.

```bash
# اجرای تست‌های مستقل از پلتفرم (هر OS):
dotnet test tests/GameTranslator.Tests/GameTranslator.Tests.csproj
```

---

## ⚠️ هشدار

امن‌بودن نسبت به انتی‌چیت‌ها به‌طور قطعی **تضمین نمی‌شود**. این ابزار فقط پیکسل‌ها را از بیرون می‌خواند
و یک پنجرهٔ اورلیِ جدا نمایش می‌دهد، ولی سیاست هر بازی متفاوت است؛ مسئولیت استفاده با خودتان است.
در بازی‌های رقابتی/رتبه‌ای با احتیاط استفاده کنید.

## License

MIT
