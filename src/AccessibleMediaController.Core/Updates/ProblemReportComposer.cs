using System.Text;

namespace AccessibleMediaController.Core.Updates;

/// <summary>Rodzaj zgloszenia wybierany z listy w oknie.</summary>
public enum ProblemReportKind
{
    NotWorking,
    FeatureRequest,
    Question
}

/// <summary>Dane, ktore uzytkownik wpisal, plus to, co program wie o sobie sam.</summary>
public sealed record ProblemReportInput(
    string Subject,
    string Body,
    ProblemReportKind Kind,
    string? Email = null,
    string? ExceptionTrace = null,
    IReadOnlyList<string>? LogTail = null);

/// <summary>Informacje techniczne dokladane automatycznie.</summary>
public sealed record ProblemReportEnvironment(
    string ApplicationVersion,
    string OperatingSystem,
    string Runtime,
    string Culture,
    string? ActiveSession = null,
    string? AudioOutput = null);

/// <summary>
/// Sklada tresc zgloszenia bledu.
///
/// Cala redakcja jest tutaj, a nie w oknie, z dwoch powodow: da sie ja zmierzyc
/// testem bez uruchamiania interfejsu, a ta sama tresc trafia do trzech miejsc
/// naraz (plik na dysku, tytul i opis zgloszenia na GitHubie, list e-mail) i
/// musi byc w kazdym z nich identyczna.
/// </summary>
public static class ProblemReportComposer
{
    /// <summary>Ile ostatnich wierszy dziennika dolaczamy. Awaria jest zawsze na koncu.</summary>
    public const int LogTailLines = 200;

    public static string DescribeKind(ProblemReportKind kind) => kind switch
    {
        ProblemReportKind.NotWorking => "Coś nie działa",
        ProblemReportKind.FeatureRequest => "Prośba o nową funkcję",
        ProblemReportKind.Question => "Pytanie lub inna uwaga",
        _ => "Coś nie działa"
    };

    /// <summary>Tytul zgloszenia na GitHubie.</summary>
    public static string ComposeTitle(ProblemReportInput input)
    {
        var subject = string.IsNullOrWhiteSpace(input.Subject) ? "(bez tematu)" : input.Subject.Trim();
        return $"[{DescribeKind(input.Kind)}] {subject}";
    }

    /// <summary>
    /// Pelna tresc zgloszenia. Kolejnosc jest celowa: najpierw to, co napisal
    /// czlowiek, potem slad bledu, na koncu dane techniczne i dziennik. Osoba
    /// czytajaca to czytnikiem ekranu dostaje najpierw rzecz, o ktora jej
    /// chodzilo, a nie pol ekranu numerow wersji.
    /// </summary>
    public static string ComposeBody(ProblemReportInput input, ProblemReportEnvironment environment)
    {
        var text = new StringBuilder();

        text.Append("Rodzaj zgłoszenia: ").Append(DescribeKind(input.Kind)).Append('\n');
        text.Append("Adres zwrotny: ")
            .Append(string.IsNullOrWhiteSpace(input.Email) ? "(nie podano)" : input.Email!.Trim())
            .Append('\n');
        text.Append('\n');

        var body = (input.Body ?? string.Empty).Trim();
        text.Append(body.Length > 0 ? body : "(opis nie został wpisany)").Append('\n');

        if (!string.IsNullOrWhiteSpace(input.ExceptionTrace))
        {
            text.Append('\n');
            text.Append("--- co zgłosił program ---\n");
            text.Append(input.ExceptionTrace!.TrimEnd()).Append('\n');
        }

        if (input.LogTail is { Count: > 0 })
        {
            var tail = input.LogTail.Count > LogTailLines
                ? input.LogTail.Skip(input.LogTail.Count - LogTailLines).ToList()
                : input.LogTail;
            text.Append('\n');
            text.Append("--- dziennik diagnostyczny, ostatnie ").Append(tail.Count).Append(" wierszy ---\n");
            foreach (var line in tail) text.Append(line).Append('\n');
        }

        text.Append('\n');
        text.Append("---\n");
        text.Append(ComposeEnvironment(environment));

        return text.ToString();
    }

    public static string ComposeEnvironment(ProblemReportEnvironment environment)
    {
        var text = new StringBuilder();
        text.Append("AMC ").Append(environment.ApplicationVersion).Append('\n');
        text.Append("Windows: ").Append(environment.OperatingSystem).Append('\n');
        text.Append(".NET: ").Append(environment.Runtime).Append('\n');
        text.Append("Ustawienia regionalne: ").Append(environment.Culture).Append('\n');
        if (!string.IsNullOrWhiteSpace(environment.ActiveSession))
            text.Append("Sesja: ").Append(environment.ActiveSession).Append('\n');
        if (!string.IsNullOrWhiteSpace(environment.AudioOutput))
            text.Append("Wyjście dźwięku: ").Append(environment.AudioOutput).Append('\n');
        return text.ToString();
    }

    /// <summary>Nazwa pliku kopii zapisywanej na dysku przed jakakolwiek wysylka.</summary>
    public static string ComposeFileName(DateTimeOffset moment) =>
        $"zgloszenie-{moment:yyyyMMdd-HHmmss}.txt";

    /// <summary>
    /// Adres formularza zgloszen z wypelnionym tytulem i trescia.
    ///
    /// GitHub odrzuca zbyt dlugie adresy (serwer odpowiada bledem 414), wiec
    /// tresc przycinamy i mowimy wprost, ze pelna wersja lezy w pliku. Milczace
    /// obciecie zabraloby czlowiekowi koncowke opisu bez jednego slowa.
    /// </summary>
    public static Uri ComposeIssueUri(string repositoryUrl, string title, string body, string? savedCopyPath)
    {
        const int maximumBodyCharacters = 5_000;
        var shortened = body;
        if (shortened.Length > maximumBodyCharacters)
        {
            shortened = shortened[..maximumBodyCharacters]
                        + "\n\n(dalszy ciąg zgłoszenia jest zbyt długi na formularz w przeglądarce"
                        + (string.IsNullOrWhiteSpace(savedCopyPath)
                            ? ")"
                            : $"; pełna treść jest w pliku {savedCopyPath})");
        }

        var builder = new UriBuilder(repositoryUrl.TrimEnd('/') + "/issues/new")
        {
            Query = "title=" + Uri.EscapeDataString(title) + "&body=" + Uri.EscapeDataString(shortened)
        };
        return builder.Uri;
    }
}
