# Project Launcher

Prosty launcher projektow dla Windows. Otwiera male okno z lista wybranych repozytoriow i uruchamia klikniety projekt w Visual Studio Code.

Powstal jako praktyczna alternatywa dla menu Start i PowerToys Run, ktore przy kilku podobnych skrotach pokazuja za duzo wynikow albo slabo odrozniaja projekty.

## Pliki

1. `ProjectLauncher.Wpf` - glowna aplikacja WPF: karty projektow w dwoch kolumnach, stan Git, worktree, odstawianie projektow, edycja danych projektu i ikona w zasobniku. Publikowana do `dist`.
2. `ProjectLauncher.Core` - biblioteka na czystym `net9.0` z cala logika: model projektu, obsluga rejestru i ustawien, uruchamianie edytora, odczyt stanu Git, tlumaczenie sciezek Windows/Linux.
3. `ProjectLauncher.Core.Tests` - testy xUnit biblioteki `Core`.
4. `ProjectLauncher.AvaloniaUi` - niedokonczony port na Avalonie z mysla o Linuksie, publikowany do `dist-avalonia`. Korzysta z tego samego `launch-projects.json` co wersja WPF i zapisuje do niego `lastLaunched`, `launchCount` oraz `shelved`.
5. `project-launcher.ps1` - starszy launcher PowerShell/Windows Forms, zostawiony jako fallback.
6. `%USERPROFILE%\ai-tools\launch-projects.json` - wspolny rejestr projektow uzywany przez launcher i narzedzia takie jak wizualizer sesji agentow.
7. `readme.md` - opis narzedzia.

## Wspolny rejestr projektow

`launch-projects.json` jest wspolnym rejestrem repozytoriow. `Project Launcher` uzywa go do wyswietlania i uruchamiania projektow, a inne narzedzia moga uzywac tych samych danych do identyfikacji projektu po sciezce repozytorium, kolorze i preferowanej puli imion agentow.

Plik znajduje sie tutaj:

```text
%USERPROFILE%\ai-tools\launch-projects.json
```

Do testow i diagnostyki katalog `%USERPROFILE%\ai-tools` moze zostac zastapiony zmienna `AI_TOOLS_HOME`.

Jesli `launch-projects.json` jeszcze nie istnieje, launcher tworzy katalog `%USERPROFILE%\ai-tools`. Rejestr projektow jest jedynym aktywnym zrodlem danych i nie jest nadpisywany automatycznie.

Format pliku to tablica obiektow JSON. Kazdy obiekt opisuje jeden projekt/repozytorium.

Narzedzia odczytujace ten plik powinny traktowac `path` jako glowny identyfikator repozytorium. Porownanie sciezek powinno byc odporne na roznice wielkosci liter i normalizacje separatorow Windows.

## Format projektu

Kazdy projekt w `launch-projects.json` ma pola:

1. `order` - liczba calkowita okreslajaca reczna kolejnosc projektu.
2. `name` - nazwa projektu widoczna w UI.
3. `path` - bezwzgledna sciezka Windows do repozytorium lub katalogu projektu.
4. `description` - krotki opis projektu; pusty string oznacza brak opisu.
5. `color` - kolor projektu w formacie `#RRGGBB`, uzywany jako wspolny identyfikator wizualny projektu.
6. `agentNames` - tablica preferowanych imion agentow dla danego repozytorium, w kolejnosci przydzielania.
7. `lastLaunched` - data ostatniego uruchomienia z launchera w formacie `yyyy-MM-dd`; pusty string oznacza brak uruchomien.
8. `launchCount` - licznik uruchomien projektu z launchera.
9. `shelved` - flaga projektu odstawionego; projekt pozostaje w rejestrze, ale w launcherze trafia do kompaktowej sekcji `Odstawione`.
10. `tags` - lista tagow projektu, uzywana przez panel filtrowania w launcherze.

Pola `order`, `name`, `path`, `description`, `color`, `agentNames`, `lastLaunched`, `launchCount`, `shelved` i `tags` sa aktualnym kontraktem danych.

Pole `hidden` jest starsza nazwa pola `shelved`. Launcher potrafi je odczytac i przy zapisie zamienia na `shelved`. Nowe narzedzia powinny uzywac `shelved`.

Znaczenie `shelved` dla innych narzedzi:

1. `shelved: false` oznacza projekt aktywny, ktory powinien byc pokazywany w glownym widoku.
2. `shelved: true` oznacza projekt odstawiony, ktory nadal istnieje w rejestrze i moze byc rozpoznawany po `path`, `color` i `agentNames`.
3. Narzedzie moze pominac odstawiony projekt w glownym UI, ale nie powinno traktowac go jako usunietego.

Przyklad:

```json
{
  "order": 1,
  "name": "projekt ai",
  "path": "P:\\ai",
  "description": "Repozytorium narzedzi, reguly pracy agentow i lokalne skrypty.",
  "color": "#2FBF8F",
  "agentNames": ["Mila", "Vivaldi", "Rachmaninow"],
  "lastLaunched": "2026-04-28",
  "launchCount": 12,
  "shelved": false,
  "tags": ["AI", "narzedzia"]
}
```

## Uruchamianie

Launcher dziala w tle z ikona rakiety w zasobniku systemowym. Klikniecie ikony pokazuje okno. Menu ikony zawiera `Pokaz projekty`, `Uruchamiaj przy starcie Windows` oraz `Zakoncz`; tylko `Zakoncz` naprawde zamyka program.

