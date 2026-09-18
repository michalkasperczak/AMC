#!/usr/bin/env bash
# Test pakowania natywnego hosta Librespot w paczce AMC.
#
# Mierzy REALNY katalog publikacji, nie deklaracje w csproj. Fabryka silnika
# uruchamia dokladnie AppContext.BaseDirectory\LibrespotHost\amc_spotify_librespot_host.exe.
# Brak tego pliku uniemozliwia odtwarzanie nowej sesji. Straznik ma wykryc
# problem przed wydaniem, niezaleznie od komunikatu pokazywanego przez adapter.
#
# Uzycie: scripts/test-pakowanie-librespot.sh <katalog-publikacji>
#
# Proweniencja przypietej binarki:
#   GitHub Actions run  35381167099
#   librespot           0.8.0 / d36f9f1907e8cc9d68a93f8ebc6b627b1bf7267d
#   AMC                 791c8dbf2b3279f1bb8fee9220bf95d714168c96
set -u

PUBLISH_DIR="${1:-}"
EXPECTED_SHA="f5b614f01d8943b5872d9a2bb86658761198b763bb25c1647aa79268a2473c8e"
EXPECTED_SIZE="6541312"
HOST_REL="LibrespotHost/amc_spotify_librespot_host.exe"

failures=0
fail() { printf 'BLAD: %s\n' "$1" >&2; failures=$((failures + 1)); }
pass() { printf 'OK:   %s\n' "$1"; }

if [ -z "$PUBLISH_DIR" ]; then
    printf 'Uzycie: %s <katalog-publikacji>\n' "$0" >&2
    exit 2
fi
if [ ! -d "$PUBLISH_DIR" ]; then
    printf 'BLAD: katalog publikacji nie istnieje: %s\n' "$PUBLISH_DIR" >&2
    exit 2
fi

host_path="$PUBLISH_DIR/$HOST_REL"

# 1. Natywny host lezy dokladnie tam, gdzie go szuka fabryka silnika.
if [ ! -f "$host_path" ]; then
    fail "Brak natywnego hosta Librespot pod sciezka fabryki: $host_path. Sesja Librespot nie bedzie mogla odtwarzac."
else
    pass "Natywny host Librespot jest w paczce pod sciezka fabryki."

    # 2. Rozmiar i suma SHA-256 zgadzaja sie z przypieta proweniencja.
    actual_size="$(stat -c%s "$host_path")"
    if [ "$actual_size" != "$EXPECTED_SIZE" ]; then
        fail "Rozmiar hosta Librespot inny niz przypiety: oczekiwano $EXPECTED_SIZE B, jest $actual_size B."
    else
        pass "Rozmiar spakowanego hosta zgodny z proweniencja ($EXPECTED_SIZE B)."
    fi

    actual_sha="$(sha256sum "$host_path" | cut -d' ' -f1)"
    if [ "$actual_sha" != "$EXPECTED_SHA" ]; then
        fail "Spakowany host NIE jest binarka z proweniencji: oczekiwano $EXPECTED_SHA, jest $actual_sha."
    else
        pass "Suma SHA-256 spakowanego hosta zgodna z proweniencja."
    fi
fi

# 3. Licencja MIT librespot jest w paczce i ma wlasciwa tresc.
license_path="$PUBLISH_DIR/licenses/librespot-MIT.txt"
if [ ! -f "$license_path" ]; then
    fail "Brak licencji MIT librespot w paczce: $license_path. Redystrybucja binarki bez tekstu licencji lamie jej warunki."
elif ! grep -qi "MIT License" "$license_path" || ! grep -q "Paul Lietar" "$license_path"; then
    fail "Licencja librespot w paczce nie zawiera naglowka MIT i praw autorskich Paula Lietara: $license_path"
else
    pass "Licencja MIT librespot (Paul Lietar) jest w paczce."
fi

# 4. Plik proweniencji jedzie z binarka i wskazuje jej sume.
provenance_path="$PUBLISH_DIR/LibrespotHost/provenance-amc-host.json"
if [ ! -f "$provenance_path" ]; then
    fail "Brak pliku proweniencji hosta Librespot w paczce: $provenance_path"
elif ! grep -qi "$EXPECTED_SHA" "$provenance_path"; then
    fail "Plik proweniencji nie wskazuje przypietej sumy SHA-256: $provenance_path"
else
    pass "Plik proweniencji hosta jest w paczce i wskazuje przypieta sume."
fi

# 5. Dodanie drugiego silnika nie moze usunac istniejacych.
for relative in \
    "spotify-player/index.html" \
    "spotify-player/bridge.js" \
    "tidal-player/index.html" \
    "tidal-player/tidal-player.js"
do
    if [ ! -f "$PUBLISH_DIR/$relative" ]; then
        fail "Dodanie hosta Librespot nie moze usuwac istniejacych silnikow. Brak w paczce: $relative"
    else
        pass "Istniejacy skladnik silnika zostal w paczce: $relative"
    fi
done

if [ "$failures" -ne 0 ]; then
    printf '\nTest pakowania Librespot NIEZALICZONY: %s niezgodnosci.\n' "$failures" >&2
    exit 1
fi

printf '\nTest pakowania Librespot zaliczony.\n'
exit 0
