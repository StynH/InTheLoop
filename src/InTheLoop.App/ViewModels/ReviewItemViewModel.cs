using CommunityToolkit.Mvvm.ComponentModel;
using InTheLoop.Core.Models;

namespace InTheLoop.App.ViewModels;

/// <summary>One card in the review list.</summary>
public partial class ReviewItemViewModel : ObservableObject
{
    private readonly Func<string, string, string> _imagePath;
    private readonly Func<string?, string?> _projectName;

    public ReviewItemViewModel(Review review, Func<string, string, string> imagePath, Func<string?, string?> projectName, DateTimeOffset now)
    {
        _imagePath = imagePath;
        _projectName = projectName;
        Review = review;
        Update(review, now);
    }

    public string Id => Review.Id;

    public Review Review { get; private set; }

    [ObservableProperty]
    public partial string Title { get; set; } = "";

    [ObservableProperty]
    public partial string Source { get; set; } = "";

    /// <summary>Where the review comes from: its project when it has one, otherwise the AI client.</summary>
    [ObservableProperty]
    public partial string Context { get; set; } = "";

    [ObservableProperty]
    public partial string Preview { get; set; } = "";

    [ObservableProperty]
    public partial string? ThumbnailPath { get; set; }

    [ObservableProperty]
    public partial ReviewStage Stage { get; set; }

    [ObservableProperty]
    public partial string StatusLabel { get; set; } = "";

    [ObservableProperty]
    public partial string RevisionLabel { get; set; } = "";

    [ObservableProperty]
    public partial string TimeLabel { get; set; } = "";

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    public void Update(Review review, DateTimeOffset now)
    {
        Review = review;
        Title = review.Title;
        Source = review.Source;
        Context = _projectName(review.ProjectId) ?? review.Source;
        Stage = review.Stage();
        StatusLabel = review.StatusLabel();
        RevisionLabel = $"v{review.RevisionCount}";
        ThumbnailPath = review.LatestScreenshot?.ImageFile is { } file ? _imagePath(review.Id, file) : null;
        Preview = DescribeLast(review);
        RefreshTime(now);
    }

    public void RefreshTime(DateTimeOffset now) => TimeLabel = ReviewPresentation.RelativeTime(Review.UpdatedAt, now);

    public bool Matches(string search) =>
        string.IsNullOrWhiteSpace(search)
        || Title.Contains(search, StringComparison.OrdinalIgnoreCase)
        || Source.Contains(search, StringComparison.OrdinalIgnoreCase)
        || Context.Contains(search, StringComparison.OrdinalIgnoreCase)
        || Review.Entries.Any(e => e.Text?.Contains(search, StringComparison.OrdinalIgnoreCase) == true);

    private static string DescribeLast(Review review)
    {
        var last = review.LastEntry;
        if (last is null)
        {
            return "";
        }

        var text = last.Text?.ReplaceLineEndings(" ");
        return last.Kind switch
        {
            EntryKind.Approval => string.IsNullOrEmpty(text) ? "You approved this" : "You: " + text,
            EntryKind.ChangeRequest or EntryKind.Message when last.Author == Author.Human => "You: " + text,
            EntryKind.Screenshot => string.IsNullOrEmpty(text) ? "New screenshot" : text,
            _ => text ?? ""
        };
    }
}
