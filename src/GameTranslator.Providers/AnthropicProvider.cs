using System.Net.Http.Json;
using System.Text.Json;
using GameTranslator.Core.Abstractions;

namespace GameTranslator.Providers;

/// <summary>
/// Anthropic (Claude) Messages API provider. Defaults to <c>claude-haiku-4-5</c> — the
/// fastest, cheapest Claude tier, which suits the low-latency, high-frequency nature of
/// live screen translation. Swap to <c>claude-sonnet-4-6</c> or <c>claude-opus-4-8</c>
/// from the UI when maximum quality matters more than speed.
/// </summary>
public sealed class AnthropicProvider : ITranslationProvider
{
    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly string _model;
    private readonly string _baseUrl;

    public const string DefaultModel = "claude-haiku-4-5";

    public AnthropicProvider(HttpClient http, string apiKey, string model = DefaultModel, string? baseUrl = null)
    {
        _http = http;
        _apiKey = apiKey;
        _model = string.IsNullOrWhiteSpace(model) ? DefaultModel : model;
        _baseUrl = string.IsNullOrWhiteSpace(baseUrl) ? "https://api.anthropic.com/v1" : baseUrl.TrimEnd('/');
    }

    public string Name => "Anthropic (Claude)";
    public bool RequiresNetwork => true;

    public async Task<IReadOnlyList<string>> TranslateBatchAsync(
        IReadOnlyList<string> lines, string sourceLang, string targetLang, CancellationToken ct = default)
    {
        if (lines.Count == 0) return Array.Empty<string>();

        var payload = new
        {
            model = _model,
            max_tokens = 2048,
            system = BatchTranslationProtocol.SystemInstruction(sourceLang, targetLang),
            messages = new object[]
            {
                new { role = "user", content = BatchTranslationProtocol.BuildUserPrompt(lines) }
            }
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/messages");
        req.Headers.Add("x-api-key", _apiKey);
        req.Headers.Add("anthropic-version", "2023-06-01");
        req.Content = JsonContent.Create(payload);

        using var resp = await _http.SendAsync(req, ct);
        resp.EnsureSuccessStatusCode();

        await using var stream = await resp.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

        // Response shape: { "content": [ { "type": "text", "text": "..." }, ... ] }
        var text = doc.RootElement.GetProperty("content")[0].GetProperty("text").GetString();
        return BatchTranslationProtocol.ParseArray(text, lines);
    }
}
