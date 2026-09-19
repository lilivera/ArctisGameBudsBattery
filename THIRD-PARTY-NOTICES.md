# 参照元とライセンス

## InzoneBudsBattery

- https://github.com/zxe-ll/InzoneBudsBattery
- 参照コミット: `be3a1bf8c7a582583cd0ec0276612232a568201d`
- Copyright (c) 2026 zxe-ll / MIT License
- Windows HID列挙・アクセス、Dalamudプラグイン、設定・オーバーレイの実装を改変して使用しています。
- 元の著作権表示とライセンス全文を同梱の `LICENSE` に保持しています。

## HeadsetControl

- https://github.com/Sapd/HeadsetControl/blob/master/lib/devices/steelseries_arctis_gamebuds.hpp
- https://github.com/Sapd/HeadsetControl/blob/master/lib/devices/protocols/steelseries_protocol.hpp
- 機器ID、HIDインターフェース、状態照会コマンド、残量・接続状態のバイト位置を調査する際に参照しました。
- HeadsetControlのソースコードやバイナリは組み込んでいません。C#の通信・解析処理は独自に実装し、Windows実機でReport IDを含む位置を確認しています。
