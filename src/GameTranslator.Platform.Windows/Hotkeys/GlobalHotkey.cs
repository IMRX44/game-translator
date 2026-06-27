using GameTranslator.Platform.Windows.Interop;
using static GameTranslator.Platform.Windows.Interop.NativeMethods;

namespace GameTranslator.Platform.Windows.Hotkeys;

/// <summary>
/// Registers a system-wide hotkey via Win32 <c>RegisterHotKey</c> so it fires even while the
/// game has focus. Kept WPF-free: the host supplies a window handle and forwards window
/// messages into <see cref="ProcessMessage"/>, which raises <see cref="Pressed"/>.
/// </summary>
public sealed class GlobalHotkey : IDisposable
{
    public const int HotkeyId = 0xB001;

    [Flags]
    public enum Modifiers : uint
    {
        Alt = 0x1,
        Control = 0x2,
        Shift = 0x4,
        Win = 0x8,
        NoRepeat = 0x4000
    }

    private readonly IntPtr _hwnd;
    private bool _registered;

    public event Action? Pressed;

    public GlobalHotkey(IntPtr hwnd) => _hwnd = hwnd;

    /// <summary>(Re)register the hotkey. Modifiers/key match the Win32 MOD_*/VK_* values.</summary>
    public bool Register(uint modifiers, uint virtualKey)
    {
        Unregister();
        _registered = RegisterHotKey(_hwnd, HotkeyId, modifiers | (uint)Modifiers.NoRepeat, virtualKey);
        return _registered;
    }

    public void Unregister()
    {
        if (_registered)
        {
            UnregisterHotKey(_hwnd, HotkeyId);
            _registered = false;
        }
    }

    /// <summary>Call from the host window's WndProc. Returns true if the message was the hotkey.</summary>
    public bool ProcessMessage(int msg, IntPtr wParam)
    {
        if (msg == WM_HOTKEY && wParam.ToInt32() == HotkeyId)
        {
            Pressed?.Invoke();
            return true;
        }
        return false;
    }

    public void Dispose() => Unregister();
}
