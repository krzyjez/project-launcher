# Project Memory

## Sources Of Truth

- `readme.md` opisuje rolę aplikacji oraz kontrakt wspólnego rejestru `%USERPROFILE%\ai-tools\launch-projects.json`.
- `ProjectLauncher.Wpf\ProjectLauncher.Wpf.csproj` określa aplikację WPF na .NET 9 i wersję programu.

## Architecture

- `ProjectLauncher.Core` to biblioteka na czystym `net9.0` z całą logiką: model projektu, obsługa rejestru, ustawienia, uruchamianie edytora, filtr tagów i kolory workspace VS Code.
- `ProjectLauncher.Wpf` zawiera już tylko warstwę interfejsu: okna, konwertery wartości i funkcje systemowe Windows.
- `project-launcher.ps1` pozostaje fallbackiem Windows Forms.
- Model `ProjectItem` nie może zawierać typów interfejsu; pędzel karty i widoczność opisu dostarczają konwertery po stronie WPF.
- Podział istnieje pod kątem portu na Avalonię i uruchomienia launchera pod Linuksem; gałąź `avalon`.

## Domain Rules

- `path` jest głównym identyfikatorem projektu w `launch-projects.json`; porównanie ścieżek jest odporne na wielkość liter i separatory Windows.
- `shelved` oznacza projekt odstawiony, a nie usunięty.
- `tags` są listą tagów projektu; zaznaczone tagi działają jako filtr OR.

## Operational Conventions

- Aplikacja jest uruchamiana przez skrót `Projekty.lnk`, wskazujący na `dist\ProjectLauncher.Wpf.exe`.
- `PublishDir` w `ProjectLauncher.Wpf.csproj` kieruje `dotnet publish` do `dist`, więc publikacja zawsze trafia tam, gdzie celuje skrót; przed publikacją trzeba zamknąć działający launcher, bo blokuje pliki w `dist`.
- Do testów globalnego rejestru można użyć zmiennej `AI_TOOLS_HOME`.

## Pitfalls

- `ProjectLauncher.Core` musi kompilować się samodzielnie, bez projektu WPF. To jest test przenośności; jeśli przestaje, do biblioteki wszedł kod związany z Windowsem.
- Repozytorium ma mieszane końce linii: `MainWindow.xaml` i `.csproj` są w CRLF, a `EditDescriptionWindow.xaml.cs` i pliki przeniesione do `Core` w LF. `sed -i` z Git Basha przepisuje cały plik na LF, więc do edycji plików źródłowych używaj narzędzi zachowujących oryginalne zakończenia linii.
- Nie przywracaj starego rejestru `projects.json` z dawnej lokalizacji `P:\ai\tools\project-launcher`; aktywnym źródłem danych jest wyłącznie globalny `launch-projects.json`.
