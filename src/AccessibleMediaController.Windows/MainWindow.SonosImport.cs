using System.IO;
using System.Windows;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Sonos;
using AccessibleMediaController.Windows.Services;
using Microsoft.Win32;

namespace AccessibleMediaController.Windows;

/// <summary>
/// IMPORT PLAYLISTY DO "MOICH STACJI SONOSA" - WPIECIE UI.
///
/// Ten plik NIE parsuje playlist i NIE scala listy. Obie te rzeczy sa juz
/// odebrane: czyta plik ISTNIEJACY <see cref="RadioPlaylistImporter"/> (ta sama
/// droga, co Radio i WiiM), a scala <see cref="SonosOwnStreamsImport"/> w Core.
/// Tutaj jest wylacznie to, czego tam byc nie moze: okno wyboru pliku,
/// rzeczywisty zapis stanu AMC i krotki komunikat dla czytnika ekranu.
///
/// Granice tego przyrostu, swiadomie wezsze niz nazwa "import":
/// - To import LOKALNY. Dziala BEZ polaczonego konta Sonos i BEZ wybranego
///   glosnika - stacje zyja w AppSettings AMC, nie w Ulubionych Sonosa.
/// - ZERO sieci: nie pobieramy adresu, nie wysylamy POST, nie tworzymy sesji
///   i niczego nie uruchamiamy po zapisie.
/// - DOPISUJEMY. Anulowanie okna, blad pliku i manifest HLS nie moga zmienic
///   ani wyczyscic zapisanych stacji.
/// </summary>
public partial class MainWindow
{
    /// <summary>
    /// Wynik proby importu widziany przez UI. <see cref="Stations"/> rozne od
    /// null znaczy: stan ZOSTAL zmieniony i zapisany, a to jest PELNA nowa
    /// lista. null znaczy: nic nie ruszylismy (anulowanie, blad, brak nowych
    /// stacji) - wolajacy ma tylko powiedziec <see cref="Message"/>.
    /// </summary>
    internal sealed record SonosOwnStreamsImportUiOutcome(
        IReadOnlyList<SonosOwnStreamSettings>? Stations,
        string? FirstAddedId,
        string Message);

    internal SonosOwnStreamsImportUiOutcome ImportSonosOwnStreamsForTests(Window owner) =>
        ImportSonosOwnStreams(owner);

    /// <summary>TESTOWE podstawienie okna wyboru pliku: pomiar nie stawia dialogu systemowego.</summary>
    internal Func<string?>? SonosOwnStreamsImportPathOverrideForTests { get; set; }

    /// <summary>
    /// MENU PLIK -> "Importuj stacje z playlisty do Moich stacji Sonosa".
    /// Po udanym imporcie otwieramy liste Moich stacji z zaznaczona pierwsza
    /// dodana stacja, zeby uzytkownik od razu slyszal, co przyszlo.
    /// </summary>
    private void ImportSonosOwnStreamsFromMenu()
    {
        var outcome = ImportSonosOwnStreams(this);
        if (outcome.Stations is null)
        {
            AnnounceEssential(outcome.Message);
            RestoreItemActionFocus();
            return;
        }
        ShowSonosOwnStreams(outcome.FirstAddedId, outcome.Message);
    }

