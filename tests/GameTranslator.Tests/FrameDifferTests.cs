using GameTranslator.Core.Capture;
using Xunit;

namespace GameTranslator.Tests;

public class FrameDifferTests
{
    private static byte[] SolidFrame(int w, int h, byte v)
    {
        var buf = new byte[w * h * 4];
        Array.Fill(buf, v);
        return buf;
    }

    [Fact]
    public void First_frame_always_changed()
    {
        var d = new FrameDiffer(sampleStride: 4);
        Assert.True(d.HasChanged(SolidFrame(64, 64, 0), 64, 64));
    }

    [Fact]
    public void Identical_frame_is_not_changed()
    {
        var d = new FrameDiffer(sampleStride: 4);
        var frame = SolidFrame(64, 64, 100);
        Assert.True(d.HasChanged(frame, 64, 64));        // first
        Assert.False(d.HasChanged(frame, 64, 64));       // identical
    }

    [Fact]
    public void Large_change_is_detected()
    {
        var d = new FrameDiffer(sampleStride: 4, changeThreshold: 0.01);
        Assert.True(d.HasChanged(SolidFrame(64, 64, 0), 64, 64));
        Assert.True(d.HasChanged(SolidFrame(64, 64, 255), 64, 64));
    }

    [Fact]
    public void Reset_forces_next_change()
    {
        var d = new FrameDiffer(sampleStride: 4);
        var frame = SolidFrame(32, 32, 50);
        d.HasChanged(frame, 32, 32);
        Assert.False(d.HasChanged(frame, 32, 32));
        d.Reset();
        Assert.True(d.HasChanged(frame, 32, 32));
    }
}
