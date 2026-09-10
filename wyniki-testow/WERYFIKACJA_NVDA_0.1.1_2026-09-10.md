# Dodatek NVDA 0.1.1 — domyślne gesty Ctrl+Windows

Na wyraźną zgodę użytkownika wszystkie 14 istniejących poleceń otrzymało domyślne akordy Ctrl+Windows. Nie zmieniano kodu odtwarzacza, prefiksu ani osobistej konfiguracji NVDA/Free Radio. Wymagany AMC pozostaje alpha 339.

- Sześć testów Python: OK. Mapa 14 unikatowych gestów, zgodność z identyfikatorami wysyłanymi do AMC, opisy użytkowe, brak Entera Narratora i cyfr w mapie; testy transportu i kolejki.
- Integracja Python/.NET: wszystkie 14 poleceń na izolowanym serwerze testowym, bez dźwięku i danych użytkownika.
- Paczka `AMC-NVDA-0.1.1.nvda-addon`: 6832 bajty, 5 wpisów; manifest 0.1.1 i 14 gestów w spakowanym źródle zweryfikowane. Bez prywatnych danych.
- SHA-256: `4B4C1810813547D3ADC27466CD916CC51B0EBD1ADFB6D746BDCD49BA9DD2E72C`.
- `git diff --check`: OK. Nowa pełna kompilacja AMC nie była potrzebna, zmiana dotyczy wyłącznie dodatku, jego testów i dokumentacji.

Nie instalowano ani nie przeładowywano NVDA; nie uruchamiano/nie zamykano AMC. Rzeczywiste przechwytywanie gestów, pomoc klawiszy, odsłuch i brajl nadal wymagają testu po instalacji. Jeżeli Free Radio nadal jest włączone, same nieużywanie go nie zapobiega konfliktom; instrukcja wskazuje wyłączenie dodatku albo rozdzielenie gestów. Własne przypisania użytkownika pozostają bez zmian.
