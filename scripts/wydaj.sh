#!/usr/bin/env bash
# Publikacja wydania AMC z SUMAMI KONTROLNYMI SHA-256 w opisie.
#
# DLACZEGO ISTNIEJE: wydanie 367 opublikowano recznie, bez sum. Aktualizator AMC
# czyta sume z opisu wydania, wiec przy jej braku mowi uzytkownikowi "zgodnosc
# pliku nie zostala sprawdzona". Recznie o sumach zapomina sie zawsze - tutaj
# nie da sie ich pominac, bo skrypt ODMAWIA publikacji bez nich.
#
# UZYCIE:
#   scripts/wydaj.sh 0.1.0-alpha.368 /tmp/amc368 [plik-z-opisem.md]
#
# Katalog musi zawierac zalaczniki wydania (instalator .exe i paczke .zip).
# Format sumy MUSI zostac dwuwierszowy (nazwa pliku, w nastepnym wierszu suma) -
# tak czyta go ApplicationUpdatePolicy.ReadChecksumFor. Pilnuje tego test
# ReleaseNotesChecksumTests; zmiana formatu tutaj bez zmiany tam zepsuje
# weryfikacje po cichu.
set -euo pipefail

WERSJA="${1:?podaj wersje, np. 0.1.0-alpha.368}"
KATALOG="${2:?podaj katalog z zalacznikami}"
OPIS="${3:-}"
REPO="michalkasperczak/AMC"
TAG="v${WERSJA}"

cd "$KATALOG"
# Zalaczniki: instalator, paczka i dodatek NVDA. Dodatek MUSI tu byc - wydanie
# 385 dodalo skrot obslugiwany przez dodatek, wiec sam .exe to za malo:
# uzytkownik zainstalowalby program bez czesci, ktora realizuje nowa funkcje.
mapfile -t PLIKI < <(ls -1 *.exe *.zip *.nvda-addon 2>/dev/null || true)
if [ "${#PLIKI[@]}" -eq 0 ]; then
    echo "BLAD: w $KATALOG nie ma zadnego pliku .exe ani .zip" >&2
    exit 1
fi

NOTATKI="$(mktemp)"
if [ -n "$OPIS" ]; then
    cat "$OPIS" > "$NOTATKI"
    printf '\n' >> "$NOTATKI"
fi

{
    echo "## Sumy kontrolne SHA-256"
    echo
    for plik in "${PLIKI[@]}"; do
        echo "$plik"
        sha256sum "$plik" | cut -d' ' -f1
        echo
    done
} >> "$NOTATKI"

# Kontrola wlasnej roboty: kazdy zalacznik MUSI miec swoja sume w opisie.
# Bez tego skrypt tylko udaje, ze pilnuje sum.
for plik in "${PLIKI[@]}"; do
    if ! grep -qF "$plik" "$NOTATKI"; then
        echo "BLAD: brak nazwy $plik w opisie wydania" >&2
        exit 1
    fi
done
if [ "$(grep -cE '^[0-9a-f]{64}$' "$NOTATKI")" -ne "${#PLIKI[@]}" ]; then
    echo "BLAD: liczba sum w opisie nie zgadza sie z liczba zalacznikow" >&2
    exit 1
fi

gh release create "$TAG" --repo "$REPO" --prerelease \
    --title "AMC $WERSJA" --notes-file "$NOTATKI" "${PLIKI[@]}"

# Potwierdzenie U ZRODLA - osobno stan zalacznikow i osobno obecnosc sum
# w opublikowanym opisie. Udany "gh release create" nie dowodzi ani jednego.
gh release view "$TAG" --repo "$REPO" \
    --json tagName,isPrerelease,assets \
    --jq '.tagName, .isPrerelease, (.assets[] | "\(.name) \(.size) \(.state)")'

BODY="$(gh release view "$TAG" --repo "$REPO" --json body --jq .body)"
for plik in "${PLIKI[@]}"; do
    SUMA="$(sha256sum "$plik" | cut -d' ' -f1)"
    if ! grep -qF "$SUMA" <<< "$BODY"; then
        echo "BLAD: suma pliku $plik nie trafila do opublikowanego opisu" >&2
        exit 1
    fi
    echo "SUMA POTWIERDZONA W WYDANIU: $plik"
done
rm -f "$NOTATKI"
