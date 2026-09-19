using Xunit;

namespace ArctisGameBudsBattery.Tests;

public sealed class ProtocolTests
{
    // 2026-09-19のWindows実機応答。先頭の00はWindows HIDのReport ID。
    private const string CapturedReport = "00B0010103033536030100003C0103CDCD50B5B552000000494950B6B6520000000000000000000000000101010000000000000000000000000000000000000000";

    [Fact]
    public void 実機の65バイト応答を左右53と54として解釈する()
    {
        Assert.True(GameBudsProtocol.TryParse(Convert.FromHexString(CapturedReport), out var reading));
        Assert.Equal(new BatteryReading(53, 54, 3, 3), reading);
    }

    [Theory]
    [InlineData(2, 3, 0, 75, null, 75)]
    [InlineData(3, 2, 42, 0, 42, null)]
    [InlineData(2, 2, 0, 0, null, null)]
    [InlineData(3, 3, 0, 100, 0, 100)]
    [InlineData(2, 3, 255, 75, null, 75)]
    [InlineData(0, 1, 50, 60, null, null)]
    public void 使用中のイヤホンだけを表示する(byte leftStatus, byte rightStatus, byte left, byte right,
        int? expectedLeft, int? expectedRight)
    {
        var report = Convert.FromHexString(CapturedReport);
        report[4] = leftStatus; report[5] = rightStatus;
        report[6] = left; report[7] = right;
        Assert.True(GameBudsProtocol.TryParse(report, out var reading));
        Assert.Equal(expectedLeft, reading!.LeftPercent);
        Assert.Equal(expectedRight, reading.RightPercent);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    [InlineData(64)]
    [InlineData(66)]
    public void 長さが異なる応答を拒否する(int length)
    {
        var report = new byte[length];
        var captured = Convert.FromHexString(CapturedReport);
        captured.AsSpan(0, Math.Min(length, captured.Length)).CopyTo(report);
        Assert.False(GameBudsProtocol.TryParse(report, out _));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 177)]
    [InlineData(6, 101)]
    [InlineData(7, 255)]
    public void 別レポートや不正な残量を拒否する(int index, byte value)
    {
        var report = Convert.FromHexString(CapturedReport);
        report[index] = value;
        Assert.False(GameBudsProtocol.TryParse(report, out var reading));
        Assert.Null(reading);
    }

    [Fact]
    public void 状態照会以外のデータを書き込まない()
    {
        var query = GameBudsProtocol.CreateStatusQuery();
        Assert.Equal(65, query.Length);
        Assert.Equal(0, query[0]);
        Assert.Equal(0xB0, query[1]);
        Assert.All(query.Skip(2), value => Assert.Equal(0, value));
    }

    [Theory]
    [InlineData("mi_03", 0xFFC0, 65, 65, true)]
    [InlineData("mi_05", 0xFF00, 65, 65, false)]
    [InlineData("mi_04", 0x000C, 5, 0, false)]
    [InlineData("mi_05", 0xFFC0, 65, 65, false)]
    [InlineData("mi_03", 0xFFC0, 64, 65, false)]
    public void 対象のHIDインターフェースだけを選択する(string pathPart, ushort usagePage,
        int inputLength, int outputLength, bool expected)
    {
        var device = new WindowsHidDevice($"hid#vid_1038&pid_230a&{pathPart}#device", inputLength, outputLength, usagePage, 1);
        Assert.Equal(expected, GameBudsProtocol.IsBatteryInterface(device));
    }
}
