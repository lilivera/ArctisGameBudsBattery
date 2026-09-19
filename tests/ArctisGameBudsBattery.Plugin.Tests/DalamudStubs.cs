// 製品のPlugin.cs / Configuration.csを直接コンパイルし、ゲームサービスだけを代替する。
// 描画内容や実機通信のテストではなく、登録・解放と設定適用の回帰テストに使用する。
using ArctisGameBudsBattery;
using ArctisGameBudsBattery.PluginTests;
using Dalamud.Configuration;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Command;
using Dalamud.Game.Gui.Dtr;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace Dalamud.Configuration
{
    public interface IPluginConfiguration { int Version { get; set; } }
}
namespace Dalamud.Plugin
{
    public interface IDalamudPlugin : IDisposable { }
    public interface IDalamudPluginInterface
    {
        TestUiBuilder UiBuilder { get; }
        IPluginConfiguration GetPluginConfig();
        void SavePluginConfig(IPluginConfiguration config);
    }
}
namespace Dalamud.Game.ClientState.Conditions
{
    public enum ConditionFlag { InCombat }
}
namespace Dalamud.Game.Command
{
    public sealed class CommandInfo(Action<string, string> handler)
    {
        public Action<string, string> Handler { get; } = handler;
        public string HelpMessage { get; set; } = "";
        public bool ShowInHelp { get; set; }
    }
}
namespace Dalamud.Game.Gui.Dtr
{
    public interface IDtrBarEntry
    {
        bool Shown { get; set; }
        string Text { get; set; }
        string Tooltip { get; set; }
    }
}
namespace Dalamud.Interface.ImGuiNotification
{
    public enum NotificationType { Info, Warning }
    public sealed class Notification
    {
        public string Title { get; set; } = "";
        public string Content { get; set; } = "";
        public NotificationType Type { get; set; }
    }
}
namespace Dalamud.Plugin.Services
{
    public interface ICommandManager { bool AddHandler(string command, CommandInfo info); void RemoveHandler(string command); }
    public interface IPluginLog
    {
        void Info(string message);
        void Warning(Exception exception, string message);
        void Error(Exception exception, string message);
    }
    public interface ICondition { bool this[ConditionFlag flag] { get; } }
    public interface IGameGui { bool GameUiHidden { get; } }
    public interface INotificationManager { void AddNotification(Notification notification); }
    public interface IDtrBar { IDtrBarEntry Get(string name); void Remove(string name); }
}
namespace Dalamud.Interface.Windowing
{
    public sealed class WindowSystem
    {
        public WindowSystem(string name) { }
        public void AddWindow(object window) { }
        public void RemoveAllWindows() { }
        public void Draw() { }
    }
}
namespace ArctisGameBudsBattery
{
    internal sealed class ArctisBatteryService
    {
        public bool Disposed => DisposeCount > 0;
        public int DisposeCount { get; private set; }
        public ArctisBatteryService(IPluginLog log, Configuration config) { ((TestEnvironment)log).BatteryService = this; }
        public BatteryState Current { get; } = new();
        public void Update() { }
        public void RequestReconnect() { }
        public void Dispose() { DisposeCount++; }
    }
    internal sealed class OverlayWindow
    {
        public OverlayWindow(Configuration config, ArctisBatteryService service) { }
        public bool IsOpen { get; set; }
    }
    internal sealed class ConfigWindow
    {
        public ConfigWindow(Configuration config, ArctisBatteryService service, OverlayWindow overlay) { }
        public bool IsOpen { get; set; }
        public void Toggle() { IsOpen = !IsOpen; }
        public void BringToFront() { }
    }
}
namespace ArctisGameBudsBattery.PluginTests
{
    public sealed class TestUiBuilder
    {
        public bool DisableUserUiHide { get; set; }
        public int DrawCount { get; private set; }
        public int SubscriberCount => (Draw?.GetInvocationList().Length ?? 0)
            + (OpenConfigUi?.GetInvocationList().Length ?? 0) + (OpenMainUi?.GetInvocationList().Length ?? 0);
        public event Action? Draw;
        public event Action? OpenConfigUi;
        public event Action? OpenMainUi;
        public void Dispatch(bool gameUiHidden)
        {
            // Dalamud 15.0.3.5のOnDrawと同じ順序で非表示を判定する。
            if (gameUiHidden && !DisableUserUiHide) return;
            DrawCount++;
            Draw?.Invoke();
        }
    }

    internal sealed class TestEnvironment : IDalamudPluginInterface, ICommandManager, IPluginLog,
        ICondition, IGameGui, INotificationManager, IDtrBar, IDtrBarEntry
    {
        public Configuration Configuration { get; } = new();
        public TestUiBuilder Builder { get; } = new();
        public TestUiBuilder UiBuilder => Builder;
        public ArctisBatteryService? BatteryService { get; set; }
        public bool FailSave { get; set; }
        public bool FailDtrGet { get; set; }
        public bool FailDtrRemove { get; set; }
        public bool CommandAlreadyOwned { get; set; }
        public int CommandRemoveCalls { get; private set; }
        public int CommandCount { get; private set; }
        public int DtrCount { get; private set; }
        public bool GameUiHidden { get; set; }
        public IPluginConfiguration GetPluginConfig() => Configuration;
        public void SavePluginConfig(IPluginConfiguration config)
        {
            if (FailSave) throw new IOException("保存失敗を注入");
        }
        public bool AddHandler(string command, CommandInfo info)
        {
            if (CommandAlreadyOwned) return false;
            CommandCount++;
            return true;
        }
        public void RemoveHandler(string command) { CommandRemoveCalls++; CommandCount--; }
        public void Info(string message) { }
        public void Warning(Exception exception, string message) { }
        public void Error(Exception exception, string message) { }
        public bool this[ConditionFlag flag] => false;
        public void AddNotification(Notification notification) { }
        public IDtrBarEntry Get(string name)
        {
            if (FailDtrGet) throw new ArgumentException("DTR重複を注入");
            DtrCount++;
            return this;
        }
        public void Remove(string name)
        {
            if (FailDtrRemove) throw new InvalidOperationException("DTR解除失敗を注入");
            DtrCount--;
        }
        public bool Shown { get; set; }
        public string Text { get; set; } = "";
        public string Tooltip { get; set; } = "";
    }
}
