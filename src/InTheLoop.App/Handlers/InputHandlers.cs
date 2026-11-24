using Microsoft.Maui.Handlers;
using Microsoft.UI.Xaml.Controls;
using WinBrush = Microsoft.UI.Xaml.Media.SolidColorBrush;

namespace InTheLoop.App.Handlers;

/// <summary>
/// Customises MAUI's platform handlers. The native WinUI text box draws its own border, underline
/// and focus fill; the app frames inputs itself, so that chrome is mapped away here.
/// </summary>
public static class InputHandlers
{
    private const string Key = "InTheLoop.Chromeless";

    private static readonly string[] BackgroundKeys =
    [
        "TextControlBackground",
        "TextControlBackgroundPointerOver",
        "TextControlBackgroundFocused",
        "TextControlBackgroundDisabled"
    ];

    private static readonly string[] BorderKeys =
    [
        "TextControlBorderBrush",
        "TextControlBorderBrushPointerOver",
        "TextControlBorderBrushFocused",
        "TextControlBorderBrushDisabled"
    ];

    public static MauiAppBuilder UseChromelessInputs(this MauiAppBuilder builder)
    {
        EntryHandler.Mapper.AppendToMapping(Key, (handler, _) => Strip(handler.PlatformView));
        EditorHandler.Mapper.AppendToMapping(Key, (handler, _) => Strip(handler.PlatformView));
        return builder;
    }

    private static void Strip(TextBox textBox)
    {
        var transparent = new WinBrush(Microsoft.UI.Colors.Transparent);
        foreach (var key in BackgroundKeys.Concat(BorderKeys))
        {
            textBox.Resources[key] = transparent;
        }

        textBox.Resources["TextControlBorderThemeThickness"] = new Microsoft.UI.Xaml.Thickness(0);
        textBox.Resources["TextControlBorderThemeThicknessFocused"] = new Microsoft.UI.Xaml.Thickness(0);
        textBox.BorderThickness = new Microsoft.UI.Xaml.Thickness(0);
        textBox.Background = transparent;
        textBox.Padding = new Microsoft.UI.Xaml.Thickness(0);
        textBox.UseSystemFocusVisuals = false;
    }
}
