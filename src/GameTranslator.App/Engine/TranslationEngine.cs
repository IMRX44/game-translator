using System.Net.Http;
using GameTranslator.Core.Abstractions;
using GameTranslator.Core.Cache;
using GameTranslator.Core.Capture;
using GameTranslator.Core.Models;
using GameTranslator.Core.Profiles;
using GameTranslator.Core.Settings;
using GameTranslator.Core.Translation;
using GameTranslator.Platform.Windows.Capture;
using GameTranslator.Providers;
using GameTranslator.Providers.Offline;

namespace GameTranslator.App.Engine;

/// <summary>
/// The pipeline that ties everything together: capture a frame → skip it if nothing changed
/// (frame-diff) → OCR (only the profile's regions when defined) → translate the batch through
/// the hybrid translator → emit a <see cref="TranslationResult"/> for the overlay.
/// </summary>
public sealed class TranslationEngine : IDisposable
{
    private readonly WindowCaptureService _capture;
    private readonly IOcrEngine _ocr;
    private readonly GameProfileStore _profiles;
    private readonly TranslationCache _cache;
    private readonly GlossaryProvider _glossary;
    private readonly ISecretStore _secrets;
    private readonly HttpClient _http;
    private readonly FrameDiffer _differ;

    private CancellationTokenSource? _liveCts;

    /// <summary>Raised on the thread pool with each (instant then refined) overlay frame.</summary>
    public event Action<TranslationResult>? ResultReady;
    /// <summary>Raised when the overlay should be cleared (toggle off / no text).</summary>
    public event Action? Cleared;
    public event Action<string>? Error;

    public AppSettings Settings { get; set; }
    public IntPtr TargetWindow { get; set; }

    public TranslationEngine(
        AppSettings settings, WindowCaptureService capture, IOcrEngine ocr,
        GameProfileStore profiles, TranslationCache cache, GlossaryProvider glossary,
        ISecretStore secrets, HttpClient http)
    {
        Settings = settings;
        _capture = capture;
        _ocr = ocr;
        _profiles = profiles;
        _cache = cache;
        _glossary = glossary;
        _secrets = secrets;
        _http = http;
        _differ = new FrameDiffer(sampleStride: Math.Max(2, settings.FrameDiffStride));
    }

    public bool IsLive => _liveCts is { IsCancellationRequested: false };

    /// <summary>Resolve the active target window (explicit selection, else foreground).</summary>
    private IntPtr ResolveTarget()
    {
        if (TargetWindow != IntPtr.Zero) return TargetWindow;
        return _capture.ForegroundWindow();
    }

    /// <summary>Translate the current frame once. <paramref name="force"/> bypasses the frame-diff gate.</summary>
    public async Task TranslateOnceAsync(bool force, CancellationToken ct = default)
    {
        try
        {
            var hwnd = ResolveTarget();
            if (hwnd == IntPtr.Zero) return;

            var profile = ResolveProfile(hwnd);
            var mode = profile?.OverlayMode ?? Settings.OverlayMode;

            CapturedFrame? frame;
            int originX, originY, regionOffsetY = 0;

            if (mode == OverlayMode.Subtitle)
            {
                // Subtitle mode: only capture the bottom strip of the window.
                var (clientW, clientH) = _capture.ClientSize(hwnd);
                var (sx, sy) = _capture.ClientOrigin(hwnd);
                if (clientW <= 0 || clientH <= 0) return;
                int stripH = Math.Max(40, clientH / 4);
                regionOffsetY = clientH - stripH;
                frame = _capture.CaptureScreenRegion(sx, sy + regionOffsetY, clientW, stripH);
                originX = sx; originY = sy;
            }
            else
            {
                frame = _capture.CaptureWindow(hwnd);
                var (sx, sy) = _capture.ClientOrigin(hwnd);
                originX = sx; originY = sy;
            }

            if (frame is null) return;

            // Frame-diff gate: skip OCR/translation when the screen hasn't changed.
            var regions = profile is { Regions.Count: > 0 } && mode != OverlayMode.Subtitle
                ? profile.Regions
                : null;
            if (!force && !_differ.HasChanged(frame.Bgra, frame.Width, frame.Height, regions))
                return;

            var blocks = await _ocr.RecognizeAsync(
                frame.Bgra, frame.Width, frame.Height, Settings.SourceLanguage, regions, ct);

            if (blocks.Count == 0)
            {
                Cleared?.Invoke();
                return;
            }

            var lines = blocks.Select(b => b.Text).ToList();
            var translator = BuildTranslator();

            void Emit(IReadOnlyList<string> translations)
            {
                var boxes = new List<TranslatedBox>(blocks.Count);
                for (int i = 0; i < blocks.Count; i++)
                {
                    var b = blocks[i];
                    var bounds = mode == OverlayMode.Subtitle
                        ? new RectI(b.Bounds.X, b.Bounds.Y + regionOffsetY, b.Bounds.Width, b.Bounds.Height)
                        : b.Bounds;
                    boxes.Add(new TranslatedBox(bounds, translations[i]));
                }
                var (cw, ch) = _capture.ClientSize(hwnd);
                ResultReady?.Invoke(new TranslationResult(boxes, originX, originY, cw, ch, mode));
            }

            var final = await translator.TranslateAsync(
                lines, Settings.SourceLanguage, Settings.TargetLanguage, Settings.Mode,
                onInstant: Emit, ct: ct);
            Emit(final);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Error?.Invoke(ex.Message);
        }
    }

    public void StartLive()
    {
        StopLive();
        _differ.Reset();
        _liveCts = new CancellationTokenSource();
        var ct = _liveCts.Token;
        _ = Task.Run(async () =>
        {
            while (!ct.IsCancellationRequested)
            {
                await TranslateOnceAsync(force: false, ct);
                try { await Task.Delay(Math.Max(120, Settings.LiveIntervalMs), ct); }
                catch (OperationCanceledException) { break; }
            }
        }, ct);
    }

    public void StopLive()
    {
        _liveCts?.Cancel();
        _liveCts?.Dispose();
        _liveCts = null;
    }

    private GameProfile? ResolveProfile(IntPtr hwnd)
    {
        var (title, proc) = _capture.DescribeWindow(hwnd);
        return _profiles.Resolve(proc, title);
    }

    /// <summary>Build a fresh hybrid translator reflecting the current provider/key settings.</summary>
    private HybridTranslator BuildTranslator()
    {
        ITranslationProvider? online = ProviderFactory.CreateOnline(Settings, _secrets, _http);
        var nmt = new OnnxNmtProvider(Settings.OfflineModelPath);
        return new HybridTranslator(_cache, _glossary,
            offlineNmt: nmt.IsAvailable ? nmt : null,
            online: online);
    }

    public void Dispose() => StopLive();
}
