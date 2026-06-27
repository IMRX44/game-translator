using System.Net.Http.Json;
using System.Text.Json;
using GameTranslator.Core.Abstractions;

namespace GameTranslator.Providers;

/// <summary>
/// OpenAI Chat Completions provider. Because the request/response shape is an industry
/// standard, this same class also drives any OpenAI-compatible gateway (ArvanCloud,
/// OpenRouter, LocalAI, Together, ...) by overriding <c>baseUrl</c>.
/// </summary>
public sealed class OpenAiProvider : ITranslationProvider
{
    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly string _model;
    private readonly string _baseUrl;

    public OpenAiProvider(HttpClient http, string apiKey, string model = "gpt-4o-mini", string? baseUrl = null)
    {
        _http = http;
        _apiKey = apiKey;
        _model = model;
        _baseUrl = string.IsNullOrWhiteSpace(baseUrl) ? "https://api.openai.com/v1" : baseUrl.TrimEnd('/');
    }

    public string Name => "OpenAI-compatible";
    public bool RequiresNetwork => true;

    public async Task<IReadOnlyList<string>> TranslateBatchAsync(
        IReadOnlyList<string> lines, string sourceLang, string targetLang, CancellationToken ct = default)
    {
        if (lines.Count == 0) return Array.Empty<string>();

        var payload = new
        {
            model = _model,
            temperature = 0.2,
            messages = new object[]
            {
                new { role = "system", content = BatchTranslationProtocol.SystemInstruction(sourceLang, targetLang) },
                new { role = "user", content = BatchTranslationProtocol.BuildUserPrompt(lines) }
            }
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/chat/completions");
        req.Headers.Add("Authorization", $"Bearer {_apiKey}");
        req.Content = JsonContent.Create(payload);

        using var resp = await _http.SendAsync(req, ct);
        resp.EnsureSuccessStatusCode();

        await using var stream = await resp.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        var text = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        return BatchTranslationProtocol.ParseArray(text, lines);
    }
}
