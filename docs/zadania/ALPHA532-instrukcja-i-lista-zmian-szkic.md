# AMC alpha.532 — szkic instrukcji i listy zmian

Szkic do korekty przez użytkownika. Nie jest potwierdzeniem odbioru NVDA ani odsłuchu.

## Instrukcja

W sesji TIDAL wybierz utwór, album, playlistę albo wykonawcę. Ctrl+Alt+Shift+cyfra przypisuje zaznaczenie do wskazanego presetu. Ctrl+Alt+Shift+P otwiera dotychczasowe przypisywanie. Preset albumu lub playlisty uruchamia odtwarzanie w AMC; Ctrl+Enter na albumie lub playliście korzysta z tego samego odtwarzacza. Jawne przekazanie pojedynczego utworu do oryginalnego TIDAL pozostało w menu kontekstowym.

W ustawieniach Radia można wybrać, czy Enter na aktywnym nagraniu rozpoczyna odsłuch na żywo, czy od początku dostępnego bufora. Ctrl+Enter uruchamia drugi wariant. Odsłuch ma suwak, sterowanie czasem, głośnością i tempem oraz odczyty Ctrl+Shift+E/R/T. Ta funkcja była już w alpha.531; nadal wymaga odbioru na rzeczywistym nagraniu.

## Lista zmian od alpha.531

- Test i Public: bezpośrednie przypisywanie presetów cyframi w TIDAL oraz natywne uruchamianie albumu i playlisty z presetu i przez Ctrl+Enter.
- Test i Public: po przekazaniu do Google Cast sprawdzany jest rzeczywisty stan odbiornika; brak odtwarzania ma zostać zgłoszony, a nie uznany za sukces.
- Test zachowuje Apple Music i HEOS. Public nie zawiera tych sesji.

AirPlay na Sonos „Biuro” nadal wymaga diagnozy: połączenie może zakończyć się bez słyszalnego dźwięku. Zmiana lokalnej zapory Windows nie jest częścią instalatora. Dźwięk, NVDA i pełna kolejność utworów z prawdziwego konta TIDAL pozostają do odbioru.
