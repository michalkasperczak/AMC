"""Pomiar CZASU przyrostowej aktualizacji na skali pelnej bazy uzytkownika.

Nie jest to suita profilujaca i nie dowodzi "przewagi nad czymkolwiek". Mierzy
jedna rzecz, ktora trzeba bylo zmierzyc, zeby nie szacowac: ile trwa wyliczenie
planu i ile operacji na kontrolce z niego wychodzi, przy liczbach wierszy
WYLICZONYCH z pelnej kopii profilu (nie zakodowanych na sztywno).

Uruchomienie:
    python3 tools/measure_list_sync.py <liczba_wierszy_glownej_listy> [...]
"""

from __future__ import annotations

import json
import sys
import time
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

from amc_wx_lite.list_model import ListModel, Row  # noqa: E402
from amc_wx_lite import list_sync  # noqa: E402


def rows(count: int, *, offset: int = 0) -> list[Row]:
    return [
        Row(
            item_id=str(i),
            title=f"Utwor numer {i} z kolekcji",
            kind="track",
            path=f"C:\\Muzyka\\utwor-{i}.mp3",
            detail=f"{3 + i % 4}:{i % 60:02d}",
        )
        for i in range(offset, offset + count)
    ]


def timed(fn, repeats: int = 5) -> tuple[float, object]:
    """Najlepszy z kilku przebiegow -- mediana szumu planisty nas nie interesuje."""
    best = float("inf")
    result = None
    for _ in range(repeats):
        start = time.perf_counter()
        result = fn()
        best = min(best, time.perf_counter() - start)
    return best * 1000.0, result


def scenario(name: str, model: ListModel, before: list[Row], after: list[Row]) -> dict:
    model.replace(before)
    shown = list_sync.model_row_texts(model)
    model.replace(after)
    desired = list_sync.model_row_texts(model)
    ms, ops = timed(lambda: list_sync.plan_row_updates(shown, desired))
    kinds: dict[str, int] = {}
    for op in ops:
        kinds[type(op).__name__] = kinds.get(type(op).__name__, 0) + 1
    return {
        "scenariusz": name,
        "wierszy_przed": len(before),
        "wierszy_po": len(after),
        "plan_ms": round(ms, 3),
        "operacji": len(ops),
        "rodzaje": kinds,
    }


def main() -> int:
    sizes = [int(a) for a in sys.argv[1:]] or [2476]
    model = ListModel()
    out: list[dict] = []
    for n in sizes:
        base = rows(n)
        out.append(scenario(f"noop {n} wierszy", model, base, list(base)))

        renamed = list(base)
        renamed[n // 2] = Row(
            item_id=renamed[n // 2].item_id,
            title="Nowa nazwa tego samego utworu",
            kind=renamed[n // 2].kind,
            path=renamed[n // 2].path,
            detail=renamed[n // 2].detail,
        )
        out.append(scenario(f"1 zmiana nazwy w {n}", model, base, renamed))
        out.append(
            scenario(f"1 dodanie w srodku {n}", model, base, base[: n // 2] + rows(1, offset=n + 5) + base[n // 2 :])
        )
        out.append(scenario(f"1 usuniecie w srodku {n}", model, base, base[: n // 2] + base[n // 2 + 1 :]))
        out.append(scenario(f"pelna podmiana zbioru {n}", model, base, rows(n, offset=n * 10)))
        out.append(scenario(f"maly -> duzy (1 -> {n})", model, rows(1), base))
        out.append(scenario(f"duzy -> pusto ({n} -> 0)", model, base, []))
    print(json.dumps(out, ensure_ascii=False, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
