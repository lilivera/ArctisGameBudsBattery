namespace ArctisGameBudsBattery;

public static class BatteryTiming
{
    public const int MinimumRefreshSeconds = 5;
    public const int MaximumRefreshSeconds = 300;
    public const int DefaultRefreshSeconds = 30;
    public const int ResponseTimeoutSeconds = 3;

    public static int ClampRefreshSeconds(int seconds) =>
        Math.Clamp(seconds, MinimumRefreshSeconds, MaximumRefreshSeconds);

    public static int MinimumStaleMinutes(int refreshSeconds) =>
        // 次回照会の応答待ちが終わるまで正常な残量を表示できるよう、分単位で切り上げる。
        (ClampRefreshSeconds(refreshSeconds) + ResponseTimeoutSeconds) / 60 + 1;
}
