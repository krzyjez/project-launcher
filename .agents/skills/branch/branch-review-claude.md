# Review w sesji Claude Code

## Wybór recenzenta

Główny agent używa narzędzia `Agent` z `subagent_type: "pr-review-toolkit:code-reviewer"` z oficjalnego pluginu Anthropic `pr-review-toolkit@claude-plugins-official`. Deleguje bezpośrednio z bieżącej sesji, bez uruchamiania dodatkowego CLI. Użytkownik zleca tylko `branch review`; agent wybiera tę ścieżkę sam.

Przed delegowaniem sprawdź, czy typ recenzenta jest dostępny w narzędziu `Agent`. Sama obecność plików pluginu nie potwierdza jego załadowania do sesji. Jeśli go brakuje, pozostaw review nieukończone i podaj konkretny brak: instalacja pluginu albo odświeżenie sesji po instalacji (`/reload-plugins`, jeśli dostępne). Nie instaluj pluginu ani nie zmieniaj ustawień użytkownika automatycznie. Nie zastępuj go po cichu agentem `general-purpose` ani osobnym procesem CLI.

Używaj definicji i modelu oficjalnego recenzenta. Nie uruchamiaj całej komendy `review-pr`, `code-simplifier` ani pluginu `code-review` opartego o GitHub PR. W tej ścieżce recenzent pracuje również z lokalnym repozytorium bez remote.

## Zlecenie dla subagenta

Przekaż samowystarczalne zlecenie bez pełnej historii implementacji:

1. Bezwzględna ścieżka repozytorium, cel workflow, wymagania użytkownika i kryteria akceptacji, także niezapisane w plikach.
2. Trzy SHA z CLI: `HeadCommit`, `BaseCommit`, `MergeBaseCommit`. Oceń cały `MergeBaseCommit..HeadCommit` oraz integrację z `BaseCommit`. Nie zastępuj tego domyślnym niezacommitowanym `git diff`. Baza może zawierać zmiany powstałe po odgałęzieniu.
3. Przeczytaj obowiązujące `CLAUDE.md` i `AGENTS.md`, również właściwe dla zmienianych podkatalogów. Odczytaj lokalny `init-session` projektu docelowego i wykonaj tylko zbieranie kontekstu, bez zapisów. Bez niego użyj README, `project-memory.md` i dokumentacji zmienianego obszaru. Nie zakładaj, że wszystkie te instrukcje zostały automatycznie wstrzyknięte.
4. Stosuj kryteria oficjalnego `code-reviewer` oraz jawne wymagania projektu. Nasz `review-code` nie zastępuje instrukcji pluginu. Oceniaj rzeczywiste konsekwencje; odróżniaj błąd działania od uwagi stylistycznej i nie zawyżaj wagi kosmetycznych problemów.
5. Tylko odczyt: nie poprawiaj kodu, nie zapisuj raportu, nie zmieniaj plików ani stanu Git, nie publikuj komentarzy. Nie deleguj dalej. W Windows używaj PowerShell. Wykonuj tylko sprawdzenia, które nie zmieniają repozytorium; zapisujące testy wymagają uzgodnionego środowiska izolowanego. Po odmowie dostępu nie szukaj obejścia przez inne narzędzia lub agentów; zgłoś ograniczenie.
6. Zwróć tekst według sekcji „Zwięzły raport review” z `branch-techdocs.md`; główny agent przekazuje ją w zleceniu. `incomplete` oznacza brak oceny istotnej części zakresu, nie tylko brak opcjonalnego narzędzia, którego wynik można wiarygodnie sprawdzić przez odczyt.

Zakaz zapisu jest instrukcją recenzenta, nie gwarancją sandboxa. Nie dodawaj globalnych lub projektowych zakazów zapisu blokujących zwykłą pracę agenta. Główny agent porównuje HEAD i stan roboczy przed oraz po ocenie. Wykryte nieoczekiwane zmiany raportuje i zatrzymuje rejestrację; nie cofa ich automatycznie.

## Odebranie wyniku

Poczekaj na zakończenie recenzenta i sprawdź kompletność zakresu oraz uzasadnienie werdyktu. `passed` i `issues` rejestruj według głównej instrukcji `branch review`, z `--reviewer pr-review-toolkit:code-reviewer`. `incomplete`, brak odpowiedzi i błąd delegowania pozostawiają review nieukończone: nie wywołuj `--complete` i nie zmieniaj wyniku na `passed`.

CLI zapisuje recenzenta w YAML. Nie dopisuj w treści raportu metody, identyfikatora instancji ani wersji pluginu. Nie zmieniaj przy okazji polityki aktualizacji pluginu.

Główny agent przekazuje raport z pliku poza repozytorium do CLI. Recenzent nie zapisuje `.workai/reviews` ani nie obsługuje PR. Obsługa raportu, GitHuba i `branch finish` jest wspólna z Codexem.
