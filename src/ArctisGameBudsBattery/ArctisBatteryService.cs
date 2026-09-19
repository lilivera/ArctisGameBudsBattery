using Dalamud.Plugin.Services;

namespace ArctisGameBudsBattery;

internal sealed class ArctisBatteryService : IDisposable
{
    private readonly BatteryMonitor _monitor = new();
    private readonly IPluginLog _log;
    private readonly Configuration _configuration;
    private DateTimeOffset? _lastLoggedAttempt;
    private string? _lastError;

    public ArctisBatteryService(IPluginLog log, Configuration configuration)
    {
        _log = log;
        _configuration = configuration;
        _monitor.SetRefreshInterval(configuration.RefreshIntervalSeconds);
    }

    public BatteryState Current => _monitor.Current;
    public void RequestReconnect() => _monitor.RequestReconnect();
    public void Dispose() => _monitor.Dispose();

    public void Update()
    {
        _monitor.SetRefreshInterval(_configuration.RefreshIntervalSeconds);
        var state = Current;
        if (state.LastRefreshAttemptAt == _lastLoggedAttempt) return;
        _lastLoggedAttempt = state.LastRefreshAttemptAt;
        if (state.ErrorMessage is { } error && error != _lastError)
            _log.Warning("GameBuds: {Error}", error);
        _lastError = state.ErrorMessage;
        if (_configuration.DebugLogging)
            _log.Debug("GameBuds HID: 接続={Connected}, L={Left}, R={Right}, 生データ={Report}",
                state.IsTransmitterConnected, state.LeftPercent?.ToString() ?? "--",
                state.RightPercent?.ToString() ?? "--", state.RawReportHex ?? "未取得");
    }
}
