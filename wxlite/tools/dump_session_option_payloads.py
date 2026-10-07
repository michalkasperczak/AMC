"""Generator payloadow Opcji sesji dla testow protokolu C#.

Testy Pythona uzywaja atrapy klienta, wiec same nie dowodza, ze host
przyjmie wyslane pola. Ten skrypt zapisuje payloady WSZYSTKICH kombinacji,
ktore dialog potrafi wyprodukowac; ``SessionOptionsPayloadTests`` czyta je
prawdziwym ``LiteAudioSettings.Read``.

Uzycie (z katalogu ``wxlite``)::

    python3 tools/dump_session_option_payloads.py /tmp/payloads.json
    AMC_SESSION_OPTIONS_PAYLOADS=/tmp/payloads.json dotnet run --project \\
        tests/AccessibleMediaController.LiteHost.ProtocolTests/...csproj
"""

from __future__ import annotations

import itertools
import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from amc_wx_lite import session_options as so
from amc_wx_lite.navigation import SessionId
from amc_wx_lite.state_store import Options


def build() -> list[dict]:
    """Payload dla kazdej kombinacji, ktora uzytkownik moze wybrac w dialogu.

    Kombinacje liczymy przed ``restricted_to``, zeby objac takze przypadek
    "wybrano cos, czego ta sesja nie umie" -- port ma to odciac, a nie
    wyslac do silnika.
    """
    options = Options()
    entries: list[dict] = []
    for session in (SessionId.FILES, SessionId.RADIO):
        caps = so.capabilities_for(session)
        for loud, trans, silence, pause in itertools.product(
            (None, True, False),
            (None, True, False),
            (None,) + so.INTER_TRACK_SILENCE_CHOICES,
            (None, True, False),
        ):
            overrides = so.SessionPlaybackOverrides(
                loudness_normalization=loud,
                smooth_track_transitions=trans,
                inter_track_silence_ms=silence,
                pause_on_player_exit=pause,
            ).restricted_to(caps)
            entries.append(
                {
                    "label": (
                        f"{session.value} loud={loud} trans={trans} "
                        f"sil={silence} pause={pause}"
                    ),
                    "payload": so.resolve_audio_payload(options, overrides),
                }
            )
    return entries


def main(argv: list[str]) -> int:
    target = Path(argv[1]) if len(argv) > 1 else Path("/tmp/amc_session_payloads.json")
    entries = build()
    target.write_text(
        json.dumps(entries, ensure_ascii=False, indent=1), encoding="utf-8"
    )
    print(f"payloadow: {len(entries)} -> {target}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv))
