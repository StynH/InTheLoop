using InTheLoop.Core.Messaging;
using InTheLoop.Core.Models;
using InTheLoop.Core.Services;
using InTheLoop.Core.Storage;

namespace InTheLoop.Core.Features;

public enum Verdict
{
    Approve,
    RequestChanges,
    Comment
}

/// <summary>The human's side of the conversation: approve, ask for changes, or just reply.</summary>
public sealed record JudgeReview(string ReviewId, Verdict Verdict, string? Feedback = null) : IRequest<Review>;

internal sealed class JudgeReviewHandler(IReviewStore store, TimeProvider clock) : IRequestHandler<JudgeReview, Review>
{
    public Task<Review> HandleAsync(JudgeReview request, CancellationToken cancellationToken)
    {
        var feedback = string.IsNullOrWhiteSpace(request.Feedback) ? null : request.Feedback.Trim();
        if (feedback is null && request.Verdict != Verdict.Approve)
        {
            throw new RequestException("Write some feedback first, so the AI knows what to change.");
        }

        var now = clock.GetUtcNow();
        return store.UpdateAsync(request.ReviewId, review => review with
        {
            Status = request.Verdict switch
            {
                Verdict.Approve => ReviewStatus.Approved,
                Verdict.RequestChanges => ReviewStatus.ChangesRequested,
                _ => review.Status
            },
            UpdatedAt = now,
            Entries =
            [
                .. review.Entries,
                new ReviewEntry
                {
                    Id = Ids.NewEntryId(),
                    Kind = request.Verdict switch
                    {
                        Verdict.Approve => EntryKind.Approval,
                        Verdict.RequestChanges => EntryKind.ChangeRequest,
                        _ => EntryKind.Message
                    },
                    Author = Author.Human,
                    Text = feedback,
                    Revision = review.RevisionCount,
                    CreatedAt = now
                }
            ]
        }, cancellationToken);
    }
}
