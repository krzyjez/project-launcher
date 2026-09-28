---
name: init-session
description: Buduje krótki kontekst roboczy dla Project Launchera na początku sesji albo po resecie kontekstu, bez zapisywania plików.
version: 7
modified-date: 2026-09-19
---

# Init Session

Zbuduj krótki i użyteczny kontekst roboczy dla bieżącej sesji w repozytorium Project Launcher.

Nie streszczaj całego repozytorium. Przeczytaj tylko tyle, aby płynnie przejść do bieżącego zadania.

## Workflow

1. Uruchom `agent-signal get-name <sessionId>` i zapamiętaj zwrócone imię jako identyfikator agenta. W Codexie argument można pominąć tylko przy dostępnej zmiennej `CODEX_THREAD_ID`. W Claude podaj identyfikator własnej sesji, ustalony z kontekstu sesji lub ścieżki jej transkryptu. Nie wybieraj ID na podstawie ostatniej aktywności w repo; jeśli nie znasz własnego ID, zgłoś brak tej informacji zamiast zgadywać.
2. Przeczytaj `readme.md` jako podstawowy opis produktu i kontraktu `launch-projects.json`.
3. Jeśli istnieją, przeczytaj całe `project-memory.md` i `todo.md`.
4. Przeczytaj ostatni wpis z `version.md` tylko wtedy, gdy zadanie dotyczy wersji, ostatnich zmian albo wydania.
5. Logika (model, rejestr, ustawienia, uruchamianie edytora) jest w `ProjectLauncher.Core`, a interfejsy w `ProjectLauncher.Wpf` i `ProjectLauncher.AvaloniaUi`. Zajrzyj do `.csproj` projektu, którego dotyczy zadanie (wersja programu jest w `ProjectLauncher.Wpf\ProjectLauncher.Wpf.csproj`), a następnie tylko do plików bezpośrednio z nim związanych.
6. Jeśli praca obejmuje workflow brancha, używaj tego samego imienia w `branch step-begin <name>` i `branch step-end <name> "<description>"`.
7. Jeśli użytkownik nie podał zadania, zakończ na krótkim `WORKING MEMORY`.

## Output Contract

Wypisz sekcję `WORKING MEMORY` w maksymalnie 10 krótkich liniach:

- `Cel` - rola launchera.
- `Źródła prawdy` - najważniejsze pliki dla bieżącego zadania.
- `Stack` - .NET 9, `Core` oraz WPF albo Avalonia, jeśli ma znaczenie.
- `Kontrakt danych` - tylko gdy zadanie dotyczy rejestru projektów.
- `Scope` - bieżący zakres sesji.

Jeśli użytkownik nie podał zadania, wpisz dosłownie:

`Scope: (nie podano zadania - init-session only)`

## Reading Rules

- Nie czytaj całego repozytorium podczas `init-session`.
- Nie czytaj katalogów `dist`, `dist-avalonia` ani wyników kompilacji.
- Nie czytaj wszystkich plików WPF, Avalonii ani `Core` bez związku z zadaniem.
- Traktuj `%USERPROFILE%\ai-tools\launch-projects.json` jako zewnętrzny rejestr danych; nie odczytuj go bez potrzeby bieżącego zadania.

## Guardrails

- Nie twórz ani nie modyfikuj plików podczas samego `init-session`.
- Nie streszczaj użytkownikowi przeczytanych plików ani tego, na czym polega projekt; `WORKING MEMORY` jest dla agenta.
- Nie zmieniaj rejestru `launch-projects.json` podczas budowania pamięci roboczej.
- Nie traktuj `project-launcher.ps1` jako głównej implementacji, chyba że zadanie wyraźnie dotyczy fallbacku PowerShell.
