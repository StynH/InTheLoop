using InTheLoop.Core.Messaging;
using InTheLoop.Core.Models;
using InTheLoop.Core.Services;
using InTheLoop.Core.Storage;

namespace InTheLoop.Core.Features;

/// <summary>The AI answers or asks the human something without posting a new screenshot.</summary>
public sealed record PostAiMessage(string ReviewId, string Message) : IRequest<Review>;

internal sealed class PostAiMessageHandler(IReviewStore store, TimeProvider clock) : IRequestHandler<PostAiMessage, Review>
{
    public Task<Review> HandleAsync(PostAiMessage request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            throw new RequestException("The message is empty.");
        }

        var now = clock.GetUtcNow();
        return store.UpdateAsync(request.ReviewId.Trim(), review => review with
        {
            UpdatedAt = now,
            Entries =
            [
                .. review.Entries,
                new ReviewEntry
                {
                    Id = Ids.NewEntryId(),
                    Kind = EntryKind.Message,
                    Author = Author.Ai,
                    Text = request.Message.Trim(),
                    Revision = review.RevisionCount,
                    CreatedAt = now
                }
            ]
        }, cancellationToken);
    }
}
