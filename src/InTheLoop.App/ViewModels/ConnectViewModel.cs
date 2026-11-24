using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InTheLoop.App.Services;

namespace InTheLoop.App.ViewModels;

/// <summary>One way of hooking an AI client up to the MCP server, with a snippet to copy.</summary>
public partial class ConnectOption(string name, string description, string snippet, IShellService shell) : ObservableObject
{
    public string Name => name;

    public string Description => description;

    public string Snippet => snippet;

    [ObservableProperty]
    public partial string CopyLabel { get; set; } = "Copy";

    [RelayCommand]
    private async Task CopyAsync()
    {
        await shell.CopyTextAsync(Snippet);
        CopyLabel = "Copied";
        await Task.Delay(1500);
        CopyLabel = "Copy";
    }
}

public sealed record ToolSummary(string Name, string Description);

public class ConnectViewModel
{
    public ConnectViewModel(IMcpServerLocator locator, IShellService shell)
    {
        var path = locator.ExecutablePath;
        ServerFound = path is not null;
        path ??= @"C:\path\to\intheloop-mcp.exe";
        var escaped = path.Replace(@"\", @"\\");

        Options =
        [
            new ConnectOption(
                "Claude Code",
                "Run this once in a terminal. It registers the server for every project.",
                $"claude mcp add --scope user intheloop -- \"{path}\"",
                shell),
            new ConnectOption(
                "Claude Desktop, Cursor and other MCP clients",
                "Add this to the client's MCP configuration file, then restart the client.",
                $$"""
                {
                  "mcpServers": {
                    "intheloop": {
                      "command": "{{escaped}}"
                    }
                  }
                }
                """,
                shell),
            new ConnectOption(
                "ChatGPT Codex",
                "Add this to ~/.codex/config.toml.",
                $"""
                [mcp_servers.intheloop]
                command = "{escaped}"
                """,
                shell),
            new ConnectOption(
                "Clients that need a URL",
                "Start the server in HTTP mode and point the client at http://127.0.0.1:5179/mcp.",
                $"\"{path}\" --http 5179",
                shell)
        ];
    }

    public bool ServerFound { get; }

    public bool ServerMissing => !ServerFound;

    public IReadOnlyList<ConnectOption> Options { get; }

    public IReadOnlyList<ToolSummary> Tools { get; } =
    [
        new("submit_screenshot", "Posts a screenshot here and waits for your verdict."),
        new("wait_for_feedback", "Keeps waiting when you need more time to decide."),
        new("reply_to_human", "Answers your question or asks you one."),
        new("get_review", "Reads a review's status and conversation."),
        new("list_reviews", "Finds earlier reviews again.")
    ];
}
