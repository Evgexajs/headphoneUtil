using System.Diagnostics;
using System.Text.Json;

namespace BtHeadphonesBattery;

public static class PnpBluetoothDeviceFinder
{
    public sealed class PnpBluetoothDevice
    {
        public string? FriendlyName { get; set; }
        public string? InstanceId { get; set; }
        public string? DeviceAddress { get; set; }
    }

    public static PnpBluetoothDevice? FindByBluetoothAddress(string btAddress)
    {
        string script = $$$"""
$addr = '{{{btAddress}}}'
$result = Get-PnpDevice -PresentOnly -Class Bluetooth -Status OK |
    ForEach-Object {
        $props = Get-PnpDeviceProperty -InstanceId $_.InstanceId -ErrorAction SilentlyContinue
        $addrProp = $props | Where-Object { $_.KeyName -eq 'DEVPKEY_Bluetooth_DeviceAddress' } | Select-Object -First 1

        [PSCustomObject]@{
            FriendlyName = $_.FriendlyName
            InstanceId = $_.InstanceId
            DeviceAddress = $addrProp.Data
        }
    } |
    Where-Object {
        $_.DeviceAddress -eq $addr -and $_.InstanceId -like 'BTHENUM\DEV_*'
    } |
    Select-Object -First 1

$result | ConvertTo-Json -Compress
""";

        var psi = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{EscapeForPowerShellArg(script)}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi);
        if (process is null)
            return null;

        string stdout = process.StandardOutput.ReadToEnd();
        string stderr = process.StandardError.ReadToEnd();

        process.WaitForExit();

        if (process.ExitCode != 0)
            throw new Exception($"powershell error: {stderr}");

        if (string.IsNullOrWhiteSpace(stdout) || stdout.Trim() == "null")
            return null;

        return JsonSerializer.Deserialize<PnpBluetoothDevice>(
            stdout,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
        );
    }

    private static string EscapeForPowerShellArg(string value)
    {
        return value.Replace("\"", "`\"");
    }
}