namespace InTheLoop.Core.Models;

public enum ReviewStatus
{
    Pending,
    Approved,
    ChangesRequested
}

public enum Author
{
    Ai,
    Human
}

public enum EntryKind
{
    Screenshot,
    Message,
    Approval,
    ChangeRequest
}

public sealed record ReviewEntry
{
    public required string Id { get; init; }
    public required EntryKind Kind { get; init; }
    public required Author Author { get; init; }
    public string? Text { get; init; }
    public string? ImageFile { get; init; }
    public int? Revision { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}

public sealed record Review
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public string Source { get; init; } = "AI";

    /// <summary>The project this review belongs to, or null when it is not in one.</summary>
    public string? ProjectId { get; init; }
    public ReviewStatus Status { get; init; } = ReviewStatus.Pending;
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
    public IReadOnlyList<ReviewEntry> Entries { get; init; } = [];

    public IEnumerable<ReviewEntry> Screenshots => Entries.Where(e => e.Kind == EntryKind.Screenshot);

    public int RevisionCount => Screenshots.Count();

    public ReviewEntry? LatestScreenshot => Screenshots.LastOrDefault();

    public ReviewEntry? LastEntry => Entries.Count > 0 ? Entries[^1] : null;

    /// <summary>True while the AI spoke last and the human still owes an answer.</summary>
    public bool AwaitingHuman => LastEntry?.Author == Author.Ai;

    /// <summary>Everything the human said after the AI's most recent entry.</summary>
    public IReadOnlyList<ReviewEntry> UnansweredHumanEntries()
    {
        var lastAi = -1;
        for (var i = Entries.Count - 1; i >= 0; i--)
        {
            if (Entries[i].Author == Author.Ai)
            {
                lastAi = i;
                break;
            }
        }

        return Entries.Skip(lastAi + 1).Where(e => e.Author == Author.Human).ToList();
    }
}
