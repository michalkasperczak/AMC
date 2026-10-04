"""Kolejnosc listy wzieta z ORYGINALNEJ logiki C#, nie z wlasnego collatora.

Problem
-------
Schemat AMC deklaruje ``COLLATE AMC_PL`` na ``local_items.title``, a host C#
rejestruje te kolacje jako::

    CultureInfo.GetCultureInfo("pl-PL").CompareInfo
        .Compare(a, b, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace)

bez zadnego tie-breaku (``LocalLibraryDatabase.cs``). Reczny port tej reguly na
Pythona (``library_db.polish_collation``) zostal ZMIERZONY i jest niezgodny:
na pelnym korpusie 11 203 tytulow z profilu rozjechal sie z C# na 10 522
pozycjach (93,9%). Rozne klasy bledow: remis ``A``/``a`` (Python dokleja
wlasny tie-break, C# zwraca 0), diakrytyki (``zebra``/``Żaba``), wazenie
interpunkcji (``_test``/``-test``). Python nie ma w standardowej bibliotece
ICU, wiec nie da sie tego "dopisac" uczciwie.

Decyzja
-------
Nie budujemy wlasnego collatora Unicode i nie przepinamy indeksu SQL ``AMC_PL``
na niezgodna funkcje. Pytamy o kolejnosc ten sam ``CompareInfo``, ktorego uzywa
host.

Dlaczego KLUCZE, a nie wywolanie na pare
----------------------------------------
SPROSTOWANIE wczesniejszego komentarza: pisalo tu, ze 11 203 tytuly to
"~125 mln par". To bylo bledne uzasadnienie. 125 mln to liczba WSZYSTKICH par
(N*(N-1)/2), a sortowanie ich nie potrzebuje -- porownan jest rzedu N log N,
czyli okolo 150 tysiecy. Prawdziwy powod jest inny i nadal mocny:

* 150 tysiecy OSOBNYCH przejsc tam i z powrotem przez potok IPC to 150 tysiecy
  opoznien round-trip. Przy nawet 0,2 ms na runde to kilkadziesiat sekund na
  jedno sortowanie -- i powtarza sie przy KAZDYM przesortowaniu kolumny.
* Comparator wolajacy IPC musialby byc synchroniczny wewnatrz ``list.sort``,
  wiec blokowalby watek GUI na caly ten czas.

Dlatego host liczy ``CompareInfo.GetSortKey(title, options).KeyData`` raz na
tytul -- 11 203 wywolania, nie 150 tysiecy rund -- w wywolaniach WSADOWYCH, a
Python porownuje gotowe bajty lokalnie. Kazde nastepne przesortowanie jest juz
czysto lokalne i nie pyta hosta w ogole.

Wsady (chunki): zadanie o klucze dzielimy na porcje po <= 64 KiB JSON-a, bo
jedna linia protokolu z 11 203 tytulami przekraczalaby bufor linii. Chunk to
tylko podzial transportu -- wynik jest identyczny jak przy jednym zadaniu.

To jest poprawne tylko wtedy, gdy klucze odtwarzaja kolejnosc ``Compare``.
Zmierzone na pelnym korpusie (``resume-after422/sortkey-contract-*.json``):

=============================  ==================  ==================
                               Linux               Windows
=============================  ==================  ==================
pozycje rozne niz ``Compare``  0 z 11 203          0 z 11 203
zerwane remisy                 0                   0
kolejnosc                      identyczna z Windows (ten sam SHA-256)
=============================  ==================  ==================

Remis zostaje remisem: przy rownych kluczach zachowujemy kolejnosc wejsciowa
(``sorted`` jest stabilne), bo C# AMC_PL tie-breaku nie ma.

Granice, ktore nazywam wprost
-----------------------------
* Bez hosta nie ma zgodnej kolejnosci. ``HostCollationUnavailable`` jest
  swiadomie bledem, a nie cichym powrotem do ``polish_collation`` -- cicha
  zla kolejnosc jest gorsza od widocznej odmowy. Wolajacy decyduje, czy
  pokazac komunikat, czy uzyc kolejnosci zastepczej Z ETYKIETA.
* ``ORDER BY title COLLATE AMC_PL`` w SQL nadal uzywa ``polish_collation``
  (SQLite musi dostac jakakolwiek funkcje o tej nazwie, inaczej zapytanie nie
  wykona sie wcale). Dlatego kolejnosc PREZENTOWANA ustalamy tutaj, po
  odczycie, i tylko ona jest zgodna z C#.
"""

from __future__ import annotations

import base64
import json
from typing import Any, Callable, Iterable, Sequence

#: Operacja protokolu hosta (JSON-lines). Jedno wywolanie na caly wsad.
COLLATION_OP = "library.collationKeys"

