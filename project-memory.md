# Project Memory

## Sources Of Truth

- `readme.md` opisuje rolę aplikacji oraz kontrakt wspólnego rejestru `%USERPROFILE%\ai-tools\launch-projects.json`.
- `ProjectLauncher.Wpf\ProjectLauncher.Wpf.csproj` określa aplikację WPF na .NET 9 i wersję programu.

## Architecture

- `ProjectLauncher.Wpf` jest główną implementacją aplikacji.
- `project-launcher.ps1` pozostaje fallbackiem Windows Forms.

## Domain Rules

- `path` jest głównym identyfikatorem projektu w `launch-projects.json`; porównanie ścieżek jest odporne na wielkość liter i separatory Windows.
- `shelved` oznacza projekt odstawiony, a nie usunięty.
- `tags` są listą tagów projektu; zaznaczone tagi działają jako filtr OR.

## Operational Conventions

- Aplikacja jest uruchamiana przez skrót `Projekty.lnk`, wskazujący na `dist\ProjectLauncher.Wpf.exe`.
- `PublishDir` w `ProjectLauncher.Wpf.csproj` kieruje `dotnet publish` do `dist`, więc publikacja zawsze trafia tam, gdzie celuje skrót; przed publikacją trzeba zamknąć działający launcher, bo blokuje pliki w `dist`.
- Do testów globalnego rejestru można użyć zmiennej `AI_TOOLS_HOME`.

## Pitfalls

- Nie przywracaj starego rejestru `projects.json` z dawnej lokalizacji `P:\ai\tools\project-launcher`; aktywnym źródłem danych jest wyłącznie globalny `launch-projects.json`.
