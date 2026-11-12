using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ClipboardImageDrop;

internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _notifyIcon;
    private readonly ClipboardMonitor _clipboardMonitor;
    private readonly ForegroundTracker _foregroundTracker;
    private readonly ToolStripMenuItem _toggleMenuItem;
    private readonly Icon _activeIcon;
    private readonly Icon _pausedIcon;

    public TrayApplicationContext()
    {
        _activeIcon = CreateTrayIcon(Color.FromArgb(0, 120, 215));
        _pausedIcon = CreateTrayIcon(Color.FromArgb(128, 128, 128));

        _clipboardMonitor = new ClipboardMonitor();
        _foregroundTracker = new ForegroundTracker();
        _foregroundTracker.ForegroundTargetChanged += _clipboardMonitor.OnForegroundWindowChanged;

        _toggleMenuItem = new ToolStripMenuItem("Активно")
        {
            Checked = true,
            CheckOnClick = true
        };
        _toggleMenuItem.CheckedChanged += OnToggleCheckedChanged;

        var cleanTempMenuItem = new ToolStripMenuItem("Очистити тимчасові файли");
        cleanTempMenuItem.Click += OnCleanTempClick;

        var exitMenuItem = new ToolStripMenuItem("Вихід");
        exitMenuItem.Click += OnExitClick;

        var contextMenu = new ContextMenuStrip();
        contextMenu.Items.Add(_toggleMenuItem);
        contextMenu.Items.Add(cleanTempMenuItem);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(exitMenuItem);

        _notifyIcon = new NotifyIcon
        {
            Icon = _activeIcon,
            Text = "Clipboard Image Drop (Активно)",
            ContextMenuStrip = contextMenu,
            Visible = true
        };

        _notifyIcon.DoubleClick += (_, _) =>
        {
            _toggleMenuItem.Checked = !_toggleMenuItem.Checked;
        };
    }

    private void OnToggleCheckedChanged(object? sender, EventArgs e)
    {
        bool enabled = _toggleMenuItem.Checked;
        _clipboardMonitor.IsEnabled = enabled;
        _notifyIcon.Icon = enabled ? _activeIcon : _pausedIcon;
        _notifyIcon.Text = enabled
            ? "Clipboard Image Drop (Активно)"
            : "Clipboard Image Drop (Призупинено)";
    }

    private void OnCleanTempClick(object? sender, EventArgs e)
    {
        _clipboardMonitor.ClearCache();
        _clipboardMonitor.CleanupOldTempFiles();
    }

    private void OnExitClick(object? sender, EventArgs e)
    {
        ExitThread();
    }

    protected override void ExitThreadCore()
    {
        _foregroundTracker.ForegroundTargetChanged -= _clipboardMonitor.OnForegroundWindowChanged;
        _foregroundTracker.Dispose();
        _clipboardMonitor.Dispose();

        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _activeIcon.Dispose();
        _pausedIcon.Dispose();

        base.ExitThreadCore();
    }

    private static Icon CreateTrayIcon(Color accentColor)
    {
        using var bmp = new Bitmap(32, 32);
        using (Graphics g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            using var bgBrush = new SolidBrush(accentColor);
            g.FillRoundedRectangle(bgBrush, 2, 4, 28, 26, 6);

            using var clipPen = new Pen(Color.White, 2.2f);
            g.DrawRoundedRectangle(clipPen, 10, 2, 12, 5, 2);

            using var whiteBrush = new SolidBrush(Color.White);
            g.FillPolygon(whiteBrush, new[]
            {
                new Point(7, 24),
                new Point(13, 16),
                new Point(18, 21),
                new Point(21, 18),
                new Point(25, 24)
            });

            g.FillEllipse(whiteBrush, 20, 10, 4, 4);
        }

        IntPtr hIcon = bmp.GetHicon();
        try
        {
            using Icon tempIcon = Icon.FromHandle(hIcon);
            return (Icon)tempIcon.Clone();
        }
        finally
        {
            NativeMethods.DestroyIcon(hIcon);
        }
    }
}

internal static class GraphicsExtensions
{
    public static void FillRoundedRectangle(this Graphics g, Brush brush, int x, int y, int width, int height, int radius)
    {
        using GraphicsPath path = CreateRoundedRectanglePath(x, y, width, height, radius);
        g.FillPath(brush, path);
    }

    public static void DrawRoundedRectangle(this Graphics g, Pen pen, int x, int y, int width, int height, int radius)
    {
        using GraphicsPath path = CreateRoundedRectanglePath(x, y, width, height, radius);
        g.DrawPath(pen, path);
    }

    private static GraphicsPath CreateRoundedRectanglePath(int x, int y, int width, int height, int radius)
    {
        int diameter = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(x, y, diameter, diameter, 180, 90);
        path.AddArc(x + width - diameter, y, diameter, diameter, 270, 90);
        path.AddArc(x + width - diameter, y + height - diameter, diameter, diameter, 0, 90);
        path.AddArc(x, y + height - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}
