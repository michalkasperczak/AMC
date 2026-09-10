using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Sessions;
using AccessibleMediaController.Core.Tidal;
using AccessibleMediaController.Windows;
using AccessibleMediaController.Windows.Controls;
using AccessibleMediaController.Windows.Services;

internal static class TidalInteractionSmokeTests
{
    internal static void Run()
    {
        // Earlier UI tests may install a dispatcher context on the runner thread.
        // The network fixture must not wait for that blocked dispatcher.
        Task.Run(TestSerializedMembership).GetAwaiter().GetResult();
        TestContext();
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { TestSelectionRefresh(); TestListFocus(); TestSearch(); }
            catch (Exception exception) { failure = exception; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        if (!thread.Join(TimeSpan.FromSeconds(25))) throw new Exception("TIDAL: przekroczono limit testu interfejsu.");
        if (failure is not null) throw new Exception("TIDAL: test interakcji", failure);
        Console.WriteLine("OK: TIDAL — seryjne przełączniki, błędy HTTP, odświeżenie zaznaczenia, kontekst odpowiedzi, fokus i wyszukiwarka");
    }

    private static MediaItem Album(string id = "one") => new()
    {
        Id = $"tidal:albums:{id}", ExternalId = $"albums:{id}", Title = $"Test album {id}", Kind = MediaItemKind.Album
    };

    private static async Task TestSerializedMembership()
    {
        using var handler = new MembershipHandler();
        using var http = new HttpClient(handler);
        using var integration = new TidalIntegrationService(new TidalSettings(), new TidalApiClient(http),
            _ => Task.FromResult(new TidalTokenSet("test-only", "test-only", DateTimeOffset.UtcNow.AddHours(1), "collection.write", "test")));
        integration.RestoreCachedCollection([]);
        var firstObject = Album();
        var staleObject = Album();
        var first = integration.ChangeCollectionMembershipAsync([firstObject], null, CancellationToken.None);
        await handler.FirstStarted.Task.WaitAsync(TimeSpan.FromSeconds(3));
        var second = integration.ChangeCollectionMembershipAsync([staleObject], null, CancellationToken.None);
        Check(!second.IsCompleted && handler.Methods.Count == 1, "Druga operacja nie czeka na potwierdzenie pierwszej.");
        handler.ReleaseFirst.TrySetResult();
        var added = await first.WaitAsync(TimeSpan.FromSeconds(3));
        var removed = await second.WaitAsync(TimeSpan.FromSeconds(3));
        Check(added.Added && !removed.Added && removed.Items.Count == 0, "Dwa szybkie skróty nie dodały i nie usunęły albumu.");
        Check(handler.Methods.SequenceEqual([HttpMethod.Post, HttpMethod.Delete]), "Przełącznik powtórzył żądanie ze starego obiektu.");
        handler.FailNext = true;
        var failedItem = Album("failed");
        try
        {
            await integration.ChangeCollectionMembershipAsync([failedItem], null, CancellationToken.None);
            throw new Exception("Nie zgłoszono błędu HTTP.");
        }
        catch (TidalApiException exception)
        {
            Check(exception.StatusCode == HttpStatusCode.NotFound && !exception.IsAuthorizationFailure,
                "404 błędnie wymaga ponownego logowania.");
        }
        Check(!failedItem.IsInLibrary, "Nieudane dodanie zmieniło kolekcję.");
        var retry = await integration.ChangeCollectionMembershipAsync([failedItem], null, CancellationToken.None);
        Check(retry.Added && failedItem.IsInLibrary, "Po błędzie nie zwolniono blokady lub przełącznik zapamiętał fałszywy sukces.");
        var explicitRemoval = await integration.ChangeCollectionMembershipAsync([Album("failed")], false, CancellationToken.None);
        Check(!explicitRemoval.Added && explicitRemoval.Items.Count == 0, "Delete nie wykonał jawnego usunięcia.");
    }

    private static void TestContext()
    {
        var context = new TidalInteractionContext(7, "tidal", "Album", "one", false);
        Check(context.CanPresent(context, true), "Bieżąca odpowiedź została odrzucona.");
        foreach (var changed in new[] { context with { NavigationVersion = 8 }, context with { SessionId = "radio" },
                     context with { View = "Kolejka" }, context with { ItemId = "two" }, context with { PlayerActive = true } })
            Check(!context.CanPresent(changed, true), "Spóźniona odpowiedź może zmienić obcy kontekst.");
        Check(!context.CanPresent(context, false), "Odpowiedź przejmuje nieaktywne okno lub dialog.");
        Check(context.CanAnnounceCollectionOutcome(context with { NavigationVersion = 8, ItemId = "two" }, true),
            "Usunięcie wiersza przez pierwszą operację wyciszyło potwierdzenie drugiego skrótu.");
        Check(!context.CanAnnounceCollectionOutcome(context with { SessionId = "radio" }, true)
              && !context.CanAnnounceCollectionOutcome(context, false), "Wynik zapisu mówi w obcym oknie lub sesji.");
        Check(SearchWindow.PreservesBrowserLocation([new("tidal", Album())], SearchResultAction.Library), "Dodanie albumu zmienia widok.");
        Check(SearchWindow.PreservesBrowserLocation([new("tidal", Album())], SearchResultAction.Favorite), "Odrzucony skrót też zmienia widok.");
        Check(!SearchWindow.PreservesBrowserLocation([new("tidal", Album())], SearchResultAction.Open), "Enter nie może otworzyć wyniku.");
    }

    private static void TestSelectionRefresh()
    {
        var list = new ListBox { ItemsSource = new[] { Album(), Album("two") }, SelectedIndex = 0 };
        var refresh = new ListSelectionRefresh();
        long version = 7;
        void Invalidate() => version++;
        string? SelectedId() => (list.SelectedItem as MediaItem)?.Id;
        TidalInteractionContext Context() => new(version, "tidal", "Album", SelectedId(), false);
        list.SelectionChanged += (_, _) => refresh.SelectionChanged(Invalidate);
        void ReplaceRows()
        {
            list.ItemsSource = new[] { Album(), Album("two") };
            list.SelectedIndex = 0;
        }

        // Reproduce the old path: the same logical row is restored, but WPF
        // fires selection events and a waiting container response is rejected.
        var before = Context();
        ReplaceRows();
        Check(!before.CanPresent(Context(), true) && before.ItemId == SelectedId(),
            "Test nie odtworzył anulowania wejścia przez przebudowę tej samej listy.");

        before = Context();
        refresh.Run(SelectedId, ReplaceRows, Invalidate);
        Check(before.CanPresent(Context(), true), "Odświeżenie tego samego wiersza anulowało pierwsze otwarcie wykonawcy.");
        Check(!before.CanPresent(Context() with { NavigationVersion = version + 1 }, true),
            "Escape lub nowa nawigacja nie unieważnia odpowiedzi po odświeżeniu.");
        Check(!before.CanPresent(Context(), false), "Odświeżenie pozwala przejąć nieaktywne okno.");

        list.SelectionMode = SelectionMode.Extended;
        list.SelectedItems.Add(list.Items[1]);
        before = Context();
        refresh.Run(SelectedId, () =>
        {
            refresh.Run(SelectedId, ReplaceRows, Invalidate);
            list.SelectedItems.Clear();
            list.SelectedItems.Add(list.Items[0]);
            list.SelectedItems.Add(list.Items[1]);
        }, Invalidate);
        Check(list.SelectedItems.Count == 2 && before.CanPresent(Context(), true),
            "Zagnieżdżone odświeżenie lub odtworzenie wielokrotnego zaznaczenia anulowało odpowiedź.");

        before = Context();
        list.SelectedIndex = 1;
        list.SelectedIndex = 0;
        Check(!before.CanPresent(Context(), true), "Ręczne odejście i powrót pozwala spóźnionej odpowiedzi przejąć widok.");
        before = Context();
        refresh.Run(SelectedId, () => { list.ItemsSource = new[] { Album("two") }; list.SelectedIndex = 0; }, Invalidate);
        Check(!before.CanPresent(Context(), true), "Usunięcie wybranego elementu nie anuluje odpowiedzi.");
        before = Context();
        try
        {
            refresh.Run(SelectedId, () => { list.ItemsSource = Array.Empty<MediaItem>(); throw new InvalidOperationException("test-refresh"); }, Invalidate);
        }
        catch (InvalidOperationException exception) when (exception.Message == "test-refresh") { }
        Check(!before.CanPresent(Context(), true), "Pusta lista po błędzie zachowała nieaktualne żądanie.");
        before = Context();
        ReplaceRows();
        Check(!before.CanPresent(Context(), true), "Wyjątek pozostawił wyłączone śledzenie nawigacji.");
    }

    private static void TestListFocus()
    {
        var filter = new TextBox();
        var list = new ListBox { ItemsSource = new[] { "First", "Second" }, SelectedIndex = 1 };
        var panel = new StackPanel(); panel.Children.Add(filter); panel.Children.Add(list);
        var window = new Window { Content = panel, Width = 400, Height = 200, ShowInTaskbar = false };
        try
        {
            window.Show(); window.Activate(); Drain(window.Dispatcher);
            void FocusRow()
            {
                list.UpdateLayout();
                var row = (ListBoxItem)list.ItemContainerGenerator.ContainerFromIndex(list.SelectedIndex);
                row.Focus(); Keyboard.Focus(row);
            }
            FocusRow();
            ListRefreshFocus.Run(list, () => { list.ItemsSource = new[] { "First", "Replacement" }; list.SelectedIndex = 1; }, FocusRow);
            Check(Keyboard.FocusedElement is ListBoxItem && list.SelectedIndex == 1, "Przebudowa utraciła fokus wiersza.");
            ListRefreshFocus.Run(list, () => { list.ItemsSource = new[] { "Remaining" }; list.SelectedIndex = 0; }, FocusRow);
            Check(Keyboard.FocusedElement is ListBoxItem, "Usunięcie wiersza zostawiło fokus na liście.");
            filter.Focus(); Keyboard.Focus(filter);
            ListRefreshFocus.Run(list, () => { list.ItemsSource = new[] { "Updated" }; list.SelectedIndex = 0; }, FocusRow);
            Drain(window.Dispatcher);
            Check(filter.IsKeyboardFocused, "Odświeżenie zabrało fokus z filtra.");
        }
        finally { window.Close(); }
    }

    private static void TestSearch()
    {
        var sessions = new SessionManager(new AppSettings { LastSessionId = "tidal" });
        var album = Album(); sessions.FindSession("tidal")!.ReplaceItems([album]);
        int actions = 0;
        for (var iteration = 0; iteration < 2; iteration++)
        {
            var window = new SearchWindow(sessions, false,
                item => item.Title + (item.IsInLibrary ? ", w Bibliotece" : ""), item => item.Title, _ => "",
                (_, _, _, _) => { actions++; return null; }, new SearchQueryHistory(new SearchHistorySettings()), "tidal", () => { }, false);
            try
            {
                window.Show(); window.Activate(); Drain(window.Dispatcher);
                var query = (TextBox)window.FindName("SearchBox"); query.Text = "Test album";
                typeof(SearchWindow).GetMethod("RunSearch", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, null);
                Drain(window.Dispatcher);
                var list = (ListBox)window.FindName("ResultsList");
                Check(list.Items.Count == 1, "Brak testowego albumu w wynikach.");
                list.SelectedIndex = 0; list.UpdateLayout();
                var row = (ListBoxItem)list.ItemContainerGenerator.ContainerFromIndex(0);
                row.Focus(); Keyboard.Focus(row);
                typeof(SearchWindow).GetMethod("CompleteSelected", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(window, [SearchResultAction.Library]);
                Drain(window.Dispatcher);
                Check(window.TidalCollectionActionRequested && window.LastDirectActionResult is null,
                    "Escape po dodaniu albumu otworzyłby wynik wyszukiwania.");
                var selectedRow = list.SelectedItem;
                window.UpdateTidalMembership(new HashSet<string> { "albums:one" });
                Drain(window.Dispatcher);
                Check(ReferenceEquals(list.SelectedItem, selectedRow) && row.IsKeyboardFocused,
                    "Aktualizacja etykiety odbudowała wiersz lub odebrała fokus.");
                Check(UIElementAutomationPeer.CreatePeerForElement(row)!.GetName().Contains("w Bibliotece")
                    && !selectedRow!.ToString()!.Contains("SearchResultRow"), "NVDA nie otrzymało aktualnej użytkowej etykiety.");
                window.UpdateTidalMembership(new HashSet<string>());
                Drain(window.Dispatcher);
                Check(!UIElementAutomationPeer.CreatePeerForElement(row)!.GetName().Contains("w Bibliotece"), "Pozostał stary stan członkostwa.");
                query.Focus(); Keyboard.Focus(query);
                window.AnnounceActionCompletion("Dodano do Biblioteki TIDAL", restoreResultFocus: false);
                Drain(window.Dispatcher);
                Check(query.IsKeyboardFocused, "Spóźniony komunikat zabrał fokus z nowego zapytania.");
            }
            finally { window.Close(); }
        }
        Check(actions == 2, "Polecenie nie dotarło do obsługi działania.");
    }

    private static void Drain(Dispatcher dispatcher)
    {
        var frame = new DispatcherFrame();
        dispatcher.BeginInvoke(() => frame.Continue = false, DispatcherPriority.ApplicationIdle);
        Dispatcher.PushFrame(frame);
    }
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }

    private sealed class MembershipHandler : HttpMessageHandler
    {
        internal readonly TaskCompletionSource FirstStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource ReleaseFirst = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly List<HttpMethod> Methods = [];
        internal bool FailNext;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Methods.Add(request.Method);
            if (Methods.Count == 1)
            {
                FirstStarted.TrySetResult();
                await ReleaseFirst.Task.WaitAsync(cancellationToken);
            }
            if (FailNext) { FailNext = false; return new(HttpStatusCode.NotFound); }
            return new(HttpStatusCode.NoContent);
        }
    }
}
