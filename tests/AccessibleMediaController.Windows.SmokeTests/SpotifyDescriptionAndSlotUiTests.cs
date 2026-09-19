using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AccessibleMediaController.Core.Commands;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Sessions;
using AccessibleMediaController.Core.Spotify;
using AccessibleMediaController.Windows;

/// <summary>
/// Pomiar RUNTIME na prawdziwych typach WPF, nie grep po zrodle:
/// (1) Alt+D rozstrzyga sie tak samo w OBU faktycznych handlerach okna i takze
/// w sesji Spotify, a edycja tekstu oraz otwarty dialog go blokuja;
/// (2) wiersze okna wyboru sesji odzwierciedlaja SLOWNIK numerow z dziurami,
/// a Enter wybiera sesje z wiersza, nie z pozycji na liscie.
/// </summary>
internal static class SpotifyDescriptionAndSlotUiTests
{
    internal static void Run()
    {
        AltDWObuHandlerach();
        WierszeOdzwierciedlajaSlownikIEnterWybiera();
        Console.WriteLine("OK: Alt+D w obu handlerach takze dla Spotify, numery sesji z dziurami w UI");
    }

    private const BindingFlags Instancja = BindingFlags.Instance | BindingFlags.NonPublic;

    private static void AltDWObuHandlerach()
    {
        // Oba faktyczne handlery MUSZA istniec i korzystac z jednej decyzji.
        var pomoc = typeof(MainWindow).GetMethod("TryResolveKeyboardHelpCommand", Instancja)
            ?? throw new Exception("Brak handlera rozpoznania skrotu.");
        var akcja = typeof(MainWindow).GetMethod("TryHandleItemActionShortcut", Instancja)
            ?? throw new Exception("Brak handlera akcji na elemencie.");
        var wspolna = typeof(MainWindow).GetMethod("TryResolvePodcastDescriptionShortcut", Instancja)
            ?? throw new Exception("Oba handlery Alt+D nie maja wspolnego punktu decyzji.");
        if (pomoc is null || akcja is null) throw new Exception("Handlery Alt+D zniknely.");

        object? Wywolaj(MainWindow window, string sessionId, bool edycja, bool dialog)
        {
            object?[] argumenty = [Key.D, ModifierKeys.Alt, true, edycja, dialog, null];
            var rozpoznano = (bool)wspolna.Invoke(window, argumenty)!;
            return rozpoznano ? argumenty[5] : null;
        }

        WOknie(window =>
        {
            var manager = Sesje(window);
            // Sesja Spotify: Alt+D MUSI dawac polecenie opisu.
            manager.SelectSession("spotify");
            if (!SpotifyPlaybackSettingsResolver.IsSpotifySession(manager.Current.Id))
                throw new Exception("Nie udalo sie przejsc do sesji Spotify.");
            if (Wywolaj(window, manager.Current.Id, false, false) as string != CommandIds.PodcastDescription)
                throw new Exception("Alt+D w sesji Spotify nie daje polecenia pelnego opisu.");
            // Sesja podcastow nadal dziala.
            manager.SelectSession("podcasts");
            if (Wywolaj(window, manager.Current.Id, false, false) as string != CommandIds.PodcastDescription)
                throw new Exception("Alt+D w sesji podcastow przestalo dzialac.");
            // Ochrona: edycja tekstu i otwarty obcy dialog blokuja skrot.
            if (Wywolaj(window, manager.Current.Id, true, false) is not null)
                throw new Exception("Alt+D przechwycono podczas edycji tekstu.");
            if (Wywolaj(window, manager.Current.Id, false, true) is not null)
                throw new Exception("Alt+D przechwycono przy otwartym dialogu.");
            // Sesja bez opisu nie dostaje polecenia.
            manager.SelectSession("local");
            if (Wywolaj(window, manager.Current.Id, false, false) is not null)
                throw new Exception("Alt+D dziala w sesji plikow lokalnych.");

            // Menu MUSI byc widoczne dokladnie tam, gdzie skrot dziala.
            var pozycja = (MenuItem)(typeof(MainWindow)
                .GetField("PlaybackPodcastDescriptionMenuItem",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?.GetValue(window)
                ?? throw new Exception("Brak pozycji menu pelnego opisu w oknie glownym."));
            var odcinek = new MediaItem
            {
                Id = "spotify:episode:E1", ExternalId = "E1", Source = "spotify:episode:E1",
                Title = "Odcinek testowy", Kind = MediaItemKind.Episode
            };
            manager.SelectSession("spotify");
            var sesja = manager.Current;
            sesja.ReplaceItems([odcinek]);
            var widok = (string)typeof(MainWindow).GetMethod("StoreSpotifyPodcastsForSession", Instancja)!
                .Invoke(window, new object[] { "spotify", new[] { odcinek } })!;
            window.ShowCurrentSession(widok);
            if (window.ActionItem?.Id != odcinek.Id)
                throw new Exception("Proba nie wybrala odcinka bez uruchamiania odtwarzania.");
            typeof(MainWindow).GetMethod("UpdateFileMenuForCurrentSession", Instancja)!
                .Invoke(window, null);
            if (pozycja.Visibility != Visibility.Visible)
                throw new Exception("Pozycja menu pelnego opisu jest ukryta w sesji Spotify z odcinkiem.");
            manager.SelectSession("local");
            typeof(MainWindow).GetMethod("UpdateFileMenuForCurrentSession", Instancja)!
                .Invoke(window, null);
            if (pozycja.Visibility == Visibility.Visible)
                throw new Exception("Pozycja menu pelnego opisu widoczna w sesji bez opisu.");
        });
    }

    private static void WierszeOdzwierciedlajaSlownikIEnterWybiera()
    {
        Exception? blad = null;
        var watek = new Thread(() =>
        {
            try
            {
                var settings = new AppSettings
                {
                    LastSessionId = "local",
                    SessionSlots = new Dictionary<int, string>
                    {
                        [1] = "local", [2] = "wiim", [4] = "tidal", [5] = "radio",
                        [7] = "podcasts", [8] = "spotify", [9] = "appleMusic"
                    }
                };
                var manager = new SessionManager(settings);
                var window = new SessionSelectionWindow(manager);
                var lista = (ListBox)(typeof(SessionSelectionWindow)
                    .GetField("SessionList",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    ?.GetValue(window)
                    ?? throw new Exception("Brak listy sesji w oknie wyboru."));
                var wiersze = lista.ItemsSource!.Cast<object>().ToList();
                var typWiersza = wiersze[0].GetType();
                int Numer(object row) => (int)typWiersza.GetProperty("Slot")!.GetValue(row)!;
                string Id(object row) => (string)typWiersza.GetProperty("SessionId")!.GetValue(row)!;

                void SprawdzZgodnoscZeSlownikiem(string etap)
                {
                    foreach (var row in wiersze)
                    {
                        var oczekiwany = manager.FindSlot(Id(row));
                        if (oczekiwany is null || Numer(row) != oczekiwany)
                        {
                            throw new Exception(
                                $"{etap}: wiersz {Id(row)} pokazuje numer {Numer(row)}, slownik mowi {oczekiwany}.");
                        }
                    }
                }
                SprawdzZgodnoscZeSlownikiem("Otwarcie okna");

                // Ruch przez DZIURE: pozostale wiersze nie moga zmienic numeru.
                var przed = wiersze.ToDictionary(Id, Numer);
                var indeksTidal = wiersze.FindIndex(row => Id(row) == "tidal");
                lista.SelectedIndex = indeksTidal;
                var przeniesiono = (bool)typeof(SessionSelectionWindow)
                    .GetMethod("MoveSelectedSession",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
                    .Invoke(window, [-1])!;
                if (!przeniesiono)
                    throw new Exception("Alt+strzalka w gore przez dziure nie przeniosla sesji.");
                wiersze = lista.ItemsSource!.Cast<object>().ToList();
                SprawdzZgodnoscZeSlownikiem("Po przeniesieniu");
                foreach (var row in wiersze)
                {
                    if (Id(row) is "tidal" or "wiim") continue;
                    if (Numer(row) != przed[Id(row)])
                        throw new Exception($"Przeniesienie przenumerowalo w UI sesje {Id(row)}.");
                }
                if (manager.SessionSlots.ContainsKey(3) || manager.SessionSlots.ContainsKey(6))
                    throw new Exception("Przeniesienie z UI wypelnilo dziury numerow.");

                // Enter na zaznaczonym wierszu wybiera sesje z TEGO wiersza.
                var zaznaczony = lista.SelectedItem!;
                try
                {
                    typeof(SessionSelectionWindow)
                        .GetMethod("Select", BindingFlags.Instance | BindingFlags.NonPublic)!
                        .Invoke(window, null);
                }
                catch (TargetInvocationException exception)
                    when (exception.InnerException is InvalidOperationException)
                {
                    // Okno nie jest pokazane jako dialog, wiec DialogResult rzuca.
                    // Numer jest ustawiany PRZED nim i to jego mierzymy.
                }
                var wybrany = (int?)typeof(SessionSelectionWindow)
                    .GetProperty("SelectedSlot")!.GetValue(window);
                if (wybrany != manager.FindSlot(Id(zaznaczony)))
                {
                    throw new Exception(
                        $"Enter zwrocil numer {wybrany}, a zaznaczona sesja {Id(zaznaczony)} ma "
                        + $"{manager.FindSlot(Id(zaznaczony))}.");
                }
                if (!manager.SessionSlots.TryGetValue(wybrany!.Value, out var idZeSlownika)
                    || !string.Equals(idZeSlownika, Id(zaznaczony), StringComparison.Ordinal)
                    || manager.SelectSession(idZeSlownika)?.Id != Id(zaznaczony))
                {
                    throw new Exception("Numer zwrocony przez Enter prowadzi do innej sesji.");
                }
                window.Close();
            }
            catch (Exception exception) { blad = exception; }
        }) { IsBackground = true };
        watek.SetApartmentState(ApartmentState.STA);
        watek.Start();
        if (!watek.Join(TimeSpan.FromSeconds(40)))
            throw new Exception("Okno wyboru sesji nie zakonczylo testu w 40 s.");
        if (blad is not null) throw new Exception("Numery sesji w oknie wyboru.", blad);
    }

    private static SessionManager Sesje(MainWindow window) =>
        (SessionManager)typeof(MainWindow).GetField("_sessions", Instancja)!.GetValue(window)!;

    private static void WOknie(Action<MainWindow> sprawdz)
    {
        Exception? blad = null;
        var watek = new Thread(() =>
        {
            var root = Path.Combine(Path.GetTempPath(), "amc-altd-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            MainWindow? window = null;
            try
            {
                var state = new PersistedState();
                state.Settings.Updates.CheckAutomatically = false;
                state.Settings.LastSessionId = "podcasts";
                var store = new ConfigurationStore(Path.Combine(root, "state.json"));
                window = new MainWindow(state, store);
                sprawdz(window);
            }
            catch (Exception exception) { blad = exception; }
            finally
            {
                window?.Close();
                try { Directory.Delete(root, true); } catch (IOException) { }
            }
        }) { IsBackground = true };
        watek.SetApartmentState(ApartmentState.STA);
        watek.Start();
        if (!watek.Join(TimeSpan.FromSeconds(60)))
            throw new Exception("Start okna nie zakonczyl testu w 60 s.");
        if (blad is not null) throw new Exception("Alt+D w rzeczywistym oknie glownym.", blad);
    }
}
