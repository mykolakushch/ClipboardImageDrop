using System;

namespace ClipboardImageDrop;

internal sealed class ForegroundTracker : IDisposable
{
    private IntPtr _hookHandle = IntPtr.Zero;
    private readonly NativeMethods.WinEventDelegate _winEventDelegate;
    private bool _lastIsExplorer;

    public event Action<bool>? ForegroundTargetChanged;

    public bool IsCurrentForegroundExplorer => NativeMethods.IsExplorerOrDesktop(NativeMethods.GetForegroundWindow());

    public ForegroundTracker()
    {
        _winEventDelegate = OnWinEvent;
        _lastIsExplorer = IsCurrentForegroundExplorer;

        _hookHandle = NativeMethods.SetWinEventHook(
            NativeMethods.EVENT_SYSTEM_FOREGROUND,
            NativeMethods.EVENT_SYSTEM_FOREGROUND,
            IntPtr.Zero,
            _winEventDelegate,
            0,
            0,
            NativeMethods.WINEVENT_OUTOFCONTEXT | NativeMethods.WINEVENT_SKIPOWNPROCESS);
    }

    private void OnWinEvent(
        IntPtr hWinEventHook,
        uint eventType,
        IntPtr hwnd,
        int idObject,
        int idChild,
        uint dwEventThread,
        uint dwmsEventTime)
    {
        if (eventType != NativeMethods.EVENT_SYSTEM_FOREGROUND || hwnd == IntPtr.Zero)
        {
            return;
        }

        bool isExplorer = NativeMethods.IsExplorerOrDesktop(hwnd);
        if (isExplorer != _lastIsExplorer)
        {
            _lastIsExplorer = isExplorer;
            ForegroundTargetChanged?.Invoke(isExplorer);
        }
    }

    public void Dispose()
    {
        if (_hookHandle != IntPtr.Zero)
        {
            NativeMethods.UnhookWinEvent(_hookHandle);
            _hookHandle = IntPtr.Zero;
        }
    }
}
