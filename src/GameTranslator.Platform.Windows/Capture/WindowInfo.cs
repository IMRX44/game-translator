namespace GameTranslator.Platform.Windows.Capture;

/// <summary>A top-level window the user can pick as the translation target.</summary>
public sealed record WindowInfo(IntPtr Handle, string Title, string ProcessName)
{
    public override string ToString() =>
        string.IsNullOrEmpty(ProcessName) ? Title : $"{Title}  —  {ProcessName}.exe";
}

/// <summary>A captured frame as top-down 32-bit BGRA pixels.</summary>
public sealed record CapturedFrame(byte[] Bgra, int Width, int Height);
