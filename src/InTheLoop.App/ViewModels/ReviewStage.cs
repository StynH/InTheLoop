using InTheLoop.Core.Models;

namespace InTheLoop.App.ViewModels;

/// <summary>Whose move it is, which is what the person at the desk cares about.</summary>
public enum ReviewStage
{
    YourTurn,
    WithAi,
    Approved
}

public static class ReviewPresentation
{
    public static ReviewStage Stage(this Review review) =>
        review.AwaitingHuman ? ReviewStage.YourTurn
        : review.Status == ReviewStatus.Approved ? ReviewStage.Approved
        : ReviewStage.WithAi;

    public static string StatusLabel(this Review review) => review.Stage() switch
    {
        ReviewStage.YourTurn => "Your turn",
        ReviewStage.Approved => "Approved",
        _ => review.Status == ReviewStatus.ChangesRequested ? "Changes requested" : "Waiting on AI"
    };

    public static string RelativeTime(DateTimeOffset moment, DateTimeOffset now)
    {
        var age = now - moment;
        if (age < TimeSpan.FromMinutes(1))
        {
            return "now";
        }

        if (age < TimeSpan.FromHours(1))
        {
            return $"{(int)age.TotalMinutes}m";
        }

        if (age < TimeSpan.FromDays(1))
        {
            return $"{(int)age.TotalHours}h";
        }

        return age < TimeSpan.FromDays(7) ? $"{(int)age.TotalDays}d" : moment.ToLocalTime().ToString("d MMM");
    }
}
