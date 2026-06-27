using GameTranslator.Core.Abstractions;
using GameTranslator.Core.Models;
using GameTranslator.Core.Settings;

namespace GameTranslator.Providers;

/// <summary>
/// Builds the configured AI <see cref="ITranslationProvider"/> from <see cref="AppSettings"/>
/// and the encrypted <see cref="ISecretStore"/>. Returns null when no usable key is present,
/// which the pipeline reads as "offline only".
/// </summary>
public static class ProviderFactory
{
    /// <summary>Secret-store key under which each provider's API key is saved.</summary>
    public static string SecretKey(ProviderKind kind) => $"apikey:{kind}";

    public static ITranslationProvider? CreateOnline(AppSettings s, ISecretStore secrets, HttpClient http)
    {
        var apiKey = secrets.Load(SecretKey(s.Provider));
        if (string.IsNullOrWhiteSpace(apiKey))
            return null;

        return s.Provider switch
        {
            ProviderKind.OpenAiCompatible =>
                new OpenAiProvider(http, apiKey, s.Model, NullIfEmpty(s.BaseUrl)),
            ProviderKind.Anthropic =>
                new AnthropicProvider(http, apiKey, s.Model, NullIfEmpty(s.BaseUrl)),
            ProviderKind.Gemini =>
                new GeminiProvider(http, apiKey, s.Model, NullIfEmpty(s.BaseUrl)),
            ProviderKind.AzureOpenAI =>
                new AzureOpenAiProvider(http, apiKey, s.BaseUrl, s.AzureDeployment),
            _ => null
        };
    }

    private static string? NullIfEmpty(string? v) => string.IsNullOrWhiteSpace(v) ? null : v;
}