    /// <summary>
    /// JEDNA droga importu dla menu Plik i dla przycisku w otwartych Moich
    /// stacjach. Druga kopia tego kodu rozjechalaby komunikat i zapis.
    /// </summary>
    private SonosOwnStreamsImportUiOutcome ImportSonosOwnStreams(Window owner)
    {
        string? path;
        if (SonosOwnStreamsImportPathOverrideForTests is { } picker) path = picker();
        else
        {
            var dialog = new OpenFileDialog
            {
                Title = "Importuj stacje z playlisty do Moich stacji Sonosa",
                Filter = "Playlisty radia (*.m3u;*.m3u8;*.pls;*.xspf;*.json)"
                    + "|*.m3u;*.m3u8;*.pls;*.xspf;*.json|Wszystkie pliki (*.*)|*.*",
                Multiselect = false,
                CheckFileExists = true
            };
            path = dialog.ShowDialog(owner) == true ? dialog.FileName : null;
        }
        // ANULOWANIE: zadnego odczytu, zadnego zapisu, lista nietknieta.
        if (string.IsNullOrWhiteSpace(path))
            return new SonosOwnStreamsImportUiOutcome(null, null, "Bez zmian na liście stacji.");

        RadioPlaylistImportResult parsed;
        try
        {
            parsed = RadioPlaylistImporter.Import(path);
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or ArgumentException
            or InvalidDataException
            or System.Text.Json.JsonException
            or System.Xml.XmlException)
        {
            // BLAD PLIKU (takze manifest HLS): komunikat i KONIEC. Zapisane
            // stacje zostaja takie, jakie byly.
            DiagnosticLog.Warning(
                "sonos-own-streams-import",
                $"Nie udało się zaimportować playlisty; błąd {exception.GetType().Name}.");
            return new SonosOwnStreamsImportUiOutcome(
                null, null, $"Nie można zaimportować playlisty: {exception.Message}");
        }

        var merged = SonosOwnStreamsImport.Merge(
            _state.Sonos.OwnStreams,
            parsed.Stations.Select(station =>
                new SonosOwnStreamsImportEntry(station.Name, station.StreamUrl)));
        var message = DescribeSonosOwnStreamsImport(
            merged.Added, parsed.SkippedEntries, merged.SkippedDuplicates, merged.SkippedInvalidAddresses);
        // NIC NOWEGO = NIC DO ZAPISANIA. Nie przepisujemy listy tylko po to,
        // zeby ruszyc plik stanu.
        if (merged.Added == 0) return new SonosOwnStreamsImportUiOutcome(null, null, message);

        var existingIds = _state.Sonos.OwnStreams.Select(station => station.Id).ToHashSet(StringComparer.Ordinal);
        var firstAddedId = merged.Stations.FirstOrDefault(station => !existingIds.Contains(station.Id))?.Id;
        // RZECZYWISTY ZAPIS ISTNIEJACA DROGA - ta sama, ktorej uzywa Dodaj/Edytuj
        // w oknie Moich stacji.
        _state.Sonos.OwnStreams = merged.Stations.Select(SonosOwnStreamsWindow.Copy).ToList();
        QueueStateSave(announceFailure: true);
        return new SonosOwnStreamsImportUiOutcome(merged.Stations, firstAddedId, message);
    }

    /// <summary>
    /// KOMUNIKAT: liczba dodanych i PELNA suma pominiec. Pominiecia maja DWA
    /// zrodla i oba trzeba doliczyc - parser odrzuca wiersze bez adresu i
    /// powtorzenia W PLIKU (<c>SkippedEntries</c>), a scalanie odrzuca adresy
    /// nieobslugiwane przez Sonosa i stacje JUZ ZAPISANE. Podanie tylko jednego
    /// z nich zostawia uzytkownika z nieprawdziwa arytmetyka.
    /// </summary>
    internal static string DescribeSonosOwnStreamsImport(
        int added, int skippedByParser, int skippedDuplicates, int skippedInvalidAddresses)
    {
        var skipped = skippedByParser + skippedDuplicates + skippedInvalidAddresses;
        var head = added > 0
            ? $"Dodano stacje do Moich stacji Sonosa: {added}"
            : "Nie dodano żadnej nowej stacji";
        if (skipped == 0)
        {
            return added > 0 ? head + "." : "Playlista nie zawiera nowych stacji dla Sonosa.";
        }
        var parts = new List<string>(3);
        if (skippedDuplicates > 0) parts.Add($"już zapisane {skippedDuplicates}");
        if (skippedInvalidAddresses > 0) parts.Add($"adresy nieobsługiwane {skippedInvalidAddresses}");
        if (skippedByParser > 0) parts.Add($"wpisy bez adresu lub powtórzone w pliku {skippedByParser}");
        return $"{head}. Pominięto {skipped}: {string.Join(", ", parts)}.";
    }
}
