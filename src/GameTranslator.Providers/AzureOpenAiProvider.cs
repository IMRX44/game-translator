using System.Net.Http.Json;
using System.Text.Json;
using GameTranslator.Core.Abstractions;

namespace GameTranslator.Providers;

/// <summary>
/// Azure OpenAI provider. Same chat payload as <see cref="OpenAiProvider"/>, but the URL is
/// built from the resource endpoint + deployment name and auth uses the <c>api-key</c> header.
/// </summary>
public sealed class AzureOpenAiProvider : ITranslationProvider
{
    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly string _endpoint;     // e.g. https://my-resource.openai.azure.com
    private readonly string _deployment;   // deployment name
    private readonly string _apiVersion;

    public AzureOpenAiProvider(
        HttpClient http, string apiKey, string endpoint, string deployment,
        string apiVersion = "2024-06-01")
    {
        _http = http;
        _apiKey = apiKey;
        _endpoint = endpoint.TrimEnd('/');
        _deployment = deployment;
        _apiVersion = apiVersion;
    }

    public string Name => "Azure OpenAI";
    public bool RequiresNetwork => true;

    public async Task<IReadOnlyList<string>> TranslateBatchAsync(
        IReadOnlyList<string> lines, string sourceLang, string targetLang, CancellationToken ct = default)
    {
        if (lines.Count == 0) return Array.Empty<string>();

        var payload = new
        {
            temperature = 0.2,
            messages = new object[]
            {
                new { role = "system", content = BatchTranslationProtocol.SystemInstruction(sourceLang, targetLang) },
                new { role = "user", content = BatchTranslationProtocol.BuildUserPrompt(lines) }
            }
        };

        var url = $"{_endpoint}/openai/deployments/{_deployment}/chat/completions?api-version={_apiVersion}";
        using var req = new HttpRequestMessage(HttpMethod.Post, url);
        req.Headers.Add("api-key", _apiKey);
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