Dziala tylko jedna instancja launchera. Kolejne uruchomienie exe, na przyklad ze skrotu, nie otwiera drugiej kopii, tylko pokazuje okno dzialajacej instancji.

Opcja `Uruchamiaj przy starcie Windows` dodaje wpis `ProjectLauncher` w `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` ze sciezka aktualnie uruchomionego exe i argumentem `--tray`, ktory startuje program schowany w zasobniku.

Launcher jest podpiety pod skrot `Projekty.lnk`, utworzony na pulpicie i w menu Start.

Domyslny hotkey skrotu to:

```text
Ctrl + Alt + P
```

Mozna go tez uruchomic przez PowerToys Run:

```text
Alt + Spacja -> Projekty -> Enter
```

## Zachowanie

1. Karty aktywnych projektow ukladaja sie wierszami w dwie kolumny (1 i 2 w pierwszym rzedzie, 3 i 4 w drugim). Karta pokazuje numer, nazwe, pelny opis oraz licznik uruchomien i date ostatniego uruchomienia. Kolumny sa niezalezne: wysokosc karty nie zalezy od karty obok.
2. Wysokosc okna dopasowuje sie do liczby rzedow kart, ale nie przekracza uzytecznej wysokosci ekranu; lista przewija sie dopiero wtedy, gdy karty nie mieszcza sie na monitorze. Limit jest liczony dla monitora, na ktorym stoi okno, i przelicza sie automatycznie po przeniesieniu okna na inny monitor.
3. Karta repozytorium Git pokazuje biezaca galaz i flage stanu:
   - pomaranczowa `brudne` - niezacommitowane albo nowe pliki;
   - zielona `czyste` - galaz jest taka sama jak na GitHubie;
   - zolta `czyste ↑N` - N lokalnych commitow niewypchnietych na GitHuba; zolte sa tez `brak na GitHubie`, `nowsze na GitHubie`, `bez GitHuba` i `GitHub niedostepny`.
4. Stan lokalny pojawia sie od razu, a porownanie z GitHubem chwile pozniej. Launcher uzywa `git ls-remote`, ktory niczego nie zapisuje w repozytorium, a `git status` uruchamia bez blokady indeksu, zeby nie przeszkadzac agentom pracujacym w repozytoriach. Katalog, ktory nie jest repozytorium Git, nie ma flagi.
5. Jesli repozytorium ma dodatkowe worktree, karta pokazuje kazdy z nich jako osobny wiersz z galezia i flaga `czyste`/`brudne` (worktree nie trafia na GitHuba, wiec nie ma porownania z remote). Dymek nad nazwa galezi worktree pokazuje opis zadania z `.workai\branch-state.json` programu `branch`, a bez niego temat ostatniego commita. Klikniecie wiersza otwiera Visual Studio Code w katalogu worktree; liczy sie to jako uruchomienie projektu i ustawia kolor projektu w VS Code.
6. Jesli biezaca galaz projektu ma aktywne zadanie programu `branch`, jego opis pokazuje dymek nad nazwa galezi na karcie.
7. Strzalka w prawym gornym rogu karty rozwija sciezke i preferowane imiona agentow tylko tego projektu. Rozwiniecie nie jest zapisywane.
8. Kolor projektu jest widoczny jako akcent karty i pozostaje zapisany w `launch-projects.json`.
9. Klikniecie dowolnego miejsca na karcie projektu uruchamia Visual Studio Code z odpowiednim folderem w osobnym oknie i chowa launcher do zasobnika.
10. Klawisze `1`-`9` uruchamiaja aktywny projekt o odpowiadajacym numerze.
11. Po udanym kliknieciu albo uzyciu numeru `lastLaunched` i `launchCount` w `launch-projects.json` sa aktualizowane automatycznie.
12. Menu kontekstowe karty pozwala edytowac nazwe, kolor, opis, imiona agentow i tagi.
13. Rzadziej uzywany projekt mozna odstawic. Przycisk `Odstawione (N)` pod tagami przelacza liste na odstawione projekty, pokazane w tym samym ukladzie kart z pelnym opisem i bez numerow. `Esc` albo ponowne klikniecie wraca do aktywnych.
14. Odstawiony projekt mozna uruchomic, przywrocic do aktywnych albo usunac calkowicie z rejestru.
15. Przyciski sortowania w naglowku wybieraja kolejnosc po `launchCount`, dacie `lastLaunched` albo alfabetycznie po nazwie; domyslny tryb to data ostatniego uruchomienia.
16. Panel tagow po prawej stronie filtruje aktywne i odstawione projekty; przy kilku zaznaczonych tagach wystarczy dopasowanie dowolnego z nich.
17. Wybrane tagi sa filtrem tymczasowym i nie sa zapisywane.
18. `Esc`, przycisk `×` i `Alt+F4` chowaja okno do zasobnika. Przy kazdym pokazaniu okna launcher wczytuje rejestr od nowa i odswieza stan Git.
19. Gdy okno jest widoczne, launcher co minute odswieza stan lokalny repozytoriow i worktree, a stan GitHuba co 5 minut. Rejestr wczytuje ponownie tylko wtedy, gdy plik zmienilo inne narzedzie; rozwiniete karty i widok odstawionych zostaja bez zmian. Schowany w zasobniku launcher niczego nie odpytuje.
20. Numer wersji aplikacji jest widoczny pod naglowkiem `Projekty`.
21. Okno nie ma belki tytulu, ale mozna je przeciagac chwytajac dowolne puste miejsce: naglowek, marginesy, tlo listy albo tlo panelu tagow. Przyciski, suwaki i karty projektow nie przenosza okna.
