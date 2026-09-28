using System.Windows;
using System.Windows.Threading;

namespace ProjectLauncher.Wpf;

public partial class App : Application
{
    /// <summary>Argument autostartu: program startuje schowany w zasobniku, bez pokazywania okna</summary>
    public const string TrayArgument = "--tray";

    // Nazwy obiektow w sesji uzytkownika: pierwsza instancja trzyma mutex, kolejne tylko sygnalizuja jej zdarzenie.
    private const string InstanceMutexName = @"Local\ProjectLauncher.Wpf.Instance";
    private const string ShowEventName = @"Local\ProjectLauncher.Wpf.Show";
    private const int AllowAnyProcess = -1;

    private Mutex? _instanceMutex;
    private EventWaitHandle? _showEvent;
    private RegisteredWaitHandle? _showWait;
    private TrayIcon? _trayIcon;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var windowName = GetArgumentValue(e.Args, "--window");
        if (string.Equals(windowName, "color-picker", StringComparison.OrdinalIgnoreCase))
        {
            ShowColorPickerDiagnostics(e.Args);
            return;
        }

        // Zrzut diagnostyczny dziala jak dawny, jednorazowy launcher, obok ewentualnej instancji w zasobniku.
        if (GetArgumentValue(e.Args, "--screenshot") is not null)
        {
            var screenshotWindow = new MainWindow();
            MainWindow = screenshotWindow;
            screenshotWindow.Show();
            return;
        }

        _StartResident(showWindow: !e.Args.Contains(TrayArgument, StringComparer.OrdinalIgnoreCase));
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _showWait?.Unregister(null);
        _showEvent?.Dispose();
        _trayIcon?.Dispose();
        _instanceMutex?.Dispose();
        base.OnExit(e);
    }

    // Launcher dziala w tle z ikona w zasobniku; kolejne uruchomienie exe (skrot, Ctrl+Alt+P) tylko pokazuje okno.
    private void _StartResident(bool showWindow)
    {
        _instanceMutex = new Mutex(initiallyOwned: false, InstanceMutexName, out var createdNew);
        _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);
        if (!createdNew)
        {
            // Pierwsza instancja moze wyciagnac okno na wierzch tylko za zgoda procesu, ktory uruchomil uzytkownik.
            AllowSetForegroundWindow(AllowAnyProcess);
            _showEvent.Set();
            Shutdown();
            return;
        }

        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        var mainWindow = new MainWindow { IsResident = true };
        MainWindow = mainWindow;

        _trayIcon = new TrayIcon(
            mainWindow.ShowLauncher,
            () => _ExitLauncher(mainWindow),
            mainWindow.ShowTaskDescriptions,
            mainWindow.SetShowTaskDescriptions);
        _showWait = ThreadPool.RegisterWaitForSingleObject(
            _showEvent,
            (_, _) => Dispatcher.BeginInvoke(mainWindow.ShowLauncher),
            null,
            Timeout.Infinite,
            executeOnlyOnce: false);

        if (showWindow)
            mainWindow.ShowLauncher();
    }

    // Jedyna droga do faktycznego zamkniecia launchera: menu ikony w zasobniku.
    private void _ExitLauncher(MainWindow mainWindow)
    {
        mainWindow.AllowClose();
        Shutdown();
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool AllowSetForegroundWindow(int processId);

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
