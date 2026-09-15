using AccessibleMediaController.Core.Configuration;

namespace AccessibleMediaController.Core.Presentation;

/// <summary>
/// Opisy wpisow historii nagrywania radia.
///
/// Zgloszenie Michala 15.09.2026: widok "Nagrane pliki" pokazywal wylacznie
/// nagrania UDANE, bo powstawal z biblioteki lokalnej. Nagranie przerwane albo
/// takie, ktore w ogole nie utworzylo pliku, przepadalo bez sladu - uzytkownik
/// nie mial skad wiedziec, ze proba sie odbyla i dlaczego nie wyszla.
///
/// Dlatego historia jest osobna od biblioteki i zawiera tez wpisy nieudane.
/// Dla czytnika ekranu skutek MUSI byc slyszalny na poczatku wiersza, przed
/// nazwa stacji, zeby nie trzeba bylo dosluchiwac do konca; sama data i nazwa
/// pliku nie odroznia nagrania gotowego od przerwanego.
///
/// Regula siedzi w Core, zeby dala sie zmierzyc testem bez uruchamiania WPF.
/// </summary>
public static class RadioRecordingHistoryLabels
{
    /// <summary>Krotka etykieta skutku, czytana jako pierwsza w wierszu listy.</summary>
    public static string OutcomeLabel(RadioRecordingOutcome outcome) => outcome switch
    {
        RadioRecordingOutcome.Completed => "Nagrane",
        RadioRecordingOutcome.Stopped => "Zatrzymane",
        RadioRecordingOutcome.Interrupted => "Przerwane",
        RadioRecordingOutcome.Failed => "Nieudane",
        _ => "Nagrane"
    };

    /// <summary>Czy wpis wskazuje plik, ktory mozna odtworzyc Enterem.</summary>
    public static bool HasPlayableFile(RadioRecordingHistorySettings entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return entry.Outcome is RadioRecordingOutcome.Completed
                or RadioRecordingOutcome.Stopped
                or RadioRecordingOutcome.Interrupted
            && !string.IsNullOrWhiteSpace(entry.Path);
    }

    /// <summary>
    /// Wiersz listy. Skutek na poczatku, potem stacja, czas i - gdy jest -
    /// powod niepowodzenia. Bez nazwy pliku, bo ta bywa dluga i zaglusza reszte.
    /// </summary>
    public static string Describe(RadioRecordingHistorySettings entry, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(entry);
        var parts = new List<string>
        {
            OutcomeLabel(entry.Outcome),
            string.IsNullOrWhiteSpace(entry.StationName) ? "Nieznana stacja" : entry.StationName
        };

        var finished = new DateTime(entry.FinishedUtcTicks, DateTimeKind.Utc).ToLocalTime();
        parts.Add(FormatWhen(finished, nowUtc.ToLocalTime()));

        if (!string.IsNullOrWhiteSpace(entry.ScheduleName))
            parts.Add($"harmonogram {entry.ScheduleName}");

        if (entry.SavedFileCount > 1)
            parts.Add($"plików {entry.SavedFileCount}");

        if (!string.IsNullOrWhiteSpace(entry.Reason))
            parts.Add(entry.Reason.TrimEnd('.'));

        return string.Join(", ", parts);
    }

    /// <summary>
    /// Komunikat po Enterze na wpisie, ktory nie ma pliku do odtworzenia.
    /// Mowi wprost, czego nie ma i dlaczego, zamiast milczec.
    /// </summary>
    public static string DescribeUnplayable(RadioRecordingHistorySettings entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        var station = string.IsNullOrWhiteSpace(entry.StationName)
            ? "nieznanej stacji"
            : entry.StationName;
        var reason = string.IsNullOrWhiteSpace(entry.Reason)
            ? "Nie zapisano pliku"
            : entry.Reason.TrimEnd('.');
        return entry.Outcome == RadioRecordingOutcome.Failed
            ? $"Nagranie {station} nie powstało. {reason}"
            : $"Brak pliku nagrania {station}. {reason}";
    }

    private static string FormatWhen(DateTime finishedLocal, DateTime nowLocal)
    {
        var date = finishedLocal.Date;
        var today = nowLocal.Date;
        if (date == today) return $"dziś {finishedLocal:HH:mm}";
        if (date == today.AddDays(-1)) return $"wczoraj {finishedLocal:HH:mm}";
        return finishedLocal.ToString("d MMMM, HH:mm");
    }
}
