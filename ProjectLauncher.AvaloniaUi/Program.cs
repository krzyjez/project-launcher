using Avalonia;

namespace ProjectLauncher.AvaloniaUi;

internal static class Program
{
    /// <summary>Punkt wejscia aplikacji; nie wolno tu uzywac kodu interfejsu przed inicjalizacja Avalonii</summary>
    [STAThread]
    public static void Main(string[] args)
    {
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    /// <summary>Konfiguracja Avalonii uzywana takze przez narzedzia projektowe</summary>
    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
    }
}
