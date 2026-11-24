using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using InTheLoop.Core.Messaging;
using InTheLoop.Core.Models;

namespace InTheLoop.Core.Storage;

/// <summary>
/// One folder per review: <c>reviews/{id}/review.json</c> plus its images. Writes replace the
/// file atomically and are serialised across processes with a lock file.
/// </summary>
public sealed class FileReviewStore : IReviewStore, IDisposable
{
    private const string ReviewFile = "review.json";
    private const string LockFile = ".lock";

    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private readonly StoreOptions _options;
    private readonly FileSystemWatcher _watcher;
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _pending = new();

    public FileReviewStore(StoreOptions options)
    {
        _options = options;
        Directory.CreateDirectory(options.ReviewsDirectory);

        _watcher = new FileSystemWatcher(options.ReviewsDirectory)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite
        };
        _watcher.Created += (_, e) => OnFileEvent(e.FullPath);
        _watcher.Changed += (_, e) => OnFileEvent(e.FullPath);
        _watcher.Deleted += (_, e) => OnFileEvent(e.FullPath);
        _watcher.Renamed += (_, e) => OnFileEvent(e.FullPath);
        _watcher.EnableRaisingEvents = true;
    }

    public event EventHandler<ReviewChangedEventArgs>? ReviewChanged;

    public async Task<Review?> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        if (!IsValidId(id))
        {
            return null;
        }

        var path = Path.Combine(ReviewDirectory(id), ReviewFile);
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                if (!File.Exists(path))
                {
                    return null;
                }

                await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                return await JsonSerializer.DeserializeAsync<Review>(stream, Json, cancellationToken);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException && attempt < 10)
            {
                await Task.Delay(30, cancellationToken);
            }
        }
    }

    public async Task<IReadOnlyList<Review>> ListAsync(CancellationToken cancellationToken = default)
    {
        var reviews = new List<Review>();
        foreach (var directory in Directory.EnumerateDirectories(_options.ReviewsDirectory))
        {
            var review = await GetAsync(Path.GetFileName(directory), cancellationToken);
            if (review is not null)
            {
                reviews.Add(review);
            }
        }

        return reviews.OrderByDescending(r => r.UpdatedAt).ToList();
    }

    public async Task<Review> CreateAsync(Review review, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(ReviewDirectory(review.Id));
        await using var _ = await AcquireLockAsync(review.Id, cancellationToken);
        await WriteAsync(review, cancellationToken);
        return review;
    }

    public async Task<Review> UpdateAsync(string id, Func<Review, Review> change, CancellationToken cancellationToken = default)
    {
        if (!IsValidId(id) || !Directory.Exists(ReviewDirectory(id)))
        {
            throw new RequestException($"Review '{id}' does not exist.");
        }

        await using var _ = await AcquireLockAsync(id, cancellationToken);
        var current = await GetAsync(id, cancellationToken) ?? throw new RequestException($"Review '{id}' does not exist.");
        var updated = change(current);
        await WriteAsync(updated, cancellationToken);
        return updated;
    }

    public async Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        if (!IsValidId(id) || !Directory.Exists(ReviewDirectory(id)))
        {
            return false;
        }

        for (var attempt = 0; ; attempt++)
        {
            try
            {
                Directory.Delete(ReviewDirectory(id), recursive: true);
                Raise(id);
                return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException && attempt < 10)
            {
                await Task.Delay(50, cancellationToken);
            }
        }
    }

    public async Task<string> SaveImageAsync(string reviewId, string fileName, ReadOnlyMemory<byte> content, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(ReviewDirectory(reviewId));
        var path = GetImagePath(reviewId, fileName);
        await File.WriteAllBytesAsync(path, content, cancellationToken);
        return path;
    }

    public string GetImagePath(string reviewId, string fileName) =>
        Path.Combine(ReviewDirectory(reviewId), Path.GetFileName(fileName));

    public void Dispose()
    {
        _watcher.Dispose();
        foreach (var cts in _pending.Values)
        {
            cts.Cancel();
        }
    }

    private static bool IsValidId(string id) =>
        !string.IsNullOrWhiteSpace(id) && id.All(c => char.IsAsciiLetterOrDigit(c) || c == '-');

    private string ReviewDirectory(string id) => Path.Combine(_options.ReviewsDirectory, id);

    private async Task WriteAsync(Review review, CancellationToken cancellationToken)
    {
        var path = Path.Combine(ReviewDirectory(review.Id), ReviewFile);
        var temp = path + "." + Guid.NewGuid().ToString("N")[..8] + ".tmp";

        await using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await JsonSerializer.SerializeAsync(stream, review, Json, cancellationToken);
        }

        for (var attempt = 0; ; attempt++)
        {
            try
            {
                File.Move(temp, path, overwrite: true);
                break;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException && attempt < 20)
            {
                await Task.Delay(25, cancellationToken);
            }
        }

        Raise(review.Id);
    }

    private async Task<FileStream> AcquireLockAsync(string id, CancellationToken cancellationToken)
    {
        var path = Path.Combine(ReviewDirectory(id), LockFile);
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException) when (attempt < 200)
            {
                await Task.Delay(25, cancellationToken);
            }
        }
    }

    private void OnFileEvent(string fullPath)
    {
        var relative = Path.GetRelativePath(_options.ReviewsDirectory, fullPath);
        var segments = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var name = segments[^1];

        if (segments.Length == 0 || !IsValidId(segments[0]) || name == LockFile || name.EndsWith(".tmp", StringComparison.Ordinal))
        {
            return;
        }

        Raise(segments[0]);
    }

    /// <summary>Coalesces the burst of events one write produces into a single notification.</summary>
    private void Raise(string id)
    {
        var cts = new CancellationTokenSource();
        _pending.AddOrUpdate(id, cts, (_, previous) =>
        {
            previous.Cancel();
            return cts;
        });

        _ = Task.Delay(80, cts.Token).ContinueWith(task =>
        {
            if (task.IsCanceled)
            {
                return;
            }

            _pending.TryRemove(new KeyValuePair<string, CancellationTokenSource>(id, cts));
            ReviewChanged?.Invoke(this, new ReviewChangedEventArgs(id));
        }, TaskScheduler.Default);
    }
}
