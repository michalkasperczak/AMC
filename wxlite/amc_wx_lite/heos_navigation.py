"""Pure HEOS browsing state for a future AMC Test session.

The UI owns the actual list and focus. This model returns only intentional
labels and stable item identities, and rejects results from an earlier view or
target. It never sends a command to a speaker.
"""

from __future__ import annotations

from dataclasses import dataclass, field

from .heos_source import HeosItem, HeosQueueItem, HeosSource, HeosTarget


@dataclass(frozen=True, slots=True)
class HeosLocation:
    kind: str
    title: str
    source_id: str = ""
    container_id: str = ""
    return_item_id: str = ""


@dataclass(frozen=True, slots=True)
class HeosListTicket:
    generation: int
    serial: int
    target_id: str
    location: HeosLocation


@dataclass(slots=True)
class HeosBrowseState:
    """One selected target and one browse stack; no automatic target switch."""

    targets: tuple[HeosTarget, ...] = ()
    selected_target: HeosTarget | None = None
    location: HeosLocation = field(default_factory=lambda: HeosLocation("targets", "Urządzenia i grupy HEOS"))
    items: tuple[HeosTarget | HeosSource | HeosItem | HeosQueueItem, ...] = ()
    selected_item_id: str = ""
    history: list[HeosLocation] = field(default_factory=list)
    _generation: int = 0
    _serial: int = 0

    def _open(self, location: HeosLocation, *, push: bool) -> None:
        if push:
            self.history.append(self.location)
        self.location = location
        self.items = ()
        self.selected_item_id = ""
        self._generation += 1

    def set_targets(self, targets: tuple[HeosTarget, ...]) -> None:
        """Keep a still-present target; never silently choose another room."""

        self.targets = targets
        if self.selected_target is not None:
            same = next((target for target in targets if target.player_id == self.selected_target.player_id
                         and target.group_id == self.selected_target.group_id), None)
            if same is None:
                self.selected_target = None
                self.history.clear()
                self._open(HeosLocation("targets", "Urządzenia i grupy HEOS"), push=False)
            else:
                self.selected_target = same
        if self.location.kind == "targets":
            self.items = targets
            if not any(target.item_id == self.selected_item_id for target in targets):
                self.selected_item_id = targets[0].item_id if targets else ""

    def choose_target(self, item_id: str) -> HeosTarget:
        target = next((target for target in self.targets if target.item_id == item_id), None)
        if target is None:
            raise ValueError("Wybrany głośnik HEOS nie jest już dostępny.")
        self.selected_target = target
        self.history.clear()
        self._open(HeosLocation("sources", "Biblioteka HEOS"), push=False)
        return target

    def show_targets(self) -> None:
        self.history.clear()
        self._open(HeosLocation("targets", "Urządzenia i grupy HEOS"), push=False)
        self.items = self.targets
        if self.selected_target is not None:
            self.selected_item_id = self.selected_target.item_id

    def show_sources(self) -> None:
        if self.selected_target is None:
            raise ValueError("Najpierw wybierz głośnik HEOS.")
        self.history.clear()
        self._open(HeosLocation("sources", "Biblioteka HEOS"), push=False)

    def show_queue(self) -> None:
        if self.selected_target is None:
            raise ValueError("Najpierw wybierz głośnik HEOS.")
        self._open(HeosLocation("queue", "Kolejka HEOS"), push=True)

    def open_source(self, source: HeosSource) -> None:
        if self.location.kind != "sources" or source not in self.items:
            raise ValueError("Źródło HEOS nie jest już na tej liście.")
        self._open(HeosLocation("catalog", source.title, source.source_id,
                                return_item_id=source.item_id), push=True)

    def open_item(self, item: HeosItem) -> None:
        if self.location.kind != "catalog" or item not in self.items or not item.can_open:
            raise ValueError("Pozycja HEOS nie otwiera katalogu.")
        if item.child_source_id:
            sid, cid = item.child_source_id, ""
        else:
            sid, cid = item.source_id, item.container_id
        if not sid or (not cid and not item.child_source_id):
            raise ValueError("Nie można otworzyć tej pozycji HEOS.")
        self._open(HeosLocation("catalog", item.title, sid, cid,
                                return_item_id=item.item_id), push=True)

    def back(self) -> bool:
        if not self.history:
            return False
        child = self.location
        self._open(self.history.pop(), push=False)
        self.selected_item_id = child.return_item_id
        return True

    def begin_list_request(self) -> HeosListTicket:
        self._serial += 1
        return self._current_ticket()

    def _current_ticket(self) -> HeosListTicket:
        return HeosListTicket(
            self._generation, self._serial,
            self.selected_target.item_id if self.selected_target else "",
            self.location,
        )

    def accepts(self, ticket: HeosListTicket) -> bool:
        return ticket == self._current_ticket()

    def apply_targets(self, ticket: HeosListTicket,
                      targets: tuple[HeosTarget, ...]) -> bool:
        if not self.accepts(ticket):
            return False
        self.set_targets(targets)
        return True

    def apply_list(
        self, ticket: HeosListTicket,
        items: tuple[HeosTarget | HeosSource | HeosItem | HeosQueueItem, ...],
    ) -> bool:
        if not self.accepts(ticket):
            return False
        expected = {"sources": HeosSource, "catalog": HeosItem,
                    "queue": HeosQueueItem}.get(self.location.kind)
        if expected is None:
            raise ValueError("Listę urządzeń HEOS odświeża się osobno.")
        if any(not isinstance(item, expected) for item in items):
            raise ValueError("HEOS zwrócił pozycje z innej listy.")
        self.items = items
        if not any(item.item_id == self.selected_item_id for item in items):
            self.selected_item_id = items[0].item_id if items else ""
        return True
