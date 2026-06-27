namespace GameTranslator.Core.Models;

/// <summary>
/// A single recognized line/block of text with its position in capture pixel space.
/// Produced by an <see cref="Abstractions.IOcrEngine"/> and consumed by the overlay.
/// </summary>
public sealed record OcrTextBlock(string Text, RectI Bounds, string Language = "")
{
    /// <summary>Translation filled in later by the pipeline (null until translated).</summary>
    public string? Translation { get; set; }
}
