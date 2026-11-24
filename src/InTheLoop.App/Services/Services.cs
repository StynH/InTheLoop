namespace InTheLoop.App.Services;

/// <summary>Runs work on the UI thread, so view models do not depend on MAUI's static helpers.</summary>
public interface IUiThread
{
    void Post(Action action);
}

public interface IDialogService
{
    Task<bool> ConfirmAsync(string title, string message, string accept, string cancel);
}

public interface IShellService
{
    Task OpenFileAsync(string path);

    void OpenFolder(string path);

    Task CopyTextAsync(string text);
}

/// <summary>Draws the user's eye to the app when the AI needs them.</summary>
public interface IAttentionService
{
    void Request();
}

/// <summary>Finds the MCP server executable that ships with the app.</summary>
public interface IMcpServerLocator
{
    string? ExecutablePath { get; }
}
