namespace GameTranslator.Core.Abstractions;

/// <summary>
/// A translation backend. Implementations include the offline glossary, the local
/// ONNX NMT model, and the AI providers (OpenAI, Anthropic, Gemini, Azure).
/// Lines are translated as a batch so a whole frame becomes a single network round-trip.
/// </summary>
public interface ITranslationProvider
{
    /// <summary>Human-readable provider name (shown in the UI).</summary>
    string Name { get; }

    /// <summary>True when this provider needs a network connection.</summary>
    bool RequiresNetwork { get; }

    /// <summary>
    /// Translate <paramref name="lines"/> from <paramref name="sourceLang"/> to
    /// <paramref name="targetLang"/> (BCP-47 / ISO codes like "en", "fa", "ja").
    /// The returned array MUST be the same length and order as the input; an entry
    /// equal to its input means "no translation available".
    /// </summary>
    Task<IReadOnlyList<string>> TranslateBatchAsync(
        IReadOnlyList<string> lines,
        string sourceLang,
        string targetLang,
        CancellationToken ct = default);
}
