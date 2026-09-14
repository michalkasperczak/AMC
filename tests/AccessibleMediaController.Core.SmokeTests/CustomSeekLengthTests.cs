using AccessibleMediaController.Core.Commands;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Sessions;

/// <summary>
/// Testy przeskoku o czas USTAWIONY przez uzytkownika (Alt+Ctrl+strzalki),
/// zadanie 4 z listy 15.09.2026.
///
/// Mierzymy trzy rzeczy: czytanie wartosci wpisanej przez uzytkownika, polska
/// odmiane w opisie dlugosci oraz to, ze polecenie przesuwa pozycje o
/// USTAWIONY czas, a nie o staly krok - i ze zmiana ustawienia od razu zmienia
/// skutek skrotu, bez restartu programu.
/// </summary>
internal static class CustomSeekLengthTests
{
    internal static void Run()
    {
        DefaultIsFiveMinutes();
        BareNumberMeansMinutes();
        ExplicitSecondsAreRespected();
        ClockNotationIsAccepted();
        NonsenseIsRejectedNotZeroed();
        RangeIsClamped();
        PolishPluralsAreCorrect();
        CommandJumpsByConfiguredLength();
        CommandFollowsSettingsChangeWithoutRestart();
        CustomJumpDoesNotDisturbFixedSteps();
    }

    private static void DefaultIsFiveMinutes()
    {
        Check(new AppSettings().CustomSeekSeconds == 300,
            "Domyślny przeskok Alt+Ctrl+strzałki to 5 minut");
        Check(PlaybackSeekRules.DescribeSeekLength(300) == "5 minut",
            "Opis domyślnej długości przeskoku");
    }

    private static void BareNumberMeansMinutes()
    {
        // Pole sluzy do dlugich przeskokow, wiec samo "5" ma znaczyc 5 minut.
        // Gdyby znaczylo 5 sekund, wartosc domyslna wpisana z palca cicho
        // zmieniala by skrot na zupelnie inna funkcje.
        Check(PlaybackSeekRules.TryParseSeekLength("5", out var five) && five == 300,
            "Sama liczba oznacza minuty");
        Check(PlaybackSeekRules.TryParseSeekLength(" 10 ", out var ten) && ten == 600,
            "Odstępy wokół liczby nie przeszkadzają");
        Check(PlaybackSeekRules.TryParseSeekLength("5 minut", out var named) && named == 300,
            "Zapis z jednostką minut");
        Check(PlaybackSeekRules.TryParseSeekLength("2 min", out var shortForm) && shortForm == 120,
            "Skrócony zapis minut");
    }

    private static void ExplicitSecondsAreRespected()
    {
        Check(PlaybackSeekRules.TryParseSeekLength("90 s", out var ninety) && ninety == 90,
            "Zapis w sekundach");
        Check(PlaybackSeekRules.TryParseSeekLength("45 sekund", out var fortyFive) && fortyFive == 45,
            "Pełna nazwa jednostki sekund");
        Check(PlaybackSeekRules.TryParseSeekLength("2,5 min", out var half) && half == 150,
            "Wartość ułamkowa z przecinkiem po polsku");
    }

    private static void ClockNotationIsAccepted()
    {
        Check(PlaybackSeekRules.TryParseSeekLength("1:30", out var mmss) && mmss == 90,
            "Zapis minuty dwukropek sekundy");
        Check(PlaybackSeekRules.TryParseSeekLength("10:00", out var round) && round == 600,
            "Zapis zegarowy pełnych minut");
        Check(!PlaybackSeekRules.TryParseSeekLength("1:75", out _),
            "Liczba sekund powyżej 59 w zapisie zegarowym jest błędem");
    }

    private static void NonsenseIsRejectedNotZeroed()
    {
        // Odrzucenie MUSI byc rozpoznawalne, bo wywolujacy zostawia wtedy stara
        // wartosc. Ciche zwrocenie zera zamienilo by skrot w nic nierobiacy.
        Check(!PlaybackSeekRules.TryParseSeekLength("", out _), "Puste pole jest błędem");
        Check(!PlaybackSeekRules.TryParseSeekLength("   ", out _), "Same odstępy są błędem");
        Check(!PlaybackSeekRules.TryParseSeekLength("dużo", out _), "Słowo bez liczby jest błędem");
        Check(!PlaybackSeekRules.TryParseSeekLength("0", out _), "Zero nie jest długością przeskoku");
        Check(!PlaybackSeekRules.TryParseSeekLength("5 godzin", out _),
            "Nieobsługiwana jednostka jest błędem, nie domysłem");
    }

