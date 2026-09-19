using Xunit;

namespace ArctisGameBudsBattery.PluginTests;

public sealed class ConfigurationTests
{
    [Theory]
    [InlineData(5, 1)]
    [InlineData(30, 1)]
    [InlineData(60, 2)]
    [InlineData(119, 3)]
    [InlineData(120, 3)]
    [InlineData(300, 6)]
    public void 更新間隔と応答待ちより長い鮮度しきい値を確保する(int refreshSeconds, int expectedMinutes)
    {
        var config = new Configuration { RefreshIntervalSeconds = refreshSeconds, StaleAfterMinutes = 1 };
        config.Save();
        Assert.Equal(expectedMinutes, config.StaleAfterMinutes);
        var now = DateTimeOffset.Now;
        var state = new BatteryState { IsTransmitterConnected = true, HasReceivedBatteryReport = true,
            LeftPercent = 52, RightPercent = 52, LastUpdatedAt = now };
        Assert.True(state.HasCurrentReading(now.AddSeconds(refreshSeconds + 3), TimeSpan.FromMinutes(config.StaleAfterMinutes)));
        Assert.True(state.IsStale(now.AddMinutes(config.StaleAfterMinutes), TimeSpan.FromMinutes(config.StaleAfterMinutes)));
    }

    [Fact]
    public void 保存済みの矛盾する設定を読み込み時にも補正する()
    {
        var environment = new TestEnvironment();
        environment.Configuration.RefreshIntervalSeconds = 300;
        environment.Configuration.StaleAfterMinutes = 2;
        environment.Configuration.Initialize(environment);
        Assert.Equal(6, environment.Configuration.StaleAfterMinutes);
    }

    [Fact]
    public void 利用者が指定した十分に長い鮮度しきい値を維持する()
    {
        var config = new Configuration { RefreshIntervalSeconds = 300, StaleAfterMinutes = 20 };
        config.Save();
        Assert.Equal(20, config.StaleAfterMinutes);
    }
}
