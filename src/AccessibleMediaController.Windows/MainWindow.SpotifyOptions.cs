using System.Windows;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Playback;
using AccessibleMediaController.Core.Sessions;
using AccessibleMediaController.Core.Spotify;

namespace AccessibleMediaController.Windows;

/// <summary>
/// Opcje odtwarzania Spotify: pojedynczy utwor lub odcinek (Alt+Shift+Enter),
/// album lub podcast, oraz cala sesja (Ctrl+Alt+Enter).
///
/// ZGLOSZENIE Michala 18.09.2026: "Spotify ALT+SHIFT+ENTER jeszcze nie
/// podlaczone". Skrot trafial w zaslepke "Opcje elementu zostana udostepnione
/// przez adapter tej usługi", a sesja Spotify i tak pamietala pozycje ZAWSZE,
/// bo nie dostawala funkcji rozstrzygajacej pamiec pozycji.
///
/// Osobny plik czesciowy, zeby zmiany opcji Spotify nie zderzaly sie z
/// rownoczesna praca nad wejsciem, nawigacja i mostkiem odtwarzacza.
/// </summary>
public partial class MainWindow
{
    /// <summary>
    /// Czy wyjscie danej sesji w ogole potrafi przetwarzac dzwiek. Sesja bez
    /// tej zdolnosci NIE MOZE wystawiac normalizacji, lagodnych przejsc ani
    /// ciszy miedzy nagraniami: zapis trafialby do Audio.OverridesBySession,
    /// gdzie nikt by go dla tej sesji nie odczytal, a uzytkownik czytnika
    /// uslyszalby ustawienie, ktore nic nie robi.
    ///
    /// Spotify gra przez wlasny odtwarzacz uslugi (Web Playback SDK w
    /// osadzonej przegladarce), nie przez nasz lancuch DSP.
    /// </summary>
    private bool SessionSupportsAudioProcessing(string? sessionId) =>
        SessionPlaybackOptionsEditor.Describe(sessionId).SupportsAudioProcessing;

    /// <summary>
    /// Czy sesja obsluguje zmiane predkosci. Spotify: NIE - Web Playback SDK
    /// nie udostepnia predkosci, wiec SpotifyMediaOutput.SupportsPlaybackRate
    /// zwraca false i SetPlaybackRate niczego nie zmienia.
    /// </summary>
    private bool SessionSupportsPlaybackRate(string? sessionId) =>
        !SpotifyPlaybackSettingsResolver.IsSpotifySession(sessionId);

    /// <summary>
    /// Ustawienia, ktore okno sesji bez DSP musi zachowac nietkniete. Bez tego
    /// zapisanie opcji sesji Spotify wymazywaloby wybory zrobione wczesniej -
    /// ten sam bezpiecznik, ktory ma WiiM przy wstrzymywaniu po wyjsciu.
    /// </summary>
    private SessionPlaybackAudioOverrides? SpotifySessionAudioOverrides(string? sessionId) =>
        SessionSupportsAudioProcessing(sessionId)
            ? null
            : _state.Settings.Audio.OverridesBySession.GetValueOrDefault(sessionId ?? string.Empty);

