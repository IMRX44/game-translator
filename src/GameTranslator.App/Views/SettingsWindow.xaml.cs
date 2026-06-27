using System.Windows;
using System.Windows.Input;
using GameTranslator.Core.Models;
using GameTranslator.Core.Settings;
using GameTranslator.Core.Abstractions;
using GameTranslator.Core.Profiles;
using GameTranslator.Platform.Windows.Capture;
using GameTranslator.Providers;

namespace GameTranslator.App.Views;

public partial class SettingsWindow : Window
{
    private sealed record ComboItem(string Label, object Value);

    private readonly AppSettings _settings;
    private readonly ISecretStore _secrets;
    private readonly WindowCaptureService _capture;
    private readonly GameProfileStore _profiles;
    private readonly HttpClient _http;
    private readonly Action<IntPtr> _applyTarget;
    private readonly Action _applySettings;

    private bool _capturingHotkey;
    private uint _hotkeyMods;
    private uint _hotkeyVk;

    public SettingsWindow(
        AppSettings settings, ISecretStore secrets, WindowCaptureService capture,
        GameProfileStore profiles, HttpClient http,
        Action<IntPtr> applyTarget, Action applySettings)
    {
        InitializeComponent();
        _settings = settings;
        _secrets = secrets;
        _capture = capture;
        _profiles = profiles;
        _http = http;
        _applyTarget = applyTarget;
        _applySettings = applySettings;

        PopulateCombos();
        LoadFromSettings();
        RefreshWindows_Click(this, new RoutedEventArgs());
    }

    private void PopulateCombos()
    {
        ProviderCombo.DisplayMemberPath = "Label";
        ProviderCombo.SelectedValuePath = "Value";
        ProviderCombo.ItemsSource = new[]
        {
            new ComboItem("OpenAI / سازگار (Arvan, OpenRouter...)", ProviderKind.OpenAiCompatible),
            new ComboItem("Anthropic (Claude)", ProviderKind.Anthropic),
            new ComboItem("Google Gemini", ProviderKind.Gemini),
            new ComboItem("Azure OpenAI", ProviderKind.AzureOpenAI)
        };

        ModeCombo.DisplayMemberPath = "Label";
        ModeCombo.SelectedValuePath = "Value";
        ModeCombo.ItemsSource = new[]
        {
            new ComboItem("خودکار (Auto)", TranslationMode.Auto),
            new ComboItem("ترکیبی هوشمند (Hybrid)", TranslationMode.Hybrid),
            new ComboItem("فقط آنلاین (Online)", TranslationMode.Online),
            new ComboItem("فقط آفلاین (Offline)", TranslationMode.Offline)
        };

        OverlayModeCombo.DisplayMemberPath = "Label";
        OverlayModeCombo.SelectedValuePath = "Value";
        OverlayModeCombo.ItemsSource = new[]
        {
            new ComboItem("روی متن (Inline)", OverlayMode.Inline),
            new ComboItem("نوار زیرنویس (Subtitle)", OverlayMode.Subtitle)
        };
    }

    private void LoadFromSettings()
    {
        ProviderCombo.SelectedValue = _settings.Provider;
        ModeCombo.SelectedValue = _settings.Mode;
        OverlayModeCombo.SelectedValue = _settings.OverlayMode;
        ModelBox.Text = _settings.Model;
        BaseUrlBox.Text = _settings.BaseUrl;
        DeploymentBox.Text = _settings.AzureDeployment;
        SourceLangBox.Text = _settings.SourceLanguage;
        TargetLangBox.Text = _settings.TargetLanguage;
        FontSizeBox.Text = _settings.FontSize.ToString("0.#");
        OpacityBox.Text = _settings.OverlayOpacity.ToString("0.##");
        LiveIntervalBox.Text = _settings.LiveIntervalMs.ToString();
        OfflineModelBox.Text = _settings.OfflineModelPath;

        _hotkeyMods = (uint)_settings.HotkeyModifiers;
        _hotkeyVk = (uint)_settings.HotkeyVirtualKey;
        HotkeyBox.Text = DescribeHotkey(_hotkeyMods, _hotkeyVk);

        var key = _secrets.Load(ProviderFactory.SecretKey(_settings.Provider));
        ApiKeyBox.Password = key ?? "";

        UpdateProviderFieldVisibility();
    }

    private void ProviderCombo_Changed(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        UpdateProviderFieldVisibility();
        if (ProviderCombo.SelectedValue is ProviderKind kind)
            ApiKeyBox.Password = _secrets.Load(ProviderFactory.SecretKey(kind)) ?? "";
    }

    private void UpdateProviderFieldVisibility()
    {
        bool azure = (ProviderKind?)ProviderCombo.SelectedValue == ProviderKind.AzureOpenAI;
        DeploymentLabel.Visibility = azure ? Visibility.Visible : Visibility.Collapsed;
        DeploymentBox.Visibility = azure ? Visibility.Visible : Visibility.Collapsed;
        BaseUrlLabel.Text = azure ? "Azure endpoint (https://...openai.azure.com)" : "Base URL (اختیاری)";
    }

    private void RefreshWindows_Click(object sender, RoutedEventArgs e)
    {
        // WindowInfo.ToString() provides the display text.
        var windows = _capture.ListWindows();
        TargetCombo.ItemsSource = windows;
        TargetCombo.SelectedItem = windows.FirstOrDefault(w =>
            !string.IsNullOrEmpty(_settings.TargetWindowContains) &&
            w.Title.Contains(_settings.TargetWindowContains, StringComparison.OrdinalIgnoreCase));
    }

