using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using GameTranslator.App.Engine;
using GameTranslator.Core.Models;
using GameTranslator.Core.Settings;
using GameTranslator.Platform.Windows.Interop;

namespace GameTranslator.App.Overlay;

/// <summary>
/// A transparent, always-on-top, click-through window that paints translated text over the
/// game. WS_EX_TRANSPARENT makes every click pass through to the game underneath, so it never
/// interferes with play. Nothing is drawn into the game itself — this is a separate window.
/// </summary>
public partial class OverlayWindow : Window
{
    private static readonly FontFamily PersianFont = new("Tahoma, Segoe UI, Arial");

    public OverlayWindow()
    {
        InitializeComponent();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new WindowInteropHelper(this).Handle;
        int ex = NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE);
        ex |= NativeMethods.WS_EX_LAYERED | NativeMethods.WS_EX_TRANSPARENT | NativeMethods.WS_EX_TOOLWINDOW;
        NativeMethods.SetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE, ex);
    }

    /// <summary>Device-pixel → DIP scale factors for the monitor this window is on.</summary>
    private (double sx, double sy) DipFactors()
    {
        var src = PresentationSource.FromVisual(this);
        if (src?.CompositionTarget is { } ct)
        {
            var m = ct.TransformFromDevice; // device px -> DIP
            return (m.M11, m.M22);
        }
        return (1.0, 1.0);
    }

    /// <summary>Position over the target's client area and draw the translated boxes.</summary>
    public void Render(TranslationResult result, AppSettings settings)
    {
        var (sx, sy) = DipFactors();

        Left = result.ClientScreenX * sx;
        Top = result.ClientScreenY * sy;
        Width = Math.Max(1, result.ClientWidth * sx);
        Height = Math.Max(1, result.ClientHeight * sy);

        RootCanvas.Children.Clear();

        if (result.Mode == OverlayMode.Subtitle)
        {
            DrawSubtitle(result, settings, sx, sy);
        }
        else
        {
            foreach (var box in result.Boxes)
                DrawInlineBox(box, settings, sx, sy);
        }

        if (!IsVisible) Show();
    }

    private void DrawInlineBox(TranslatedBox box, AppSettings settings, double sx, double sy)
    {
        var border = MakeLabel(box.Text, settings);
        border.MaxWidth = Math.Max(40, box.Bounds.Width * sx + 20);
        Canvas.SetLeft(border, box.Bounds.X * sx);
        Canvas.SetTop(border, box.Bounds.Y * sy);
        RootCanvas.Children.Add(border);
    }

    private void DrawSubtitle(TranslationResult result, AppSettings settings, double sx, double sy)
    {
        // Join all detected lines into one subtitle bar at the bottom-center.
        var text = string.Join("  ", result.Boxes.Select(b => b.Text));
        if (string.IsNullOrWhiteSpace(text)) return;

        var border = MakeLabel(text, settings);
        border.MaxWidth = Math.Max(200, result.ClientWidth * sx * 0.9);
        border.Measure(new Size(border.MaxWidth, double.PositiveInfinity));

        double w = border.DesiredSize.Width;
        Canvas.SetLeft(border, (result.ClientWidth * sx - w) / 2);
        Canvas.SetTop(border, result.ClientHeight * sy - 80);
        RootCanvas.Children.Add(border);
    }

    private Border MakeLabel(string text, AppSettings settings)
    {
        var tb = new TextBlock
        {
            Text = text,
            Foreground = Brushes.White,
            FontFamily = PersianFont,
            FontSize = settings.FontSize,
            FlowDirection = FlowDirection.RightToLeft,
            TextWrapping = TextWrapping.Wrap
        };
        return new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(
                (byte)(Math.Clamp(settings.OverlayOpacity, 0, 1) * 255), 0, 0, 0)),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(4, 1, 4, 1),
            Child = tb
        };
    }

    public void ClearOverlay()
    {
        RootCanvas.Children.Clear();
        if (IsVisible) Hide();
    }
}