    private static void RangeIsClamped()
    {
        Check(PlaybackSeekRules.NormalizeCustomSeekSeconds(0) == 5,
            "Wartość poniżej zakresu podnoszona do 5 sekund");
        Check(PlaybackSeekRules.NormalizeCustomSeekSeconds(99999) == 1800,
            "Wartość powyżej zakresu obcinana do 30 minut");
        Check(PlaybackSeekRules.TryParseSeekLength("120 min", out var tooMuch) && tooMuch == 1800,
            "Wpisana wartość powyżej zakresu też jest obcinana");
    }

    private static void PolishPluralsAreCorrect()
    {
        Check(PlaybackSeekRules.DescribeSeekLength(60) == "1 minutę", "Jedna minuta");
        Check(PlaybackSeekRules.DescribeSeekLength(120) == "2 minuty", "Dwie minuty");
        Check(PlaybackSeekRules.DescribeSeekLength(300) == "5 minut", "Pięć minut");
        Check(PlaybackSeekRules.DescribeSeekLength(1320) == "22 minuty", "Dwadzieścia dwie minuty");
        Check(PlaybackSeekRules.DescribeSeekLength(45) == "45 sekund", "Czterdzieści pięć sekund");
        Check(PlaybackSeekRules.DescribeSeekLength(5) == "5 sekund", "Pięć sekund");
        Check(PlaybackSeekRules.DescribeSeekLength(22) == "22 sekundy", "Dwadzieścia dwie sekundy");
    }

    private static void CommandJumpsByConfiguredLength()
    {
        var (router, sessions, _) = BuildRouter(customSeekSeconds: 300);
        sessions.Current.SetPosition(TimeSpan.FromMinutes(20));

        router.Execute(CommandIds.SeekForwardCustom);
        Check(sessions.Current.Position == TimeSpan.FromMinutes(25),
            $"Przewijanie o ustawione 5 minut, a wyszlo {sessions.Current.Position}");

        router.Execute(CommandIds.SeekBackwardCustom);
        Check(sessions.Current.Position == TimeSpan.FromMinutes(20),
            $"Cofanie o ustawione 5 minut, a wyszlo {sessions.Current.Position}");
    }

    private static void CommandFollowsSettingsChangeWithoutRestart()
    {
        var (router, sessions, settings) = BuildRouter(customSeekSeconds: 600);
        sessions.Current.SetPosition(TimeSpan.FromMinutes(30));

        router.Execute(CommandIds.SeekForwardCustom);
        Check(sessions.Current.Position == TimeSpan.FromMinutes(40),
            "Skrot bierze aktualna wartosc z ustawien");

        // Zmiana w Ustawieniach musi dzialac od razu - odczyt raz na start
        // dawalby stary przeskok do konca dzialania programu.
        settings.CustomSeekSeconds = 60;
        router.Execute(CommandIds.SeekForwardCustom);
        Check(sessions.Current.Position == TimeSpan.FromMinutes(41),
            "Nowa wartosc dziala bez ponownego uruchomienia programu");
    }

    private static void CustomJumpDoesNotDisturbFixedSteps()
    {
        // Zadanie mowilo o NOWYM skrocie, nie o zmianie istniejacych.
        var (router, sessions, _) = BuildRouter(customSeekSeconds: 900);
        sessions.Current.SetPosition(TimeSpan.FromMinutes(20));

        router.Execute(CommandIds.SeekForward10);
        Check(sessions.Current.Position == TimeSpan.FromMinutes(20) + TimeSpan.FromSeconds(10),
            "Strzalka bez modyfikatorow nadal przewija o 10 sekund");
        router.Execute(CommandIds.SeekForward60);
        Check(sessions.Current.Position == TimeSpan.FromMinutes(21) + TimeSpan.FromSeconds(10),
            "Ctrl+strzalka nadal przewija o minute");
    }

    /// <summary>
    /// Sklada router nad sesja demonstracyjna i ustawia element DLUGI (ponad
    /// dwie godziny), bo przeskok jest przycinany dlugoscia utworu - na utworze
    /// czterominutowym kazdy przeskok konczylby sie na jego koncu i test
    /// pokazywalby zgodnosc, ktorej nie ma.
    /// </summary>
    private static (CommandRouter Router, SessionManager Sessions, AppSettings Settings) BuildRouter(
        int customSeekSeconds)
    {
        var settings = new AppSettings { CustomSeekSeconds = customSeekSeconds };
        var sessions = new SessionManager(settings);
        var longItem = sessions.Current.Items.First(item => item.Duration >= TimeSpan.FromHours(2));
        sessions.Current.SelectItem(longItem);
        var router = new CommandRouter(sessions, settings, new SilentSink(), new FakeActions(longItem));
        return (router, sessions, settings);
    }

    private sealed class SilentSink : IAnnouncementSink
    {
        public string LastMessage { get; private set; } = string.Empty;
        public void Announce(string message) => LastMessage = message;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
