using System.Drawing;
using System.Windows.Forms;

namespace ProjectLauncher.Wpf;

/// <summary>Ikona launchera w zasobniku systemowym: klikniecie pokazuje okno, menu obsluguje autostart i zamkniecie programu</summary>
internal sealed class TrayIcon : IDisposable
{
    private readonly NotifyIcon _notifyIcon;

    /// <summary>Tworzy widoczna ikone; akcje sa wywolywane w watku UI, bo NotifyIcon dziala na petli komunikatow WPF</summary>
    public TrayIcon(Action showLauncher, Action exitLauncher)
    {
        var autostartItem = new ToolStripMenuItem("Uruchamiaj przy starcie Windows")
        {
            Checked = AutostartRegistration.IsEnabled(),
            CheckOnClick = true
        };
        autostartItem.CheckedChanged += (_, _) => AutostartRegistration.SetEnabled(autostartItem.Checked);

        var menu = new ContextMenuStrip();
        menu.Items.Add("Pokaz projekty", null, (_, _) => showLauncher());
        menu.Items.Add(autostartItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Zakoncz", null, (_, _) => exitLauncher());

        _notifyIcon = new NotifyIcon
        {
            Icon = _LoadIcon(),
            Text = "Projekty",
            ContextMenuStrip = menu,
            Visible = true
        };

        _notifyIcon.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
                showLauncher();
        };
    }

    /// <summary>Usuwa ikone z zasobnika; bez tego po zamknieciu programu zostaje martwa ikona do najechania myszka</summary>
    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.ContextMenuStrip?.Dispose();
        _notifyIcon.Dispose();
    }

    // Ikona pliku exe, czyli ta sama rakieta co w oknie i na pasku zadan.
    private static Icon _LoadIcon()
    {
        var executablePath = Environment.ProcessPath;
        return executablePath is not null && Icon.ExtractAssociatedIcon(executablePath) is { } icon
            ? icon
            : SystemIcons.Application;
    }
}
