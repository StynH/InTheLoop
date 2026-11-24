using InTheLoop.Core.Messaging;
using InTheLoop.Core.Models;
using InTheLoop.Core.Services;
using InTheLoop.Core.Storage;

namespace InTheLoop.Core.Features;

/// <summary>Creates a project, or returns the existing one when the name is already taken.</summary>
public sealed record CreateProject(string Name, string? Description = null) : IRequest<CreateProjectResult>;

public sealed record CreateProjectResult(Project Project, bool Created);

internal sealed class CreateProjectHandler(IProjectStore projects, TimeProvider clock) : IRequestHandler<CreateProject, CreateProjectResult>
{
    public async Task<CreateProjectResult> HandleAsync(CreateProject request, CancellationToken cancellationToken)
    {
        var name = request.Name?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            throw new RequestException("A project needs a name.");
        }

        if (name.Length > 60)
        {
            throw new RequestException("A project name can be at most 60 characters.");
        }

        if (await projects.FindAsync(name, cancellationToken) is { } existing)
        {
            return new CreateProjectResult(existing, Created: false);
        }

        var project = new Project
        {
            Id = Ids.NewProjectId(),
            Name = name,
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            CreatedAt = clock.GetUtcNow()
        };
        await projects.SaveAsync(project, cancellationToken);
        return new CreateProjectResult(project, Created: true);
    }
}

public sealed record ListProjects : IRequest<IReadOnlyList<ProjectSummary>>;

/// <param name="Reviews">How many reviews the project holds.</param>
/// <param name="AwaitingHuman">How many of those are waiting for the human.</param>
public sealed record ProjectSummary(Project Project, int Reviews, int AwaitingHuman);

internal sealed class ListProjectsHandler(IProjectStore projects, IReviewStore reviews) : IRequestHandler<ListProjects, IReadOnlyList<ProjectSummary>>
{
    public async Task<IReadOnlyList<ProjectSummary>> HandleAsync(ListProjects request, CancellationToken cancellationToken)
    {
        var byProject = (await reviews.ListAsync(cancellationToken)).ToLookup(r => r.ProjectId);
        return (await projects.ListAsync(cancellationToken))
            .Select(p => new ProjectSummary(p, byProject[p.Id].Count(), byProject[p.Id].Count(r => r.AwaitingHuman)))
            .ToList();
    }
}

/// <summary>Removes a project. Its reviews are kept and simply no longer belong to a project.</summary>
public sealed record DeleteProject(string ProjectId) : IRequest<bool>;

internal sealed class DeleteProjectHandler(IProjectStore projects, IReviewStore reviews) : IRequestHandler<DeleteProject, bool>
{
    public async Task<bool> HandleAsync(DeleteProject request, CancellationToken cancellationToken)
    {
        foreach (var review in (await reviews.ListAsync(cancellationToken)).Where(r => r.ProjectId == request.ProjectId))
        {
            await reviews.UpdateAsync(review.Id, r => r with { ProjectId = null }, cancellationToken);
        }

        return await projects.DeleteAsync(request.ProjectId, cancellationToken);
    }
}

internal static class ProjectLookup
{
    /// <summary>Resolves what an AI passed as "project" and explains how to recover when it is unknown.</summary>
    public static async Task<Project> RequireAsync(this IProjectStore projects, string idOrName, CancellationToken cancellationToken)
    {
        if (await projects.FindAsync(idOrName, cancellationToken) is { } project)
        {
            return project;
        }

        var known = (await projects.ListAsync(cancellationToken)).Select(p => $"\"{p.Name}\"").ToList();
        var hint = known.Count == 0 ? "There are no projects yet." : "Existing projects: " + string.Join(", ", known) + ".";
        throw new RequestException($"Project '{idOrName.Trim()}' does not exist. {hint} Call create_project to make one.");
    }
}
