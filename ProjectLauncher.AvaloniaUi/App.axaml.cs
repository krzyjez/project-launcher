using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace ProjectLauncher.AvaloniaUi;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow(_ReadScreenshotArgument(desktop.Args ?? []));
        }

        base.OnFrameworkInitializationCompleted();
    }

    // Tryb diagnostyczny: okno zapisuje swoj zrzut i konczy prace.
    private static string? _ReadScreenshotArgument(IReadOnlyList<string> args)
    {
        for (var index = 0; index < args.Count - 1; index++)
        {
            if (string.Equals(args[index], "--screenshot", StringComparison.OrdinalIgnoreCase))
                return args[index + 1];
        }

        return null;
    }
}
