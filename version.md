# 1.1.2 2026-07-28 17:04
- Wysokosc okna dopasowuje sie do liczby projektow (`SizeToContent`) i jest ograniczona uzyteczna wysokoscia ekranu zamiast stalymi 720 px, wiec przewijanie listy wlacza sie dopiero po wypelnieniu monitora.
- Margines okna wzgledem obszaru roboczego zmniejszony z 48 px do 16 px.
- Zweryfikowano na obszarze roboczym 1707x912: 7 projektow daje okno 802 px bez przewijania, 30 projektow zatrzymuje sie na 896 px z przewijaniem, okno pozostaje wysrodkowane.

# 1.1.1 2026-07-28 16:58
- Sekcja `Odstawione` pokazuje pierwsza linie opisu projektu zamiast sciezki; sciezka pozostaje w dymku karty.
- Wiersz odstawionego projektu uzyty jest teraz jako trzykolumnowy grid, dzieki czemu dlugi opis przycina sie wielokropkiem zamiast dobijac do licznika uruchomien.
- `ProjectItem` udostepnia wyliczana wlasciwosc `DescriptionFirstLine` z powiadomieniem o zmianie opisu.
- `PublishDir` w pliku projektu kieruje `dotnet publish` do katalogu `dist`, na ktory wskazuje skrot `Projekty.lnk`.
- `.gitignore` obejmuje ogolne `bin/` i `obj/`, smieci Visual Studio oraz `.claude/settings.local.json`.

# 1.1.0 2026-07-28 00:00
- Zainicjalizowano historię wersji na podstawie wersji aplikacji WPF.
