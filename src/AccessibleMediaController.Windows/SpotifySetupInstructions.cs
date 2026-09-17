namespace AccessibleMediaController.Windows;

/// <summary>
/// Instrukcja pierwszego logowania Spotify, czytana w oknie treści.
///
/// Trzymana w programie, nie w osobnym pliku obok instalatora: użytkownik
/// potrzebuje jej dokładnie w chwili, gdy stoi w oknie konta, a nie wtedy,
/// gdy szuka plików na dysku. Tekst jest ułożony pod czytnik ekranu -
/// nagłówki, kolejne kroki, jedna myśl w akapicie, bez tabel.
/// </summary>
internal static class SpotifySetupInstructions
{
    public const string Text = """
        # Pierwsze logowanie Spotify

        Spotify nie pozwala programom logować użytkowników bez rejestracji
        samego programu. Rejestracja jest darmowa i robi się ją raz. Poniższe
        kroki wykonujesz na swoim koncie Spotify, w przeglądarce.

        ## Co jest potrzebne

        Konto Spotify Premium, jeśli chcesz, żeby muzyka grała wewnątrz AMC.
        Bez Premium Spotify nie przyznaje prawa do odtwarzania - zostaje
        biblioteka i sterowanie oficjalną aplikacją.

        ## Krok 1: otwórz panel aplikacji

        W oknie konta Spotify wybierz przycisk "Otwórz panel aplikacji
        Spotify". Otworzy się strona developer.spotify.com w przeglądarce.
        Zaloguj się swoim zwykłym kontem Spotify, jeśli strona o to poprosi.

        ## Krok 2: utwórz aplikację

        Na stronie panelu wybierz "Create app". Wypełnij pola:

        Nazwa aplikacji: wpisz cokolwiek, na przykład AMC.

        Opis: wpisz cokolwiek, na przykład "dostępny odtwarzacz".

        Redirect URI: wpisz adres powrotu skopiowany z okna konta Spotify w
        AMC. Musi być wpisany znak w znak, razem z ukośnikiem na końcu.
        Domyślnie jest to:

        http://127.0.0.1:43822/spotify/callback/

        Który interfejs API zamierzasz używać: zaznacz "Web API" oraz "Web
        Playback SDK".

        Zaznacz zgodę na warunki i wybierz "Save".

        ## Krok 3: skopiuj identyfikator

        Po utworzeniu aplikacji wejdź w jej ustawienia ("Settings"). Znajdziesz
        tam "Client ID" - to jest identyfikator aplikacji. Skopiuj go i wklej
        w oknie konta Spotify w AMC, w pole "Identyfikator aplikacji".

        Nie kopiuj "Client Secret". AMC go nie potrzebuje i celowo nigdzie go
        nie zapisuje.

        ## Krok 4: dopisz swoje konto

        W panelu aplikacji wejdź w "User Management" i dopisz tam adres poczty
        przypisany do Twojego konta Spotify.

        Ten krok jest łatwy do pominięcia, a bez niego Spotify odmawia dostępu
        do danych konta i logowanie kończy się błędem, mimo że hasło było
        poprawne. Nowa aplikacja działa w trybie rozwojowym, w którym obsługuje
        tylko konta wpisane na tę listę.

        ## Krok 5: zaloguj się w AMC

        W oknie konta Spotify wybierz "Zapisz ustawienia", a potem "Zaloguj w
        przeglądarce". Otworzy się oficjalna strona Spotify z pytaniem o zgodę.
        Hasło wpisujesz wyłącznie tam - AMC go nie widzi.

        Po zgodzie przeglądarka wróci do AMC i pokaże komunikat o zakończonym
        logowaniu. AMC powie wtedy nazwę konta i rodzaj abonamentu.

        ## Gdzie AMC trzyma logowanie

        Token logowania idzie do Menedżera poświadczeń Windows. Nie ma go w
        pliku ustawień AMC, w kopiach zapasowych ani w dzienniku
        diagnostycznym. Przycisk "Odłącz konto" usuwa go z powrotem.

        ## Co Spotify ogranicza po swojej stronie

        Zawartości playlist redakcyjnych Spotify nie da się pokazać. Spotify
        odciął do nich dostęp programom w lutym 2026 roku. Widać nazwę
        playlisty, nie widać listy utworów. Twoje własne playlisty są dostępne
        w całości.
        """;
}
