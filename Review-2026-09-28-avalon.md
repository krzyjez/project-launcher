# Review 2026-09-28 - galaz avalon wzgledem main

## Problemy krytyczne

Brak.

## Wazne problemy

1. `ProjectLauncher.AvaloniaUi/MainWindow.axaml.cs:151-155` - `_OpenProject` po cichu nic nie robi, gdy nie znajdzie VS Code albo katalogu projektu. WPF w tej samej sytuacji pokazuje komunikat (`ProjectLauncher.Wpf/MainWindow.xaml.cs:227-237`). Pod Linuksem to bedzie domyslny scenariusz (patrz 2 i 3): klikniecie karty nie da zadnej reakcji i nie bedzie wiadomo dlaczego. Naprawa: pokazac komunikat w oknie (Avalonia nie ma wbudowanego MessageBox - wystarczy prosty dialog albo pasek bledu w naglowku).

2. `ProjectLauncher.Core/ProjectPathTranslator.cs` - tlumacz sciezek ma testy, ale nie jest nigdzie uzywany. Avalonia przekazuje `project.Path` w zapisie Windows prosto do `Directory.Exists`, `WorkspaceColorSettings.Apply` i `EditorLauncher.CreateStartInfo`, wiec pod Linuksem zaden projekt sie nie otworzy. To czesciowa implementacja: klasa wyglada na gotowa, ale nie jest wpieta w przeplyw. Naprawa: w `_OpenProject` raz wyliczyc `ProjectPathTranslator.ToCurrentSystem(project.Path)` i uzywac tej sciezki we wszystkich trzech wywolaniach. Decyzja o konwencji montowania jest wciaz otwarta w `todo.md` (1.d), wiec mozna to swiadomie odlozyc, ale warto to zapisac.

3. `ProjectLauncher.Core/EditorLauncher.cs:9-20` - wyszukiwanie edytora zna tylko Windows: kandydaci to `Code.exe`/`code.cmd`, a w PATH szuka `code.cmd` i `code.exe`. Pod Linuksem plik nazywa sie `code`, wiec `ResolveEditorPath` zwroci `null`. Klasa trafila do przenosnego `Core`, a nadal jest zalezna od Windowsa - to sprzeczne z zalozeniem z `project-memory.md` ("Core ... pod katem portu na Avalonie i Linuksa"). Naprawa: gdy `!OperatingSystem.IsWindows()`, szukac w PATH `code`.

## Drobne uwagi

4. `ProjectLauncher.AvaloniaUi/MainWindow.axaml.cs:34-39, 159-162` - brak obslugi bledow przy wczytaniu rejestru, zapisie i `Process.Start`. Uszkodzony `launch-projects.json` wywroci aplikacje w konstruktorze okna bez komunikatu. WPF ma to samo przy wczytaniu, wiec to nie jest regresja, ale warto to naprawic razem z punktem 1.

5. `ProjectLauncher.Core/ProjectLauncherPaths.cs:18-23` - przeniesiony z WPF kod nadal kopiuje stary `projects.json` znaleziony gdziekolwiek nad katalogiem aplikacji. `project-memory.md` wprost mowi, zeby nie przywracac starego rejestru. Teraz ta logika dziala tez dla `dist-avalonia`. Kod byl juz wczesniej, ale przy przeniesieniu do `Core` byla dobra okazja, zeby go usunac - zostawiam do decyzji. Dodatkowo getter sciezki ma efekty uboczne (tworzy katalog, kopiuje plik).

6. `ProjectLauncher.Core/ProjectPathTranslator.cs:25-38, 74-90` - dwa przypadki brzegowe: punkt montowania `/` daje `//ai`, a dwie litery zmapowane na ten sam punkt daja niedeterministyczny wynik `ToWindows`. Niskie ryzyko, bo mapy montowan konfiguruje uzytkownik.

7. `ProjectLauncher.Core/EditorLauncher.cs`, `ProjectLauncher.Core/ProjectLauncherPaths.cs` - publiczne klasy bez komentarza klasy, a metody w `ProjectLauncherPaths` bez komentarzy XML. Reguly projektu tego wymagaja.

## Proponowane testy

1. `ProjectRegistry` z `AI_TOOLS_HOME` ustawionym na katalog tymczasowy: migracja `hidden` -> `shelved`, uzupelnianie `order` i `color`, zapis bez BOM, brak pliku -> pusta lista. Rejestr jest wspolnym kontraktem z innymi narzedziami, a teraz siedzi w testowalnym `Core`.
2. `ProjectRegistry.LoadSettings`: uszkodzony JSON i tryb `Order` -> wartosci domyslne.
3. `ProjectPathTranslator`: punkt montowania `/` i konflikt dwoch liter na jednym punkcie (po decyzji, jakie zachowanie jest poprawne).
4. Po wpieciu punktow 2 i 3: test `EditorLauncher` dla PATH z plikiem `code` pod systemem innym niz Windows (mozna ustawic PATH na katalog tymczasowy).

## Co jest dobre i nie powinno byc ruszane

1. Podzial na `Core` (czysty `net9.0`) i cienkie warstwy UI - `MainWindow.xaml.cs` w WPF schudl o ok. 500 linii, a Avalonia korzysta z tej samej logiki rejestru, sortowania i filtra tagow.
2. `ProjectItem` bez typow UI, pedzle i widocznosc w konwerterach po stronie kazdego UI - dokladnie zgodnie z decyzja w `project-memory.md`.
3. `ProjectPathTranslator` jest dobrze przemyslany: dopasowanie na granicy katalogu (`/home` nie trafia pod `/h`), najdluzszy punkt montowania wygrywa, testy round-trip.
4. `dist-avalonia` osobno od `dist` - skrot `Projekty.lnk` dalej wskazuje na WPF, wiec port nie zagraza codziennemu uzyciu.

## Werdykt

Gotowe - do zamkniecia galezi. WPF buduje sie bez ostrzezen, testy `Core` przechodza (18/18), a problemy 1-3 dotycza niedokonczonego portu na Linuksa, ktory jeszcze nie jest uzywany produkcyjnie. Warto je dopisac do `todo.md` (punkt 1). WPF nie byl uruchamiany recznie - weryfikacja braku regresji ogranicza sie do kompilacji.

## Zakres review

Wszystkie zmiany na galezi `avalon` wzgledem `main` (`git diff main...avalon`, 7 commitow, 28 plikow): `ProjectLauncher.Core`, `ProjectLauncher.Core.Tests`, `ProjectLauncher.AvaloniaUi`, zmiany w `ProjectLauncher.Wpf`, `.gitignore`, `project-memory.md`, `todo.md`. XAML Avalonii przejrzany pobieznie.

## Wykorzystany kontekst

`readme.md`, `project-memory.md`, `todo.md`, `review-code-csharp.md`. Bez subagentow - zakres miescil sie w jednym przebiegu.

## Wykryty stack

C# / .NET 9, WPF, Avalonia 12.1.2, xUnit.
