using System.Text;
using System.Text.Json;

namespace GameTranslator.Providers;

/// <summary>
/// Shared prompt + parsing logic for the AI providers. All chat-style providers (OpenAI,
/// Anthropic, Gemini, Azure) translate a whole frame in one request by asking the model to
/// return a JSON array of strings in the same order, then parsing it back. Keeping this in
/// one place means every provider behaves identically and only the transport differs.
/// </summary>
public static class BatchTranslationProtocol
{
    public static string SystemInstruction(string sourceLang, string targetLang) =>
        $"You are a fast, accurate video-game UI/dialogue translator. " +
        $"Translate from {LangName(sourceLang)} to {LangName(targetLang)}. " +
        "Keep translations natural and concise for on-screen display. Preserve numbers, " +
        "placeholders, and proper nouns. Do not add notes or explanations.";

    /// <summary>
    /// Builds the user message: a numbered JSON array of source lines plus strict output rules.
    /// </summary>
    public static string BuildUserPrompt(IReadOnlyList<string> lines)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Translate every string in this JSON array.");
        sb.AppendLine("Return ONLY a JSON array of strings — same length, same order, no keys, no extra text.");
        sb.AppendLine("Input:");
        sb.Append(JsonSerializer.Serialize(lines));
        return sb.ToString();
    }

    /// <summary>
    /// Extracts the translated array from a model's raw text answer. Tolerates code fences and
    /// surrounding prose by scanning for the first '[' .. last ']'. On any failure returns the
    /// originals unchanged so the pipeline degrades gracefully.
    /// </summary>
    public static IReadOnlyList<string> ParseArray(string? modelText, IReadOnlyList<string> originals)
    {
        if (string.IsNullOrWhiteSpace(modelText))
            return originals;

        try
        {
            var span = modelText.AsSpan();
            int start = modelText.IndexOf('[');
            int end = modelText.LastIndexOf(']');
            if (start < 0 || end <= start)
                return originals;

            var json = modelText.Substring(start, end - start + 1);
            var arr = JsonSerializer.Deserialize<List<string>>(json);
            if (arr is null)
                return originals;

            // Pad/trim to match the input length exactly (contract for ITranslationProvider).
            var result = new string[originals.Count];
            for (int i = 0; i < originals.Count; i++)
                result[i] = i < arr.Count && !string.IsNullOrEmpty(arr[i]) ? arr[i] : originals[i];
            return result;
        }
        catch
        {
            return originals;
        }
    }

    private static string LangName(string code) => code.ToLowerInvariant() switch
    {
        "fa" => "Persian (Farsi)",
        "en" => "English",
        "ja" => "Japanese",
        "zh" => "Chinese",
        "ko" => "Korean",
        "ru" => "Russian",
        "de" => "German",
        "fr" => "French",
        "es" => "Spanish",
        "ar" => "Arabic",
        "tr" => "Turkish",
        _ => code
    };
}
