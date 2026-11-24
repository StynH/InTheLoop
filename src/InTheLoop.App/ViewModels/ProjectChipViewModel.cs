using CommunityToolkit.Mvvm.ComponentModel;

namespace InTheLoop.App.ViewModels;

/// <summary>One entry in the project switcher. "All" and "No project" are chips too.</summary>
public partial class ProjectChipViewModel(string key, string name, string? projectId, string? description) : ObservableObject
{
    public const string AllKey = "*";
    public const string NoneKey = "";

    /// <summary>Identifies the chip: a project id, <see cref="AllKey"/> or <see cref="NoneKey"/>.</summary>
    public string Key => key;

    /// <summary>The project behind this chip, or null for the "All" and "No project" chips.</summary>
    public string? ProjectId => projectId;

    public string Description => description ?? Name;

    public bool IsAll => key == AllKey;

    [ObservableProperty]
    public partial string Name { get; set; } = name;

    public string Glyph => IsAll ? "\uE8B9" : projectId is null ? "\uE7C3" : "\uE838";

    public bool HasBadge => YourTurnCount > 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasBadge))]
    public partial int YourTurnCount { get; set; }

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    public bool Contains(string? reviewProjectId) =>
        IsAll || (projectId is null ? reviewProjectId is null : projectId == reviewProjectId);
}
