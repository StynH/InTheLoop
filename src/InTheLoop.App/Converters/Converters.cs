using System.Globalization;
using InTheLoop.App.ViewModels;
using InTheLoop.Core.Models;

namespace InTheLoop.App.Converters;

/// <summary>True when the bound value's name equals the converter parameter.</summary>
public sealed class EqualsConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        string.Equals(value?.ToString(), parameter?.ToString(), StringComparison.Ordinal);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>True when the bound value is a non-empty string.</summary>
public sealed class HasValueConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        !string.IsNullOrWhiteSpace(value?.ToString());

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Maps whose turn it is to a palette colour. Parameter "Soft" selects the tinted background.</summary>
public sealed class StageColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var soft = parameter as string == "Soft";
        var key = value switch
        {
            ReviewStage.YourTurn => soft ? "AccentSoft" : "AccentText",
            ReviewStage.Approved => soft ? "SuccessSoft" : "Success",
            _ => soft ? "WarningSoft" : "Warning"
        };
        return Palette.Get(key);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Colours the action line of a conversation entry.</summary>
public sealed class EntryKindColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => Palette.Get(value switch
    {
        EntryKind.Approval => "Success",
        EntryKind.ChangeRequest => "Warning",
        EntryKind.Screenshot => "AccentText",
        _ => "TextMuted"
    });

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

internal static class Palette
{
    public static Color Get(string key) =>
        Application.Current is { } app && app.Resources.TryGetValue(key, out var value) && value is Color color
            ? color
            : Colors.Transparent;
}
