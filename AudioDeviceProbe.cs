using System.Text.RegularExpressions;
using NAudio.CoreAudioApi;

namespace BtHeadphonesBattery;

public static class AudioDeviceProbe
{
    public static string? GetCurrentBluetoothAddress()
    {
        using var enumerator = new MMDeviceEnumerator();
        using var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);

        for (int i = 0; i < device.Properties.Count; i++)
        {
            object? value;

            try
            {
                value = device.Properties[i].Value;
            }
            catch
            {
                continue;
            }

            if (value is not string s)
                continue;

            if (!s.Contains("BTHENUM", StringComparison.OrdinalIgnoreCase))
                continue;

            var match = Regex.Match(
                s,
                @"([0-9A-F]{12})_C00000000",
                RegexOptions.IgnoreCase
            );

            if (match.Success)
                return match.Groups[1].Value.ToUpperInvariant();
        }

        return null;
    }
}