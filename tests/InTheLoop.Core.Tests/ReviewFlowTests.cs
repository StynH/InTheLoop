using InTheLoop.Core.Features;
using InTheLoop.Core.Messaging;
using InTheLoop.Core.Models;
using Microsoft.Extensions.DependencyInjection;

namespace InTheLoop.Core.Tests;

public sealed class ReviewFlowTests : IDisposable
{
    private static readonly string Png = Convert.ToBase64String([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0]);

    private readonly string _directory = Path.Combine(Path.GetTempPath(), "intheloop-tests-" + Guid.NewGuid().ToString("N"));
    private readonly ServiceProvider _services;
    private readonly IRequestDispatcher _dispatcher;

    public ReviewFlowTests()
    {
        _services = new ServiceCollection().AddInTheLoopCore(o => o.DataDirectory = _directory).BuildServiceProvider();
        _dispatcher = _services.GetRequiredService<IRequestDispatcher>();
    }

    public void Dispose()
    {
        _services.Dispose();
        Directory.Delete(_directory, recursive: true);
    }

    [Fact]
    public async Task A_new_screenshot_waits_for_the_human()
    {
        var review = await Submit("Login page");

        Assert.Equal(ReviewStatus.Pending, review.Status);
        Assert.True(review.AwaitingHuman);
        Assert.Equal(1, review.RevisionCount);

        var result = await _dispatcher.SendAsync(new WaitForFeedback(review.Id, TimeSpan.Zero));
        Assert.False(result.HasFeedback);
    }

    [Fact]
    public async Task Waiting_returns_as_soon_as_the_human_requests_changes()
    {
        var review = await Submit("Login page");

        var waiting = _dispatcher.SendAsync(new WaitForFeedback(review.Id, TimeSpan.FromSeconds(20)));
        await Task.Delay(200);
        await _dispatcher.SendAsync(new JudgeReview(review.Id, Verdict.RequestChanges, "Bigger button"));

        var result = await waiting.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(ReviewStatus.ChangesRequested, result.Review.Status);
        Assert.Equal("Bigger button", Assert.Single(result.Feedback).Text);
    }

    [Fact]
    public async Task A_new_revision_puts_the_review_back_with_the_human()
    {
        var review = await Submit("Login page");
        await _dispatcher.SendAsync(new JudgeReview(review.Id, Verdict.RequestChanges, "Bigger button"));

        var revised = await _dispatcher.SendAsync(new SubmitScreenshot { ReviewId = review.Id, ImageBase64 = Png });

        Assert.Equal(2, revised.RevisionCount);
        Assert.Equal(ReviewStatus.Pending, revised.Status);
        Assert.True(revised.AwaitingHuman);
        Assert.Equal("Login page", revised.Title);
    }

    [Fact]
    public async Task Approval_needs_no_feedback_but_a_change_request_does()
    {
        var review = await Submit("Login page");

        await Assert.ThrowsAsync<RequestException>(() => _dispatcher.SendAsync(new JudgeReview(review.Id, Verdict.RequestChanges)));

        var approved = await _dispatcher.SendAsync(new JudgeReview(review.Id, Verdict.Approve));
        Assert.Equal(ReviewStatus.Approved, approved.Status);
        Assert.False(approved.AwaitingHuman);
    }

    [Fact]
    public async Task A_comment_reaches_the_ai_without_changing_the_status()
    {
        var review = await Submit("Login page");
        await _dispatcher.SendAsync(new JudgeReview(review.Id, Verdict.Comment, "Which font is this?"));

        var result = await _dispatcher.SendAsync(new WaitForFeedback(review.Id, TimeSpan.Zero));
        Assert.True(result.HasFeedback);
        Assert.Equal(ReviewStatus.Pending, result.Review.Status);

        var answered = await _dispatcher.SendAsync(new PostAiMessage(review.Id, "Inter"));
        Assert.True(answered.AwaitingHuman);
    }

    [Fact]
    public async Task Files_that_are_not_images_are_rejected()
    {
        var notAnImage = Convert.ToBase64String("hello"u8.ToArray());

        var error = await Assert.ThrowsAsync<RequestException>(() =>
            _dispatcher.SendAsync(new SubmitScreenshot { Title = "x", ImageBase64 = notAnImage }));
        Assert.Contains("not a PNG", error.Message);
    }

    [Fact]
    public async Task Projects_are_created_once_and_group_reviews()
    {
        var first = await _dispatcher.SendAsync(new CreateProject("Webshop"));
        var again = await _dispatcher.SendAsync(new CreateProject("webshop"));
        Assert.True(first.Created);
        Assert.False(again.Created);
        Assert.Equal(first.Project.Id, again.Project.Id);

        await Submit("Checkout", "Webshop");
        await Submit("Loose one");

        var inProject = await _dispatcher.SendAsync(new ListReviews(Project: "Webshop"));
        Assert.Equal("Checkout", Assert.Single(inProject).Title);

        var summary = Assert.Single(await _dispatcher.SendAsync(new ListProjects()));
        Assert.Equal(1, summary.Reviews);
        Assert.Equal(1, summary.AwaitingHuman);
    }

    [Fact]
    public async Task An_unknown_project_is_an_error_the_ai_can_recover_from()
    {
        await _dispatcher.SendAsync(new CreateProject("Webshop"));

        var error = await Assert.ThrowsAsync<RequestException>(() => Submit("Checkout", "Nope"));
        Assert.Contains("\"Webshop\"", error.Message);
        Assert.Contains("create_project", error.Message);
    }

    [Fact]
    public async Task Deleting_a_project_keeps_its_reviews()
    {
        var project = (await _dispatcher.SendAsync(new CreateProject("Webshop"))).Project;
        var review = await Submit("Checkout", "Webshop");

        Assert.True(await _dispatcher.SendAsync(new DeleteProject(project.Id)));

        var kept = await _dispatcher.SendAsync(new GetReview(review.Id));
        Assert.NotNull(kept);
        Assert.Null(kept.ProjectId);
        Assert.Empty(await _dispatcher.SendAsync(new ListProjects()));
    }

    private Task<Review> Submit(string title, string? project = null) =>
        _dispatcher.SendAsync(new SubmitScreenshot { Title = title, ImageBase64 = Png, Project = project });
}
