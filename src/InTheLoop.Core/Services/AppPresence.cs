using System.Diagnostics;
using System.Text.Json;
using InTheLoop.Core.Storage;

namespace InTheLoop.Core.Services;

/// <summary>Lets an MCP server find out whether the desktop app is open, and open it if not.</summary>
public interface IAppPresence
{
    bool IsRunning { get; }

    /// <summary>Called by the desktop app on startup. Returns false when another instance already runs.</summary>
    bool Announce(string executablePath);

    /// <summary>Starts the desktop app when it is not running. Returns true when it is (now) running.</summary>
    bool EnsureRunning();
}

public sealed class AppPresence(StoreOptions options) : IAppPresence, IDisposable
{
    private const string MutexName = @"Local\InTheLoop.App";

    private Mutex? _owned;

    private string InfoFile => Path.Combine(options.DataDirectory, "app.json");

    public bool IsRunning
    {
        get
        {
            if (_owned is not null)
            {
                return true;
            }

            if (!OperatingSystem.IsWindows())
            {
                return false;
            }

            if (Mutex.TryOpenExisting(MutexName, out var existing))
            {
                existing.Dispose();
                return true;
            }

            return false;
        }
    }

    public bool Announce(string executablePath)
    {
        var mutex = new Mutex(initiallyOwned: false, MutexName, out var createdNew);
        if (!createdNew)
        {
            mutex.Dispose();
            return false;
        }

        _owned = mutex;
        Directory.CreateDirectory(options.DataDirectory);
        File.WriteAllText(InfoFile, JsonSerializer.Serialize(new AppInfo(executablePath)));
        return true;
    }

    public bool EnsureRunning()
    {
        if (IsRunning)
        {
            return true;
        }

        try
        {
            if (!File.Exists(InfoFile))
            {
                return false;
            }

            var info = JsonSerializer.Deserialize<AppInfo>(File.ReadAllText(InfoFile));
            if (info is null || !File.Exists(info.ExecutablePath))
            {
                return false;
            }

            Process.Start(new ProcessStartInfo(info.ExecutablePath)
            {
                UseShellExecute = true,
                WorkingDirectory = Path.GetDirectoryName(info.ExecutablePath)
            });
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public void Dispose() => _owned?.Dispose();

    private sealed record AppInfo(string ExecutablePath);
}
