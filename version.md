# 1.1.2 2026-07-28 17:04
- Okno startuje na calej uzytecznej wysokosci ekranu zamiast na stalych 720 px, wiec przewijanie listy projektow wlacza sie dopiero po wypelnieniu monitora.
- Margines okna wzgledem obszaru roboczego zmniejszony z 48 px do 16 px.

# 1.1.1 2026-07-28 16:58
- Sekcja `Odstawione` pokazuje pierwsza linie opisu projektu zamiast sciezki; sciezka pozostaje w dymku karty.
- Wiersz odstawionego projektu uzyty jest teraz jako trzykolumnowy grid, dzieki czemu dlugi opis przycina sie wielokropkiem zamiast dobijac do licznika uruchomien.
- `ProjectItem` udostepnia wyliczana wlasciwosc `DescriptionFirstLine` z powiadomieniem o zmianie opisu.
- `PublishDir` w pliku projektu kieruje `dotnet publish` do katalogu `dist`, na ktory wskazuje skrot `Projekty.lnk`.
- `.gitignore` obejmuje ogolne `bin/` i `obj/`, smieci Visual Studio oraz `.claude/settings.local.json`.

# 1.1.0 2026-07-28 00:00
- Zainicjalizowano historię wersji na podstawie wersji aplikacji WPF.
