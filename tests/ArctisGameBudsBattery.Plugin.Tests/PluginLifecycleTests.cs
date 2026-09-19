using Xunit;

namespace ArctisGameBudsBattery.PluginTests;

public sealed class PluginLifecycleTests
{
    [Fact]
    public void 設定保存に失敗しても取得処理と登録をすべて解除する()
    {
        var environment = new TestEnvironment();
        var plugin = Create(environment);
        environment.FailSave = true;

        Assert.Null(Record.Exception(plugin.Dispose));
        Assert.True(environment.BatteryService!.Disposed);
        Assert.Equal(0, environment.CommandCount);
        Assert.Equal(0, environment.DtrCount);
        Assert.Equal(0, environment.Builder.SubscriberCount);
        plugin.Dispose();
        Assert.Equal(1, environment.BatteryService.DisposeCount);
    }

    [Fact]
    public void DTR初期化失敗でも取得処理を終了する()
    {
        var environment = new TestEnvironment { FailDtrGet = true };
        Assert.Throws<ArgumentException>(() => Create(environment));
        Assert.True(environment.BatteryService!.Disposed);
        Assert.Equal(0, environment.CommandCount);
        Assert.Equal(0, environment.Builder.SubscriberCount);
    }

    [Fact]
    public void コマンド重複時に他の所有者のコマンドを消さずに後始末する()
    {
        var environment = new TestEnvironment { CommandAlreadyOwned = true };
        Assert.Throws<InvalidOperationException>(() => Create(environment));
        Assert.True(environment.BatteryService!.Disposed);
        Assert.Equal(0, environment.DtrCount);
        Assert.Equal(0, environment.CommandRemoveCalls);
    }

    [Fact]
    public void 終了処理の途中で例外が起きても残りの後始末を実行する()
    {
        var environment = new TestEnvironment();
        var plugin = Create(environment);
        environment.FailDtrRemove = true;
        Assert.Null(Record.Exception(plugin.Dispose));
        Assert.True(environment.BatteryService!.Disposed);
        Assert.Equal(0, environment.CommandCount);
        Assert.Equal(0, environment.Builder.SubscriberCount);
    }

    [Fact]
    public void UI非表示中のロードでも保存された表示設定を適用する()
    {
        var environment = new TestEnvironment { GameUiHidden = true };
        environment.Configuration.ShowWhenGameUiHidden = true;
        using var plugin = Create(environment);
        Assert.True(environment.Builder.DisableUserUiHide);
        environment.Builder.Dispatch(environment.GameUiHidden);
        Assert.Equal(1, environment.Builder.DrawCount);
    }

    [Fact]
    public void 描画されない間の設定変更も即座に反映する()
    {
        var environment = new TestEnvironment { GameUiHidden = true };
        using var plugin = Create(environment);
        environment.Configuration.ShowWhenGameUiHidden = true;
        environment.Configuration.Save();
        Assert.True(environment.Builder.DisableUserUiHide);
        environment.Builder.Dispatch(true);
        Assert.Equal(1, environment.Builder.DrawCount);
        environment.Configuration.ShowWhenGameUiHidden = false;
        environment.Configuration.Save();
        environment.Builder.Dispatch(true);
        Assert.Equal(1, environment.Builder.DrawCount);
    }

    [Fact]
    public void 保存失敗と初期化失敗でも非表示設定を元へ戻す()
    {
        var environment = new TestEnvironment { FailDtrGet = true };
        environment.Configuration.ShowWhenGameUiHidden = true;
        Assert.Throws<ArgumentException>(() => Create(environment));
        Assert.False(environment.Builder.DisableUserUiHide);

        environment = new TestEnvironment();
        environment.Configuration.ShowWhenGameUiHidden = true;
        var plugin = Create(environment);
        environment.FailSave = true;
        Assert.Null(Record.Exception(plugin.Dispose));
        Assert.False(environment.Builder.DisableUserUiHide);
    }

    private static Plugin Create(TestEnvironment environment) => new(environment, environment, environment,
        environment, environment, environment, environment);
}
