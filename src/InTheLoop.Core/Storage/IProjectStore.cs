using InTheLoop.Core.Models;

namespace InTheLoop.Core.Storage;

public interface IProjectStore
{
    /// <summary>Raised (on a background thread) whenever any process adds, changes or removes a project.</summary>
    event EventHandler? ProjectsChanged;

    Task<IReadOnlyList<Project>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>Finds a project by its id or, failing that, by its name (ignoring case).</summary>
    Task<Project?> FindAsync(string idOrName, CancellationToken cancellationToken = default);

    Task SaveAsync(Project project, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default);
}
