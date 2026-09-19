using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Command;
using Dalamud.Game.Gui.Dtr;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace ArctisGameBudsBattery;

public sealed class Plugin : IDalamudPlugin
{
    private const string CommandName = "/gamebuds";
    private const string DtrEntryName = "ArctisGameBudsBattery";

    private readonly IDalamudPluginInterface _pluginInterface;
    private readonly ICommandManager _commandManager;
    private readonly IPluginLog _log;
    private readonly ICondition _condition;
    private readonly IGameGui _gameGui;
    private readonly INotificationManager _notificationManager;
    private readonly IDtrBar _dtrBar;
    private readonly IDtrBarEntry _dtrEntry;
    private readonly WindowSystem _windowSystem = new("ArctisGameBudsBattery");
    private readonly Configuration _configuration;
    private readonly ArctisBatteryService _batteryService;
    private readonly OverlayWindow _overlayWindow;
    private readonly ConfigWindow _configWindow;
    private readonly Stack<Action> _cleanupActions = new();
    private bool _disposed;

    public Plugin(
        IDalamudPluginInterface pluginInterface,
        ICommandManager commandManager,
        IPluginLog log,
        ICondition condition,
        IGameGui gameGui,
        INotificationManager notificationManager,
        IDtrBar dtrBar)
    {
        _pluginInterface = pluginInterface;
        _commandManager = commandManager;
        _log = log;
        _condition = condition;
        _gameGui = gameGui;
        _notificationManager = notificationManager;
        _dtrBar = dtrBar;

        _configuration = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        var previousUiHide = pluginInterface.UiBuilder.DisableUserUiHide;
        _cleanupActions.Push(() => pluginInterface.UiBuilder.DisableUserUiHide = previousUiHide);
        try
        {
            _configuration.Initialize(pluginInterface);
            _batteryService = new ArctisBatteryService(log, _configuration);
            _cleanupActions.Push(_batteryService.Dispose);

            _overlayWindow = new OverlayWindow(_configuration, _batteryService);
            _configWindow = new ConfigWindow(_configuration, _batteryService, _overlayWindow);
            _cleanupActions.Push(_windowSystem.RemoveAllWindows);
            _windowSystem.AddWindow(_overlayWindow);
            _windowSystem.AddWindow(_configWindow);

            _dtrEntry = dtrBar.Get(DtrEntryName);
            _cleanupActions.Push(() => _dtrBar.Remove(DtrEntryName));
            _cleanupActions.Push(() => _dtrEntry.Shown = false);
            _dtrEntry.Shown = false;

            if (!commandManager.AddHandler(
                    CommandName,
                    new CommandInfo(OnCommand)
                    {
                        HelpMessage = "設定画面を開きます。status / reconnect / debug も使用できます。",
                        ShowInHelp = true,
                    }))
                throw new InvalidOperationException("/gamebuds は既に登録されています。");
            // 自分が登録に成功したコマンドだけを解除する。
            _cleanupActions.Push(() => _commandManager.RemoveHandler(CommandName));

            pluginInterface.UiBuilder.Draw += DrawUi;
            _cleanupActions.Push(() => pluginInterface.UiBuilder.Draw -= DrawUi);
            pluginInterface.UiBuilder.OpenConfigUi += OpenConfigUi;
            _cleanupActions.Push(() => pluginInterface.UiBuilder.OpenConfigUi -= OpenConfigUi);
            pluginInterface.UiBuilder.OpenMainUi += OpenConfigUi;
            _cleanupActions.Push(() => pluginInterface.UiBuilder.OpenMainUi -= OpenConfigUi);
            _log.Info("Arctis GameBuds Batteryを読み込みました。");
        }
        catch
        {
            // 初期化途中で失敗した場合も、取得ワーカーを含む確保済みリソースを解放する。
            _disposed = true;
            CleanupResources();
            throw;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        try
        {
            _configuration.Save();
        }
        catch (Exception exception)
        {
            _log.Warning(exception, "設定の保存に失敗しました。取得処理の終了は継続します。");
        }
        finally
        {
            CleanupResources();
        }
        _log.Info("Arctis GameBuds Batteryをアンロードしました。");
    }

    private void CleanupResources()
    {
        List<Exception>? errors = null;
        while (_cleanupActions.TryPop(out var cleanup))
        {
            try
            {
                cleanup();
            }
            catch (Exception exception)
            {
                // 一つの解除処理が失敗しても、残りをすべて実行する。
                (errors ??= []).Add(exception);
            }
        }
        if (errors is not null)
            _log.Error(new AggregateException(errors), "プラグインの終了処理の一部でエラーが発生しました。");
    }

    private void DrawUi()
    {
        _batteryService.Update();
        var state = _batteryService.Current;
        UpdateDtr(state);

        var shouldTreatAsDisconnected = state.ShouldTreatAsDisconnected(
            DateTimeOffset.Now,
            TimeSpan.FromMinutes(_configuration.StaleAfterMinutes));

        var shouldShowOverlay = _configuration.OverlayEnabled
                                && (_configuration.ShowInCombat || !_condition[ConditionFlag.InCombat])
                                && (_configuration.ShowWhenGameUiHidden || !_gameGui.GameUiHidden)
                                && (!_configuration.HideWhenDisconnected || !shouldTreatAsDisconnected);
        _overlayWindow.IsOpen = shouldShowOverlay;
        _windowSystem.Draw();
    }

    private void UpdateDtr(BatteryState state)
    {
        _dtrEntry.Shown = _configuration.DtrEnabled;
        if (!_configuration.DtrEnabled)
        {
            return;
        }

        var hasCurrentBatteryReport = state.HasCurrentReading(
            DateTimeOffset.Now, TimeSpan.FromMinutes(_configuration.StaleAfterMinutes));
        var leftPercent = hasCurrentBatteryReport ? state.LeftPercent : null;
        var rightPercent = hasCurrentBatteryReport ? state.RightPercent : null;
        var minimumEarbudPercent = hasCurrentBatteryReport ? state.MinimumEarbudPercent : null;

        _dtrEntry.Text = _configuration.DtrShowDetails
            ? FormatEarbuds(leftPercent, rightPercent)
            : minimumEarbudPercent is { } minimum
                ? $"GameBuds {minimum}%"
                : "GameBuds --";

        _dtrEntry.Tooltip = state.IsTransmitterConnected
            ? hasCurrentBatteryReport
                ? state.MinimumEarbudPercent is null
                    ? "Arctis GameBuds\nイヤホン未接続（ケース内・電源OFFなど）"
                    : $"Arctis GameBuds\nL {Format(state.LeftPercent)}  R {Format(state.RightPercent)}"
                : "Arctis GameBuds\n残量取得待ち、または古い値"
            : "Arctis GameBuds\n未接続";
    }

    private void OnCommand(string command, string arguments)
    {
        switch (arguments.Trim().ToLowerInvariant())
        {
            case "":
                _configWindow.Toggle();
                break;
            case "status":
                ShowStatusNotification();
                break;
            case "reconnect":
                _batteryService.RequestReconnect();
                _notificationManager.AddNotification(
                    new Notification
                    {
                        Title = "Arctis GameBuds Battery",
                        Content = "HID再接続を開始しました。",
                        Type = NotificationType.Info,
                    });
                break;
            case "debug":
                _configuration.DebugLogging = !_configuration.DebugLogging;
                _configuration.Save();
                _notificationManager.AddNotification(
                    new Notification
                    {
                        Title = "Arctis GameBuds Battery",
                        Content = $"デバッグログ: {(_configuration.DebugLogging ? "ON" : "OFF")}",
                        Type = NotificationType.Info,
                    });
                break;
            default:
                _notificationManager.AddNotification(
                    new Notification
                    {
                        Title = "Arctis GameBuds Battery",
                        Content = "使用法: /gamebuds [status|reconnect|debug]",
                        Type = NotificationType.Warning,
                    });
                break;
        }
    }

    private void OpenConfigUi()
    {
        _configWindow.IsOpen = true;
        _configWindow.BringToFront();
    }

    private void ShowStatusNotification()
    {
        var state = _batteryService.Current;
        var content = !state.IsTransmitterConnected
            ? "トランシッター未接続"
            : !state.HasReceivedBatteryReport
                ? "接続済み・残量取得待ち"
                : state.MinimumEarbudPercent is null
                    ? "イヤホン未接続（ケース内・電源OFFなど）"
                    : $"L {Format(state.LeftPercent)} / R {Format(state.RightPercent)}";
        if (state.IsStale(DateTimeOffset.Now, TimeSpan.FromMinutes(_configuration.StaleAfterMinutes)))
            content += "（古い値）";

        _notificationManager.AddNotification(
            new Notification
            {
                Title = "Arctis GameBuds Battery",
                Content = content,
                Type = NotificationType.Info,
            });
    }

    private static string Format(int? value) => value is { } percentage ? $"{percentage}%" : "--";

    private static string FormatEarbuds(int? left, int? right) => (left, right) switch
    {
        ({ } l, { } r) => $"L{l}% R{r}%",
        ({ } l, null) => $"L{l}%",
        (null, { } r) => $"R{r}%",
        _ => "GameBuds --",
    };
}
