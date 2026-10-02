using System.Windows.Threading;
using AccessibleMediaController.Core.Commands;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Sonos;

namespace AccessibleMediaController.Windows;

/// <summary>
/// POWROT DO RZECZYWISTEJ PODLISTY SONOSA po przelaczeniu sesji.
///
/// ZGLOSZONY BLAD: Ctrl+cyfra z wnetrza Moich stacji / Ulubionych / Playlist nie
/// przelaczala sesji wcale (modal wylacza okno glowne), a gdy uzytkownik zamknal
/// liste RECZNIE i wrocil Ctrl+cyfra do Sonosa, ladowal w KORZENIU sesji - nie w
/// tej podliscie, nie na tym wierszu.
///
/// CO TU JEST: dwie polowy jednej drogi.
///  1) <see cref="RequestSessionSwitchFromSonosSublist"/> - podlista oddaje gest,
///     my ZAPAMIETUJEMY miejsce i wykonujemy ZWYKLE polecenie zmiany sesji.
///  2) <see cref="TryReopenSonosSublistAfterSessionReturn"/> - wejscie w sesje
///     Sonos sprawdza, czy ktos czeka na powrot, i otwiera TE SAMA podliste
///     ISTNIEJACA droga Biblioteki.
///
/// ZADNEGO NOWEGO TORU: otwarcie idzie przez <c>OpenSonosLibraryCategory</c>,
/// czyli dokladnie ten kod, ktory obsluguje Enter na kategorii. Nie powstaje tu
/// drugi, rownolegly sposob pokazywania podlist.
/// </summary>
public partial class MainWindow
{
    /// <summary>
    /// ZADANIE powrotu, czekajace na nastepne wejscie w sesje Sonos. Trzymamy je w
    /// stanie sesji (<see cref="SessionNavigationState.SonosSublistReturn"/>), a
    /// to pole mowi tylko, ze powrot jest WLASNIE w toku - zeby otwarcie nie
    /// zapetlilo sie samo na sobie.
    /// </summary>
    private bool _sonosSublistReturnInProgress;

    /// <summary>Pomiar: ile razy powrot RZECZYWISCIE otworzyl podliste.</summary>
    internal int SonosSublistReopenedForTests { get; private set; }

    /// <summary>Pomiar: co aktualnie czeka na powrot (albo null).</summary>
    internal SonosSublistReturnState? SonosSublistReturnForTests =>
        _sessions is null ? null : GetSessionNavigationState(SonosSessionId).SonosSublistReturn;

    /// <summary>
    /// PRZELACZENIE SESJI ZLECONE Z PODLISTY. Publiczne dla okien podlist w tym
    /// samym zestawie; nie jest to globalny przechwyt - wola je TYLKO okno, ktore
    /// samo dostalo klawisz.
    ///
    /// Kolejnosc ma znaczenie: miejsce zapamietujemy PRZED zmiana sesji, bo po
    /// zmianie dom i cel moglyby byc juz inne i zapis opisywalby nieprawde.
    ///
    /// Samo przelaczenie oddajemy ZWYKLEMU <c>ExecuteCommand</c>: ten sam kod, ta
    /// sama zapowiedz sesji, te same granice. Zero skrotu obok routera.
    /// </summary>
    internal void RequestSessionSwitchFromSonosSublist(int slot, string categoryId, string? selectedRowId)
    {
        if (_isClosing || _sessions is null) return;

        var navigation = GetSessionNavigationState(SonosSessionId);
        navigation.SonosSublistReturn = SonosSublistReturnPolicy.Capture(
            categoryId,
            selectedRowId,
            _state.Sonos.SelectedHouseholdId,
            _state.Sonos.SelectedGroupId);

        // ZAMKNIECIE modalu konczy sie DOPIERO po powrocie ze ShowDialog, a to
        // nastepuje po tym wywolaniu. Przelaczenie musi wiec poczekac na pusty
        // stos modalny - inaczej nowa sesja rysowalaby sie pod wylaczonym oknem.
        //
        // CZEKAMY NA STAN, NIE NA PRIORYTET KOLEJKI. Pierwsza wersja wysylala to
        // jednym BeginInvoke na ApplicationIdle i przelaczenie NIE NASTEPOWALO:
        // kazda petla komunikatow, ktora konczy sie na Background (tak pompuja i
        // modale, i aparatura pomiaru), nigdy nie zdejmuje elementu o NIZSZYM
        // priorytecie. Dlatego sprawdzamy warunek i - jesli modal jeszcze zyje -
        // przekladamy sie na nastepna turę.
        PostSessionSwitchWhenModalsClosed(slot, attempt: 0);
    }

