namespace ArctisGameBudsBattery;

public sealed record BatteryState
{
    public int? LeftPercent { get; init; }
    public int? RightPercent { get; init; }
    public byte? LeftStatus { get; init; }
    public byte? RightStatus { get; init; }
    public DateTimeOffset? LastUpdatedAt { get; init; }
    public DateTimeOffset? LastRefreshAttemptAt { get; init; }
    public bool IsTransmitterConnected { get; init; }
    public bool HasReceivedBatteryReport { get; init; }
    public string? ErrorMessage { get; init; }
    public string? DevicePath { get; init; }
    public string? RawReportHex { get; init; }

    public int? MinimumEarbudPercent => LeftPercent is { } left && RightPercent is { } right
        ? Math.Min(left, right) : LeftPercent ?? RightPercent;

    public bool IsStale(DateTimeOffset now, TimeSpan staleAfter) =>
        LastUpdatedAt is { } updated && now - updated >= staleAfter;

    public bool HasCurrentReading(DateTimeOffset now, TimeSpan staleAfter) =>
        IsTransmitterConnected && HasReceivedBatteryReport && !IsStale(now, staleAfter);

    public bool ShouldTreatAsDisconnected(DateTimeOffset now, TimeSpan staleAfter) =>
        !HasCurrentReading(now, staleAfter) || MinimumEarbudPercent is null;

    internal BatteryState Apply(BatteryQueryResult result, DateTimeOffset now)
    {
        var updated = this with
        {
            IsTransmitterConnected = result.IsConnected,
            DevicePath = result.DevicePath,
            ErrorMessage = result.Error,
            LastRefreshAttemptAt = now,
        };
        if (!result.IsConnected)
            return updated with
            {
                LeftPercent = null, RightPercent = null, LeftStatus = null, RightStatus = null,
                HasReceivedBatteryReport = false, LastUpdatedAt = null, RawReportHex = null,
            };

        // タイムアウトで以前の残量の受信時刻を更新しない。
        if (result.Reading is not { } reading) return updated;
        return updated with
        {
            LeftPercent = reading.LeftPercent, RightPercent = reading.RightPercent,
            LeftStatus = reading.LeftStatus, RightStatus = reading.RightStatus,
            HasReceivedBatteryReport = true, LastUpdatedAt = now, RawReportHex = result.RawReportHex,
        };
    }
}
