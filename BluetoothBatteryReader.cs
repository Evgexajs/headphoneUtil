using System.Text;
using Windows.Devices.Enumeration;

namespace BtHeadphonesBattery;

public static class BluetoothBatteryReader
{
    private static readonly string LogFilePath =
        Path.Combine(AppContext.BaseDirectory, "bt-log.txt");

    public static async Task<(string Name, int BatteryPercent)?> GetHeadphonesBatteryAsync()
    {
        var log = new StringBuilder();
        log.AppendLine($"===== {DateTime.Now:yyyy-MM-dd HH:mm:ss} =====");

        string[] selectors =
        {
            "System.Devices.Aep.ProtocolId:=\"{e0cbf06c-cd8b-4647-bb8a-263b43f0f974}\"",
            "System.Devices.Aep.ProtocolId:=\"{bb7bb05e-5972-42b5-94fc-76eaa7084d49}\""
        };

        string[] props =
        {
            "System.ItemNameDisplay",
            "System.Devices.Aep.IsConnected",
            "System.Devices.Aep.Bluetooth.Le.IsConnectable",
            "System.Devices.Aep.DeviceAddress",
            "System.Devices.AepService.ProtocolId",
            "System.Devices.ClassGuid",
            "System.Devices.ContainerId",
            "System.Devices.InterfaceClassGuid"
        };

        for (int i = 0; i < selectors.Length; i++)
        {
            log.AppendLine($"--- SELECTOR {i} ---");
            var devices = await DeviceInformation.FindAllAsync(selectors[i], props);

            log.AppendLine($"Найдено устройств: {devices.Count}");

            foreach (var d in devices)
            {
                log.AppendLine($"Name='{d.Name}'");
                log.AppendLine($"Id='{d.Id}'");

                foreach (var p in d.Properties)
                {
                    log.AppendLine($"  {p.Key} = {p.Value}");
                }

                log.AppendLine();
            }
        }

        File.AppendAllText(LogFilePath, log.ToString() + Environment.NewLine);
        return null;
    }
}