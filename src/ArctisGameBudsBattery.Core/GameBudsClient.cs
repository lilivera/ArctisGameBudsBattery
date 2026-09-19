namespace ArctisGameBudsBattery;

internal sealed record BatteryQueryResult(
    bool IsConnected, BatteryReading? Reading = null, string? DevicePath = null,
    string? RawReportHex = null, string? Error = null);

internal interface IGameBudsClient
{
    Task<BatteryQueryResult> QueryAsync(CancellationToken cancellationToken);
}

internal sealed class GameBudsClient : IGameBudsClient
{
    public async Task<BatteryQueryResult> QueryAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var device = WindowsHid.Enumerate(GameBudsProtocol.VendorId, GameBudsProtocol.ProductId)
            .FirstOrDefault(GameBudsProtocol.IsBatteryInterface);
        if (device is null) return new BatteryQueryResult(false);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(BatteryTiming.ResponseTimeoutSeconds));
        try
        {
            // 問い合わせごとに開き直し、前回からキューに溜まった応答の再利用を避ける。
            // SteelSeries GGとの共存を考慮して共有ハンドルで開く。
            using var stream = device.OpenReadWrite();
            await stream.WriteAsync(GameBudsProtocol.CreateStatusQuery(), timeout.Token).ConfigureAwait(false);
            await stream.FlushAsync(timeout.Token).ConfigureAwait(false);

            // 同一FileStreamの読み書きは直列にする。先にReadを待機させるとWriteが進まない。
            var input = new byte[GameBudsProtocol.ReportLength];
            while (true)
            {
                var length = await stream.ReadAsync(input, timeout.Token).ConfigureAwait(false);
                if (length == 0) throw new IOException("HID接続が閉じられました。");
                if (GameBudsProtocol.TryParse(input.AsSpan(0, length), out var reading))
                    return new BatteryQueryResult(true, reading, device.DevicePath, Convert.ToHexString(input));
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new BatteryQueryResult(true, DevicePath: device.DevicePath,
                Error: "状態照会がタイムアウトしました。次の照会で再試行します。");
        }
    }
}
