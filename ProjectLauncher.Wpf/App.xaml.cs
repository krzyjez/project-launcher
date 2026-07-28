using System.Windows;
using System.Windows.Threading;

namespace ProjectLauncher.Wpf;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var windowName = GetArgumentValue(e.Args, "--window");
        if (string.Equals(windowName, "color-picker", StringComparison.OrdinalIgnoreCase))
        {
            ShowColorPickerDiagnostics(e.Args);
            return;
        }

        var mainWindow = new MainWindow();
        MainWindow = mainWindow;
        mainWindow.Show();
    }

    private void ShowColorPickerDiagnostics(string[] args)
    {
        var initialColor = GetArgumentValue(args, "--color") ?? "#9B7CFF";
        var selectedColor = GetArgumentValue(args, "--selected-color");
        var screenshotPath = GetArgumentValue(args, "--screenshot");
        var window = new ColorPickerWindow(initialColor, selectedColor)
        {
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            ShowInTaskbar = true
        };

        MainWindow = window;

        if (!string.IsNullOrWhiteSpace(screenshotPath))
        {
            window.Loaded += async (_, _) =>
            {
                await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                WindowScreenshot.SaveToPng(window, screenshotPath);
                Shutdown();
            };
        }

        window.Show();
    }

    private static string? GetArgumentValue(string[] args, string name)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
            {
                return args[i + 1];
            }
        }

        return null;
    }
}
