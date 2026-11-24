using InTheLoop.Core.Messaging;
using InTheLoop.Core.Models;
using InTheLoop.Core.Storage;

namespace InTheLoop.Core.Features;

/// <summary>Blocks until the human has answered the AI's latest entry, or the timeout passes.</summary>
public sealed record WaitForFeedback(string ReviewId, TimeSpan Timeout) : IRequest<FeedbackResult>;

/// <param name="Review">The review as it is now.</param>
/// <param name="Feedback">What the human said since the AI last spoke. Empty when still waiting.</param>
public sealed record FeedbackResult(Review Review, IReadOnlyList<ReviewEntry> Feedback)
{
    public bool HasFeedback => Feedback.Count > 0;
}

internal sealed class WaitForFeedbackHandler(IReviewStore store) : IRequestHandler<WaitForFeedback, FeedbackResult>
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);

    public async Task<FeedbackResult> HandleAsync(WaitForFeedback request, CancellationToken cancellationToken)
    {
        var id = request.ReviewId.Trim();
        var signal = new SemaphoreSlim(0);

        void OnChanged(object? sender, ReviewChangedEventArgs e)
        {
            if (e.ReviewId == id)
            {
                signal.Release();
            }
        }

        store.ReviewChanged += OnChanged;
        try
        {
            var deadline = DateTime.UtcNow + request.Timeout;
            while (true)
            {
                var review = await store.GetAsync(id, cancellationToken)
                    ?? throw new RequestException($"Review '{id}' does not exist (it may have been deleted by the human).");

                var feedback = review.UnansweredHumanEntries();
                var remaining = deadline - DateTime.UtcNow;
                if (feedback.Count > 0 || remaining <= TimeSpan.Zero)
                {
                    return new FeedbackResult(review, feedback);
                }

                // The watcher is the fast path; the poll interval covers a missed file event.
                await signal.WaitAsync(remaining < PollInterval ? remaining : PollInterval, cancellationToken);
            }
        }
        finally
        {
            store.ReviewChanged -= OnChanged;
        }
    }
}
