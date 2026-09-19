namespace ArctisGameBudsBattery;

public sealed class BatteryMonitor : IDisposable, IAsyncDisposable
{
    private readonly IGameBudsClient _client;
    private readonly object _gate = new();
    private readonly Task _worker;
    private CancellationTokenSource? _cycle;
    private BatteryState _state = new();
    private int _refreshSeconds = 30;
    private long _generation;
    private bool _disposed;

    public BatteryMonitor() : this(new GameBudsClient()) { }

    internal BatteryMonitor(IGameBudsClient client)
    {
        _client = client;
        _worker = Task.Run(RunAsync);
    }

    public BatteryState Current => Volatile.Read(ref _state);

    public void SetRefreshInterval(int seconds) => Volatile.Write(ref _refreshSeconds, Math.Clamp(seconds, 5, 300));

    public void RequestReconnect()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _generation++;
            Volatile.Write(ref _state, new BatteryState());
            // 待機中も読み取り中もキャンセルして、次の列挙へ進める。
            _cycle?.Cancel();
        }
    }

    public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();

    public async ValueTask DisposeAsync()
    {
        lock (_gate)
        {
            _disposed = true;
            _cycle?.Cancel();
        }
        await _worker.ConfigureAwait(false);
    }

    private async Task RunAsync()
    {
        while (true)
        {
            CancellationTokenSource cycle;
            long generation;
            lock (_gate)
            {
                if (_disposed) return;
                cycle = new CancellationTokenSource();
                _cycle = cycle;
                generation = _generation;
            }

            try
            {
                BatteryQueryResult result;
                try
                {
                    result = await _client.QueryAsync(cycle.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cycle.IsCancellationRequested) { throw; }
                catch (Exception exception)
                {
                    // デバイス取り外しや共有オープン失敗で、ゲーム側へ例外を伝播させない。
                    result = new BatteryQueryResult(false, Error: $"HID取得失敗: {exception.Message}");
                }

                lock (_gate)
                {
                    if (!_disposed && generation == _generation)
                        Volatile.Write(ref _state, Current.Apply(result, DateTimeOffset.Now));
                }

                var seconds = result.IsConnected && result.Reading is not null
                    ? Volatile.Read(ref _refreshSeconds) : 5;
                await Task.Delay(TimeSpan.FromSeconds(seconds), cycle.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cycle.IsCancellationRequested)
            {
                // 再接続・アンロードによるキャンセルは正常終了として扱う。
            }
            finally
            {
                lock (_gate)
                {
                    _cycle = null;
                    cycle.Dispose();
                }
            }
        }
    }
}
