using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using GameTranslator.Core.Models;
using GameTranslator.Core.Profiles;
using GameTranslator.Platform.Windows.Capture;

namespace GameTranslator.App.Views;

/// <summary>
/// Lets the user draw the rectangles where text appears (menus / subtitle band) on a snapshot
/// of the game window and save them as a per-game profile, so OCR only scans those areas.
/// </summary>
public partial class RegionEditor : Window
{
    private readonly WindowCaptureService _capture;
    private readonly GameProfileStore _profiles;
    private readonly IntPtr _hwnd;

    private int _pixelW, _pixelH;
    private readonly List<RectI> _regions = new();

    private bool _addMode;
    private Point _start;
    private Rectangle? _temp;

    public RegionEditor(WindowCaptureService capture, GameProfileStore profiles, IntPtr hwnd)
    {
        InitializeComponent();
        _capture = capture;
        _profiles = profiles;
        _hwnd = hwnd;

        var (title, proc) = _capture.DescribeWindow(hwnd);
        NameBox.Text = string.IsNullOrEmpty(proc) ? title : proc;

        // Pre-load an existing profile's regions if one matches.
        var existing = _profiles.Resolve(proc, title);
        if (existing is not null)
        {
            NameBox.Text = existing.Name;
            _regions.AddRange(existing.Regions);
        }

        Loaded += (_, _) => { Recapture(); RedrawRegions(); };
    }

    private void Recapture()
    {
        var frame = _capture.CaptureWindow(_hwnd);
        if (frame is null)
        {
            Status.Text = "کپچر ناموفق بود (پنجره مینیمایز/تمام‌صفحه‌ی اختصاصی؟).";
            return;
        }
        _pixelW = frame.Width;
        _pixelH = frame.Height;
        Shot.Source = BitmapSource.Create(frame.Width, frame.Height, 96, 96,
            PixelFormats.Bgra32, null, frame.Bgra, frame.Width * 4);
        Status.Text = $"{frame.Width}×{frame.Height} px";
    }

    private void Recapture_Click(object sender, RoutedEventArgs e) { Recapture(); RedrawRegions(); }

    private void AddMode_Click(object sender, RoutedEventArgs e)
    {
        _addMode = true;
        Hint.Text = "روی تصویر بکشید تا ناحیه ساخته شود";
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        _regions.Clear();
        RedrawRegions();
    }

    // Map a point on the displayed (Uniform-scaled) image back to source pixel coordinates.
    private double Scale() => _pixelW <= 0 || Shot.ActualWidth <= 0 ? 1.0 : _pixelW / Shot.ActualWidth;

    private void Canvas_Down(object sender, MouseButtonEventArgs e)
    {
        if (!_addMode) return;
        _start = e.GetPosition(DrawCanvas);
        _temp = new Rectangle
        {
            Stroke = Brushes.Lime,
            StrokeThickness = 2,
            Fill = new SolidColorBrush(Color.FromArgb(40, 0, 255, 0))
        };
        Canvas.SetLeft(_temp, _start.X);
        Canvas.SetTop(_temp, _start.Y);
        DrawCanvas.Children.Add(_temp);
        DrawCanvas.CaptureMouse();
    }

    private void Canvas_Move(object sender, MouseEventArgs e)
    {
        if (_temp is null) return;
        var p = e.GetPosition(DrawCanvas);
        double x = Math.Min(p.X, _start.X), y = Math.Min(p.Y, _start.Y);
        _temp.Width = Math.Abs(p.X - _start.X);
        _temp.Height = Math.Abs(p.Y - _start.Y);
        Canvas.SetLeft(_temp, x);
        Canvas.SetTop(_temp, y);
    }

    private void Canvas_Up(object sender, MouseButtonEventArgs e)
    {
        DrawCanvas.ReleaseMouseCapture();
        if (_temp is null) return;

        double scale = Scale();
        var rect = new RectI(
            (int)(Canvas.GetLeft(_temp) * scale),
            (int)(Canvas.GetTop(_temp) * scale),
            (int)(_temp.Width * scale),
            (int)(_temp.Height * scale));

        _temp = null;
        _addMode = false;
        Hint.Text = "";

        if (rect.Width > 5 && rect.Height > 5)
            _regions.Add(rect);
        RedrawRegions();
    }

    private void RedrawRegions()
    {
        DrawCanvas.Children.Clear();
        double inv = Scale() == 0 ? 1 : 1 / Scale();
        foreach (var r in _regions)
        {
            var rect = new Rectangle
            {
                Width = r.Width * inv,
                Height = r.Height * inv,
                Stroke = Brushes.DeepSkyBlue,
                StrokeThickness = 2,
                Fill = new SolidColorBrush(Color.FromArgb(40, 0, 180, 255))
            };
            Canvas.SetLeft(rect, r.X * inv);
            Canvas.SetTop(rect, r.Y * inv);
            DrawCanvas.Children.Add(rect);
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var (title, proc) = _capture.DescribeWindow(_hwnd);
        var profile = new GameProfile
        {
            Name = string.IsNullOrWhiteSpace(NameBox.Text) ? (proc.Length > 0 ? proc : "profile") : NameBox.Text.Trim(),
            ProcessName = proc,
            WindowTitleContains = "",
            Regions = new List<RectI>(_regions)
        };
        _profiles.Save(profile);
        Status.Text = "ذخیره شد ✓";
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
