using Microsoft.Win32;

namespace ProjectLauncher.Wpf;

/// <summary>Wpis launchera w autostarcie biezacego uzytkownika (HKCU\...\Run); program startuje wtedy schowany w zasobniku</summary>
internal static class AutostartRegistration
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "ProjectLauncher";

    /// <summary>Sprawdza, czy w autostarcie jest wpis launchera</summary>
    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
        return key?.GetValue(ValueName) is string;
    }

    /// <summary>Dodaje albo usuwa wpis; zapisywana jest sciezka aktualnie uruchomionego exe</summary>
    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
        if (enabled)
            key.SetValue(ValueName, $"\"{Environment.ProcessPath}\" {App.TrayArgument}");
        else
            key.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}