    /// <summary>
    /// Zlecenie zmiany sesji, PRZEKLADANE dopoki nad oknem glownym stoi modal.
    ///
    /// Granica prob jest celowa: gdyby okno z jakiegos powodu nigdy nie wrocilo do
    /// stanu uzywalnego, lepiej CICHO ODPUSCIC niz zostawic zadanie krecace sie w
    /// kolejce dyspozytora przez cale zycie programu.
    /// </summary>
    private void PostSessionSwitchWhenModalsClosed(int slot, int attempt)
    {
        const int maxAttempts = 400;
        Dispatcher.BeginInvoke(
            () =>
            {
                if (_isClosing || _sessions is null) return;
                if (HasVisibleOwnedModal() && attempt < maxAttempts)
                {
                    PostSessionSwitchWhenModalsClosed(slot, attempt + 1);
                    return;
                }

                ExecuteCommand(CommandIds.SessionSlot(slot));
            },
            DispatcherPriority.Background);
    }

    /// <summary>
    /// Czy nad oknem glownym stoi jeszcze JAKIES widoczne okno naleznace do nas -
    /// czyli czy stos modalny zdazyl zejsc.
    /// </summary>
    private bool HasVisibleOwnedModal()
    {
        foreach (System.Windows.Window owned in OwnedWindows)
        {
            if (owned.IsVisible) return true;
        }

        return false;
    }

    /// <summary>
    /// WYJSCIE Z PODLISTY BEZ PRZELACZANIA SESJI (Escape, przycisk Zamknij).
    /// Czysci zadanie powrotu: uzytkownik sam zamknal liste, wiec nastepne wejscie
    /// w sesje Sonos NIE MA prawa otwierac jej ponownie.
    /// </summary>
    internal void ClearSonosSublistReturn()
    {
        if (_sessions is null) return;
        GetSessionNavigationState(SonosSessionId).SonosSublistReturn = null;
    }

    /// <summary>
    /// POWROT: czy ktos czeka na otwarcie podlisty, i jesli tak - otworz TE SAMA.
    ///
    /// Zadanie ZDEJMUJEMY ze stanu PRZED otwarciem. Gdyby otwarcie odmowilo (brak
    /// celu, nieaktywne okno), nie zostaje wiszace zadanie, ktore probowaloby
    /// znowu przy kazdym wejsciu.
    ///
    /// Decyzje "czy wolno" podejmuje <see cref="SonosSublistReturnPolicy"/>: po
    /// zmianie domu albo celu zapamietane miejsce przepada i uzytkownik zostaje w
    /// korzeniu sesji, a nie w liscie opisujacej inny cel.
    /// </summary>
    private bool TryReopenSonosSublistAfterSessionReturn()
    {
        if (_isClosing || _sessions is null) return false;
        if (_sonosSublistReturnInProgress) return false;
        if (!IsSonosSession(_sessions.Current.Id)) return false;
        if (_playerViewActive) return false;

        var navigation = GetSessionNavigationState(SonosSessionId);
        var remembered = navigation.SonosSublistReturn;
        navigation.SonosSublistReturn = null;
        if (!SonosSublistReturnPolicy.CanReopen(
            remembered,
            _state.Sonos.SelectedHouseholdId,
            _state.Sonos.SelectedGroupId))
        {
            return false;
        }

        _sonosSublistReturnInProgress = true;
        try
        {
            // WIERSZ do zaznaczenia oddajemy oknu podlisty; samo otwarcie idzie
            // ISTNIEJACA droga kategorii Biblioteki.
            _sonosSublistPendingRowId = remembered!.SelectedRowId;
            SonosSublistReopenedForTests++;
            // WIERSZ bierzemy z ISTNIEJACEGO opisu kategorii, nie skladamy wlasnego:
            // nieznany identyfikator odpadl juz w polityce powyzej.
            var category = SonosLibraryPresentation.DescribeCategories()
                .FirstOrDefault(row => string.Equals(
                    row.CategoryId, remembered.CategoryId, StringComparison.Ordinal));
            if (category is null) return false;
            OpenSonosLibraryCategory(category);
            return true;
        }
        finally
        {
            _sonosSublistReturnInProgress = false;
        }
    }

    /// <summary>
    /// WIERSZ, ktory nowo otwierana podlista ma zaznaczyc.
    ///
    /// UWAGA NA CZAS ZYCIA: Ulubione i Playlisty otwieraja sie DROGA ASYNCHRONICZNA
    /// - <c>OpenSonosLibraryCategory</c> wraca, zanim okno powstanie. Dlatego pola
    /// NIE WOLNO czyscic w <c>finally</c> powyzej (tak bylo najpierw i wiersz ginal,
    /// zanim ktokolwiek go przeczytal). Czysci je DOPIERO odbiorca, przez
    /// <see cref="ConsumeSonosSublistPendingRowId"/>.
    /// </summary>
    private string? _sonosSublistPendingRowId;

    /// <summary>
    /// ODBIOR wiersza do zaznaczenia - JEDEN RAZ. Kolejne, zwykle otwarcie listy
    /// dostanie juz null i zostanie na pierwszym wierszu.
    /// </summary>
    internal string? ConsumeSonosSublistPendingRowId()
    {
        var pending = _sonosSublistPendingRowId;
        _sonosSublistPendingRowId = null;
        return pending;
    }

    internal string? SonosSublistPendingRowId => _sonosSublistPendingRowId;
}