#: Tryb kluczy: kolacja ``AMC_PL`` ze schematu SQL.
#:
#: ``CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace``
#: (``LocalLibraryDatabase.cs``). Uzywaja go Foldery i indeks ``AMC_PL`` i
#: MUSI zostac bez zmian -- ``IgnoreNonSpace`` jest tam zamierzone.
COLLATION_AMC_PL = "AMC_PL"

#: Tryb kluczy: tytul w widokach "Wszystkie pliki" i alfabetycznych Ulubionych.
#:
#: C# sortuje je ``StringComparer.CurrentCultureIgnoreCase``, czyli
#: ``CompareOptions.IgnoreCase`` SAMO -- bez ``IgnoreNonSpace``. Roznica jest
#: widoczna: ``AMC_PL`` zrownuje ``"e"`` z ``"é"``, a widok NIE (zmierzone,
#: ``collection-sort-after422/probe-order-modes``).
COLLATION_TITLE_IGNORE_CASE = "TITLE_IGNORE_CASE"

#: Tryb kluczy: tie-break po ``Source`` (dla pozycji lokalnej: sciezka pliku).
#:
#: C#: ``StringComparer.OrdinalIgnoreCase``. To NIE jest kolacja jezykowa --
#: host oddaje napis przepuszczony przez ``Rune.ToUpperInvariant`` SKALAR PO
#: SKALARZE, zakodowany w UTF-8. Dwie pulapki, obie zmierzone:
#:
#: * ``str.upper()`` w Pythonie rozwija ``"ß"`` do ``"SS"`` (pelne mapowanie
#:   jezykowe). ``OrdinalIgnoreCase`` takiego rozwiniecia NIE zna, wiec
#:   ``"C:\\ss"`` idzie PRZED ``"C:\\ß"`` -- a po ``str.upper()`` oba klucze
#:   sa rowne i tie-break znika.
#: * ``ToUpperInvariant`` na calym napisie w .NET tez nie wystarcza: mierzone
#:   warianty UTF-16 rozjechaly sie z oryginalem na 4 parach z 4 (pary
#:   zastepcze), bo ``OrdinalIgnoreCase`` porzadkuje SKALARY, a nie jednostki
#:   kodowe UTF-16. UTF-8 zachowuje porzadek punktow kodowych, wiec porownanie
#:   bajtow w Pythonie odtwarza go dokladnie: 0 niezgodnych par z oryginalnym
#:   ``StringComparer.OrdinalIgnoreCase``.
COLLATION_ORDINAL_IGNORE_CASE = "ORDINAL_IGNORE_CASE"

#: Tryby, ktore host potrafi policzyc. Nieznany tryb to blad, nie cichy AMC_PL.
COLLATION_MODES = (
    COLLATION_AMC_PL,
    COLLATION_TITLE_IGNORE_CASE,
    COLLATION_ORDINAL_IGNORE_CASE,
)

#: Gorny prog jednego wsadu w SZTUKACH. Zabezpieczenie zdrowego rozsadku.
MAX_BATCH = 50_000

#: Gorny prog jednego ZADANIA w bajtach wiersza protokolu.
#:
#: Host celowo odrzuca wiersze dluzsze niz ``MaximumLineLength = 64 * 1024``
#: (``Protocol/LiteRequest.cs``) i odpowiada bledem ``line_too_long``. Ta
#: odpowiedz NIE ma pola ``id``, wiec klient nie dopasuje jej do swojego
#: zadania i doczeka do timeoutu -- zmierzone na zywym hoscie: 65 505 B
#: przechodzi, 65 557 B wiesza wywolanie na caly timeout.
#:
#: Zostawiamy zapas na koperte zadania (``id``, ``op``) i na to, ze host
#: liczy ZNAKI UTF-16, a my bajty UTF-8.
MAX_REQUEST_BYTES = 56 * 1024

#: Sam szkielet zadania bez tytulow -- tyle miejsca zabiera koperta.
_ENVELOPE_OVERHEAD = 160


class HostCollationUnavailable(RuntimeError):
    """Nie da sie uzyskac kolejnosci zgodnej z C#.

    Swiadomie wyjatek, nie wartosc zastepcza: milczace sortowanie niezgodnym
    collatorem wyglada jak dzialajaca funkcja i dlatego jest grozniejsze.
    """


