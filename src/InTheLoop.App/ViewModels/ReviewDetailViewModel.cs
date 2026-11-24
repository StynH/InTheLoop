using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InTheLoop.App.Services;
using InTheLoop.Core.Features;
using InTheLoop.Core.Messaging;
using InTheLoop.Core.Models;
using InTheLoop.Core.Storage;

namespace InTheLoop.App.ViewModels;

/// <summary>The open review: its screenshots, the conversation, and the verdict buttons.</summary>
public partial class ReviewDetailViewModel(
    IRequestDispatcher dispatcher,
    IReviewStore store,
    IShellService shell,
    IDialogService dialogs) : ObservableObject
{
    private Review? _review;

    /// <summary>Raised after entries were added, so the view can scroll to the newest one.</summary>
    public event EventHandler? TimelineChanged;

    /// <summary>Raised when the open review was deleted.</summary>
    public event EventHandler<string>? Deleted;

    public ObservableCollection<EntryViewModel> Timeline { get; } = [];

    public ObservableCollection<RevisionViewModel> Revisions { get; } = [];

    [ObservableProperty]
    public partial string Title { get; set; } = "";

    [ObservableProperty]
    public partial string Source { get; set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasProject))]
    public partial string? ProjectName { get; set; }

    public bool HasProject => ProjectName is not null;

    [ObservableProperty]
    public partial ReviewStage Stage { get; set; }

    [ObservableProperty]
    public partial string StatusLabel { get; set; } = "";

    [ObservableProperty]
    public partial string RevisionCaption { get; set; } = "";

    [ObservableProperty]
    public partial string? ImagePath { get; set; }

    [ObservableProperty]
    public partial bool HasMultipleRevisions { get; set; }

    [ObservableProperty]
    public partial bool IsOldRevision { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RequestChangesCommand), nameof(SendCommentCommand))]
    public partial string FeedbackText { get; set; } = "";

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    private bool HasFeedback => !string.IsNullOrWhiteSpace(FeedbackText);

    public void Show(Review? review, string? projectName = null)
    {
        ProjectName = projectName;
        var switched = review?.Id != _review?.Id;
        _review = review;

        if (review is null)
        {
            Timeline.Clear();
            Revisions.Clear();
            ImagePath = null;
            return;
        }

        if (switched)
        {
            Timeline.Clear();
            Revisions.Clear();
            FeedbackText = "";
            ErrorMessage = null;
        }

        Title = review.Title;
        Source = review.Source;
        Stage = review.Stage();
        StatusLabel = review.StatusLabel();

        var known = Timeline.Select(e => e.Id).ToHashSet();
        var added = false;
        foreach (var entry in review.Entries.Where(e => !known.Contains(e.Id)))
        {
            Timeline.Add(new EntryViewModel(entry, review.Source));
            added = true;
        }

        var revisionsAdded = false;
        foreach (var screenshot in review.Screenshots.Skip(Revisions.Count))
        {
            Revisions.Add(new RevisionViewModel(screenshot, store.GetImagePath(review.Id, screenshot.ImageFile!)));
            revisionsAdded = true;
        }

        HasMultipleRevisions = Revisions.Count > 1;
        if (revisionsAdded && Revisions.Count > 0)
        {
            SelectRevision(Revisions[^1]);
        }

        if (added)
        {
            TimelineChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    [RelayCommand]
    private void SelectRevision(RevisionViewModel? revision)
    {
        if (revision is null)
        {
            return;
        }

        foreach (var item in Revisions)
        {
            item.IsSelected = item == revision;
        }

        ImagePath = revision.ImagePath;
        IsOldRevision = revision != Revisions[^1];
        RevisionCaption = Revisions.Count > 1
            ? $"Version {revision.Number} of {Revisions.Count}  ·  {revision.CreatedAt.ToLocalTime():d MMM, HH:mm}"
            : $"{revision.CreatedAt.ToLocalTime():d MMM, HH:mm}";
    }

    [RelayCommand]
    private Task ApproveAsync() => JudgeAsync(Verdict.Approve);

    [RelayCommand(CanExecute = nameof(HasFeedback))]
    private Task RequestChangesAsync() => JudgeAsync(Verdict.RequestChanges);

    [RelayCommand(CanExecute = nameof(HasFeedback))]
    private Task SendCommentAsync() => JudgeAsync(Verdict.Comment);

    [RelayCommand]
    private Task OpenImageAsync() => ImagePath is { } path ? shell.OpenFileAsync(path) : Task.CompletedTask;

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (_review is not { } review)
        {
            return;
        }

        var confirmed = await dialogs.ConfirmAsync(
            "Delete this review?",
            $"\"{review.Title}\" and its screenshots will be removed. The AI can no longer get feedback on it.",
            "Delete",
            "Keep");
        if (!confirmed)
        {
            return;
        }

        // Let go of the image files before removing them from disk.
        Show(null);
        Deleted?.Invoke(this, review.Id);
        await Task.Delay(150);

        try
        {
            await dispatcher.SendAsync(new DeleteReview(review.Id));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ErrorMessage = "Could not delete every file of this review. Try again in a moment.";
        }
    }

    private async Task JudgeAsync(Verdict verdict)
    {
        if (_review is not { } review)
        {
            return;
        }

        try
        {
            ErrorMessage = null;
            var updated = await dispatcher.SendAsync(new JudgeReview(review.Id, verdict, FeedbackText));
            FeedbackText = "";
            Show(updated, ProjectName);
        }
        catch (RequestException ex)
        {
            ErrorMessage = ex.Message;
        }
    }
}
