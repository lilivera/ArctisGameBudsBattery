using Dalamud.Configuration;
using Dalamud.Plugin;

namespace ArctisGameBudsBattery;

public enum EarbudDisplayMode
{
    Individual,
    Minimum,
}

public sealed class Configuration : IPluginConfiguration
{
    [NonSerialized]
    private IDalamudPluginInterface? _pluginInterface;

    public int Version { get; set; } = 1;

    public bool OverlayEnabled { get; set; } = true;

    public EarbudDisplayMode DisplayMode { get; set; } = EarbudDisplayMode.Individual;

    public bool ShowBackground { get; set; } = true;

    public float PositionX { get; set; } = 100f;

    public float PositionY { get; set; } = 100f;

    public float FontScale { get; set; } = 1.15f;

    public float Opacity { get; set; } = 0.75f;

    public bool LockPosition { get; set; }

    public int LowBatteryThreshold { get; set; } = 20;

    public int CriticalBatteryThreshold { get; set; } = 10;

    public bool ShowInCombat { get; set; } = true;

    public bool ShowWhenGameUiHidden { get; set; }

    public bool ShowLastUpdated { get; set; }

    public int StaleAfterMinutes { get; set; } = 2;

    public int RefreshIntervalSeconds { get; set; } = 30;

    public bool HideWhenDisconnected { get; set; }

    public bool DtrEnabled { get; set; } = true;

    public bool DtrShowDetails { get; set; }

    public bool DebugLogging { get; set; }

    public void Initialize(IDalamudPluginInterface pluginInterface)
    {
        _pluginInterface = pluginInterface;
        Normalize();
    }

    public void Save()
    {
        Normalize();
        _pluginInterface?.SavePluginConfig(this);
    }

    private void Normalize()
    {
        if (!float.IsFinite(FontScale)) FontScale = 1.15f;
        if (!float.IsFinite(Opacity)) Opacity = 0.75f;
        if (!float.IsFinite(PositionX)) PositionX = 100f;
        if (!float.IsFinite(PositionY)) PositionY = 100f;
        if (!Enum.IsDefined(DisplayMode)) DisplayMode = EarbudDisplayMode.Individual;
        FontScale = Math.Clamp(FontScale, 0.7f, 3f);
        Opacity = Math.Clamp(Opacity, 0f, 1f);
        LowBatteryThreshold = Math.Clamp(LowBatteryThreshold, 1, 100);
        CriticalBatteryThreshold = Math.Clamp(CriticalBatteryThreshold, 1, LowBatteryThreshold);
        StaleAfterMinutes = Math.Clamp(StaleAfterMinutes, 1, 1440);
        RefreshIntervalSeconds = Math.Clamp(RefreshIntervalSeconds, 5, 300);
    }
}
