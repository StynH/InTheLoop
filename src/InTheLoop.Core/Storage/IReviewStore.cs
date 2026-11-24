using InTheLoop.Core.Models;

namespace InTheLoop.Core.Storage;

public sealed class ReviewChangedEventArgs(string reviewId) : EventArgs
{
    public string ReviewId { get; } = reviewId;
}

/// <summary>
/// Shared persistence for reviews. The desktop app and every MCP server process use the same
/// store, so it is the channel through which both sides talk to each other.
/// </summary>
public interface IReviewStore
{
    /// <summary>Raised (on a background thread) whenever any process creates, changes or deletes a review.</summary>
    event EventHandler<ReviewChangedEventArgs>? ReviewChanged;

    Task<Review?> GetAsync(string id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Review>> ListAsync(CancellationToken cancellationToken = default);

    Task<Review> CreateAsync(Review review, CancellationToken cancellationToken = default);

    /// <summary>Atomically applies <paramref name="change"/> to the stored review, safe across processes.</summary>
    Task<Review> UpdateAsync(string id, Func<Review, Review> change, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default);

    Task<string> SaveImageAsync(string reviewId, string fileName, ReadOnlyMemory<byte> content, CancellationToken cancellationToken = default);

    string GetImagePath(string reviewId, string fileName);
}
