# ProFak BigSebowskyMod

ProFak BigSebowskyMod to rozwijany fork programu [ProFak](https://github.com/lkosson/profak) autorstwa Łukasza Kossona.

Bazowy projekt pozostaje dziełem oryginalnego autora. Ten fork rozwija go dalej pod konkretne potrzeby praktyczne: faktury walutowe, rozszerzenia KSeF, wygodniejszą obsługę zakupów, dodatkowe wydruki i usprawnienia codziennej pracy.

Dziękuję Łukaszowi Kossonowi za stworzenie i udostępnienie ProFak jako solidnej bazy do dalszego rozwoju.

## Oryginalny projekt

- Repozytorium autora: [lkosson/profak](https://github.com/lkosson/profak)
- Oryginalne wydania: [github.com/lkosson/profak/releases/latest](https://github.com/lkosson/profak/releases/latest/)

## Wydania tej wersji

Aktualne wydania tego forka znajdują się tutaj:

- [github.com/BigSebowsky/ProFak_BigSebowskyMod/releases/latest](https://github.com/BigSebowsky/ProFak_BigSebowskyMod/releases/latest/)

## Co zawiera ta wersja

Poza możliwościami bazowego ProFak, ten fork zawiera m.in.:

- obsługę faktur w PLN i walutach obcych z automatycznym kursem NBP
- zapisywanie `DataKursu` na fakturze oraz prezentację kursu i daty kursu w edytorze, na listach i wydrukach
- podgląd i listy kwot w PLN dla dokumentów walutowych
- ekran `Kursy walut` oraz lokalną tabelę kursów NBP z uzupełnianiem braków
- wiele rachunków bankowych kontrahenta i firmy
- rozszerzone warianty wydruku faktury, w tym szablony PL/EN oraz EN
- Inbox KSeF dla zakupów:
  pobieranie dokumentów, oznaczanie nowych, statusy workflow, import do zakupu, zapis XML/PDF i synchronizacja w tle
- lepszy import danych z KSeF:
  kontrahenci KSeF, rachunki, płatności i sensowniejsze mapowanie sposobu płatności
- poprawki stabilności migracji SQLite i zgodności danych po aktualizacjach

## Możliwości bazowego ProFak

- wystawianie faktur z VAT i bez, VAT marża, korygujących i proforma
- prowadzenie rejestru faktur zakupu i kosztowych
- podpinanie i przechowywanie załączników do faktur
- rejestrowanie wpłat i przypomnienia o płatnościach
- generowanie JPK_VAT i JPK_FA
- obliczanie podatku dochodowego: liniowego, według skali i ryczałtu
- obliczanie składek zdrowotnych i społecznych
- prowadzenie księgi przychodów i rozchodów
- prowadzenie ewidencji przychodów
- integrację z białą listą rachunków VAT
- integrację z GUS
- integrację z KSeF
- wysyłkę faktur e-mailem
- własne wzory wydruku faktury
- interfejs programistyczny dla zewnętrznych narzędzi

## Poza zakresem projektu

- obsługa i rozliczanie magazynu
- obsługa kas i drukarek fiskalnych
- prowadzenie ksiąg handlowych
- ewidencja środków trwałych i amortyzacja
- obsługa kadr i umów cywilnoprawnych

## Instalacja

Aby zainstalować tę wersję programu, pobierz plik `ProFak.msi` albo paczkę `ProFak.zip` z wydań tego repozytorium:

- [Pobierz najnowsze wydanie BigSebowskyMod](https://github.com/BigSebowsky/ProFak_BigSebowskyMod/releases/latest/)

Jeśli chcesz korzystać z wersji oryginalnej autora, użyj zamiast tego:

- [Pobierz najnowsze wydanie oryginalnego ProFak](https://github.com/lkosson/profak/releases/latest/)

Podczas pierwszego uruchomienia programu zostanie wyświetlone okno, w którym można wybrać miejsce przechowywania bazy danych. Jeśli nie wiesz co wybrać, pozostaw ustawienie domyślne.

## Wymagania techniczne

- Windows 7 lub nowszy
- około 1 GB RAM
- około 200 MB miejsca na dysku
- SQLite jako plikowa baza danych

Program:

- nie wymaga instalacji do działania w wersji ZIP
- nie wymaga stałego dostępu do internetu
- nie ma limitu liczby wystawianych i przechowywanych dokumentów
- może być uruchamiany z udziału sieciowego

## Zrzuty ekranu

### Okno główne
![Główne okno programu](/Materiały/zrzut-faktury.png)

### Edycja faktury
![Okno edycji faktury](/Materiały/zrzut-faktura.png)

### Podgląd wydruku
![Podgląd wydruku faktury](/Materiały/zrzut-wydruk.png)

## Licencja

Licencja na korzystanie z programu jest bezpłatna i bezterminowa. W ramach licencji dozwolone jest powielanie i korzystanie z programu na dowolnej liczbie stanowisk. Program nie jest objęty gwarancją. Osoba korzystająca z programu ponosi pełną i wyłączną odpowiedzialność za dane przetwarzane przez program, w szczególności za poprawność wykonywanych obliczeń i ich zgodność z obowiązującymi przepisami.

Dozwolone jest dystrybuowanie programu w niezmienionej postaci oraz modyfikowanie programu na własny użytek.
