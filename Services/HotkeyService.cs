using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;
using MajsoulReview.Models;

namespace MajsoulReview.Services;

public sealed class HotkeyService : IDisposable
{
    private const int HotkeyMessage = 0x0312;
    private const int HotkeyId = 0x4D52;
    private const uint NoRepeat = 0x4000;

    private IntPtr _windowHandle;
    private HwndSource? _source;
    private bool _registered;

    public event EventHandler? Pressed;

    public void Initialize(IntPtr windowHandle)
    {
        _windowHandle = windowHandle;
        _source = HwndSource.FromHwnd(windowHandle);
        _source?.AddHook(WindowProc);
    }

    public bool Register(AppSettings settings)
    {
        Unregister();

        if (!Enum.TryParse<Key>(settings.Hotkey, ignoreCase: true, out var key))
        {
            return false;
        }

        uint modifiers = NoRepeat;
        if (settings.UseAlt) modifiers |= 0x0001;
        if (settings.UseControl) modifiers |= 0x0002;
        if (settings.UseShift) modifiers |= 0x0004;
        if (settings.UseWindows) modifiers |= 0x0008;

        var virtualKey = (uint)KeyInterop.VirtualKeyFromKey(key);
        _registered = RegisterHotKey(_windowHandle, HotkeyId, modifiers, virtualKey);
        return _registered;
    }

    public void Unregister()
    {
        if (_registered)
        {
            UnregisterHotKey(_windowHandle, HotkeyId);
            _registered = false;
        }
    }

    public void Dispose()
    {
        Unregister();
        _source?.RemoveHook(WindowProc);
    }

    private IntPtr WindowProc(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == HotkeyMessage && wParam.ToInt32() == HotkeyId)
        {
            Pressed?.Invoke(this, EventArgs.Empty);
            handled = true;
        }

        return IntPtr.Zero;
    }

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint virtualKey);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr window, int id);
}
