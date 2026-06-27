using System.IO;
using System.Net.Http;
using System.Windows;
using GameTranslator.App.Engine;
using GameTranslator.App.Infrastructure;
using GameTranslator.App.Overlay;
using GameTranslator.App.Views;
using GameTranslator.Core.Abstractions;
using GameTranslator.Core.Cache;
using GameTranslator.Core.Profiles;
using GameTranslator.Core.Settings;
using GameTranslator.Core.Translation;
using GameTranslator.Platform.Windows.Capture;
using GameTranslator.Platform.Windows.Ocr;
using GameTranslator.Platform.Windows.Security;

namespace GameTranslator.App;

/// <summary>
/// Composition root. The app lives in the system tray; a global hotkey (or the live loop)
/// drives the capture → OCR → translate → overlay pipeline.
/// </summary>
public partial class App : Application
{
    private AppSettings _settings = null!;
    private ISecretStore _secrets = null!;
    private WindowCaptureService _capture = null!;
    private GameProfileStore _profiles = null!;
    private TranslationCache _cache = null!;
    private GlossaryProvider _glossary = null!;
    private HttpClient _http = null!;
    private TranslationEngine _engine = null!;
    private OverlayWindow _overlay = null!;
    private HotkeyHost _hotkey = null!;
    private TrayIconManager _tray = null!;

    private bool _overlayShown;
    private static Mutex? _singleInstance;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _singleInstance = new Mutex(true, "GameTranslator.SingleInstance", out bool isNew);
        if (!isNew)
        {
            Shutdown();
            return;
        }

        _settings = AppSettings.Load();
        _secrets = new DpapiSecretStore();
        _capture = new WindowCaptureService();
        _profiles = new GameProfileStore(GameProfileStore.DefaultDirectory);
        _cache = TranslationCache.Load(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "GameTranslator", "cache.json"));
        _glossary = GlossaryProvider.FromFile(GlossaryPath());
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

        var ocr = new WindowsOcrEngine();
        _engine = new TranslationEngine(_settings, _capture, ocr, _profiles, _cache, _glossary, _secrets, _http);
        _engine.ResultReady += OnResultReady;
        _engine.Cleared += OnCleared;
        _engine.Error += msg => Dispatcher.Invoke(() => _tray.Notify("خطا: " + msg, System.Windows.Forms.ToolTipIcon.Error));

        _overlay = new OverlayWindow();
        // Force HWND creation up front so the click-through extended styles are applied and a
        // PresentationSource exists (correct DPI) before the very first overlay frame.
        _overlay.Show();
        _overlay.Hide();

        _hotkey = new HotkeyHost();
        _hotkey.Pressed += OnHotkey;
        _hotkey.Bind((uint)_settings.HotkeyModifiers, (uint)_settings.HotkeyVirtualKey);

        _tray = new TrayIconManager();
        _tray.TranslateNowRequested += () => _ = _engine.TranslateOnceAsync(force: true);
        _tray.LiveToggled += SetLive;
        _tray.SettingsRequested += OpenSettings;
        _tray.ExitRequested += () => Shutdown();

        if (!WindowsOcrEngine.IsAvailable)
            _tray.Notify("هیچ پک زبان OCR نصب نیست. از Settings ویندوز یک زبان اضافه کنید.",
                System.Windows.Forms.ToolTipIcon.Warning);
        else
            _tray.Notify("GameTranslator فعال است. شورتکات را بزنید یا از منوی tray استفاده کنید.");
    }

    private void OnHotkey()
    {
        // Toggle: if the overlay is showing, clear it; otherwise translate the current screen.
        if (_overlayShown)
            OnCleared();
        else
            _ = _engine.TranslateOnceAsync(force: true);
    }

    private void OnResultReady(TranslationResult result)
    {
        Dispatcher.Invoke(() =>
        {
            _overlay.Render(result, _settings);
            _overlayShown = true;
        });
    }

    private void OnCleared()
    {
        Dispatcher.Invoke(() =>
        {
            _overlay.ClearOverlay();
            _overlayShown = false;
        });
    }

    private void SetLive(bool on)
    {
        if (on) _engine.StartLive();
        else { _engine.StopLive(); OnCleared(); }
    }

    private void OpenSettings()
    {
        Dispatcher.Invoke(() =>
        {
            var win = new SettingsWindow(_settings, _secrets, _capture, _profiles, _http,
                applyTarget: hwnd => _engine.TargetWindow = hwnd,
                applySettings: ApplySettings);
            win.ShowDialog();
        });
    }

    private void ApplySettings()
    {
        _engine.Settings = _settings;
        _hotkey.Bind((uint)_settings.HotkeyModifiers, (uint)_settings.HotkeyVirtualKey);
        if (_engine.IsLive) _engine.StartLive(); // restart with new interval/diff settings
    }

    private string GlossaryPath()
    {
        var beside = Path.Combine(AppContext.BaseDirectory, "assets", "glossary", "fa.json");
        return beside;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            _engine?.Dispose();
            _hotkey?.Dispose();
            _tray?.Dispose();
            _cache?.Flush();
            _http?.Dispose();
        }
        catch { }
        base.OnExit(e);
    }
}