    /// <summary>
    /// Opcje odtwarzania pojedynczej pozycji Spotify albo albumu/podcastu
    /// (Alt+Shift+Enter). Okno wystawia WYLACZNIE pamiec pozycji, bo to jedyne
    /// ustawienie, ktore w Spotify faktycznie dziala.
    /// </summary>
    private void ShowSpotifyItemPlaybackOptions(MediaItem item)
    {
        var settings = _state.Settings;
        var sessionId = ActionSession.Id;

        // Album i podcast dostaja ustawienie pojemnika, ktore obejmuje
        // wszystkie ich utwory lub odcinki. Utwor i odcinek dostaja ustawienie
        // wlasne, nadrzedne wobec pojemnika.
        var container = item.Kind is MediaItemKind.Album or MediaItemKind.Podcast;
        var target = container
            ? ItemPlaybackOptionsTarget.SpotifyContainer
            : ItemPlaybackOptionsTarget.SpotifyItem;
        var current = container
            ? SpotifyPlaybackSettingsResolver.ResolveContainerMode(settings, item, sessionId)
            : SpotifyPlaybackSettingsResolver.ResolveItemMode(settings, item, sessionId);

        var dialog = new ItemPlaybackOptionsWindow(
            item.Title,
            current,
            playbackRateOverride: null,
            loudnessNormalizationOverride: null,
            smoothTrackTransitionsOverride: null,
            interTrackSilenceMillisecondsOverride: null,
            target: target,
            showAudioProcessingOptions: false,
            showPlaybackRateOption: false)
        {
            Owner = this
        };
        if (dialog.ShowDialog() != true)
        {
            RestoreItemActionFocus();
            return;
        }

        var chosen = dialog.SelectedResumePositionMode;
        if (container)
        {
            SpotifyPlaybackSettingsResolver.SetContainerMode(settings, item, chosen, sessionId);
        }
        else
        {
            SpotifyPlaybackSettingsResolver.SetItemMode(settings, item, chosen, sessionId);
        }

        ApplySpotifyResumePolicyToLivePlayback(item, sessionId);
        var persisted = QueueStateSave(announceFailure: true);
        if (_playerViewActive) UpdatePlayerView(true);
        UpdatePlaybackStatusBar();
        if (persisted)
        {
            Announce(
                $"Zapisano opcje: {item.Title}. "
                + (container
                    ? DescribeSpotifyContainerMode(settings, item, sessionId)
                    : SpotifyPlaybackSettingsResolver.DescribeItemMode(settings, item, sessionId)));
        }
        RestoreItemActionFocus();
    }

    private static string DescribeSpotifyContainerMode(AppSettings settings, MediaItem item, string sessionId) =>
        SpotifyPlaybackSettingsResolver.ResolveContainerMode(settings, item, sessionId) switch
        {
            ResumePositionMode.Remember => "Pamiętaj pozycję odtwarzania",
            ResumePositionMode.StartFromBeginning => "Zawsze od początku",
            _ => SpotifyPlaybackSettingsResolver.ShouldRemember(settings, item, sessionId)
                ? "Jak ustawienie sesji Spotify: pamiętaj pozycję odtwarzania"
                : "Jak ustawienie sesji Spotify: zawsze od początku"
        };

    /// <summary>
    /// Nowy wybor musi zadzialac NATYCHMIAST, nie od nastepnego uruchomienia.
    /// Wylaczenie pamieci pozycji czysci to, co jest juz zapamietane w sesji i
    /// w trwalym stanie; wlaczenie przywraca zapisana pozycje do sesji.
    /// </summary>
    private void ApplySpotifyResumePolicyToLivePlayback(MediaItem item, string sessionId)
    {
        var session = _sessions.FindSession(sessionId);
        if (session is null) return;
        var settings = _state.Settings;
        var affected = session.Items
            .Where(candidate => IsAffectedBySpotifySetting(candidate, item))
            .ToArray();

        foreach (var candidate in affected)
        {
            if (SpotifyPlaybackSettingsResolver.ShouldRemember(settings, candidate, sessionId))
            {
                var stored = SpotifyPlaybackSettingsResolver.ResolvePosition(settings, candidate, sessionId);
                if (stored > TimeSpan.Zero) session.SetRememberedPosition(candidate.Id, stored);
            }
            else
            {
                session.ClearRememberedPosition(candidate.Id);
                SpotifyPlaybackSettingsResolver.StorePosition(settings, candidate, TimeSpan.Zero, sessionId);
            }
        }
    }

