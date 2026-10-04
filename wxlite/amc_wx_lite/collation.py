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


def _batch_titles(titles: Sequence[str]) -> list[list[str]]:
    """Podziel tytuly na wsady, ktore zmieszcza sie w jednym wierszu protokolu.

    Dzielimy po BAJTACH, nie po liczbie pozycji: 2596 krotkich tytulow to
    ~100 KB, a kilkaset bardzo dlugich moze przekroczyc prog szybciej.
    """
    batches: list[list[str]] = []
    current: list[str] = []
    current_bytes = _ENVELOPE_OVERHEAD

    for title in titles:
        # json.dumps z domyslnymi ustawieniami rozdziela elementy przez ", "
        # -- DWA bajty, nie jeden. Policzone za nisko o bajt na tytul daje
        # przy 1300 tytulach 1,3 KB nadmiaru i wiersz ponad progiem.
        cost = len(json.dumps(title, ensure_ascii=False).encode("utf-8")) + 2
        if cost + _ENVELOPE_OVERHEAD > MAX_REQUEST_BYTES:
            raise HostCollationUnavailable(
                f"Pojedynczy tytul zajmuje {cost} B i nie zmiesci sie w wierszu "
                f"protokolu ({MAX_REQUEST_BYTES} B). Host odrzucilby to zadanie."
            )
        if current and current_bytes + cost > MAX_REQUEST_BYTES:
            batches.append(current)
            current = []
            current_bytes = _ENVELOPE_OVERHEAD
        current.append(title)
        current_bytes += cost

    if current:
        batches.append(current)
    return batches


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
    """Klucze sortowania AMC_PL policzone przez host C#, pamietane lokalnie."""

    def __init__(self, call: Callable[..., Any]) -> None:
        self._call = call
        self._keys: dict[str, bytes] = {}

    # ------------------------------------------------------------- budowanie

    @classmethod
    def from_payload(cls, payload: dict) -> "HostCollation":
        """Zbuduj z gotowego wsadu (kwit pomiaru albo zapamietana odpowiedz)."""

        def refuse(*_args, **_kwargs):
            raise HostCollationUnavailable("Ta kolacja nie ma zywego hosta.")

        collation = cls(refuse)
        titles = payload.get("titles")
        keys = payload.get("keys") or []
        if titles is None:
            raise HostCollationUnavailable("Wsad bez listy tytulow.")
        collation._absorb(titles, keys)
        return collation

    def _absorb(self, titles: Sequence[str], keys: Sequence[str]) -> None:
        if len(keys) != len(titles):
            raise HostCollationUnavailable(
                f"Host oddal {len(keys)} kluczy na {len(titles)} tytulow."
            )
        for title, encoded in zip(titles, keys):
            if not isinstance(encoded, str):
                raise HostCollationUnavailable(f"Klucz dla {title!r} nie jest napisem base64.")
            self._keys[title] = base64.b64decode(encoded)

    def load(self, titles: Iterable[str]) -> None:
        """Dociagnij klucze dla brakujacych tytulow.

        Wsad dzielimy na tyle zadan, ile trzeba, zeby ZADEN wiersz protokolu
        nie przekroczyl progu hosta. Nadal jest to sortowanie wsadowe: liczba
        wywolan rosnie z rozmiarem danych (2-3 na pelny poziom Biblioteki), a
        NIE z liczba porownywanych par.
        """
        wanted = list(dict.fromkeys(titles))
        missing = [t for t in wanted if t not in self._keys]
        if not missing:
            return
        if len(missing) > MAX_BATCH:
            raise HostCollationUnavailable(
                f"Wsad {len(missing)} tytulow przekracza prog {MAX_BATCH}."
            )
        for batch in _batch_titles(missing):
            response = self._call(COLLATION_OP, {"titles": batch}, timeout=60.0)
            if not isinstance(response, dict):
                raise HostCollationUnavailable("Host nie oddal obiektu z kluczami.")
            self._absorb(batch, response.get("keys") or [])

    # ------------------------------------------------------------ sortowanie

    def key_for(self, title: str) -> bytes | None:
        return self._keys.get(title)

    def sort(self, titles: Sequence[str]) -> list[str]:
        """Tytuly w kolejnosci C#. Doczyta brakujace klucze, jesli ma hosta."""
        if any(t not in self._keys for t in titles):
            self.load(titles)
        return [t for t, _ in sort_key_order((t, self._keys.get(t)) for t in titles)]

    def sort_rows(self, rows: Sequence[Any], *, key: Callable[[Any], str]) -> list[Any]:
        """To samo dla wierszy listy: ``key`` wyciaga tytul z wiersza."""
        titles = [key(row) for row in rows]
        if any(t not in self._keys for t in titles):
            self.load(titles)
        return [
            row
            for row, _ in sort_key_order((row, self._keys.get(key(row))) for row in rows)
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
