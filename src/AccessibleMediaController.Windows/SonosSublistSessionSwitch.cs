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

        // WLASCICIEL moze byc DALEJ NIZ JEDEN KROK: podlisty otwiera Biblioteka,
        // ktora sama jest modalem nad oknem glownym (Biblioteka -> Moje stacje).
        var owner = FindOwningMainWindow(window);
        if (owner is null) return true;

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
    /// OKNO GLOWNE w gorze lancucha wlascicieli. Petla, nie jeden krok, bo miedzy
    /// podlista a oknem glownym stoi jeszcze modal Biblioteki. Limit krokow chroni
    /// przed cyklem we wlascicielach.
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
