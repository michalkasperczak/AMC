using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AccessibleMediaController.Core.Commands;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Sonos;
using AccessibleMediaController.Windows;

/// <summary>
/// APARATURA dla zgloszenia po 4.1.7. Wszystko po stronie testu: zadnej nowej
/// publicznej fabryki w produkcji. Uzywa WYLACZNIE hookow, ktore JUZ istnieja
/// w zrodle (sprawdzone przed uzyciem, nie z pamieci).
/// </summary>
internal static partial class SonosFavoritePlayRealOwnerTests
{
    private sealed partial class RealHarness
    {
        /// <summary>
        /// FOKUS NA LISTE GLOWNEGO OKNA produkcyjna droga - F5 bez fokusu na
        /// liscie mierzylby inna sciezke niz ta, ktora ma uzytkownik.
        /// </summary>
        internal void FocusMediaListForMeasurement()
        {
            var method = typeof(MainWindow).GetMethod("FocusMediaList", Instance)
                ?? throw new Exception("Nie ma prawdziwej metody FocusMediaList.");
            method.Invoke(Window, null);
            PumpQuietly(TimeSpan.FromMilliseconds(120));
        }

        /// <summary>
        /// KLAWISZ do PRAWDZIWEGO handlera OKNA GLOWNEGO, z RZECZYWISCIE
        /// wcisnietymi modyfikatorami w stanie watku.
        /// </summary>
        internal void PressKeyOnMainWindow(Key key, ModifierKeys modifiers) =>
            SendKeyWithModifiers(Window, key, modifiers);

        /// <summary>
        /// STAN ODCZYTANY produkcyjna droga <c>ReadSonosGroupStateAsync</c>:
        /// bramka polecen wymaga ODCZYTU, nie nazwy gestu. Bez tego Spacja
        /// odmowilaby "Stan Sonos nie został odczytany" i pomiar nie dotykalby
        /// zgloszonego bledu.
        /// </summary>
        internal void PrimeSonosPlaybackStateForMeasurement()
        {
            // PRAWDZIWY Sonos nad grajacym, pauzowalnym materialem ZGLASZA
            // canPause. Wspolna trasa proby tego pola nie oddaje, wiec bramka
            // odmawiala z POWODU NIEODCZYTANEGO UPRAWNIENIA - czyli nie z
            // powodu zgloszonego bledu. Uzupelniamy odpowiedz, a NIE oslabiamy
            // bramki w produkcji.
            Handler.RouteOverride = (request, _) =>
                request.Method == HttpMethod.Get
                && request.RequestUri!.AbsolutePath.EndsWith("/playback", StringComparison.Ordinal)
                    ? Json("{\"playbackState\":\"PLAYBACK_STATE_PLAYING\","
                        + "\"itemId\":\"POZYCJA-1\",\"positionMillis\":12000,"
                        + "\"availablePlaybackActions\":{\"canPause\":true,\"canSkip\":true,"
                        + "\"canSkipBack\":true,\"canSeek\":true,\"canCrossfade\":false}}")
                    : null;
            var method = typeof(MainWindow).GetMethod("ReadSonosGroupStateAsync", Instance)
                ?? throw new Exception("Nie ma prawdziwej metody ReadSonosGroupStateAsync.");
            Pump((Task)method.Invoke(Window, null)!);
            if (Window.SonosPlaybackForTests is null)
            {
                throw new Exception("Odczyt stanu grupy nie dostarczył stanu odtwarzania - "
                    + "bramka poleceń odmówiłaby z innego powodu niż zgłoszony błąd.");
            }

            if (Window.SonosPlaybackForTests!.AvailablePlaybackActions is not { CanPause: true })
            {
                throw new Exception("Syntetyczna chmura nie zgłosiła canPause - pomiar Spacji "
                    + "mierzyłby odmowę uprawnienia, a nie zgłoszony błąd.");
            }
        }

        /// <summary>
        /// SPRAWDZENIE, ze po ISTNIEJACYM ClearSonosTargetForTests lista grup
        /// jest FAKTYCZNIE pusta - inaczej pomiar Ctrl+F5 nie mierzylby
        /// zgloszonej pustki.
        /// </summary>
        internal void AssertNoSonosTargetForMeasurement()
        {
            // PRODUKCYJNE czyszczenie stanu celu - to samo, co robi wyjscie z
            // sesji. Istniejacy ClearSonosTargetForTests zdejmuje tylko wybor i
            // topologie, a lista wierszy zostaje; bez tego pomiar Ctrl+F5 nie
            // mierzylby zgloszonej PUSTKI.
            (typeof(MainWindow).GetMethod("ClearSonosTargetState", Instance)
                ?? throw new Exception("Nie ma prawdziwej metody ClearSonosTargetState."))
                .Invoke(Window, null);
            PumpQuietly(TimeSpan.FromMilliseconds(80));
            if (Window.SonosGroupRows.Count != 0)
            {
                throw new Exception("Czyszczenie celu nie opróżniło listy grup - "
                    + "pomiar Ctrl+F5 nie mierzyłby zgłoszonej pustki.");
            }
        }

        /// <summary>
        /// PRZYPISANIE presetu Sonos do slotu w ISTNIEJACYM magazynie ustawien.
        /// Slot jest NIEPUSTY, dokladnie jak u uzytkownika (ulubione w slotach
        /// 1-6, playlista w 11). Zwraca fragment adresu, ktorego oczekujemy w
        /// RZECZYWISTYM POST - sama mowa nie jest dowodem.
        /// </summary>
        internal string AssignSonosFavoritePresetToSlotForMeasurement(int slot)
        {
            var sessionId = Window.SessionsForTests.Current.Id;
            if (!MainWindow.IsSonosSession(sessionId))
            {
                throw new Exception("Preset przypisywany poza sesją Sonos: " + sessionId);
            }

            var entries = Window.StateForTests.SessionPresets.EntriesBySession
                .TryGetValue(sessionId, out var existing) ? existing : [];
            entries.RemoveAll(entry => entry.Slot == slot);
            entries.Add(new SessionPresetEntry
            {
                Slot = slot,
                TargetId = "ULU-PIERWSZY",
                TargetKind = SonosPresetKinds.Favorite,
                TargetTitle = "Nokturny",
                SonosHouseholdId = HouseholdId
            });
            Window.StateForTests.SessionPresets.EntriesBySession[sessionId] = entries;
            return "groups/" + GroupId + "/favorites";
        }
    }
}
