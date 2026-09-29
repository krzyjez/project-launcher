using System.Windows;
using System.Windows.Controls;

namespace ProjectLauncher.Wpf;

/// <summary>Uklada elementy na zmiane w dwie niezalezne kolumny (1 i 2 obok siebie, 3 pod 1, 4 pod 2); wysokosc karty nie zalezy od sasiada</summary>
public sealed class TwoColumnPanel : Panel
{
    public static readonly DependencyProperty ColumnGapProperty = DependencyProperty.Register(
        nameof(ColumnGap),
        typeof(double),
        typeof(TwoColumnPanel),
        new FrameworkPropertyMetadata(10.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

    /// <summary>Odstep miedzy kolumnami</summary>
    public double ColumnGap
    {
        get => (double)GetValue(ColumnGapProperty);
        set => SetValue(ColumnGapProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var columnWidth = _ColumnWidth(availableSize.Width);
        var heights = new double[2];

        for (var index = 0; index < InternalChildren.Count; index++)
        {
            var child = InternalChildren[index];
            child.Measure(new Size(columnWidth, double.PositiveInfinity));
            heights[index % 2] += child.DesiredSize.Height;
        }

        var width = double.IsInfinity(availableSize.Width) ? columnWidth * 2 + ColumnGap : availableSize.Width;
        return new Size(width, Math.Max(heights[0], heights[1]));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var columnWidth = _ColumnWidth(finalSize.Width);
        var offsets = new double[2];

        for (var index = 0; index < InternalChildren.Count; index++)
        {
            var child = InternalChildren[index];
            var column = index % 2;
            var height = child.DesiredSize.Height;
            child.Arrange(new Rect(column * (columnWidth + ColumnGap), offsets[column], columnWidth, height));
            offsets[column] += height;
        }

        return finalSize;
    }

    // Bez ograniczenia szerokosci (np. przy pierwszym pomiarze) przyjmujemy rozsadna szerokosc karty.
    private double _ColumnWidth(double totalWidth)
    {
        return double.IsInfinity(totalWidth)
            ? 480
            : Math.Max(0, (totalWidth - ColumnGap) / 2);
    }
}
