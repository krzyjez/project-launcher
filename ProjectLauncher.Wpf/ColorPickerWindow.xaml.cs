using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ProjectLauncher.Wpf;

public partial class ColorPickerWindow : Window
{
    private readonly List<Button> swatchButtons = [];
    private readonly string originalColor;

    private static readonly string[] Palette =
    [
        "#FF3B30", "#FF6B1A", "#FFCC00", "#B6F000", "#2ECC71", "#16E0A8",
        "#00C2FF", "#3D7BFF", "#7C5CFF", "#B84DFF", "#FF4FD8", "#FF5C8A",
        "#FFFFFF", "#D8D9E6", "#B8B8C8", "#85858F", "#5D6475", "#111318",
        "#C62828", "#AD4B00", "#8D6E00", "#4D7C0F", "#00796B", "#006D9C",
        "#283593", "#5B21B6", "#86198F", "#BE185D", "#7F1D1D", "#3F3F46"
    ];

    public ColorPickerWindow(string initialColor)
        : this(initialColor, selectedColor: null)
    {
    }

    public ColorPickerWindow(string initialColor, string? selectedColor)
    {
        InitializeComponent();
        originalColor = NormalizeExistingColor(initialColor);
        var currentColor = string.IsNullOrWhiteSpace(selectedColor)
            ? originalColor
            : NormalizeExistingColor(selectedColor);
        BuildPalette();
        ColorBox.Text = currentColor;
        OriginalValueText.Text = originalColor;
        OriginalPreview.Background = new SolidColorBrush(ParseColor(originalColor));
        UpdateSelectionState();
        ColorBox.Focus();
        ColorBox.SelectAll();
    }

    public string SelectedColor => NormalizeColor(ColorBox.Text.Trim());

    private void BuildPalette()
    {
        foreach (var color in Palette)
        {
            var button = new Button
            {
                Style = (Style)FindResource("SwatchButtonStyle"),
                Background = new SolidColorBrush(ParseColor(color)),
                Tag = color,
                ToolTip = color,
            };

            button.Click += SwatchButton_Click;
            swatchButtons.Add(button);
            PaletteGrid.Children.Add(button);
        }
    }

    private void SwatchButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string color })
        {
            ColorBox.Text = color;
            ColorBox.CaretIndex = ColorBox.Text.Length;
        }
    }

    private void ColorBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateSelectionState();
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (!IsValidColor(SelectedColor))
        {
            MessageBox.Show(this, "Kolor musi miec format #RRGGBB.", "Projekty", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void UpdateSelectionState()
    {
        var isValid = IsValidColor(SelectedColor);

        SelectedPreview.Background = isValid
            ? new SolidColorBrush(ParseColor(SelectedColor))
            : new SolidColorBrush(Color.FromRgb(60, 62, 72));
        SelectedPreview.BorderBrush = isValid
            ? new SolidColorBrush(Color.FromRgb(247, 242, 232))
            : new SolidColorBrush(Color.FromRgb(255, 92, 138));

        SelectedValueText.Text = isValid ? SelectedColor : "Niepoprawny";
        ValidationText.Visibility = isValid ? Visibility.Collapsed : Visibility.Visible;

        foreach (var button in swatchButtons)
        {
            var isSelected = isValid && string.Equals((string?)button.Tag, SelectedColor, StringComparison.OrdinalIgnoreCase);
            button.BorderBrush = new SolidColorBrush(isSelected ? Color.FromRgb(247, 242, 232) : Color.FromRgb(60, 62, 72));
            button.BorderThickness = isSelected ? new Thickness(3) : new Thickness(1);
        }
    }

    private static Color ParseColor(string value)
    {
        return (Color)ColorConverter.ConvertFromString(value);
    }

    private static bool IsValidColor(string value)
    {
        try
        {
            _ = ParseColor(value);
            return value.Length == 7 && value.StartsWith('#');
        }
        catch
        {
            return false;
        }
    }

    private static string NormalizeColor(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "#FF6B1A";
        }

        return value.StartsWith('#') ? value.ToUpperInvariant() : $"#{value.ToUpperInvariant()}";
    }

    private static string NormalizeExistingColor(string value)
    {
        var normalized = NormalizeColor(value);
        return IsValidColor(normalized) ? normalized : "#FF6B1A";
    }
}
