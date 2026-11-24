using System.Text.Json;
using InTheLoop.Core.Models;

namespace InTheLoop.Core.Storage;

/// <summary>One small file per project: <c>projects/{id}.json</c>.</summary>
public sealed class FileProjectStore : IProjectStore, IDisposable
{
    private readonly StoreOptions _options;
    private readonly FileSystemWatcher _watcher;
    private readonly Lock _gate = new();
    private CancellationTokenSource? _pending;

    public FileProjectStore(StoreOptions options)
    {
        _options = options;
        Directory.CreateDirectory(options.ProjectsDirectory);

        _watcher = new FileSystemWatcher(options.ProjectsDirectory, "*.json")
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite
        };
        _watcher.Created += (_, _) => Raise();
        _watcher.Changed += (_, _) => Raise();
        _watcher.Deleted += (_, _) => Raise();
        _watcher.Renamed += (_, _) => Raise();
        _watcher.EnableRaisingEvents = true;
    }

    public event EventHandler? ProjectsChanged;

    public async Task<IReadOnlyList<Project>> ListAsync(CancellationToken cancellationToken = default)
    {
        var projects = new List<Project>();
        foreach (var file in Directory.EnumerateFiles(_options.ProjectsDirectory, "*.json"))
        {
            if (await ReadAsync(file, cancellationToken) is { } project)
            {
                projects.Add(project);
            }
        }

        return projects.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public async Task<Project?> FindAsync(string idOrName, CancellationToken cancellationToken = default)
    {
        var wanted = idOrName.Trim();
        var projects = await ListAsync(cancellationToken);
        return projects.FirstOrDefault(p => p.Id == wanted)
            ?? projects.FirstOrDefault(p => string.Equals(p.Name, wanted, StringComparison.OrdinalIgnoreCase));
    }

    public async Task SaveAsync(Project project, CancellationToken cancellationToken = default)
    {
        var path = PathFor(project.Id);
        var temp = path + ".tmp";
        await using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await JsonSerializer.SerializeAsync(stream, project, StoreJson.Options, cancellationToken);
        }

        File.Move(temp, path, overwrite: true);
        Raise();
    }

    public Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        var path = PathFor(id);
        if (!File.Exists(path))
        {
            return Task.FromResult(false);
        }

        File.Delete(path);
        Raise();
        return Task.FromResult(true);
    }

    public void Dispose()
    {
        _watcher.Dispose();
        _pending?.Cancel();
    }

    private string PathFor(string id) => Path.Combine(_options.ProjectsDirectory, Path.GetFileName(id) + ".json");

    private static async Task<Project?> ReadAsync(string path, CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                return await JsonSerializer.DeserializeAsync<Project>(stream, StoreJson.Options, cancellationToken);
            }
            catch (FileNotFoundException)
            {
                return null;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException && attempt < 10)
            {
                await Task.Delay(30, cancellationToken);
            }
        }
    }

    private void Raise()
    {
        var cts = new CancellationTokenSource();
        lock (_gate)
        {
            _pending?.Cancel();
            _pending = cts;
        }

        _ = Task.Delay(80, cts.Token).ContinueWith(task =>
        {
            if (!task.IsCanceled)
            {
                ProjectsChanged?.Invoke(this, EventArgs.Empty);
            }
        }, TaskScheduler.Default);
    }
}
