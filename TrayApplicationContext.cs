using Microsoft.Win32;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace BtHeadphonesBattery;

public sealed class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _notifyIcon;
    private readonly System.Windows.Forms.Timer _timer;

    private string _deviceName = "Наушники";
    private int? _batteryPercent = 72;

    public TrayApplicationContext()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Обновить", null, async (_, _) => await RefreshAsync());
        menu.Items.Add("Автозапуск: вкл/выкл", null, (_, _) => ToggleAutostart());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Выход", null, (_, _) => ExitApplication());

        _notifyIcon = new NotifyIcon
        {
            Visible = true,
            Text = "Bluetooth-наушники",
            ContextMenuStrip = menu,
            Icon = CreateTextIcon("BT")
        };

        _notifyIcon.DoubleClick += async (_, _) => await RefreshAsync();

        _timer = new System.Windows.Forms.Timer
        {
            Interval = 15000
        };
        _timer.Tick += async (_, _) => await RefreshAsync();
        _timer.Start();

        _ = RefreshAsync();
    }

    private Task RefreshAsync()
    {
        try
        {
            string? btAddress = AudioDeviceProbe.GetCurrentBluetoothAddress();

            if (string.IsNullOrWhiteSpace(btAddress))
            {
                _deviceName = "Нет BT";
                _batteryPercent = null;
                _notifyIcon.Text = TrimTooltip("У активного аудиоустройства не найден Bluetooth-адрес");
                _notifyIcon.Icon = CreateTextIcon("??");

                return Task.CompletedTask;
            }

            var pnp = PnpBluetoothDeviceFinder.FindByBluetoothAddress(btAddress);

            string logPath = Path.Combine(AppContext.BaseDirectory, "audio-log.txt");
            File.AppendAllText(
                logPath,
                $"===== {DateTime.Now:yyyy-MM-dd HH:mm:ss} ====={Environment.NewLine}" +
                $"BtAddress: {btAddress}{Environment.NewLine}" +
                $"PnpFriendlyName: {pnp?.FriendlyName ?? "<not found>"}{Environment.NewLine}" +
                $"PnpInstanceId: {pnp?.InstanceId ?? "<not found>"}{Environment.NewLine}{Environment.NewLine}"
            );

            _deviceName = pnp?.FriendlyName ?? "BT device";
            _batteryPercent = null;
            _notifyIcon.Text = TrimTooltip(
                pnp is null
                    ? $"BT адрес {btAddress}, PnP-устройство не найдено"
                    : $"{pnp.FriendlyName}"
            );
            _notifyIcon.Icon = CreateTextIcon("BT");
        }
        catch (Exception ex)
        {
            File.AppendAllText(
                Path.Combine(AppContext.BaseDirectory, "audio-log.txt"),
                $"===== {DateTime.Now:yyyy-MM-dd HH:mm:ss} ====={Environment.NewLine}" +
                $"ERROR: {ex}{Environment.NewLine}{Environment.NewLine}"
            );

            _deviceName = "Ошибка";
            _batteryPercent = null;
            _notifyIcon.Text = TrimTooltip($"Ошибка: {ex.Message}");
            _notifyIcon.Icon = CreateTextIcon("!");
        }

        return Task.CompletedTask;
    }

    private void UpdateTray()
    {
        string textForIcon = _batteryPercent is int p
            ? (p >= 100 ? "100" : p.ToString())
            : "??";

        _notifyIcon.Icon = CreateTextIcon(textForIcon);
        _notifyIcon.Text = TrimTooltip(
            _batteryPercent is int battery
                ? $"{_deviceName}: {battery}%"
                : $"{_deviceName}: заряд неизвестен"
        );
    }

    private static string TrimTooltip(string text)
    {
        return text.Length <= 63 ? text : text[..63];
    }

    private void ToggleAutostart()
    {
        const string runKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string appName = "BtHeadphonesBattery";

        using var key = Registry.CurrentUser.OpenSubKey(runKey, writable: true)
                        ?? Registry.CurrentUser.CreateSubKey(runKey);

        var current = key.GetValue(appName)?.ToString();

        if (string.IsNullOrWhiteSpace(current))
        {
            string exePath = Application.ExecutablePath;
            key.SetValue(appName, $"\"{exePath}\"");
            _notifyIcon.ShowBalloonTip(1500, "Автозапуск", "Автозапуск включён", ToolTipIcon.Info);
        }
        else
        {
            key.DeleteValue(appName, false);
            _notifyIcon.ShowBalloonTip(1500, "Автозапуск", "Автозапуск выключен", ToolTipIcon.Info);
        }
    }

    private void ExitApplication()
    {
        _timer.Stop();
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        ExitThread();
    }

    private static Icon CreateTextIcon(string text)
    {
        const int size = 16;

        using var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);

        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);

        using var bgBrush = new SolidBrush(Color.FromArgb(35, 35, 35));
        using var borderPen = new Pen(Color.FromArgb(90, 90, 90));
        using var textBrush = new SolidBrush(Color.White);

        g.FillRoundedRectangle(bgBrush, new Rectangle(0, 0, 16, 16), 4);
        g.DrawRoundedRectangle(borderPen, new Rectangle(0, 0, 15, 15), 4);

        using var font = new Font("Segoe UI", text.Length >= 3 ? 6.0f : 7.5f, FontStyle.Bold, GraphicsUnit.Pixel);
        var rect = new RectangleF(0, 0, 16, 16);
        var sf = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center
        };

        g.DrawString(text, font, textBrush, rect, sf);

        IntPtr hIcon = bmp.GetHicon();
        try
        {
            using var tmp = Icon.FromHandle(hIcon);
            return (Icon)tmp.Clone();
        }
        finally
        {
            DestroyIcon(hIcon);
        }
    }

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool DestroyIcon(IntPtr handle);
}

internal static class GraphicsExtensions
{
    public static void FillRoundedRectangle(this Graphics g, Brush brush, Rectangle bounds, int radius)
    {
        using var path = RoundedRect(bounds, radius);
        g.FillPath(brush, path);
    }

    public static void DrawRoundedRectangle(this Graphics g, Pen pen, Rectangle bounds, int radius)
    {
        using var path = RoundedRect(bounds, radius);
        g.DrawPath(pen, path);
    }

    private static GraphicsPath RoundedRect(Rectangle bounds, int radius)
    {
        int d = radius * 2;
        var path = new GraphicsPath();

        path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
        path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
        path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
        path.CloseFigure();

        return path;
    }
}