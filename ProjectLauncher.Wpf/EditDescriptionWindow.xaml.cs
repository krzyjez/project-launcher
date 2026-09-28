using System.Windows;
using System.Windows.Media;
using ProjectLauncher.Core;

namespace ProjectLauncher.Wpf;

public partial class EditDescriptionWindow : Window
{
    private string selectedColor = "#FF6B1A";

    public EditDescriptionWindow(ProjectItem project)
        : this(project, isNewProject: false)
    {
    }

    public EditDescriptionWindow(ProjectItem project, bool isNewProject)
    {
        InitializeComponent();
        Title = isNewProject ? "Dodaj projekt" : "Edytuj projekt";
        NameBox.Text = project.Name;
        selectedColor = NormalizeExistingColor(project.Color);
        AgentNamesBox.Text = string.Join(", ", project.AgentNames);
        TagsBox.Text = string.Join(", ", project.Tags);
        DescriptionBox.Text = project.Description;
        UpdateColorPreview();

        if (isNewProject)
        {
            NameBox.Focus();
            NameBox.SelectAll();
        }
        else
        {
            DescriptionBox.Focus();
            DescriptionBox.CaretIndex = DescriptionBox.Text.Length;
        }
    }

    public string ProjectName => NameBox.Text.Trim();

    public string ProjectColor => selectedColor;

    public List<string> AgentNames => AgentNamesBox.Text
        .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();

    public List<string> Tags => TagsBox.Text
        .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();

    public string DescriptionText => DescriptionBox.Text.Trim();

    private void ColorPickerButton_Click(object sender, RoutedEventArgs e)
    {
        var picker = new ColorPickerWindow(ProjectColor)
        {
            Owner = this
        };

        if (picker.ShowDialog() == true)
        {
            selectedColor = picker.SelectedColor;
            UpdateColorPreview();
        }
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ProjectName))
        {
            MessageBox.Show(this, "Nazwa projektu nie moze byc pusta.", "Projekty", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!IsValidColor(ProjectColor))
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

    private void UpdateColorPreview()
    {
        ColorPreview.Background = IsValidColor(ProjectColor)
            ? new SolidColorBrush((Color)ColorConverter.ConvertFromString(ProjectColor))
            : new SolidColorBrush(Color.FromRgb(60, 62, 72));
    }

    private static bool IsValidColor(string value)
    {
        try
        {
            _ = (Color)ColorConverter.ConvertFromString(value);
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
