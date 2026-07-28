using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ProjectLauncher.Wpf;

public static class WindowScreenshot
{
    public static void SaveToPng(Window window, string screenshotPath)
    {
        var targetWidth = Math.Max(1, (int)Math.Ceiling(window.ActualWidth));
        var targetHeight = Math.Max(1, (int)Math.Ceiling(window.ActualHeight));
        var renderBitmap = new RenderTargetBitmap(targetWidth, targetHeight, 96, 96, PixelFormats.Pbgra32);
        renderBitmap.Render(window);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(renderBitmap));

        Directory.CreateDirectory(Path.GetDirectoryName(screenshotPath)!);
        using var stream = File.Create(screenshotPath);
        encoder.Save(stream);
    }
}
