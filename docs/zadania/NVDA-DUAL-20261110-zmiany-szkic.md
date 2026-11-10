# AMC 0.1.0-alpha.492 i dodatek NVDA 0.4.2 — szkic listy zmian

Status: szkic do korekty użytkownika, bez instalacji i publikacji.

- Jeden dodatek NVDA kieruje istniejące skróty do AMC Test lub AMC Public według aktywnego okna, także gdy fokus jest w dialogu programu.
- Po przejściu poza AMC używany jest ostatnio wybrany profil. Wybór jest zapamiętywany po restarcie NVDA.
- Polecenie oczekujące w kolejce zachowuje profil wybrany w chwili naciśnięcia skrótu. Polecenia głośności i przewijania łączą się tylko w obrębie tego profilu.
- Gdy cel nie jest jednoznaczny albo wybrany profil został zamknięty, NVDA podaje zrozumiały komunikat i prosi o wskazanie okna właściwego programu.
- Gesty nie zostały zmienione. Magazyny danych, nagrania i poświadczenia Test/Public pozostają oddzielne.

Weryfikacja źródeł i testów jest zapisana w `NVDA-DUAL-20261110-dwa-profile-jeden-dodatek.md`. Pełne paczki, instalatory, odbiór z żywym NVDA i publikacja czekają na bezpieczne zakończenie aktywnego nagrania.