    // ---- Hotkey capture ----
    private void SetHotkey_Click(object sender, RoutedEventArgs e)
    {
        _capturingHotkey = true;
        HotkeyBox.Text = "کلید را فشار دهید...";
        HotkeyBox.Focus();
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (!_capturingHotkey) { base.OnPreviewKeyDown(e); return; }

        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
            or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin)
        {
            e.Handled = true;
            return; // wait for a non-modifier key
        }

        uint mods = 0;
        var m = Keyboard.Modifiers;
        if (m.HasFlag(ModifierKeys.Alt)) mods |= 0x1;
        if (m.HasFlag(ModifierKeys.Control)) mods |= 0x2;
        if (m.HasFlag(ModifierKeys.Shift)) mods |= 0x4;
        if (m.HasFlag(ModifierKeys.Windows)) mods |= 0x8;

        _hotkeyMods = mods;
        _hotkeyVk = (uint)KeyInterop.VirtualKeyFromKey(key);
        HotkeyBox.Text = DescribeHotkey(_hotkeyMods, _hotkeyVk);
        _capturingHotkey = false;
        e.Handled = true;
    }

    private static string DescribeHotkey(uint mods, uint vk)
    {
        var parts = new List<string>();
        if ((mods & 0x2) != 0) parts.Add("Ctrl");
        if ((mods & 0x1) != 0) parts.Add("Alt");
        if ((mods & 0x4) != 0) parts.Add("Shift");
        if ((mods & 0x8) != 0) parts.Add("Win");
        var key = KeyInterop.KeyFromVirtualKey((int)vk);
        parts.Add(key.ToString());
        return string.Join(" + ", parts);
    }

    // ---- Test connection ----
    private async void Test_Click(object sender, RoutedEventArgs e)
    {
        TestResult.Foreground = System.Windows.Media.Brushes.Khaki;
        TestResult.Text = "در حال تست...";
        try
        {
            var probe = BuildProbeProvider();
            if (probe is null) { TestResult.Text = "کلید/تنظیمات ناقص است."; return; }
            var res = await probe.TranslateBatchAsync(new[] { "Hello" },
                SourceLangBox.Text.Trim(), TargetLangBox.Text.Trim());
            TestResult.Foreground = System.Windows.Media.Brushes.LightGreen;
            TestResult.Text = $"موفق ✓ — {res.FirstOrDefault()}";
        }
        catch (Exception ex)
        {
            TestResult.Foreground = System.Windows.Media.Brushes.IndianRed;
            TestResult.Text = "خطا: " + ex.Message;
        }
    }

    private ITranslationProvider? BuildProbeProvider()
    {
        var key = ApiKeyBox.Password;
        if (string.IsNullOrWhiteSpace(key)) return null;
        var kind = (ProviderKind)(ProviderCombo.SelectedValue ?? ProviderKind.OpenAiCompatible);
        var model = ModelBox.Text.Trim();
        var baseUrl = string.IsNullOrWhiteSpace(BaseUrlBox.Text) ? null : BaseUrlBox.Text.Trim();
        return kind switch
        {
            ProviderKind.OpenAiCompatible => new OpenAiProvider(_http, key, model, baseUrl),
            ProviderKind.Anthropic => new AnthropicProvider(_http, key, model, baseUrl),
            ProviderKind.Gemini => new GeminiProvider(_http, key, model, baseUrl),
            ProviderKind.AzureOpenAI => new AzureOpenAiProvider(_http, key, BaseUrlBox.Text.Trim(), DeploymentBox.Text.Trim()),
            _ => null
        };
    }

    private void Profiles_Click(object sender, RoutedEventArgs e)
    {
        var hwnd = (TargetCombo.SelectedItem as WindowInfo)?.Handle
                   ?? _capture.ForegroundWindow();
        var editor = new RegionEditor(_capture, _profiles, hwnd) { Owner = this };
        editor.ShowDialog();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var kind = (ProviderKind)(ProviderCombo.SelectedValue ?? ProviderKind.OpenAiCompatible);
        _settings.Provider = kind;
        _settings.Mode = (TranslationMode)(ModeCombo.SelectedValue ?? TranslationMode.Auto);
        _settings.OverlayMode = (OverlayMode)(OverlayModeCombo.SelectedValue ?? OverlayMode.Inline);
        _settings.Model = ModelBox.Text.Trim();
        _settings.BaseUrl = BaseUrlBox.Text.Trim();
        _settings.AzureDeployment = DeploymentBox.Text.Trim();
        _settings.SourceLanguage = NonEmpty(SourceLangBox.Text, "en");
        _settings.TargetLanguage = NonEmpty(TargetLangBox.Text, "fa");
        _settings.OfflineModelPath = OfflineModelBox.Text.Trim();
        _settings.HotkeyModifiers = (int)_hotkeyMods;
        _settings.HotkeyVirtualKey = (int)_hotkeyVk;

        if (double.TryParse(FontSizeBox.Text, out var fs)) _settings.FontSize = Math.Clamp(fs, 8, 96);
        if (double.TryParse(OpacityBox.Text, out var op)) _settings.OverlayOpacity = Math.Clamp(op, 0, 1);
        if (int.TryParse(LiveIntervalBox.Text, out var li)) _settings.LiveIntervalMs = Math.Max(120, li);

        if (TargetCombo.SelectedItem is WindowInfo wi)
        {
            _settings.TargetWindowContains = wi.Title;
            _applyTarget(wi.Handle);
        }

        // Persist the API key encrypted (only if the user typed one).
        if (!string.IsNullOrWhiteSpace(ApiKeyBox.Password))
            _secrets.Save(ProviderFactory.SecretKey(kind), ApiKeyBox.Password);

        _settings.Save();
        _applySettings();
        Close();
    }

    private static string NonEmpty(string v, string fallback) =>
        string.IsNullOrWhiteSpace(v) ? fallback : v.Trim();

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
