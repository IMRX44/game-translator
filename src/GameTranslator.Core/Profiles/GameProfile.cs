using GameTranslator.Core.Models;

namespace GameTranslator.Core.Profiles;

/// <summary>
/// Per-game settings. Lets OCR focus only on the regions where text actually appears
/// (menus / subtitle band), which is both faster and more accurate than scanning the
/// whole frame. Matched to a running game by process name and/or window-title substring.
/// </summary>
public sealed class GameProfile
{
    public string Name { get; set; } = "Untitled";

    /// <summary>Process name without extension, e.g. "eldenring". Empty = match any.</summary>
    public string ProcessName { get; set; } = "";

    /// <summary>Case-insensitive substring matched against the window title. Empty = match any.</summary>
    public string WindowTitleContains { get; set; } = "";

    /// <summary>Source language hint for OCR (e.g. "en", "ja").</summary>
    public string SourceLanguage { get; set; } = "en";

    /// <summary>Regions of interest in capture pixel space. Empty = scan whole frame.</summary>
    public List<RectI> Regions { get; set; } = new();

    public OverlayMode OverlayMode { get; set; } = OverlayMode.Inline;

    public bool Matches(string processName, string windowTitle)
    {
        bool procOk = string.IsNullOrEmpty(ProcessName) ||
                      processName.Equals(ProcessName, StringComparison.OrdinalIgnoreCase);
        bool titleOk = string.IsNullOrEmpty(WindowTitleContains) ||
                       windowTitle.Contains(WindowTitleContains, StringComparison.OrdinalIgnoreCase);
        return procOk && titleOk;
    }
}
