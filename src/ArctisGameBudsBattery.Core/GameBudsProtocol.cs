namespace ArctisGameBudsBattery;

public sealed record BatteryReading(int? LeftPercent, int? RightPercent, byte LeftStatus, byte RightStatus);

internal static class GameBudsProtocol
{
    public const int VendorId = 0x1038;
    public const int ProductId = 0x230A;
    public const int ReportLength = 65;
    public const byte StatusCommand = 0xB0;
    public const byte ActiveStatus = 0x03;

    public static bool IsBatteryInterface(WindowsHidDevice device) =>
        device.UsagePage == 0xFFC0 && device.Usage == 0x0001
        && device.InputReportLength == ReportLength && device.OutputReportLength == ReportLength
        && device.DevicePath.Contains("&mi_03", StringComparison.OrdinalIgnoreCase);

    public static byte[] CreateStatusQuery()
    {
        // WindowsではReport ID 0を含む65バイトで送る。機器の設定は変更しない。
        var report = new byte[ReportLength];
        report[1] = StatusCommand;
        return report;
    }

    public static bool TryParse(ReadOnlySpan<byte> report, out BatteryReading? reading)
    {
        reading = null;
        // 別コマンドの応答や切れたレポートを残量として扱わない。
        if (report.Length != ReportLength || report[0] != 0 || report[1] != StatusCommand)
            return false;

        // HIDAPIの資料はReport IDを除いた位置。Windowsの受信配列では1バイト後ろになる。
        var leftStatus = report[4];
        var rightStatus = report[5];
        if ((leftStatus == ActiveStatus && report[6] > 100)
            || (rightStatus == ActiveStatus && report[7] > 100))
            return false;

        // ケース内・電源OFF側の0やFFは0%として表示しない。
        reading = new BatteryReading(
            leftStatus == ActiveStatus ? report[6] : null,
            rightStatus == ActiveStatus ? report[7] : null,
            leftStatus, rightStatus);
        return true;
    }
}
