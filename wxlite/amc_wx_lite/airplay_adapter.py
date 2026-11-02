"""Experimental AirPlay audio sender boundary for AMC Test.

The adapter is deliberately independent of wx and imports pyatv only when
called. It is not advertised as an output until real receiver tests pass.
"""

from __future__ import annotations

import asyncio
from dataclasses import dataclass
from pathlib import Path
from typing import Any


class AirPlayUnavailable(RuntimeError):
    """A user-facing failure to discover or use an AirPlay audio receiver."""


@dataclass(frozen=True, slots=True)
class AirPlayDevice:
    device_id: str
    name: str

    @property
    def label(self) -> str:
        return self.name

    def __str__(self) -> str:
        return self.label


def _library(module: Any | None) -> Any:
    if module is not None:
        return module
    try:
        import pyatv
    except ImportError as error:
        raise AirPlayUnavailable("Obsługa AirPlay nie jest jeszcze zainstalowana w AMC") from error
    return pyatv


async def discover_airplay_devices(*, module: Any | None = None,
                                   timeout: int = 4) -> tuple[AirPlayDevice, ...]:
    """Return only RAOP-capable devices with stable IDs and spoken names."""
    library = _library(module)
    protocol = library.const.Protocol.RAOP
    configs = await library.scan(asyncio.get_running_loop(), timeout=timeout,
                                 protocol=protocol)
    result: list[AirPlayDevice] = []
    seen: set[str] = set()
    for config in configs:
        device_id = getattr(config, "identifier", None)
        name = getattr(config, "name", None)
        service = config.get_service(protocol)
        if (service is None or not isinstance(device_id, str) or not device_id
                or not isinstance(name, str) or not name.strip() or device_id in seen):
            continue
        seen.add(device_id)
        result.append(AirPlayDevice(device_id, name.strip()))
    return tuple(result)


async def stream_local_file(device_id: str, path: str | Path,
                            *, module: Any | None = None, timeout: int = 4) -> None:
    """One-shot file proof, not a session output or a mirroring implementation.

    ``stream_file`` runs until the file completes. A later session adapter must
    own cancellation, queue changes, volume, pairing and recovery.
    """
    source = Path(path)
    if not source.is_file():
        raise AirPlayUnavailable("Nie znaleziono pliku do wysłania przez AirPlay")
    library = _library(module)
    protocol = library.const.Protocol.RAOP
    loop = asyncio.get_running_loop()
    configs = await library.scan(loop, timeout=timeout, protocol=protocol,
                                 identifier=device_id)
    config = next((item for item in configs
                   if getattr(item, "identifier", None) == device_id
                   and item.get_service(protocol) is not None), None)
    if config is None:
        raise AirPlayUnavailable("Wybrane urządzenie AirPlay jest niedostępne")
    receiver = await library.connect(config, loop, protocol=protocol)
    try:
        await receiver.stream.stream_file(str(source))
    finally:
        receiver.close()
