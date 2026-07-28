---
name: init-session
description: Buduje krótki kontekst roboczy dla Project Launchera na początku sesji albo po resecie kontekstu, bez zapisywania plików.
version: 1
modified-date: 2026-07-28
---

# Init Session

Zbuduj krótki i użyteczny kontekst roboczy dla bieżącej sesji w repozytorium Project Launcher.

Nie streszczaj całego repozytorium. Przeczytaj tylko tyle, aby płynnie przejść do bieżącego zadania.

## Workflow

1. Uruchom `agent-signal get-name` i zapamiętaj zwrócone imię jako identyfikator agenta.
2. Przeczytaj `readme.md` jako podstawowy opis produktu i kontraktu `launch-projects.json`.
3. Jeśli istnieją, przeczytaj całe `project-memory.md` i `todo.md`.
4. Przeczytaj ostatni wpis z `version.md` tylko wtedy, gdy zadanie dotyczy wersji, ostatnich zmian albo wydania.
5. Dla zadania dotyczącego aplikacji WPF zajrzyj do `ProjectLauncher.Wpf\ProjectLauncher.Wpf.csproj`, a następnie tylko do plików bezpośrednio związanych z zadaniem.
6. Jeśli praca obejmuje workflow brancha, używaj tego samego imienia w `branch step-begin <name>` i `branch step-end <name> "<description>"`.
7. Jeśli użytkownik nie podał zadania, zakończ na krótkim `WORKING MEMORY`.

## Output Contract

Wypisz sekcję `WORKING MEMORY` w maksymalnie 10 krótkich liniach:

- `Cel` - rola launchera.
- `Źródła prawdy` - najważniejsze pliki dla bieżącego zadania.
- `Stack` - WPF i .NET 9, jeśli ma znaczenie.
- `Kontrakt danych` - tylko gdy zadanie dotyczy rejestru projektów.
- `Scope` - bieżący zakres sesji.

Jeśli użytkownik nie podał zadania, wpisz dosłownie:

`Scope: (nie podano zadania - init-session only)`

## Reading Rules

- Nie czytaj całego repozytorium podczas `init-session`.
- Nie czytaj katalogu `dist` ani wyników kompilacji.
- Nie czytaj wszystkich plików WPF bez związku z zadaniem.
- Traktuj `%USERPROFILE%\ai-tools\launch-projects.json` jako zewnętrzny rejestr danych; nie odczytuj go bez potrzeby bieżącego zadania.

## Guardrails

- Nie twórz ani nie modyfikuj plików podczas samego `init-session`.
- Nie zmieniaj rejestru `launch-projects.json` podczas budowania pamięci roboczej.
- Nie traktuj `project-launcher.ps1` jako głównej implementacji, chyba że zadanie wyraźnie dotyczy fallbacku PowerShell.
