using System.Windows;
using System.Windows.Input;

namespace AccessibleMediaController.Windows;

/// <summary>
/// PRZELACZANIE SESJI Z WNETRZA PODLISTY SONOSA (Moje stacje, Ulubione, Playlisty).
///
/// DLACZEGO TO TU SIEDZI: podlisty sa MODALNE, a modal WYLACZA okno glowne jako
/// swojego wlasciciela. Router skrotow <c>MainWindow</c> nie dostaje wtedy ani
/// jednego klawisza, wiec Ctrl+cyfra NIE przelaczala sesji - uzytkownik musial
/// najpierw RECZNIE zamknac liste. Dokladnie to bylo zgloszone.
///
/// DROGA, KTORA WYBRANO: ten sam wzorzec, ktorym okna podlist obsluguja juz
/// Ctrl+Alt+Shift+P (przypisanie presetu) - WLASNY <c>PreviewKeyDown</c> okna
/// oddaje gest wlascicielowi. ZERO globalnych przechwytow i zero nadpisywania
/// polecen w obcych oknach: slyszymy wylacznie klawisze, ktore przyszly DO NAS.
///
/// CZEGO TO NIE ROBI: nie zamyka okien edycji i nie obchodzi ich pytania o
/// niezapisane dane - edytor stacji jest osobnym modalem NAD podlista i to on ma
/// wtedy fokus, wiec ten kod go nie widzi.
///
/// CO JEST ZMIERZONE, A CO NIE: ze podlisty sa modalne i ze Ctrl+cyfra dociera
/// TUTAJ - tak, mierzy to pomiar nawigacji. Twierdzenie, ze Biblioteka zostaje
/// ZYWYM wlascicielem podlisty, bylo NIEPRAWDA: Biblioteka zamyka sie PRZED
/// otwarciem kategorii, wiec wlascicielem podlisty jest okno glowne.
/// </summary>
internal static class SonosSublistSessionSwitch
{
    /// <summary>
    /// Czy gest to CTRL+CYFRA, i ktory to slot sesji.
    ///
    /// Cyfry glowne i numeryczne traktujemy tak samo - dokladnie jak router
    /// skrotow okna glownego, zeby ta sama reka dawala ten sam wynik w obu
    /// miejscach. ZERO oznacza liste sesji i NIE jest tu obslugiwane: otwieranie
    /// kolejnego okna wyboru z wnetrza modalu nie jest tym, o co proszono.
    /// </summary>
    internal static bool TryGetSessionSlot(KeyEventArgs e, out int slot)
    {
        ArgumentNullException.ThrowIfNull(e);
        slot = 0;

        // Z ALTEM WPF podaje Key.System; cyfry z samym Ctrl przychodza normalnie,
        // ale normalizacja jest darmowa i chroni przed niespodzianka ukladu.
        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        // DOKLADNIE Ctrl: Ctrl+Shift+cyfra nalezy do presetow radiowych, a
        // Ctrl+Alt+cyfra do innych drog. Nie zabieramy im gestu.
        if (Keyboard.Modifiers != ModifierKeys.Control) return false;

        var digit = key switch
        {
            >= Key.D1 and <= Key.D9 => (int)key - (int)Key.D0,
            >= Key.NumPad1 and <= Key.NumPad9 => (int)key - (int)Key.NumPad0,
            _ => 0
        };
        if (digit == 0) return false;
        slot = digit;
        return true;
    }

    /// <summary>
    /// OBSLUGA gestu w oknie podlisty: pochlaniamy klawisz, zapamietujemy miejsce
    /// i oddajemy przelaczenie WLASCICIELOWI, po czym zamykamy wlasne okno.
    ///
    /// Zamkniecie jest konieczne, nie kosmetyczne: dopoki modal zyje, okno glowne
    /// jest wylaczone i nie moze pokazac nowej sesji. Autopowtarzanie klawisza
    /// odrzucamy PRZED jakimkolwiek dzialaniem, zeby przytrzymanie nie zlozylo
    /// serii przelaczen.
    /// </summary>
    internal static bool TryHandle(
        Window window,
        KeyEventArgs e,
        string categoryId,
        Func<string?> selectedRowId)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(e);
        ArgumentNullException.ThrowIfNull(selectedRowId);

        if (!TryGetSessionSlot(e, out var slot)) return false;

        // KLAWISZ ZATRZYMANY zawsze, gdy go rozpoznalismy - takze przy
        // autopowtarzaniu i wtedy, gdy wlasciciela nie ma. Inaczej gest poszedlby
        // dalej i mogl wpasc w domyslny przycisk okna.
        e.Handled = true;
        if (e.IsRepeat) return true;

        // WLASCICIELEM PODLISTY JEST OKNO GLOWNE, nie Biblioteka: Biblioteka
        // ZAMYKA SIE PRZED wywolaniem zwrotnym otwarcia kategorii
        // (SonosLibraryWindow.OpenSelected), a podlisty ustawiaja Owner = okno
        // glowne. Zagniezdzony stos WYWOLAN nie dowodzi zywego lancucha OKIEN.
        //
        // Petla zostaje mimo to: jest tania, a zadne miejsce w kodzie nie
        // GWARANTUJE, ze kazda przyszla podlista bedzie wisiec dokladnie jeden
        // krok od okna glownego. Limit krokow chroni przed cyklem we wlascicielach.
        var owner = FindOwningMainWindow(window);
        if (owner is null) return true;

