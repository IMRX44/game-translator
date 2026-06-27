using System.Drawing;
using WinForms = System.Windows.Forms;

namespace GameTranslator.App.Infrastructure;

/// <summary>
/// System-tray presence and right-click menu. Uses WinForms <see cref="WinForms.NotifyIcon"/>
/// (WPF has no native tray support); the app otherwise lives entirely in the tray to stay light.
/// </summary>
public sealed class TrayIconManager : IDisposable
{
    private readonly WinForms.NotifyIcon _icon;
    private readonly WinForms.ToolStripMenuItem _liveItem;

    public event Action? TranslateNowRequested;
    public event Action<bool>? LiveToggled;
    public event Action? SettingsRequested;
    public event Action? ExitRequested;

    public TrayIconManager()
    {
        var menu = new WinForms.ContextMenuStrip();

        var translateNow = new WinForms.ToolStripMenuItem("ترجمه الان (Translate now)");
        translateNow.Click += (_, _) => TranslateNowRequested?.Invoke();

        _liveItem = new WinForms.ToolStripMenuItem("ترجمه‌ی زنده (Live)") { CheckOnClick = true };
        _liveItem.CheckedChanged += (_, _) => LiveToggled?.Invoke(_liveItem.Checked);

        var settings = new WinForms.ToolStripMenuItem("تنظیمات (Settings)");
        settings.Click += (_, _) => SettingsRequested?.Invoke();

        var exit = new WinForms.ToolStripMenuItem("خروج (Exit)");
        exit.Click += (_, _) => ExitRequested?.Invoke();

        menu.Items.Add(translateNow);
        menu.Items.Add(_liveItem);
        menu.Items.Add(new WinForms.ToolStripSeparator());
        menu.Items.Add(settings);
        menu.Items.Add(exit);

        _icon = new WinForms.NotifyIcon
        {
            Icon = SystemIcons.Application,
            Visible = true,
            Text = "GameTranslator",
            ContextMenuStrip = menu
        };
        _icon.DoubleClick += (_, _) => SettingsRequested?.Invoke();
    }

    public bool LiveChecked
    {
        get => _liveItem.Checked;
        set => _liveItem.Checked = value;
    }

    public void Notify(string message, WinForms.ToolTipIcon icon = WinForms.ToolTipIcon.Info)
    {
        _icon.BalloonTipTitle = "GameTranslator";
        _icon.BalloonTipText = message;
        _icon.BalloonTipIcon = icon;
        _icon.ShowBalloonTip(2500);
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
    }
}
