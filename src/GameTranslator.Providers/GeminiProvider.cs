using System.Net.Http.Json;
using System.Text.Json;
using GameTranslator.Core.Abstractions;

namespace GameTranslator.Providers;

/// <summary>
/// Google Gemini provider (generateContent API). Defaults to a fast "flash" tier model,
/// which is the right trade-off for live translation.
/// </summary>
public sealed class GeminiProvider : ITranslationProvider
{
    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly string _model;
    private readonly string _baseUrl;

    public const string DefaultModel = "gemini-2.0-flash";

    public GeminiProvider(HttpClient http, string apiKey, string model = DefaultModel, string? baseUrl = null)
    {
        _http = http;
        _apiKey = apiKey;
        _model = string.IsNullOrWhiteSpace(model) ? DefaultModel : model;
        _baseUrl = string.IsNullOrWhiteSpace(baseUrl)
            ? "https://generativelanguage.googleapis.com/v1beta"
            : baseUrl.TrimEnd('/');
    }

    public string Name => "Google Gemini";
    public bool RequiresNetwork => true;

    public async Task<IReadOnlyList<string>> TranslateBatchAsync(
        IReadOnlyList<string> lines, string sourceLang, string targetLang, CancellationToken ct = default)
    {
        if (lines.Count == 0) return Array.Empty<string>();

        var payload = new
        {
            systemInstruction = new
            {
                parts = new[] { new { text = BatchTranslationProtocol.SystemInstruction(sourceLang, targetLang) } }
            },
            contents = new[]
            {
                new
                {
                    role = "user",
                    parts = new[] { new { text = BatchTranslationProtocol.BuildUserPrompt(lines) } }
                }
            },
            generationConfig = new { temperature = 0.2 }
        };

        var url = $"{_baseUrl}/models/{_model}:generateContent?key={Uri.EscapeDataString(_apiKey)}";
        using var resp = await _http.PostAsJsonAsync(url, payload, ct);
        resp.EnsureSuccessStatusCode();

        await using var stream = await resp.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

        var text = doc.RootElement
            .GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString();

        return BatchTranslationProtocol.ParseArray(text, lines);
    }
}
