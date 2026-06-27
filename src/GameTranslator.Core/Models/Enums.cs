namespace GameTranslator.Core.Models;

/// <summary>How translation is sourced.</summary>
public enum TranslationMode
{
    /// <summary>Glossary + local NMT only. No network.</summary>
    Offline,
    /// <summary>AI provider only.</summary>
    Online,
    /// <summary>Use AI provider when a key is configured, otherwise fall back to offline.</summary>
    Auto,
    /// <summary>Show offline instantly, then replace with the AI result when it arrives.</summary>
    Hybrid
}

/// <summary>Supported AI translation providers.</summary>
public enum ProviderKind
{
    /// <summary>OpenAI, or any OpenAI-compatible endpoint (ArvanCloud, OpenRouter, LocalAI...) via custom BaseUrl.</summary>
    OpenAiCompatible,
    Anthropic,
    Gemini,
    AzureOpenAI
}

/// <summary>What the overlay draws.</summary>
public enum OverlayMode
{
    /// <summary>Draw each translation over its original on-screen position.</summary>
    Inline,
    /// <summary>Single subtitle bar at the bottom of the screen (story games).</summary>
    Subtitle
}
