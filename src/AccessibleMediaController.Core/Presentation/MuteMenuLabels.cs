namespace AccessibleMediaController.Core.Presentation;

/// <summary>
/// Nazwy pozycji wyciszenia w menu Odtwarzanie.
///
/// Zgloszenie Michala 15.09.2026: pozycja "Wycisz lub przywroc dzwiek biezacej
/// sesji" byla polem wyboru, wiec NVDA czytal nazwe opisujaca OBA kierunki naraz
/// plus stan "nieoznaczone". Z tego nie wynika, co zrobi Enter. Nazwa musi
/// nazywac SKUTEK jednego nacisniecia, a nie wymieniac obie mozliwosci.
///
/// Regula siedzi w Core, zeby dala sie zmierzyc testem bez uruchamiania WPF.
/// </summary>
public static class MuteMenuLabels
{
    public static string CurrentSession(bool muted) =>
        muted ? "Przywróć dźwięk bieżącej sesji" : "Wycisz bieżącą sesję";

    public static string AllSessions(bool muted) =>
        muted ? "Przywróć dźwięk wszystkich sesji AMC" : "Wycisz wszystkie sesje AMC";
}
