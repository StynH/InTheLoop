using CommunityToolkit.Mvvm.ComponentModel;
using InTheLoop.Core.Models;

namespace InTheLoop.App.ViewModels;

/// <summary>One message in the conversation between the AI and the human.</summary>
public sealed class EntryViewModel(ReviewEntry entry, string aiName)
{
    public string Id => entry.Id;

    public EntryKind Kind => entry.Kind;

    public bool IsAi => entry.Author == Author.Ai;

    public bool IsHuman => !IsAi;

    public string AuthorName => IsAi ? aiName : "You";

    public string Time => entry.CreatedAt.ToLocalTime().ToString("HH:mm");

    public string? Text => entry.Text;

    public bool HasText => !string.IsNullOrWhiteSpace(entry.Text);

    public bool HasAction => entry.Kind != EntryKind.Message;

    public string Action => entry.Kind switch
    {
        EntryKind.Screenshot => $"Posted screenshot v{entry.Revision}",
        EntryKind.Approval => $"Approved v{entry.Revision}",
        EntryKind.ChangeRequest => $"Requested changes to v{entry.Revision}",
        _ => ""
    };
}

/// <summary>One screenshot in the revision strip.</summary>
public partial class RevisionViewModel(ReviewEntry entry, string imagePath) : ObservableObject
{
    public int Number => entry.Revision ?? 1;

    public string Label => $"v{Number}";

    public string ImagePath => imagePath;

    public string? Note => entry.Text;

    public DateTimeOffset CreatedAt => entry.CreatedAt;

    [ObservableProperty]
    public partial bool IsSelected { get; set; }
}
