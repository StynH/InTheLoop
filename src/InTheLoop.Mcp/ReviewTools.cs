using System.ComponentModel;
using InTheLoop.Core.Features;
using InTheLoop.Core.Messaging;
using InTheLoop.Core.Models;
using InTheLoop.Core.Services;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace InTheLoop.Mcp;

[McpServerToolType]
public sealed class ReviewTools(IDispatcher dispatcher, IAppPresence app)
{
    public const string Instructions =
        """
        In The Loop puts a human in the loop for visual work. You post a screenshot, the human looks at it
        in a desktop app and either approves it or tells you what to change.

        The loop:
        1. submit_screenshot with a title, a short description and the image. It returns a review_id and,
           if the human answers quickly, their verdict.
        2. If the result says the review is still pending, call wait_for_feedback until it is not.
        3. approved: you are done. changes_requested: make the changes, take a new screenshot and call
           submit_screenshot again with the same review_id. comment: the human asked or said something;
           answer with reply_to_human, or act on it and submit a new revision.

        Never assume approval. Only an "approved" status counts.
        """;

    private const int MaxWaitSeconds = 3600;

    [McpServerTool(Name = "submit_screenshot", Title = "Submit screenshot for review")]
    [Description(
        "Show a screenshot to the human and ask for their verdict. Starts a new review, or adds a new revision " +
        "when review_id is given. Waits briefly for an answer; if the result says 'pending', call wait_for_feedback.")]
    public async Task<string> SubmitScreenshot(
        McpServer server,
        [Description("Short name of what is shown, e.g. 'Login page - dark mode'. Required for a new review.")] string? title = null,
        [Description("What the human should look at or decide. Mention what changed when this is a new revision.")] string? description = null,
        [Description("Absolute path of the image file (PNG, JPEG, GIF, BMP or WebP). Preferred over image_base64.")] string? image_path = null,
        [Description("The image as base64 (a data: URL also works). Use only when you cannot write a file.")] string? image_base64 = null,
        [Description("Id of an existing review to post a new revision to, after the human requested changes.")] string? review_id = null,
        [Description("Seconds to wait for the verdict before returning (0-3600). Default 45.")] int wait_seconds = 45,
        CancellationToken cancellationToken = default)
    {
        var review = await Run(() => dispatcher.SendAsync(new SubmitScreenshot
        {
            Title = title,
            Description = description,
            ImagePath = image_path,
            ImageBase64 = image_base64,
            ReviewId = review_id,
            Source = server.ClientInfo?.Title ?? server.ClientInfo?.Name
        }, cancellationToken));

        var appOpen = app.EnsureRunning();
        var header = $"Submitted revision {review.RevisionCount} of review {review.Id} (\"{review.Title}\").";
        if (!appOpen)
        {
            header += " Note: the In The Loop desktop app is not running, so ask the human to open it.";
        }

        var result = await Run(() => dispatcher.SendAsync(
            new WaitForFeedback(review.Id, Seconds(wait_seconds)), cancellationToken));

        return header + "\n\n" + Describe(result);
    }

    [McpServerTool(Name = "wait_for_feedback", Title = "Wait for the human's feedback", ReadOnly = true)]
    [Description(
        "Wait until the human has judged or answered a review. Returns 'approved', 'changes_requested' with their " +
        "feedback, 'comment' when they replied without a verdict, or 'pending' when the wait timed out (call again).")]
    public async Task<string> WaitForFeedback(
        [Description("The review_id returned by submit_screenshot.")] string review_id,
        [Description("Seconds to wait before returning 'pending' (0-3600). Default 50.")] int timeout_seconds = 50,
        CancellationToken cancellationToken = default)
    {
        var result = await Run(() => dispatcher.SendAsync(
            new WaitForFeedback(review_id, Seconds(timeout_seconds)), cancellationToken));

        return Describe(result);
    }

    [McpServerTool(Name = "reply_to_human", Title = "Reply to the human")]
    [Description(
        "Send the human a text message inside a review without a new screenshot: answer their question, or ask " +
        "for clarification. Then call wait_for_feedback for their answer.")]
    public async Task<string> ReplyToHuman(
        [Description("The review_id the message belongs to.")] string review_id,
        [Description("The message for the human.")] string message,
        CancellationToken cancellationToken = default)
    {
        var review = await Run(() => dispatcher.SendAsync(new PostAiMessage(review_id, message), cancellationToken));
        app.EnsureRunning();
        return $"Message sent in review {review.Id}. Call wait_for_feedback(review_id=\"{review.Id}\") for the answer.";
    }

