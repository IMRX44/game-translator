using System.Runtime.InteropServices.WindowsRuntime;
using GameTranslator.Core.Abstractions;
using GameTranslator.Core.Models;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;

namespace GameTranslator.Platform.Windows.Ocr;

/// <summary>
/// Offline OCR using the OS-built-in <see cref="OcrEngine"/> (Windows.Media.Ocr). Free, fast,
/// works without a network, supports many source languages, and returns per-line bounding
/// boxes — exactly what the overlay needs to place each translation over its original text.
///
/// When a per-game profile defines regions, only those sub-rectangles are scanned, which is
/// both faster and more accurate than OCR-ing the whole frame.
/// </summary>
public sealed class WindowsOcrEngine : IOcrEngine
{
    public string Name => "Windows.Media.Ocr";

    /// <summary>True when at least one OCR language pack is available on this machine.</summary>
    public static bool IsAvailable => OcrEngine.AvailableRecognizerLanguages.Count > 0;

    public async Task<IReadOnlyList<OcrTextBlock>> RecognizeAsync(
        byte[] bgra, int width, int height, string sourceLang,
        IReadOnlyList<RectI>? regions = null, CancellationToken ct = default)
    {
        var engine = CreateEngine(sourceLang);
        if (engine is null)
            return Array.Empty<OcrTextBlock>();

        if (regions is { Count: > 0 })
        {
            var all = new List<OcrTextBlock>();
            foreach (var r in regions)
            {
                ct.ThrowIfCancellationRequested();
                var clamped = Clamp(r, width, height);
                if (clamped.Width <= 0 || clamped.Height <= 0) continue;

                var (sub, sw, sh) = Crop(bgra, width, height, clamped);
                var blocks = await RecognizeBufferAsync(engine, sub, sw, sh, offsetX: clamped.X, offsetY: clamped.Y);
                all.AddRange(blocks);
            }
            return all;
        }

        return await RecognizeBufferAsync(engine, bgra, width, height, 0, 0);
    }

    private static async Task<IReadOnlyList<OcrTextBlock>> RecognizeBufferAsync(
        OcrEngine engine, byte[] bgra, int width, int height, int offsetX, int offsetY)
    {
        using var bitmap = SoftwareBitmap.CreateCopyFromBuffer(
            bgra.AsBuffer(), BitmapPixelFormat.Bgra8, width, height);

        var result = await engine.RecognizeAsync(bitmap);
        var blocks = new List<OcrTextBlock>(result.Lines.Count);

        foreach (var line in result.Lines)
        {
            if (string.IsNullOrWhiteSpace(line.Text)) continue;

            // Union of the word boxes gives the line's bounding rectangle.
            double left = double.MaxValue, top = double.MaxValue, right = 0, bottom = 0;
            foreach (var word in line.Words)
            {
                var b = word.BoundingRect;
                left = Math.Min(left, b.X);
                top = Math.Min(top, b.Y);
                right = Math.Max(right, b.X + b.Width);
                bottom = Math.Max(bottom, b.Y + b.Height);
            }
            if (left == double.MaxValue) continue;

            var rect = new RectI(
                offsetX + (int)left,
                offsetY + (int)top,
                (int)(right - left),
                (int)(bottom - top));
            blocks.Add(new OcrTextBlock(line.Text, rect));
        }
        return blocks;
    }

    private static OcrEngine? CreateEngine(string sourceLang)
    {
        if (!string.IsNullOrWhiteSpace(sourceLang))
        {
            try
            {
                var lang = new Language(sourceLang);
                var byLang = OcrEngine.TryCreateFromLanguage(lang);
                if (byLang is not null) return byLang;
            }
            catch
            {
                // invalid/unsupported tag — fall through to user-profile languages
            }
        }
        return OcrEngine.TryCreateFromUserProfileLanguages();
    }

    private static RectI Clamp(RectI r, int width, int height)
    {
        int x = Math.Clamp(r.X, 0, width);
        int y = Math.Clamp(r.Y, 0, height);
        int right = Math.Clamp(r.Right, 0, width);
        int bottom = Math.Clamp(r.Bottom, 0, height);
        return RectI.FromLtrb(x, y, right, bottom);
    }

    /// <summary>Copy a sub-rectangle out of a BGRA buffer into a new tightly-packed buffer.</summary>
    private static (byte[] buffer, int w, int h) Crop(byte[] src, int width, int height, RectI r)
    {
        int w = r.Width, h = r.Height;
        var dst = new byte[w * h * 4];
        int srcStride = width * 4;
        int dstStride = w * 4;
        for (int row = 0; row < h; row++)
        {
            int srcOffset = ((r.Y + row) * width + r.X) * 4;
            Array.Copy(src, srcOffset, dst, row * dstStride, dstStride);
        }
        return (dst, w, h);
    }
}
