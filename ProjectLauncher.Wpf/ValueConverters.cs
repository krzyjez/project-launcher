using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using ProjectLauncher.Core;

namespace ProjectLauncher.Wpf;

/// <summary>Zamienia kolor projektu zapisany jako #RRGGBB na pedzel uzywany w kartach</summary>
public sealed class ProjectColorToBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return _CreateBrush(value as string ?? "");
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }

    // Przy blednym zapisie koloru wracamy do pierwszego koloru domyslnego.
    private static Brush _CreateBrush(string value)
    {
        try
        {
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString(_Normalize(value)));
        }
        catch (FormatException)
        {
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString(ProjectItem.DefaultColors[0]));
        }
    }

    private static string _Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return ProjectItem.DefaultColors[0];

        var trimmed = value.Trim();
        return trimmed.StartsWith('#') ? trimmed.ToUpperInvariant() : $"#{trimmed.ToUpperInvariant()}";
    }
}

/// <summary>Pokazuje element, gdy powiazana flaga jest wylaczona; odwrotnosc wbudowanego BooleanToVisibilityConverter</summary>
public sealed class InverseBooleanToVisibilityConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is true ? Visibility.Collapsed : Visibility.Visible;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

/// <summary>Ukrywa element, gdy projekt jest w kategorii podanej w parametrze; np. "Przenies do archiwum" znika w archiwum</summary>
public sealed class ProjectStatusNotEqualToVisibilityConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is ProjectStatus status && Enum.TryParse<ProjectStatus>(parameter as string, out var hidden) && status == hidden
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

/// <summary>Ukrywa element, gdy powiazany tekst jest pusty</summary>
public sealed class EmptyTextToVisibilityConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return string.IsNullOrWhiteSpace(value as string) ? Visibility.Collapsed : Visibility.Visible;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
