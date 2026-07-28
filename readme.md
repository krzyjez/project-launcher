# Project Launcher

Prosty launcher projektow dla Windows. Otwiera male okno z lista wybranych repozytoriow i uruchamia klikniety projekt w Visual Studio Code.

Powstal jako praktyczna alternatywa dla menu Start i PowerToys Run, ktore przy kilku podobnych skrotach pokazuja za duzo wynikow albo slabo odrozniaja projekty.

## Pliki

1. `ProjectLauncher.Wpf` - aplikacja WPF z kartami projektow, drag/drop, odstawianiem rzadziej uzywanych projektow i edycja danych projektu.
2. `project-launcher.ps1` - starszy launcher PowerShell/Windows Forms, zostawiony jako fallback.
3. `%USERPROFILE%\ai-tools\launch-projects.json` - wspolny rejestr projektow uzywany przez launcher i narzedzia takie jak wizualizer sesji agentow.
4. `readme.md` - opis narzedzia.

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

1. Okno pokazuje kompaktowa liste projektow: numer, nazwe projektu oraz licznik uruchomien i date ostatniego uruchomienia.
2. Okno zajmuje cala uzyteczna wysokosc ekranu, wiec lista aktywnych projektow przewija sie dopiero wtedy, gdy karty nie mieszcza sie na monitorze.
3. Przelacznik `Details` pokazuje lub ukrywa sciezke, opis i preferowane imiona agentow dla projektu.
4. Kolor projektu jest widoczny jako akcent karty i pozostaje zapisany w `launch-projects.json`.
5. Klikniecie dowolnego miejsca na karcie projektu uruchamia Visual Studio Code z odpowiednim folderem w osobnym oknie.
6. Klawisze `1`-`9` uruchamiaja projekt o odpowiadajacym numerze na liscie.
7. Po udanym kliknieciu albo uzyciu numeru `lastLaunched` i `launchCount` w `launch-projects.json` sa aktualizowane automatycznie.
8. Menu kontekstowe karty pozwala edytowac nazwe, kolor, opis i imiona agentow.
9. Rzadziej uzywany projekt mozna odstawic. Odstawione projekty pozostaja widoczne na dole okna mala czcionka i mozna je przywrocic do aktywnych. Wiersz odstawionego projektu pokazuje nazwe i pierwsza linie opisu; sciezka pozostaje w dymku karty.
10. Przyciski sortowania w naglowku wybieraja kolejnosc po `launchCount`, dacie `lastLaunched` albo alfabetycznie po nazwie; domyslny tryb to data ostatniego uruchomienia.
11. Odstawiony projekt mozna przywrocic albo usunac calkowicie z rejestru.
12. Panel tagow po prawej stronie filtruje aktywne i odstawione projekty; przy kilku zaznaczonych tagach wystarczy dopasowanie dowolnego z nich.
13. Wybrane tagi sa filtrem tymczasowym i nie sa zapisywane po zamknieciu launchera.
14. W widoku skroconym karta projektu pokazuje jedna linie opisu; widok `Details` rozwija pelny opis wraz ze sciezka i imionami agentow.
15. Numer wersji aplikacji jest widoczny pod naglowkiem `Projekty`.
