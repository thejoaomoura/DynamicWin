using System;
using System.Runtime.InteropServices;
using Halo.Interop;

namespace Halo.Shell;

/// <summary>
/// Halo's icon in the notification area, and the menu behind it. Until now the pill was the whole
/// interface: there was no way to put it away for a moment, and no way to quit short of Task
/// Manager. This is that missing handle on the app.
///
/// It owns a window of its own rather than borrowing the notch's. The notch is WS_EX_NOACTIVATE by
/// design -- it must never steal focus from what you are working in -- and a popup menu whose owner
/// cannot take the foreground never closes when you click away from it.
/// </summary>
internal sealed class TrayIcon
{
    private const string ClassName = "HaloTrayWindow";
    private const string Tooltip = "Halo";
    private const uint CallbackMessage = Win32.WM_APP + 1;
    private const uint IconId = 1;
    private const uint CmdToggle = 1, CmdExit = 2;

    private Win32.WndProc _wndProc = null!;
    private uint _taskbarCreated;
    private IntPtr _hwnd;
    private IntPtr _icon;
    private bool _added;

    /// <summary>The user asked for the pill to be put away, or brought back.</summary>
    public event Action? ToggleRequested;

    /// <summary>The user asked Halo to quit.</summary>
    public event Action? ExitRequested;

    /// <summary>
    /// Whether the pill is currently hidden. The tray does not decide this -- it is told, and only
    /// uses it to word the first menu item.
    /// </summary>
    public bool Hidden { get; set; }

    public void Show()
    {
        var hInstance = Win32.GetModuleHandle(null);
        _wndProc = WndProc;

        var wc = new Win32.WNDCLASSEX
        {
            cbSize = Marshal.SizeOf<Win32.WNDCLASSEX>(),
            lpfnWndProc = _wndProc,
            hInstance = hInstance,
            lpszClassName = ClassName,
        };
        if (Win32.RegisterClassEx(ref wc) == 0)
            throw new InvalidOperationException($"RegisterClassEx failed: {Marshal.GetLastWin32Error()}");

        // Never shown, and kept out of Alt-Tab: it exists to receive the icon's callbacks and to
        // own the menu.
        _hwnd = Win32.CreateWindowEx(Win32.WS_EX_TOOLWINDOW, ClassName, Tooltip, Win32.WS_POPUP,
            0, 0, 0, 0, IntPtr.Zero, IntPtr.Zero, hInstance, IntPtr.Zero);
        if (_hwnd == IntPtr.Zero)
            throw new InvalidOperationException($"CreateWindowEx failed: {Marshal.GetLastWin32Error()}");

        // An Explorer restart takes every notification icon with it. This is how the shell tells
        // the processes that outlived it to put theirs back.
        _taskbarCreated = Win32.RegisterWindowMessageW("TaskbarCreated");
        _icon = LoadAppIcon();
        Add();
    }

    /// <summary>
    /// Takes the icon out of the notification area. Safe to call twice, which matters because exit
    /// runs it once on the way out and once more after the message loop returns.
    /// </summary>
    public void Remove()
    {
        if (_added)
        {
            var data = NewData();
            Win32.Shell_NotifyIcon(Win32.NIM_DELETE, ref data);
            _added = false;
        }
        if (_icon != IntPtr.Zero)
        {
            Win32.DestroyIcon(_icon);
            _icon = IntPtr.Zero;
        }
    }

    private void Add()
    {
        var data = NewData();
        data.uFlags = Win32.NIF_MESSAGE | Win32.NIF_ICON | Win32.NIF_TIP;
        data.uCallbackMessage = CallbackMessage;
        data.hIcon = _icon;
        data.szTip = Tooltip;
        _added = Win32.Shell_NotifyIcon(Win32.NIM_ADD, ref data);
    }

    private Win32.NOTIFYICONDATAW NewData() => new()
    {
        cbSize = Marshal.SizeOf<Win32.NOTIFYICONDATAW>(),
        hWnd = _hwnd,
        uID = IconId,
        szTip = "",
        szInfo = "",
        szInfoTitle = "",
    };

    /// <summary>
    /// The icon Halo already ships as its executable's own, at whatever size this machine's shell
    /// asks for. An icon we cannot read is not worth failing over: the entry still appears, just
    /// blank, and the menu behind it still works.
    /// </summary>
    private static IntPtr LoadAppIcon()
    {
        try
        {
            string? exe = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exe)) return IntPtr.Zero;
            var icons = new IntPtr[1];
            var ids = new int[1];
            uint n = Win32.PrivateExtractIcons(exe, 0,
                Win32.GetSystemMetrics(Win32.SM_CXSMICON), Win32.GetSystemMetrics(Win32.SM_CYSMICON),
                icons, ids, 1, 0);
            return n > 0 ? icons[0] : IntPtr.Zero;
        }
        catch { return IntPtr.Zero; }
    }

    private void ShowMenu()
    {
        var menu = Win32.CreatePopupMenu();
        if (menu == IntPtr.Zero) return;
        try
        {
            Win32.AppendMenuW(menu, Win32.MF_STRING, (UIntPtr)CmdToggle, Hidden ? "Show Halo" : "Hide Halo");
            Win32.AppendMenuW(menu, Win32.MF_SEPARATOR, UIntPtr.Zero, null);
            Win32.AppendMenuW(menu, Win32.MF_STRING, (UIntPtr)CmdExit, "Exit Halo");

            // A tray menu only dismisses on click-away if its owner is the foreground window, and
            // the trailing post is what lets it dismiss the first time it is opened.
            Win32.SetForegroundWindow(_hwnd);
            Win32.GetCursorPos(out var p);
            int cmd = Win32.TrackPopupMenuEx(menu,
                Win32.TPM_RIGHTBUTTON | Win32.TPM_RETURNCMD | Win32.TPM_NONOTIFY,
                p.X, p.Y, _hwnd, IntPtr.Zero);
            Win32.PostMessage(_hwnd, Win32.WM_NULL, IntPtr.Zero, IntPtr.Zero);

            if (cmd == (int)CmdToggle) ToggleRequested?.Invoke();
            else if (cmd == (int)CmdExit) ExitRequested?.Invoke();
        }
        finally { Win32.DestroyMenu(menu); }
    }

    private IntPtr WndProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == _taskbarCreated && _taskbarCreated != 0)
        {
            _added = false;
            Add();
            return IntPtr.Zero;
        }

        if (msg == CallbackMessage)
        {
            uint mouse = (uint)(lParam.ToInt64() & 0xFFFF);
            if (mouse == Win32.WM_LBUTTONUP) ToggleRequested?.Invoke();
            else if (mouse is Win32.WM_RBUTTONUP or Win32.WM_CONTEXTMENU) ShowMenu();
            return IntPtr.Zero;
        }

        return Win32.DefWindowProc(hwnd, msg, wParam, lParam);
    }
}
