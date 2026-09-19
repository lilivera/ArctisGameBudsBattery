using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace ArctisGameBudsBattery;

internal sealed class OverlayWindow : Window
{
    private static readonly Vector4 NormalColor = new(1f, 1f, 1f, 1f);
    private static readonly Vector4 LowColor = new(1f, 0.78f, 0.2f, 1f);
    private static readonly Vector4 CriticalColor = new(1f, 0.25f, 0.2f, 1f);
    private static readonly Vector4 StaleColor = new(0.72f, 0.72f, 0.72f, 1f);

    private readonly Configuration _configuration;
    private readonly ArctisBatteryService _batteryService;
    private bool _moveRequested = true;
    private Vector2? _lastPosition;
    private bool _positionChanged;

    public OverlayWindow(Configuration configuration, ArctisBatteryService batteryService)
        : base("Arctis GameBuds Overlay###ArctisGameBudsBatteryOverlay")
    {
        _configuration = configuration;
        _batteryService = batteryService;
        DisableWindowSounds = true;
        DisableFadeInFadeOut = true;
        ShowCloseButton = false;
        RespectCloseHotkey = false;
        Position = new Vector2(configuration.PositionX, configuration.PositionY);
        PositionCondition = ImGuiCond.FirstUseEver;
        IsOpen = true;
    }

    public override void PreDraw()
    {
        // Alwaysを保持するとドラッグできなくなるので、移動指示は一度だけ適用する。
        Position = _moveRequested ? new Vector2(_configuration.PositionX, _configuration.PositionY) : null;
        PositionCondition = ImGuiCond.Always;
        _moveRequested = false;
        Flags = ImGuiWindowFlags.NoTitleBar
                | ImGuiWindowFlags.NoScrollbar
                | ImGuiWindowFlags.NoScrollWithMouse
                | ImGuiWindowFlags.AlwaysAutoResize
                | ImGuiWindowFlags.NoSavedSettings
                | ImGuiWindowFlags.NoFocusOnAppearing
                | ImGuiWindowFlags.NoNav;

        if (_configuration.LockPosition)
        {
            Flags |= ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoInputs;
        }

        BgAlpha = _configuration.ShowBackground ? _configuration.Opacity : 0f;
    }

    public override void Draw()
    {
        ImGui.SetWindowFontScale(_configuration.FontScale);
        var state = _batteryService.Current;

        var compactDisplay = _configuration.DisplayMode == EarbudDisplayMode.Minimum
                             && state.IsTransmitterConnected
                             && state.HasReceivedBatteryReport;
        if (!compactDisplay)
        {
            ImGui.TextUnformatted("Arctis GameBuds");
        }

        if (!state.IsTransmitterConnected)
        {
            DrawColoredText("未接続", StaleColor);
            RememberPosition();
            return;
        }

        if (!state.HasReceivedBatteryReport)
        {
            DrawColoredText("残量取得待ち...", StaleColor);
            RememberPosition();
            return;
        }

        var isStale = state.IsStale(
            DateTimeOffset.Now,
            TimeSpan.FromMinutes(_configuration.StaleAfterMinutes));
        var color = GetBatteryColor(state.MinimumEarbudPercent, isStale);

        if (state.MinimumEarbudPercent is null)
        {
            DrawColoredText("イヤホン未接続", StaleColor);
        }
        else if (_configuration.DisplayMode == EarbudDisplayMode.Minimum)
        {
            DrawColoredText(
                state.MinimumEarbudPercent is { } minimum ? $"GameBuds {minimum}%" : "GameBuds --%",
                color);
        }
        else
        {
            if (state.LeftPercent is { } left)
                DrawColoredText($"L {left}%", GetBatteryColor(left, isStale));
            if (state.RightPercent is { } right)
            {
                if (state.LeftPercent is not null) ImGui.SameLine();
                DrawColoredText($"R {right}%", GetBatteryColor(right, isStale));
            }
        }

        if (isStale) DrawColoredText("古い値", StaleColor);

        if (_configuration.ShowLastUpdated && state.LastUpdatedAt is { } lastUpdated)
        {
            var age = DateTimeOffset.Now - lastUpdated;
            var ageText = age.TotalMinutes < 1
                ? "たった今"
                : age.TotalHours < 1
                    ? $"{(int)age.TotalMinutes}分前"
                    : $"{(int)age.TotalHours}時間前";
            DrawColoredText($"更新: {ageText}", StaleColor);
        }

        RememberPosition();
    }

    public void MoveToConfiguredPosition()
    {
        _moveRequested = true;
    }

    private void RememberPosition()
    {
        var position = ImGui.GetWindowPos();
        if (!_configuration.LockPosition && _lastPosition is { } previous && position != previous)
        {
            _configuration.PositionX = position.X;
            _configuration.PositionY = position.Y;
            _positionChanged = true;
        }
        _lastPosition = position;

        // 設定欄の数値を毎フレーム上書きせず、ドラッグ終了時にだけ位置を保存する。
        if (_positionChanged && !ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            _positionChanged = false;
            _configuration.Save();
        }
    }

    private Vector4 GetBatteryColor(int? percentage, bool isStale)
    {
        if (isStale || percentage is null)
        {
            return StaleColor;
        }

        if (percentage <= _configuration.CriticalBatteryThreshold)
        {
            return CriticalColor;
        }

        return percentage <= _configuration.LowBatteryThreshold ? LowColor : NormalColor;
    }

    private static void DrawColoredText(string text, Vector4 color)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, color);
        ImGui.TextUnformatted(text);
        ImGui.PopStyleColor();
    }
}
