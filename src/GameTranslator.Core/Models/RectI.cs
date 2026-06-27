namespace GameTranslator.Core.Models;

/// <summary>
/// Simple integer rectangle in screen / capture pixel space. Kept platform-agnostic
/// (no dependency on System.Drawing or WPF) so Core can build and be tested on any OS.
/// </summary>
public readonly record struct RectI(int X, int Y, int Width, int Height)
{
    public int Right => X + Width;
    public int Bottom => Y + Height;
    public int Area => Width * Height;

    public bool Contains(int px, int py) => px >= X && px < Right && py >= Y && py < Bottom;

    public bool IntersectsWith(RectI other) =>
        X < other.Right && Right > other.X && Y < other.Bottom && Bottom > other.Y;

    public static RectI FromLtrb(int left, int top, int right, int bottom) =>
        new(left, top, right - left, bottom - top);
}