    [McpServerTool(Name = "get_review", Title = "Get a review", ReadOnly = true)]
    [Description("Get the current status and the full conversation of one review, without waiting.")]
    public async Task<string> GetReview(
        [Description("The review_id returned by submit_screenshot.")] string review_id,
        CancellationToken cancellationToken = default)
    {
        var review = await Run(() => dispatcher.SendAsync(new GetReview(review_id), cancellationToken))
            ?? throw new McpException($"Review '{review_id}' does not exist.");

        var lines = new List<string>
        {
            $"Review {review.Id}: \"{review.Title}\"",
            $"Status: {StatusName(review.Status)} | Revisions: {review.RevisionCount} | Waiting on: {(review.AwaitingHuman ? "human" : "you")}",
            "",
            "Conversation:"
        };
        lines.AddRange(review.Entries.Select(DescribeEntry));
        return string.Join('\n', lines);
    }

    [McpServerTool(Name = "list_reviews", Title = "List reviews", ReadOnly = true)]
    [Description("List reviews, newest first. Useful to find a review_id again after losing context.")]
    public async Task<string> ListReviews(
        [Description("Only reviews with this status: 'pending', 'approved' or 'changes_requested'. Omit for all.")] string? status = null,
        CancellationToken cancellationToken = default)
    {
        ReviewStatus? filter = status?.Trim().ToLowerInvariant() switch
        {
            null or "" or "all" => null,
            "pending" => ReviewStatus.Pending,
            "approved" => ReviewStatus.Approved,
            "changes_requested" => ReviewStatus.ChangesRequested,
            _ => throw new McpException("status must be 'pending', 'approved' or 'changes_requested'.")
        };

        var reviews = await Run(() => dispatcher.SendAsync(new ListReviews(filter), cancellationToken));
        if (reviews.Count == 0)
        {
            return "No reviews found.";
        }

        return string.Join('\n', reviews.Take(50).Select(r =>
            $"{r.Id} | {StatusName(r.Status)} | revision {r.RevisionCount} | waiting on {(r.AwaitingHuman ? "human" : "you")} | \"{r.Title}\""));
    }

    private static TimeSpan Seconds(int seconds) => TimeSpan.FromSeconds(Math.Clamp(seconds, 0, MaxWaitSeconds));

    private static string Describe(FeedbackResult result)
    {
        var review = result.Review;
        if (!result.HasFeedback)
        {
            return $"Status: pending\nThe human has not answered yet. " +
                   $"Call wait_for_feedback(review_id=\"{review.Id}\") to keep waiting.";
        }

        var verdict = result.Feedback.LastOrDefault(e => e.Kind is EntryKind.Approval or EntryKind.ChangeRequest);
        var lines = new List<string>();

        switch (verdict?.Kind)
        {
            case EntryKind.Approval:
                lines.Add("Status: approved");
                lines.Add($"The human approved revision {verdict.Revision} of review {review.Id}.");
                break;
            case EntryKind.ChangeRequest:
                lines.Add("Status: changes_requested");
                lines.Add($"The human wants changes to revision {verdict.Revision} of review {review.Id}.");
                break;
            default:
                lines.Add("Status: comment");
                lines.Add($"The human replied in review {review.Id} without giving a verdict yet.");
                break;
        }

        var texts = result.Feedback.Where(e => !string.IsNullOrWhiteSpace(e.Text)).Select(e => e.Text!).ToList();
        if (texts.Count > 0)
        {
            lines.Add("");
            lines.Add("Feedback from the human:");
            lines.AddRange(texts.Select(t => "> " + t.ReplaceLineEndings("\n> ")));
        }

        lines.Add("");
        lines.Add(verdict?.Kind switch
        {
            EntryKind.Approval => "Next: nothing more to do for this review.",
            EntryKind.ChangeRequest =>
                $"Next: make the changes, take a new screenshot and call submit_screenshot with review_id=\"{review.Id}\".",
            _ => $"Next: answer with reply_to_human(review_id=\"{review.Id}\"), or act on it and submit a new revision."
        });

        return string.Join('\n', lines);
    }

    private static string DescribeEntry(ReviewEntry entry)
    {
        var who = entry.Author == Author.Ai ? "you" : "human";
        var what = entry.Kind switch
        {
            EntryKind.Screenshot => $"posted revision {entry.Revision}",
            EntryKind.Approval => $"approved revision {entry.Revision}",
            EntryKind.ChangeRequest => $"requested changes to revision {entry.Revision}",
            _ => "said"
        };
        var text = string.IsNullOrWhiteSpace(entry.Text) ? "" : ": " + entry.Text.ReplaceLineEndings(" ");
        return $"- [{entry.CreatedAt.ToLocalTime():yyyy-MM-dd HH:mm}] {who} {what}{text}";
    }

    private static string StatusName(ReviewStatus status) => status switch
    {
        ReviewStatus.Approved => "approved",
        ReviewStatus.ChangesRequested => "changes_requested",
        _ => "pending"
    };

    /// <summary>Turns handler errors into tool errors the model can read and act on.</summary>
    private static async Task<T> Run<T>(Func<Task<T>> action)
    {
        try
        {
            return await action();
        }
        catch (RequestException ex)
        {
            throw new McpException(ex.Message);
        }
    }
}
