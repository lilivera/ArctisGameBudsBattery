using Xunit;

namespace ArctisGameBudsBattery.Tests;

public sealed class StateTests
{
    private static readonly DateTimeOffset ReceivedAt = new(2026, 9, 19, 12, 0, 0, TimeSpan.FromHours(9));
    private static BatteryState Connected() => new BatteryState().Apply(
        new BatteryQueryResult(true, new BatteryReading(53, 54, 3, 3)), ReceivedAt);

    [Fact]
    public void タイムアウトでは残量の鮮度を更新しない()
    {
        var state = Connected().Apply(new BatteryQueryResult(true, Error: "タイムアウト"), ReceivedAt.AddMinutes(3));
        Assert.Equal(ReceivedAt, state.LastUpdatedAt);
        Assert.Equal(53, state.MinimumEarbudPercent);
        Assert.True(state.IsStale(ReceivedAt.AddMinutes(3), TimeSpan.FromMinutes(2)));
        Assert.False(state.HasCurrentReading(ReceivedAt.AddMinutes(3), TimeSpan.FromMinutes(2)));
    }

    [Fact]
    public void 切断したら残量をクリアする()
    {
        var state = Connected().Apply(new BatteryQueryResult(false), ReceivedAt.AddSeconds(30));
        Assert.Null(state.LeftPercent);
        Assert.Null(state.RightPercent);
        Assert.False(state.HasReceivedBatteryReport);
        Assert.Null(state.LastUpdatedAt);
        Assert.False(state.HasCurrentReading(ReceivedAt, TimeSpan.FromMinutes(2)));
    }

    [Fact]
    public void 片耳をケースへ戻したら以前の値を残さない()
    {
        var state = Connected().Apply(new BatteryQueryResult(true, new BatteryReading(null, 52, 2, 3)), ReceivedAt.AddSeconds(30));
        Assert.Null(state.LeftPercent);
        Assert.Equal(52, state.MinimumEarbudPercent);
        Assert.False(state.ShouldTreatAsDisconnected(ReceivedAt.AddSeconds(30), TimeSpan.FromMinutes(2)));
    }

    [Fact]
    public void 両耳がケース内なら非表示条件に一致する()
    {
        var state = Connected().Apply(new BatteryQueryResult(true, new BatteryReading(null, null, 2, 2)), ReceivedAt);
        Assert.True(state.ShouldTreatAsDisconnected(ReceivedAt, TimeSpan.FromMinutes(2)));
        Assert.True(state.IsTransmitterConnected);
    }

    [Fact]
    public void 古い値の境界を含めて判定する()
    {
        Assert.False(Connected().IsStale(ReceivedAt.AddSeconds(119), TimeSpan.FromMinutes(2)));
        Assert.True(Connected().IsStale(ReceivedAt.AddSeconds(120), TimeSpan.FromMinutes(2)));
    }
}
