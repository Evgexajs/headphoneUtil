using System.Diagnostics;
using System.Text.Json;

namespace BtHeadphonesBattery;

public static class PnpBatteryReader
{
    public sealed class BatteryReadResult
    {
        public string? FriendlyName { get; set; }
        public string? RootInstanceId { get; set; }
        public string? BatteryNodeInstanceId { get; set; }
        public int? BatteryPercent { get; set; }
    }

    public static BatteryReadResult? ReadByRootInstanceId(string rootInstanceId)
    {
        string script = $$$"""
$rootInstanceId = '{{{rootInstanceId}}}'

$rootProps = Get-PnpDeviceProperty -InstanceId $rootInstanceId -ErrorAction SilentlyContinue
if ($null -eq $rootProps) {
    return
}

$friendlyName = ($rootProps | Where-Object { $_.KeyName -eq 'DEVPKEY_Device_FriendlyName' } | Select-Object -First 1).Data
$siblingsProp = $rootProps | Where-Object { $_.KeyName -eq 'DEVPKEY_Device_Siblings' } | Select-Object -First 1

$batteryNode = $null
$batteryValue = $null

if ($null -ne $siblingsProp -and $null -ne $siblingsProp.Data) {
    $siblings = @($siblingsProp.Data)

    foreach ($sib in $siblings) {
        if ($sib -notlike 'BTHENUM\{0000111e-0000-1000-8000-00805f9b34fb}_*') {
            continue
        }

        $batteryProp = Get-PnpDeviceProperty -InstanceId $sib -KeyName '{104EA319-6EE2-4701-BD47-8DDBF425BBE5} 2' -ErrorAction SilentlyContinue

        if ($null -ne $batteryProp -and $null -ne $batteryProp.Data) {
            $batteryNode = $sib
            $batteryValue = [int]$batteryProp.Data
            break
        }
    }
}

$result = [PSCustomObject]@{
    FriendlyName = if ($null -ne $friendlyName) { [string]$friendlyName } else { $null }
    RootInstanceId = $rootInstanceId
    BatteryNodeInstanceId = if ($null -ne $batteryNode) { [string]$batteryNode } else { $null }
    BatteryPercent = if ($null -ne $batteryValue) { [int]$batteryValue } else { $null }
}

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

        return JsonSerializer.Deserialize<BatteryReadResult>(
            stdout,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
        );
    }

    private static string EscapeForPowerShellArg(string value)
    {
        return value.Replace("\"", "`\"");
    }
}