def _batch_values(values: Sequence[str]) -> list[list[str]]:
    """Podziel napisy na wsady, ktore zmieszcza sie w jednym wierszu protokolu.

    Dzielimy po BAJTACH, nie po liczbie pozycji: 2596 krotkich tytulow to
    ~100 KB, a kilkaset bardzo dlugich moze przekroczyc prog szybciej.

    Dotyczy tak samo tytulow jak sciezek -- sciezki potrafia byc DLUZSZE, wiec
    ten sam limit obowiazuje bez wyjatku.
    """
    batches: list[list[str]] = []
    current: list[str] = []
    current_bytes = _ENVELOPE_OVERHEAD

    for value in values:
        # json.dumps z domyslnymi ustawieniami rozdziela elementy przez ", "
        # -- DWA bajty, nie jeden. Policzone za nisko o bajt na tytul daje
        # przy 1300 tytulach 1,3 KB nadmiaru i wiersz ponad progiem.
        cost = len(json.dumps(value, ensure_ascii=False).encode("utf-8")) + 2
        if cost + _ENVELOPE_OVERHEAD > MAX_REQUEST_BYTES:
            raise HostCollationUnavailable(
                f"Pojedynczy napis zajmuje {cost} B i nie zmiesci sie w wierszu "
                f"protokolu ({MAX_REQUEST_BYTES} B). Host odrzucilby to zadanie."
            )
        if current and current_bytes + cost > MAX_REQUEST_BYTES:
            batches.append(current)
            current = []
            current_bytes = _ENVELOPE_OVERHEAD
        current.append(value)
        current_bytes += cost

    if current:
        batches.append(current)
    return batches


#: Dawna nazwa, zostaje dla czytelnosci starszych kwitow i testow.
_batch_titles = _batch_values


def sort_key_order(
    rows: Iterable[tuple[Any, bytes | None]],
) -> list[tuple[Any, bytes]]:
    """Ulozy ``(obiekt, klucz)`` po BAJTACH klucza, stabilnie.

    Brak klucza dla choc jednego elementu to blad -- inaczej czesc listy
    poszlaby w kolejnosci zgodnej z C#, a czesc nie, i nikt by tego nie zauwazyl.
    """
    materialized = list(rows)
    missing = [item for item, key in materialized if key is None]
    if missing:
        raise HostCollationUnavailable(
            f"Brak klucza sortowania dla {len(missing)} pozycji "
            f"(pierwsza: {missing[0]!r})."
        )
    # sorted() jest stabilne, wiec rowne klucze zachowuja kolejnosc wejsciowa.
    return sorted(materialized, key=lambda pair: pair[1])  # type: ignore[arg-type,return-value]


