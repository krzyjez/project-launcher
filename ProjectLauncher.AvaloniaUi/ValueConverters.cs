using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using ProjectLauncher.Core;

namespace ProjectLauncher.AvaloniaUi;

/// <summary>Zamienia kolor projektu zapisany jako #RRGGBB na pedzel uzywany w kartach</summary>
public sealed class ProjectColorToBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var text = value as string;
        if (!string.IsNullOrWhiteSpace(text) && Color.TryParse(text, out var color))
            return new SolidColorBrush(color);

        return new SolidColorBrush(Color.Parse(ProjectItem.DefaultColors[0]));
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

/// <summary>Zwraca prawde dla tekstu, ktory ma cokolwiek do pokazania</summary>
public sealed class TextIsNotEmptyConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return !string.IsNullOrWhiteSpace(value as string);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
