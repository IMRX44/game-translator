using GameTranslator.Core.Models;

namespace GameTranslator.Core.Capture;

/// <summary>
/// Decides whether a newly captured frame is different enough from the previous one to
/// justify running OCR + translation again. Uses cheap strided pixel sampling (every Nth
/// pixel) instead of comparing the whole buffer, so it stays light on CPU — this is the
/// key to "only translate when the screen actually changed".
/// </summary>
public sealed class FrameDiffer
{
    private readonly int _sampleStride;
    private readonly double _changeThreshold;
    private byte[]? _previousSamples;

    /// <param name="sampleStride">
    /// Sample one pixel every <paramref name="sampleStride"/> pixels. Bigger = cheaper/coarser.
    /// </param>
    /// <param name="changeThreshold">
    /// Fraction of sampled pixels (0..1) that must differ beyond <see cref="_pixelDelta"/>
    /// for the frame to count as changed.
    /// </param>
    public FrameDiffer(int sampleStride = 16, double changeThreshold = 0.01)
    {
        if (sampleStride < 1) throw new ArgumentOutOfRangeException(nameof(sampleStride));
        _sampleStride = sampleStride;
        _changeThreshold = changeThreshold;
    }

    // A single channel must move by at least this much to count as "changed" (ignores noise).
    private const int PixelDelta = 12;

    /// <summary>Forget the previous frame; the next call will report a change.</summary>
    public void Reset() => _previousSamples = null;

    /// <summary>
    /// Returns true when the frame differs enough from the previous one (or when there was
    /// no previous frame). When <paramref name="regions"/> is given, only those areas are
    /// sampled, so the per-game profile makes change detection both cheaper and more focused.
    /// </summary>
    public bool HasChanged(byte[] bgra, int width, int height, IReadOnlyList<RectI>? regions = null)
    {
        var samples = Sample(bgra, width, height, regions);

        if (_previousSamples is null || _previousSamples.Length != samples.Length)
        {
            _previousSamples = samples;
            return true;
        }

        int changed = 0;
        for (int i = 0; i < samples.Length; i++)
        {
            if (Math.Abs(samples[i] - _previousSamples[i]) >= PixelDelta)
                changed++;
        }

        bool isChanged = samples.Length > 0 &&
                         (double)changed / samples.Length >= _changeThreshold;

        if (isChanged)
            _previousSamples = samples;

        return isChanged;
    }

    /// <summary>
    /// Builds a luminance sample array. For region mode we sample within each rect; otherwise
    /// across the whole frame. Luminance is enough to detect text appearing/disappearing.
    /// </summary>
    private byte[] Sample(byte[] bgra, int width, int height, IReadOnlyList<RectI>? regions)
    {
        if (bgra.Length < width * height * 4)
            throw new ArgumentException("Pixel buffer smaller than width*height*4.", nameof(bgra));

        var result = new List<byte>(Math.Max(64, width * height / (_sampleStride * _sampleStride)));

        if (regions is { Count: > 0 })
        {
            foreach (var r in regions)
            {
                int x0 = Math.Clamp(r.X, 0, width);
                int y0 = Math.Clamp(r.Y, 0, height);
                int x1 = Math.Clamp(r.Right, 0, width);
                int y1 = Math.Clamp(r.Bottom, 0, height);
                SampleArea(bgra, width, x0, y0, x1, y1, result);
            }
        }
        else
        {
            SampleArea(bgra, width, 0, 0, width, height, result);
        }

        return result.ToArray();
    }

    private void SampleArea(byte[] bgra, int width, int x0, int y0, int x1, int y1, List<byte> outBytes)
    {
        for (int y = y0; y < y1; y += _sampleStride)
        {
            for (int x = x0; x < x1; x += _sampleStride)
            {
                int idx = (y * width + x) * 4; // BGRA
                byte b = bgra[idx];
                byte g = bgra[idx + 1];
                byte r = bgra[idx + 2];
                // Rec. 601 luma, integer approximation.
                outBytes.Add((byte)((r * 77 + g * 150 + b * 29) >> 8));
            }
        }
    }
}
