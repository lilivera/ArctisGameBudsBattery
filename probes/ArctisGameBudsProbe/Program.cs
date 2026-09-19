using ArctisGameBudsBattery;

var list = false;
var raw = false;
var samples = 1;
var intervalSeconds = 5;
for (var index = 0; index < args.Length; index++)
{
    switch (args[index])
    {
        case "--list": list = true; break;
        case "--raw": raw = true; break;
        case "--samples" when index + 1 < args.Length && int.TryParse(args[++index], out samples)
            && samples is >= 1 and <= 1000: break;
        case "--interval" when index + 1 < args.Length && int.TryParse(args[++index], out intervalSeconds)
            && intervalSeconds is >= 1 and <= 300: break;
        default:
            Console.Error.WriteLine("使用法: ArctisGameBudsProbe [--list] [--raw] [--samples 1..1000] [--interval 1..300]");
            return 2;
    }
}

using var shutdown = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) => { eventArgs.Cancel = true; shutdown.Cancel(); };
try
{
    if (list)
    {
        var devices = WindowsHid.Enumerate(GameBudsProtocol.VendorId, GameBudsProtocol.ProductId);
        foreach (var device in devices)
            Console.WriteLine($"{(GameBudsProtocol.IsBatteryInterface(device) ? "対象" : "対象外")}: 入力={device.InputReportLength} 出力={device.OutputReportLength} Usage={device.UsagePage:X4}:{device.Usage:X4}\n  {device.DevicePath}");
        if (devices.Count == 0) Console.WriteLine("GameBudsのUSBドングルが見つかりません。");
        return devices.Any(GameBudsProtocol.IsBatteryInterface) ? 0 : 1;
    }

    var client = new GameBudsClient();
    var failures = 0;
    for (var sample = 0; sample < samples; sample++)
    {
        var result = await client.QueryAsync(shutdown.Token);
        var prefix = $"{DateTimeOffset.Now:HH:mm:ss} [{sample + 1}/{samples}]";
        if (result.Reading is { } reading)
        {
            Console.WriteLine($"{prefix} 左={Format(reading.LeftPercent)} 右={Format(reading.RightPercent)} 状態={reading.LeftStatus:X2}/{reading.RightStatus:X2}");
            if (raw) Console.WriteLine($"受信: {result.RawReportHex}");
        }
        else
        {
            failures++;
            Console.Error.WriteLine($"{prefix} {result.Error ?? "USBドングル未接続"}");
        }
        if (sample + 1 < samples)
            await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), shutdown.Token);
    }
    return failures == 0 ? 0 : 1;
}
catch (OperationCanceledException) when (shutdown.IsCancellationRequested)
{
    Console.WriteLine("終了しました。");
    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine($"取得失敗: {exception.Message}");
    return 1;
}

static string Format(int? percentage) => percentage is { } value ? $"{value}%" : "未接続";
