namespace InTheLoop.Core.Models;

/// <summary>A named group of reviews, usually one per codebase or feature the AI is working on.</summary>
public sealed record Project
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}
