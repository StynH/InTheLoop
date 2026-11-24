using System.Diagnostics;
using System.Runtime.InteropServices;

namespace InTheLoop.App.Services;

internal sealed class UiThread : IUiThread
{
    public void Post(Action action)
    {
        if (MainThread.IsMainThread)
        {
            action();
        }
        else
        {
            MainThread.BeginInvokeOnMainThread(action);
        }
    }
}

internal sealed class DialogService : IDialogService
{
    public Task<bool> ConfirmAsync(string title, string message, string accept, string cancel)
    {
        var page = Application.Current?.Windows.FirstOrDefault()?.Page;
        return page is null ? Task.FromResult(false) : page.DisplayAlertAsync(title, message, accept, cancel);
    }
}

internal sealed class ShellService : IShellService
{
    public Task OpenFileAsync(string path) =>
        File.Exists(path) ? Launcher.Default.OpenAsync(new OpenFileRequest(Path.GetFileName(path), new ReadOnlyFile(path))) : Task.CompletedTask;

    public void OpenFolder(string path)
    {
        Directory.CreateDirectory(path);
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
    }

    public Task CopyTextAsync(string text) => Clipboard.Default.SetTextAsync(text);
}

internal sealed class AttentionService : IAttentionService
{
    private const uint FlashAll = 0x3;
    private const uint FlashUntilForeground = 0xC;

    public void Request()
    {
        if (Application.Current?.Windows.FirstOrDefault()?.Handler?.PlatformView is not MauiWinUIWindow window)
        {
            return;
        }

        var info = new FlashInfo
        {
            Size = (uint)Marshal.SizeOf<FlashInfo>(),
            Window = window.WindowHandle,
            Flags = FlashAll | FlashUntilForeground,
            Count = 3
        };
        FlashWindowEx(ref info);
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool FlashWindowEx(ref FlashInfo info);

    [StructLayout(LayoutKind.Sequential)]
    private struct FlashInfo
    {
        public uint Size;
        public nint Window;
        public uint Flags;
        public uint Count;
        public uint Timeout;
    }
}

internal sealed class McpServerLocator : IMcpServerLocator
{
    private const string FileName = "intheloop-mcp.exe";

    public string? ExecutablePath { get; } = Locate();

    private static string? Locate()
    {
        var shipped = Path.Combine(AppContext.BaseDirectory, "mcp", FileName);
        if (File.Exists(shipped))
        {
            return shipped;
        }

        var beside = Path.Combine(AppContext.BaseDirectory, FileName);
        return File.Exists(beside) ? beside : null;
    }
}
