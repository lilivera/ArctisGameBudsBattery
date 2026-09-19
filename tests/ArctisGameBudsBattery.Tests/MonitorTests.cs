using Xunit;

namespace ArctisGameBudsBattery.Tests;

public sealed class MonitorTests
{
    [Fact]
    public async Task 再接続で処理中の読み取りをキャンセルして取得を再開する()
    {
        var started = Signal();
        var cancelled = Signal();
        var calls = 0;
        var client = new FakeClient(async token =>
        {
            if (Interlocked.Increment(ref calls) == 1)
            {
                started.SetResult();
                try { await Task.Delay(Timeout.Infinite, token); }
                catch (OperationCanceledException) { cancelled.SetResult(); throw; }
            }
            return new BatteryQueryResult(true, new BatteryReading(40, 50, 3, 3));
        });
        await using var monitor = new BatteryMonitor(client);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(3));
        monitor.RequestReconnect();
        await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(3));
        await WaitUntil(() => monitor.Current.LeftPercent == 40);
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task アンロードで待機中の読み取りを終了する()
    {
        var started = Signal();
        var monitor = new BatteryMonitor(new FakeClient(async token =>
        {
            started.SetResult();
            await Task.Delay(Timeout.Infinite, token);
            return new BatteryQueryResult(false);
        }));
        try { await started.Task.WaitAsync(TimeSpan.FromSeconds(3)); }
        finally { await monitor.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(3)); }
        await monitor.DisposeAsync();
        monitor.RequestReconnect();
    }

    [Fact]
    public async Task 再接続前の遅延応答を新しい接続に反映しない()
    {
        var firstStarted = Signal();
        var secondStarted = Signal();
        var releaseFirst = Signal();
        var calls = 0;
        await using var monitor = new BatteryMonitor(new FakeClient(async token =>
        {
            if (Interlocked.Increment(ref calls) == 1)
            {
                firstStarted.SetResult();
                // キャンセル直前に受信が完了する競合を再現する。
                await releaseFirst.Task;
                return new BatteryQueryResult(true, new BatteryReading(99, 99, 3, 3));
            }
            secondStarted.SetResult();
            await Task.Delay(Timeout.Infinite, token);
            return new BatteryQueryResult(false);
        }));
        try
        {
            await firstStarted.Task.WaitAsync(TimeSpan.FromSeconds(3));
            monitor.RequestReconnect();
        }
        finally { releaseFirst.TrySetResult(); }
        await secondStarted.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.False(monitor.Current.HasReceivedBatteryReport);
        Assert.Null(monitor.Current.LeftPercent);
    }

    [Fact]
    public async Task 通信例外を状態へ変換し再接続後に回復する()
    {
        var calls = 0;
        await using var monitor = new BatteryMonitor(new FakeClient(_ =>
            Interlocked.Increment(ref calls) == 1 ? throw new IOException("取り外し")
                : Task.FromResult(new BatteryQueryResult(true, new BatteryReading(20, 30, 3, 3)))));
        await WaitUntil(() => monitor.Current.ErrorMessage is not null);
        Assert.False(monitor.Current.IsTransmitterConnected);
        monitor.RequestReconnect();
        await WaitUntil(() => monitor.Current.LeftPercent == 20);
        Assert.Null(monitor.Current.ErrorMessage);
    }

    private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static async Task WaitUntil(Func<bool> predicate)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        while (!predicate()) await Task.Delay(10, deadline.Token);
    }

    private sealed class FakeClient(Func<CancellationToken, Task<BatteryQueryResult>> query) : IGameBudsClient
    {
        public Task<BatteryQueryResult> QueryAsync(CancellationToken cancellationToken) => query(cancellationToken);
    }
}
