# Project Memory

## Sources Of Truth

- `readme.md` opisuje rolę aplikacji oraz kontrakt wspólnego rejestru `%USERPROFILE%\ai-tools\launch-projects.json`.
- `ProjectLauncher.Wpf\ProjectLauncher.Wpf.csproj` określa aplikację WPF na .NET 9 i wersję programu.

## Architecture

- `ProjectLauncher.Core` to biblioteka na czystym `net9.0` z całą logiką: model projektu, obsługa rejestru, ustawienia, uruchamianie edytora, filtr tagów, kolory workspace VS Code i odczyt stanu Git (`GitStatusReader`).
- `ProjectLauncher.Wpf` zawiera już tylko warstwę interfejsu: okna, konwertery wartości i funkcje systemowe Windows (ikona w zasobniku, autostart, jedna instancja).
- `project-launcher.ps1` pozostaje fallbackiem Windows Forms.
- Model `ProjectItem` nie może zawierać typów interfejsu; pędzel karty i widoczność opisu dostarczają konwertery po stronie WPF. Pola widoku (`Number`, `IsExpanded`, `GitStatus`) mają `[JsonIgnore]` i nie trafiają do rejestru.
- Podział istnieje pod kątem portu na Avalonię i uruchomienia launchera pod Linuksem; port w `ProjectLauncher.AvaloniaUi` jest niedokończony (patrz `todo.md`).
- WPF działa jako jedna instancja w tle: pierwsza trzyma mutex `Local\ProjectLauncher.Wpf.Instance`, kolejne sygnalizują zdarzenie `Local\ProjectLauncher.Wpf.Show` i kończą się. `Close()` w trybie zasobnika tylko chowa okno; realne wyjście jest wyłącznie przez menu ikony. Tryb `--screenshot` omija ten mechanizm i działa jednorazowo.

## Domain Rules

- `path` jest głównym identyfikatorem projektu w `launch-projects.json`; porównanie ścieżek jest odporne na wielkość liter i separatory Windows.
- `shelved` oznacza projekt odstawiony, a nie usunięty.
- `tags` są listą tagów projektu; zaznaczone tagi działają jako filtr OR.

## Operational Conventions

- Aplikacja jest uruchamiana przez skrót `Projekty.lnk`, wskazujący na `dist\ProjectLauncher.Wpf.exe`.
- `PublishDir` w `ProjectLauncher.Wpf.csproj` kieruje `dotnet publish` do `dist`, więc publikacja zawsze trafia tam, gdzie celuje skrót; przed publikacją trzeba zamknąć działający launcher przez `Zakończ` w menu ikony w zasobniku (samo `×` tylko chowa okno), bo blokuje pliki w `dist`.
- Do testów globalnego rejestru można użyć zmiennej `AI_TOOLS_HOME`. Wygląd okna sprawdza się przez `--screenshot <plik.png>`, który czeka na odczyt stanu Git przed zrzutem; kliknięcia można sprawdzać przez UI Automation na testowym rejestrze.
- Launcher odczytuje repozytoria, w których równolegle pracują agenci: `git status` idzie z `GIT_OPTIONAL_LOCKS=0`, a stan GitHuba z `git ls-remote` (bez `fetch`, bez zapisu w repo).

## Pitfalls

- `ProjectLauncher.Core` musi kompilować się samodzielnie, bez projektu WPF. To jest test przenośności; jeśli przestaje, do biblioteki wszedł kod związany z Windowsem.
- Repozytorium ma mieszane końce linii: `MainWindow.xaml` i `.csproj` są w CRLF, a `EditDescriptionWindow.xaml.cs` i pliki przeniesione do `Core` w LF. `sed -i` z Git Basha przepisuje cały plik na LF, więc do edycji plików źródłowych używaj narzędzi zachowujących oryginalne zakończenia linii.
- W XAML atrybut ustawiony wprost na elemencie (np. `TextWrapping`) przesłania wyzwalacze stylu; wartości zmieniane przez `DataTrigger` ustawiaj przez `Setter` w stylu.
- Nie uruchamiaj równolegle dwóch `dotnet build` projektu WPF: kompilacja XAML zostawia wtedy w katalogu projektu `*_wpftmp.csproj`, przez który kolejne `dotnet build ProjectLauncher.Wpf` kończy się błędem MSB1050.
- Projekt WPF ma `UseWindowsForms` tylko dla `NotifyIcon`; globalne usingi WinForms są usunięte w `.csproj`, bo kolidowałyby z typami WPF (`MessageBox`, `Button`, `Point`).
- Nie przywracaj starego rejestru `projects.json` z dawnej lokalizacji `P:\ai\tools\project-launcher`; aktywnym źródłem danych jest wyłącznie globalny `launch-projects.json`.