        // CYFRA, KTORA DONIKAD NIE PROWADZI, NIE MA PRAWA ZWINAC LISTY.
        // Slot nieprzypisany konczy sie sama zapowiedzia "Sesja N nieprzypisana",
        // a slot BIEZACEJ sesji nie zmienia niczego. Gdybysmy mimo to zamkneli
        // stos modalny, uzytkownik stracilby podliste w zamian za nic.
        if (!owner.ShouldLeaveSonosSublistForSlot(slot))
        {
            owner.AnnounceSonosSublistSlotRefusal(slot);
            return true;
        }

        owner.RequestSessionSwitchFromSonosSublist(slot, categoryId, selectedRowId());

        // CALY stos modalny musi zejsc, nie tylko nasze okno: dopoki ZYJE
        // ktorykolwiek modal nad oknem glownym, okno glowne jest WYLACZONE i nowa
        // sesja nie moglaby sie pokazac. Zamykamy od wierzchu w dol, az do okna
        // glownego - jego NIE zamykamy.
        for (Window? current = window; current is not null and not MainWindow;)
        {
            var next = current.Owner;
            try { current.Close(); } catch (InvalidOperationException) { }
            current = next;
        }

        return true;
    }

    /// <summary>
    /// TRANSPORT I PRESETY Z WNETRZA PODLISTY - ta sama droga, co Ctrl+cyfra wyzej.
    ///
    /// DLACZEGO TU: modal WYLACZA okno glowne, wiec jego router nie widzi ani
    /// Spacji, ani Ctrl+Shift+cyfry. Zgloszono, ze dzialaja wylacznie w glownym,
    /// niezablokowanym oknie.
    ///
    /// CO ROBI, A CZEGO NIE:
    ///   * Spacja = pauza/wznowienie BIEZACEGO materialu (CommandIds.PlayPause),
    ///     dokladnie jak w reszcie AMC. Enter NIE jest tu ruszany - uruchamianie
    ///     WSKAZANEJ pozycji zostaje tam, gdzie bylo, w kazdym oknie podlisty.
    ///     Spacja nie staje sie drugim Enterem.
    ///   * Ctrl+Shift+cyfra = ISTNIEJACE polecenie presetu, przez ten sam
    ///     MainWindowShortcutRouter.ResolveDigit, co okno glowne - wiec 0, minus
    ///     i rowna sie dzialaja zgodnie z mapa, bez drugiej mapy klawiszy.
    ///   * PODLISTA ZOSTAJE OTWARTA: nie zwijamy stosu modalnego, bo uzytkownik
    ///     prosil o ZACHOWANIE tego samego wiersza i fokusu. To rozni ten gest od
    ///     Ctrl+cyfra, ktore sesje ZMIENIA i dlatego musi liste zamknac.
    ///   * ZERO globalnego hooka: slyszymy tylko klawisze, ktore przyszly do nas.
    ///   * Pole tekstowe (filtr, edycja) ma pierwszenstwo dla Spacji - inaczej
    ///     nie dalo by sie wpisac odstepu. Ctrl+Shift+cyfra nie jest znakiem,
    ///     wiec jej nie dotyczy.
    /// </summary>
    internal static bool TryHandleTransportAndPresets(Window window, KeyEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(e);

        var owner = FindOwningMainWindow(window);
        if (owner is null) return false;

        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        // PRESET: pytamy ISTNIEJACY router, nie wlasna mape cyfr.
        if (owner.TryResolveSublistPresetSlot(key, out var presetSlot))
        {
            e.Handled = true;
            if (e.IsRepeat) return true;
            owner.ActivateSonosSublistPreset(presetSlot);
            return true;
        }

        if (key != Key.Space || Keyboard.Modifiers != ModifierKeys.None) return false;

        // ODSTEP W POLU TEKSTOWYM TO ZNAK, NIE TRANSPORT.
        if (Keyboard.FocusedElement is System.Windows.Controls.Primitives.TextBoxBase
            or System.Windows.Controls.PasswordBox)
        {
            return false;
        }

        e.Handled = true;
        if (e.IsRepeat) return true;
        owner.TogglePlaybackFromSonosSublist();
        return true;
    }

    /// <summary>
    /// OKNO GLOWNE w gorze lancucha wlascicieli. Petla, a nie jeden krok, bo
    /// podlista NIE MA zagwarantowanej odleglosci od okna glownego - dzis jest to
    /// jeden krok (Biblioteka zamyka sie przed otwarciem kategorii), ale kod tego
    /// nie wymusza. Limit krokow chroni przed cyklem we wlascicielach.
    /// </summary>
    private static MainWindow? FindOwningMainWindow(Window window)
    {
        var current = window.Owner;
        for (var step = 0; current is not null && step < 8; step++)
        {
            if (current is MainWindow main) return main;
            current = current.Owner;
        }

        return null;
    }
}
