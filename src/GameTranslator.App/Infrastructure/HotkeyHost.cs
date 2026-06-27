using System.Windows.Interop;
using GameTranslator.Platform.Windows.Hotkeys;

namespace GameTranslator.App.Infrastructure;

/// <summary>
/// Hosts an invisible message-only window to receive WM_HOTKEY and drive a
/// <see cref="GlobalHotkey"/>. Keeps all the Win32 message plumbing out of the UI windows.
/// </summary>
public sealed class HotkeyHost : IDisposable
{
    private readonly HwndSource _source;
    private readonly GlobalHotkey _hotkey;

    public event Action? Pressed;

    public HotkeyHost()
    {
        _source = new HwndSource(new HwndSourceParameters("GameTranslatorHotkey")
        {
            Width = 0,
            Height = 0,
            WindowStyle = 0
        });
        _source.AddHook(WndProc);

        _hotkey = new GlobalHotkey(_source.Handle);
        _hotkey.Pressed += () => Pressed?.Invoke();
    }

    /// <summary>(Re)bind the hotkey to the given Win32 modifier flags + virtual-key code.</summary>
    public bool Bind(uint modifiers, uint virtualKey) => _hotkey.Register(modifiers, virtualKey);

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (_hotkey.ProcessMessage(msg, wParam))
            handled = true;
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        _hotkey.Dispose();
        _source.RemoveHook(WndProc);
        _source.Dispose();
    }
}
