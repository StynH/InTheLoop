using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InTheLoop.App.Services;
using InTheLoop.Core.Features;
using InTheLoop.Core.Messaging;
using InTheLoop.Core.Models;
using InTheLoop.Core.Storage;

namespace InTheLoop.App.ViewModels;

public enum ReviewFilter
{
    All,
    YourTurn,
    WithAi,
    Approved
}

public partial class MainViewModel : ObservableObject
{
    private readonly IRequestDispatcher _dispatcher;
    private readonly IReviewStore _store;
    private readonly IProjectStore _projectStore;
    private readonly StoreOptions _options;
    private readonly IUiThread _ui;
    private readonly IAttentionService _attention;
    private readonly IShellService _shell;
    private readonly IDialogService _dialogs;
    private readonly TimeProvider _clock;
    private readonly List<ReviewItemViewModel> _all = [];
    private IReadOnlyList<Project> _projects = [];
    private bool _initialized;

    public MainViewModel(
        IRequestDispatcher dispatcher,
        IReviewStore store,
        IProjectStore projectStore,
        StoreOptions options,
        IUiThread ui,
        IAttentionService attention,
        IShellService shell,
        IDialogService dialogs,
        TimeProvider clock,
        ReviewDetailViewModel detail,
        ConnectViewModel connect)
    {
        _dispatcher = dispatcher;
        _store = store;
        _projectStore = projectStore;
        _options = options;
        _ui = ui;
        _attention = attention;
        _shell = shell;
        _dialogs = dialogs;
        _clock = clock;
        Detail = detail;
        Connect = connect;

        Detail.Deleted += (_, id) => Apply(id, null);
        Greeting = GreetingFor(clock.GetLocalNow().Hour);
        SelectedProject = new ProjectChipViewModel(ProjectChipViewModel.AllKey, "All reviews", null, "Every review, across all projects") { IsSelected = true };
        Projects.Add(SelectedProject);
    }

    public ReviewDetailViewModel Detail { get; }

    public ConnectViewModel Connect { get; }

    public ObservableCollection<ReviewItemViewModel> Reviews { get; } = [];

    public ObservableCollection<ProjectChipViewModel> Projects { get; } = [];

    [ObservableProperty]
    public partial string Greeting { get; set; }

    [ObservableProperty]
    public partial string Summary { get; set; } = "";

    [ObservableProperty]
    public partial string ListTitle { get; set; } = "All reviews";

    [ObservableProperty]
    public partial int YourTurnCount { get; set; }

    [ObservableProperty]
    public partial int WithAiCount { get; set; }

    [ObservableProperty]
    public partial int ApprovedCount { get; set; }

    [ObservableProperty]
    public partial ReviewFilter Filter { get; set; }

