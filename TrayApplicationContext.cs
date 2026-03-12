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
    private readonly AppSettings _settings;

    private string _deviceName = "Наушники";
    private int? _batteryPercent = null;

    public TrayApplicationContext()
    {
        _settings = AppSettings.Load();
    
        var menu = new ContextMenuStrip();

        var refreshMenu = new ToolStripMenuItem("Частота обновления");
        AddRefreshMenuItem(refreshMenu, "30 сек", 30);
        AddRefreshMenuItem(refreshMenu, "1 мин", 60);
        AddRefreshMenuItem(refreshMenu, "5 мин", 300);
        AddRefreshMenuItem(refreshMenu, "15 мин", 900);
        AddRefreshMenuItem(refreshMenu, "30 мин", 1800);
        AddRefreshMenuItem(refreshMenu, "1 час", 3600);
        menu.Items.Add(refreshMenu);

        menu.Items.Add("Обновить", null, async (_, _) => await RefreshAsync());
        
        var autostartItem = new ToolStripMenuItem();
        autostartItem.Click += (_, _) =>
        {
            ToggleAutostart();
            autostartItem.Text = GetAutostartMenuText();
        };

        autostartItem.Text = GetAutostartMenuText();
        menu.Items.Add(autostartItem);

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
            Interval = _settings.RefreshSeconds * 1000
        };
        _timer.Tick += async (_, _) => await RefreshAsync();
        _timer.Start();

        _ = RefreshAsync();
    }

    private static string GetAutostartMenuText()
    {
        const string runKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string appName = "BtHeadphonesBattery";

        using var key = Registry.CurrentUser.OpenSubKey(runKey, writable: false);

        var current = key?.GetValue(appName)?.ToString();

        return string.IsNullOrWhiteSpace(current)
            ? "Включить автозапуск"
            : "Выключить автозапуск";
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
                UpdateTray();
                return Task.CompletedTask;
            }

            var pnp = PnpBluetoothDeviceFinder.FindByBluetoothAddress(btAddress);

            var batteryInfo = pnp?.InstanceId is not null
                ? PnpBatteryReader.ReadByRootInstanceId(pnp.InstanceId)
                : null;

            _deviceName = batteryInfo?.FriendlyName
                ?? pnp?.FriendlyName
                ?? "BT device";

            _batteryPercent = batteryInfo?.BatteryPercent;
            
            UpdateTray();
        }
        catch (Exception ex)
        {
            _deviceName = "Ошибка";
            _batteryPercent = null;
            _notifyIcon.Text = TrimTooltip($"Ошибка: {ex.Message}");

            var oldIcon = _notifyIcon.Icon;
            _notifyIcon.Icon = CreateTextIcon("!");
            oldIcon?.Dispose();
        }

        return Task.CompletedTask;
    }

    private void AddRefreshMenuItem(ToolStripMenuItem parent, string text, int seconds)
    {
        var item = new ToolStripMenuItem(text)
        {
            Checked = _settings.RefreshSeconds == seconds
        };

        item.Click += (_, _) =>
        {
            _settings.RefreshSeconds = seconds;
            _settings.Save();
            _timer.Interval = seconds * 1000;

            foreach (ToolStripMenuItem sibling in parent.DropDownItems)
                sibling.Checked = false;

            item.Checked = true;

            _notifyIcon.ShowBalloonTip(
                1500,
                "Частота обновления",
                $"Теперь обновление каждые {text}",
                ToolTipIcon.Info
            );
        };

        parent.DropDownItems.Add(item);
    }

    private void UpdateTray()
    {
        string textForIcon = _batteryPercent is int p
            ? (p >= 100 ? "100" : p.ToString())
            : "??";

        var oldIcon = _notifyIcon.Icon;
        _notifyIcon.Icon = CreateTextIcon(textForIcon);
        oldIcon?.Dispose();

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

        var oldIcon = _notifyIcon.Icon;
        _notifyIcon.Icon = null;
        oldIcon?.Dispose();

        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        ExitThread();
    }

    private static Icon CreateTextIcon(string text)
    {
        const int size = 16;

        using var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);

        g.SmoothingMode = SmoothingMode.None;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.SingleBitPerPixelGridFit;
        g.Clear(Color.Transparent);

        using var textBrush = new SolidBrush(Color.White);

        float fontSize =
            text.Length switch
            {
                1 => 12f,
                2 => 10f,
                _ => 7f
            };

        using var font = new Font("Segoe UI", fontSize, FontStyle.Bold, GraphicsUnit.Pixel);

        var rect = new RectangleF(0, -1, 16, 16);
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