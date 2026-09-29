# Todo

# 2. [todo] Przejście innych narzędzi na pole `status` w rejestrze

a) Wizualizer sesji (`P:\ai\tools\agent-session-visualizer`, `ProjectRegistryItem.cs`) i `project-audit-report` (`ProjectRegistryReader.cs`) czytają jeszcze `shelved`; skill `project-launcher` w `P:\ai` przy dodawaniu projektu wpisuje `shelved: false`. Przełączyć je na `status` (`active`/`shelved`/`closed`, brak = `active`).
b) Po ich aktualizacji przestać zapisywać `shelved` w launcherze (`ProjectItem.Shelved`) i zaktualizować kontrakt w `readme.md`.

# 1. [todo] Uruchomienie launchera pod Linux Mint (Cinnamon)

a) Przed pierwszym startem dual boota wyłączyć w Windowsie szybkie uruchamianie. Bez tego Mint montuje wspólne dyski NTFS tylko do odczytu albo odmawia montowania. Instrukcja do podania, gdy nowy dysk pod Linuksa będzie gotowy.
b) Ustalić stałe punkty montowania dysków we `/etc/fstab`, identyfikując partycje po UUID. Bez tego ścieżki projektów mogą się zmieniać między uruchomieniami.
c) Katalog `ai-tools` przenieść na wspólny dysk P i wskazać go z obu systemów, żeby rejestr projektów i pozostałe narzędzia korzystały z jednego zestawu danych.
d) Tłumaczenie ścieżek Windows/Linux dla pola `path` w rejestrze. Osobna decyzja przed implementacją: konwencja punktów montowania kontra tablica przypisań w ustawieniach.
e) Projekty na dyskach niedostępnych pod Linuksem, na przykład katalog na dysku Google, wymagają decyzji, co launcher ma z nimi robić.
f) Skrót uruchamiający: plik `.desktop` plus własny skrót klawiszowy w Cinnamonie. Sprawdzić, czy `Ctrl+Alt+P` nie jest zajęty.
g) `EditorLauncher` szuka tylko `Code.exe`, `code.cmd` i `code.exe`; pod Linuksem trzeba szukać w PATH programu `code`.
h) Avalonia po cichu nic nie robi, gdy nie znajdzie edytora albo katalogu projektu (`_OpenProject`); WPF pokazuje wtedy komunikat. Dodać komunikat, razem z obsługą błędów przy wczytaniu i zapisie rejestru.