    [ObservableProperty]
    public partial string SearchText { get; set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanDeleteProject))]
    [NotifyCanExecuteChangedFor(nameof(DeleteProjectCommand))]
    public partial ProjectChipViewModel SelectedProject { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoProjects))]
    public partial bool HasProjects { get; set; }

    public bool HasNoProjects => !HasProjects;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection), nameof(HasNoSelection))]
    public partial ReviewItemViewModel? SelectedReview { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsConnectSection))]
    public partial bool IsReviewsSection { get; set; } = true;

    [ObservableProperty]
    public partial bool IsListEmpty { get; set; } = true;

    [ObservableProperty]
    public partial bool HasNoReviews { get; set; } = true;

    [ObservableProperty]
    public partial string EmptyListText { get; set; } = "";

    public bool IsConnectSection => !IsReviewsSection;

    public bool HasSelection => SelectedReview is not null;

    public bool HasNoSelection => SelectedReview is null;

    public bool CanDeleteProject => SelectedProject.ProjectId is not null;

    public async Task InitializeAsync()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        _store.ReviewChanged += OnReviewChanged;
        _projectStore.ProjectsChanged += OnProjectsChanged;

        _projects = await _projectStore.ListAsync();
        var now = _clock.GetUtcNow();
        foreach (var review in await _dispatcher.SendAsync(new ListReviews()))
        {
            _all.Add(new ReviewItemViewModel(review, _store.GetImagePath, ProjectName, now));
        }

        Rebuild();
        Select(Reviews.FirstOrDefault());
        _ = RefreshTimesAsync();
    }

    [RelayCommand]
    private void Select(ReviewItemViewModel? item)
    {
        if (SelectedReview is { } previous)
        {
            previous.IsSelected = false;
        }

        SelectedReview = item;
        if (item is not null)
        {
            item.IsSelected = true;
        }

        Detail.Show(item?.Review, ProjectName(item?.Review.ProjectId));
    }

    [RelayCommand]
    private void SelectProject(ProjectChipViewModel chip)
    {
        if (chip == SelectedProject && IsReviewsSection)
        {
            return;
        }

        IsReviewsSection = true;
        SelectedProject.IsSelected = false;
        chip.IsSelected = true;
        SelectedProject = chip;
        Rebuild();

        if (SelectedReview is null || !Reviews.Contains(SelectedReview))
        {
            Select(Reviews.FirstOrDefault());
        }
    }

    [RelayCommand(CanExecute = nameof(CanDeleteProject))]
    private async Task DeleteProjectAsync()
    {
        if (SelectedProject.ProjectId is not { } id)
        {
            return;
        }

        var confirmed = await _dialogs.ConfirmAsync(
            $"Delete project \"{SelectedProject.Name}\"?",
            "Its reviews are kept and move to \"No project\".",
            "Delete project",
            "Keep");
        if (confirmed)
        {
            await _dispatcher.SendAsync(new DeleteProject(id));
        }
    }

    [RelayCommand]
    private void ToggleFilter(string name)
    {
        var filter = Enum.Parse<ReviewFilter>(name);
        Filter = Filter == filter ? ReviewFilter.All : filter;
    }

    [RelayCommand]
    private void ShowReviews() => IsReviewsSection = true;

    [RelayCommand]
    private void ShowConnect() => IsReviewsSection = false;

    [RelayCommand]
    private void OpenDataFolder() => _shell.OpenFolder(_options.DataDirectory);

    partial void OnFilterChanged(ReviewFilter value) => Rebuild();

    // The sidebar highlights either the open project or the Connect page, never both.
    partial void OnIsReviewsSectionChanged(bool value) => SelectedProject.IsSelected = value;

    partial void OnSearchTextChanged(string value) => Rebuild();

    private string? ProjectName(string? projectId) =>
        projectId is null ? null : _projects.FirstOrDefault(p => p.Id == projectId)?.Name;

    private void OnProjectsChanged(object? sender, EventArgs e) => _ = Task.Run(async () =>
    {
        try
        {
            var projects = await _projectStore.ListAsync();
            _ui.Post(() =>
            {
                _projects = projects;
                var now = _clock.GetUtcNow();
                foreach (var item in _all)
                {
                    item.Update(item.Review, now);
                }

                Rebuild();
                if (SelectedReview is { } selected)
                {
                    Detail.Show(selected.Review, ProjectName(selected.Review.ProjectId));
                }
            });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The next change notification reloads the projects.
        }
    });

    private void OnReviewChanged(object? sender, ReviewChangedEventArgs e) => _ = Task.Run(async () =>
    {
        try
        {
            var review = await _dispatcher.SendAsync(new GetReview(e.ReviewId));
            _ui.Post(() => Apply(e.ReviewId, review));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A later change notification will bring this review up to date.
        }
    });

    private void Apply(string id, Review? review)
    {
        var item = _all.FirstOrDefault(r => r.Id == id);

        if (review is null)
        {
            if (item is null)
            {
                return;
            }

            _all.Remove(item);
            Rebuild();
            if (SelectedReview == item)
            {
                Select(Reviews.FirstOrDefault());
            }

            return;
        }

        var previousLast = item?.Review.LastEntry?.Id;
        if (item is null)
        {
            item = new ReviewItemViewModel(review, _store.GetImagePath, ProjectName, _clock.GetUtcNow());
            _all.Add(item);
        }
        else
        {
            item.Update(review, _clock.GetUtcNow());
        }

        Rebuild();

        var aiSpoke = review.AwaitingHuman && review.LastEntry?.Id != previousLast;
        if (aiSpoke)
        {
            _attention.Request();
        }

        if (SelectedReview == item)
        {
            Detail.Show(review, ProjectName(review.ProjectId));
        }
        else if (Reviews.Contains(item)
                 && (SelectedReview is null || (aiSpoke && SelectedReview.Stage != ReviewStage.YourTurn && !HasDraft)))
        {
            // Bring the new work forward, unless the user is busy with another review.
            Select(item);
        }
    }

    private bool HasDraft => !string.IsNullOrWhiteSpace(Detail.FeedbackText);

    private void Rebuild()
    {
        RebuildProjects();

        var scope = _all.Where(r => SelectedProject.Contains(KnownProjectId(r.Review))).ToList();
        YourTurnCount = scope.Count(r => r.Stage == ReviewStage.YourTurn);
        WithAiCount = scope.Count(r => r.Stage == ReviewStage.WithAi);
        ApprovedCount = scope.Count(r => r.Stage == ReviewStage.Approved);

        var waiting = _all.Count(r => r.Stage == ReviewStage.YourTurn);
        Summary = waiting switch
        {
            0 when _all.Count == 0 => "Waiting for your AI to post a screenshot.",
            0 => "You're all caught up.",
            1 => "1 screenshot is waiting for you.",
            _ => $"{waiting} screenshots are waiting for you."
        };

        var stage = Filter switch
        {
            ReviewFilter.YourTurn => "Your turn",
            ReviewFilter.WithAi => "With the AI",
            ReviewFilter.Approved => "Approved",
            _ => "All reviews"
        };
        ListTitle = SelectedProject.IsAll ? stage : $"{SelectedProject.Name}  ·  {stage}";

        var visible = scope
            .Where(r => Filter == ReviewFilter.All || r.Stage.ToString() == Filter.ToString())
            .Where(r => r.Matches(SearchText))
            .OrderBy(r => r.Stage == ReviewStage.YourTurn ? 0 : 1)
            .ThenByDescending(r => r.Review.UpdatedAt)
            .ToList();

        if (!visible.SequenceEqual(Reviews))
        {
            Reviews.Clear();
            foreach (var item in visible)
            {
                Reviews.Add(item);
            }
        }

        HasNoReviews = _all.Count == 0;
        IsListEmpty = visible.Count == 0;
        EmptyListText = HasNoReviews
            ? "No screenshots yet. Connect your AI and ask it to show you its work."
            : scope.Count == 0
                ? "This project has no screenshots yet."
                : "Nothing here. Try another filter or search.";
    }

    /// <summary>Keeps the project switcher in step with the stored projects, preserving the selection.</summary>
    private void RebuildProjects()
    {
        var wanted = new List<ProjectChipViewModel> { Projects.First(p => p.IsAll) };
        foreach (var project in _projects)
        {
            var chip = Projects.FirstOrDefault(p => p.Key == project.Id)
                ?? new ProjectChipViewModel(project.Id, project.Name, project.Id, project.Description);
            chip.Name = project.Name;
            wanted.Add(chip);
        }

        if (_projects.Count > 0 && _all.Any(r => ProjectName(r.Review.ProjectId) is null))
        {
            wanted.Add(Projects.FirstOrDefault(p => p.Key == ProjectChipViewModel.NoneKey)
                ?? new ProjectChipViewModel(ProjectChipViewModel.NoneKey, "No project", null, "Reviews that are not in a project"));
        }

        foreach (var chip in wanted)
        {
            chip.YourTurnCount = _all.Count(r => r.Stage == ReviewStage.YourTurn && chip.Contains(KnownProjectId(r.Review)));
        }

        if (!wanted.SequenceEqual(Projects))
        {
            Projects.Clear();
            foreach (var chip in wanted)
            {
                Projects.Add(chip);
            }
        }

        if (!Projects.Contains(SelectedProject))
        {
            SelectedProject.IsSelected = false;
            SelectedProject = Projects[0];
            SelectedProject.IsSelected = IsReviewsSection;
        }

        HasProjects = _projects.Count > 0;
    }

    /// <summary>A review whose project was deleted counts as having no project.</summary>
    private string? KnownProjectId(Review review) => ProjectName(review.ProjectId) is null ? null : review.ProjectId;

    private async Task RefreshTimesAsync()
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        while (await timer.WaitForNextTickAsync())
        {
            _ui.Post(() =>
            {
                var now = _clock.GetUtcNow();
                foreach (var item in _all)
                {
                    item.RefreshTime(now);
                }

                Greeting = GreetingFor(_clock.GetLocalNow().Hour);
            });
        }
    }

    private static string GreetingFor(int hour) => hour switch
    {
        < 5 => "Good night",
        < 12 => "Good morning",
        < 18 => "Good afternoon",
        _ => "Good evening"
    };
}
