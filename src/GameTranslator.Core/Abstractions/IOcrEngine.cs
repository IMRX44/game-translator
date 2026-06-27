using GameTranslator.Core.Models;

namespace GameTranslator.Core.Abstractions;

/// <summary>
/// Recognizes text in a captured frame. The frame is passed as raw 32-bit BGRA pixels
/// (the format produced by Windows Graphics Capture / GDI), keeping this abstraction free
/// of any image library so callers can swap engines (Windows.Media.Ocr, Tesseract...).
/// </summary>
public interface IOcrEngine
{
    string Name { get; }

    /// <summary>
    /// Recognize text in the given BGRA frame.
    /// </summary>
    /// <param name="bgra">Pixel buffer, length must be width*height*4.</param>
    /// <param name="width">Frame width in pixels.</param>
    /// <param name="height">Frame height in pixels.</param>
    /// <param name="sourceLang">Source language hint (e.g. "en", "ja"). Empty = engine default.</param>
    /// <param name="regions">
    /// Optional regions of interest in pixel space. When provided, only these areas are scanned
    /// (per-game profile optimization). Null/empty = whole frame.
    /// </param>
    Task<IReadOnlyList<OcrTextBlock>> RecognizeAsync(
        byte[] bgra,
        int width,
        int height,
        string sourceLang,
        IReadOnlyList<RectI>? regions = null,
        CancellationToken ct = default);
}
