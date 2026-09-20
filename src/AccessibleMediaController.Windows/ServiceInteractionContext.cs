namespace AccessibleMediaController.Windows;

/// <summary>
/// Kontekst interakcji jednej usludze strumieniowej, WSPOLNY dla TIDAL i Spotify.
///
/// Zadanie moze po wyjsciu uzytkownika uzupelnic swoj cache danych, ale NIE MOZE
/// przestawic nowego kontekstu interfejsu: czytnik ekranu przeczytalby wtedy co
/// innego, niz uzytkownik wybral. Dlatego <see cref="PlayerActive"/> jest czescia
/// tozsamosci kontekstu - wejscie do odtwarzacza tez jest zmiana miejsca.
/// </summary>
internal sealed record ServiceInteractionContext(
    long NavigationVersion,
    string SessionId,
    string View,
    string? ItemId,
    bool PlayerActive)
{
    internal bool CanPresent(ServiceInteractionContext current, bool windowAvailable) =>
        windowAvailable && this == current;

    // Membership completion only speaks; it never navigates. Its own preceding
    // write may remove the selected row, so do not suppress the next confirmation.
    internal bool CanAnnounceCollectionOutcome(ServiceInteractionContext current, bool windowAvailable) =>
        windowAvailable && SessionId == current.SessionId && View == current.View
        && PlayerActive == current.PlayerActive;
}