    private static bool IsAffectedBySpotifySetting(MediaItem candidate, MediaItem changed)
    {
        var changedKey = SpotifyPlaybackSettingsResolver.StorageKey(changed);
        if (changedKey.Length > 0
            && string.Equals(
                SpotifyPlaybackSettingsResolver.StorageKey(candidate),
                changedKey,
                StringComparison.Ordinal))
        {
            return true;
        }

        // Zmiana na albumie lub podcascie obejmuje jego utwory i odcinki.
        var changedContainer = SpotifyPlaybackSettingsResolver.ContainerKey(changed);
        if (string.IsNullOrEmpty(changedContainer)) return false;
        return string.Equals(
            SpotifyPlaybackSettingsResolver.ContainerKey(candidate),
            changedContainer,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// Zapisuje pozycje odtwarzania Spotify do TRWALEGO stanu, zeby przezyla
    /// zamkniecie programu i odswiezenie biblioteki. Wywolywane cyklicznie z
    /// zegara odtwarzacza oraz przy przelaczeniu pozycji.
    /// </summary>
    private void CaptureSpotifyPlaybackPosition()
    {
        foreach (var session in _sessions.Sessions.Where(session =>
                     SpotifyPlaybackSettingsResolver.IsSpotifySession(session.Id)))
        {
            foreach (var item in session.Items)
            {
                if (session.RememberedPositions.TryGetValue(item.Id, out var remembered))
                    SpotifyPlaybackSettingsResolver.StorePosition(_state.Settings, item, remembered, session.Id);
            }
            if (session.HasCurrentItem)
                SpotifyPlaybackSettingsResolver.StorePosition(
                    _state.Settings, session.CurrentItem, session.Position, session.Id);
        }
    }

    /// <summary>
    /// Wznawia pozycje pobrane z trwalego stanu po restarcie i po odswiezeniu
    /// biblioteki. Klucz zapisu nie zalezy od losowego MediaItem.Id, wiec
    /// pozycja trafia w te same utwory po ponownym pobraniu katalogu.
    /// </summary>
    private void RestoreSpotifyRememberedPositions()
    {
        foreach (var session in _sessions.Sessions.Where(session =>
                     SpotifyPlaybackSettingsResolver.IsSpotifySession(session.Id)))
        {
            foreach (var item in session.Items)
            {
                var stored = SpotifyPlaybackSettingsResolver.ResolvePosition(_state.Settings, item, session.Id);
                if (stored > TimeSpan.Zero) session.SetRememberedPosition(item.Id, stored);
            }
        }
    }

    /// <summary>
    /// Cykliczny zapis pozycji Spotify. Ten sam prog co dla plikow lokalnych i
    /// podcastow (15 sekund), zeby odtwarzanie nie generowalo zapisu na dysk
    /// przy kazdym tyknieciu zegara.
    /// </summary>
    private void SaveSpotifyStateIfDue()
    {
        var current = _sessions.Sessions
            .Where(session => SpotifyPlaybackSettingsResolver.IsSpotifySession(session.Id) && session.HasCurrentItem)
            .ToDictionary(session => session.Id,
                session => (ItemId: session.CurrentItem.Id, PositionTicks: session.Position.Ticks), StringComparer.Ordinal);
        if (current.Count == _lastSavedSpotifyPositions.Count
            && current.All(pair => _lastSavedSpotifyPositions.TryGetValue(pair.Key, out var old) && old == pair.Value)) return;
        if (DateTime.UtcNow - _lastSpotifyStateSaveUtc < TimeSpan.FromSeconds(15)) return;

        CaptureSpotifyPlaybackPosition();
        if (!QueueStateSave()) return;
        _lastSavedSpotifyPositions = current;
        _lastSpotifyStateSaveUtc = DateTime.UtcNow;
    }

    private Dictionary<string, (string ItemId, long PositionTicks)> _lastSavedSpotifyPositions = new(StringComparer.Ordinal);
    private DateTime _lastSpotifyStateSaveUtc = DateTime.MinValue;
}
