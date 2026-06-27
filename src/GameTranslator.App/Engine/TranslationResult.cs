using GameTranslator.Core.Models;

namespace GameTranslator.App.Engine;

/// <summary>One translated text box: where the original sat (client pixels) and the Persian text.</summary>
public sealed record TranslatedBox(RectI Bounds, string Text);

/// <summary>
/// A full overlay frame: the boxes to draw plus the target window's client geometry in screen
/// pixels, so the overlay can position itself exactly over the game's content.
/// </summary>
public sealed record TranslationResult(
    IReadOnlyList<TranslatedBox> Boxes,
    int ClientScreenX,
    int ClientScreenY,
    int ClientWidth,
    int ClientHeight,
    OverlayMode Mode);
