using System.Text.Json;
using System.Text.Json.Serialization;
using GameTranslator.Core.Models;

namespace GameTranslator.Core.Settings;

/// <summary>
/// All user-configurable options, persisted as JSON in %AppData%/GameTranslator/settings.json.
/// API keys are NOT stored here — they live in the encrypted <see cref="Abstractions.ISecretStore"/>.
/// </summary>
public sealed class AppSettings
{
    // ---- Hotkey (global shortcut) ----
    /// <summary>Modifier flags matching Win32 MOD_ALT(1)|MOD_CONTROL(2)|MOD_SHIFT(4)|MOD_WIN(8).</summary>
    public int HotkeyModifiers { get; set; } = 2 | 1; // Ctrl + Alt
    /// <summary>Virtual-key code of the hotkey. Default 'T' (0x54).</summary>
    public int HotkeyVirtualKey { get; set; } = 0x54;

    // ---- Translation ----
    public TranslationMode Mode { get; set; } = TranslationMode.Auto;
    public ProviderKind Provider { get; set; } = ProviderKind.OpenAiCompatible;
    public string Model { get; set; } = "gpt-4o-mini";
    /// <summary>Custom endpoint for OpenAI-compatible / Azure / self-hosted gateways. Empty = provider default.</summary>
    public string BaseUrl { get; set; } = "";
    /// <summary>Azure OpenAI deployment name (Azure provider only).</summary>
    public string AzureDeployment { get; set; } = "";
    public string SourceLanguage { get; set; } = "en";
    public string TargetLanguage { get; set; } = "fa";

    // ---- Offline NMT ----
    /// <summary>Path to the optional local ONNX en→fa model. Empty = NMT disabled.</summary>
    public string OfflineModelPath { get; set; } = "";

    // ---- Overlay ----
    public OverlayMode OverlayMode { get; set; } = OverlayMode.Inline;
    public double FontSize { get; set; } = 16;
    /// <summary>Background opacity of translation boxes, 0..1.</summary>
    public double OverlayOpacity { get; set; } = 0.75;

    // ---- Capture / performance ----
    /// <summary>When true, translate continuously (frame-diff gated). When false, only on hotkey.</summary>
    public bool AutoLiveTranslate { get; set; } = false;
    /// <summary>Target capture cadence for live mode (ms between frame checks).</summary>
    public int LiveIntervalMs { get; set; } = 400;
    public int FrameDiffStride { get; set; } = 16;
    /// <summary>Optional explicit target window title (substring). Empty = foreground window.</summary>
    public string TargetWindowContains { get; set; } = "";

    public bool StartWithWindows { get; set; } = false;

    // ---- Persistence ----
    [JsonIgnore]
    public static string DefaultPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "GameTranslator", "settings.json");

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static AppSettings Load(string? path = null)
    {
        path ??= DefaultPath;
        try
        {
            if (File.Exists(path))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path), JsonOpts) ?? new AppSettings();
        }
        catch
        {
            // ignore corrupt settings; fall back to defaults
        }
        return new AppSettings();
    }

    public void Save(string? path = null)
    {
        path ??= DefaultPath;
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        File.WriteAllText(path, JsonSerializer.Serialize(this, JsonOpts));
    }
}