class HostCollation:
    """Klucze sortowania policzone przez host C#, pamietane lokalnie.

    Jeden obiekt obsluguje KILKA trybow (``COLLATION_MODES``), bo jeden widok
    potrzebuje dwoch naraz: tytul po ``IgnoreCase`` i sciezke po
    ``OrdinalIgnoreCase``. Cache jest rozdzielony PER TRYB -- te same bajty
    wejsciowe daja w roznych trybach rozne klucze, wiec wspolny slownik
    mieszalby ``"e"`` z ``AMC_PL`` i ``"e"`` z ``IgnoreCase``.

    API dla wolajacego sie NIE zmienia: ``HostCollation(client.call)``, a
    ``load``/``key_for`` bez ``mode`` dzialaja jak dotad (``AMC_PL``).
    """

    def __init__(self, call: Callable[..., Any]) -> None:
        self._call = call
        #: tryb -> {napis: klucz}. Nigdy jeden wspolny slownik.
        self._keys: dict[str, dict[str, bytes]] = {m: {} for m in COLLATION_MODES}

    # ------------------------------------------------------------- budowanie

    def _cache(self, mode: str) -> dict[str, bytes]:
        if mode not in self._keys:
            raise HostCollationUnavailable(
                f"Nieznany tryb kolacji {mode!r}. Znane: {', '.join(COLLATION_MODES)}."
            )
        return self._keys[mode]

    @classmethod
    def from_payload(cls, payload: dict) -> "HostCollation":
        """Zbuduj z gotowego wsadu (kwit pomiaru albo zapamietana odpowiedz).

        Wsad bez ``mode`` traktujemy jako ``AMC_PL`` -- tak wygladaja wszystkie
        starsze kwity i nie chcemy ich przepisywac.
        """

        def refuse(*_args, **_kwargs):
            raise HostCollationUnavailable("Ta kolacja nie ma zywego hosta.")

        collation = cls(refuse)
        titles = payload.get("titles")
        keys = payload.get("keys") or []
        if titles is None:
            raise HostCollationUnavailable("Wsad bez listy tytulow.")
        collation._absorb(titles, keys, payload.get("mode") or COLLATION_AMC_PL)
        return collation

    def _absorb(self, values: Sequence[str], keys: Sequence[str], mode: str) -> None:
        if len(keys) != len(values):
            raise HostCollationUnavailable(
                f"Host oddal {len(keys)} kluczy na {len(values)} napisow."
            )
        cache = self._cache(mode)
        for value, encoded in zip(values, keys):
            if not isinstance(encoded, str):
                raise HostCollationUnavailable(f"Klucz dla {value!r} nie jest napisem base64.")
            cache[value] = base64.b64decode(encoded)

    def load(self, values: Iterable[str], *, mode: str = COLLATION_AMC_PL) -> None:
        """Dociagnij klucze dla brakujacych napisow w danym trybie.

        Wsad dzielimy na tyle zadan, ile trzeba, zeby ZADEN wiersz protokolu
        nie przekroczyl progu hosta. Nadal jest to sortowanie wsadowe: liczba
        wywolan rosnie z rozmiarem danych (2-3 na pelny poziom Biblioteki), a
        NIE z liczba porownywanych par.

        Tryb jedzie w zadaniu jako ``mode``. Host, ktory tego pola nie zna,
        policzy ``AMC_PL`` i oddalby ZLE klucze dla tytulu -- dlatego
        sprawdzamy ``collation`` w odpowiedzi i odmawiamy, zamiast cicho
        posortowac niezgodnie.
        """
        cache = self._cache(mode)
        wanted = list(dict.fromkeys(values))
        missing = [v for v in wanted if v not in cache]
        if not missing:
            return
        if len(missing) > MAX_BATCH:
            raise HostCollationUnavailable(
                f"Wsad {len(missing)} napisow przekracza prog {MAX_BATCH}."
            )
        for batch in _batch_values(missing):
            response = self._call(
                COLLATION_OP, {"titles": batch, "mode": mode}, timeout=60.0
            )
            if not isinstance(response, dict):
                raise HostCollationUnavailable("Host nie oddal obiektu z kluczami.")
            # Stary host nie zna ``mode`` i ODPOWIE etykieta "AMC_PL" nawet na
            # prosbe o inny tryb. Bez tej kontroli dostalibysmy klucze z
            # IgnoreNonSpace pod nazwa IgnoreCase -- czyli dokladnie ten blad,
            # ktory naprawiamy, tylko trudniejszy do zauwazenia.
            declared = response.get("collation")
            if declared is not None and declared != mode:
                raise HostCollationUnavailable(
                    f"Poprosilem o tryb {mode!r}, host oddal {declared!r}. "
                    "Przebuduj LiteHost -- stara wersja nie zna pola \"mode\"."
                )
            self._absorb(batch, response.get("keys") or [], mode)

    # ------------------------------------------------------------ sortowanie

    def key_for(self, value: str, *, mode: str = COLLATION_AMC_PL) -> bytes | None:
        return self._cache(mode).get(value)

    def sort(self, titles: Sequence[str], *, mode: str = COLLATION_AMC_PL) -> list[str]:
        """Napisy w kolejnosci C#. Doczyta brakujace klucze, jesli ma hosta."""
        cache = self._cache(mode)
        if any(t not in cache for t in titles):
            self.load(titles, mode=mode)
        return [t for t, _ in sort_key_order((t, cache.get(t)) for t in titles)]

    def sort_rows(
        self,
        rows: Sequence[Any],
        *,
        key: Callable[[Any], str],
        mode: str = COLLATION_AMC_PL,
    ) -> list[Any]:
        """To samo dla wierszy listy: ``key`` wyciaga napis z wiersza."""
        cache = self._cache(mode)
        titles = [key(row) for row in rows]
        if any(t not in cache for t in titles):
            self.load(titles, mode=mode)
        return [
            row
            for row, _ in sort_key_order((row, cache.get(key(row))) for row in rows)
        ]


def order_library_rows(rows: Sequence[Any], collation: "HostCollation | None") -> list[Any]:
    """Uloz wiersze jednego poziomu Biblioteki w kolejnosci C#.

    Grupy zostaja nietkniete, bo tak uklada je pelne AMC: najpierw wiersz
    ``..``, potem foldery, potem utwory. Sortujemy WEWNATRZ grup -- gdybysmy
    sortowali calosc jednym ciagiem, utwor "Aaa" wskoczylby miedzy foldery.

    ``collation=None`` (brak hosta) zwraca wejscie BEZ ZMIAN, zamiast udawac
    zgodnosc niezgodnym collatorem. Wolajacy wie wtedy, ze kolejnosc jest
    zastepcza, i moze to powiedziec uzytkownikowi.
    """
    if collation is None:
        return list(rows)

    head = [r for r in rows if getattr(r, "kind", "") == "parent"]
    folders = [r for r in rows if getattr(r, "kind", "") == "folder"]
    rest = [r for r in rows if getattr(r, "kind", "") not in ("parent", "folder")]

    title_of: Callable[[Any], str] = lambda row: getattr(row, "title", "")
    return (
        head
        + collation.sort_rows(folders, key=title_of)
        + collation.sort_rows(rest, key=title_of)
    )
