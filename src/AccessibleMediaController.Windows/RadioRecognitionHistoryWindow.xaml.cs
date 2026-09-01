using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Windows.Controls;
using AccessibleMediaController.Windows.Services;
using Microsoft.Win32;

namespace AccessibleMediaController.Windows;

public partial class RadioRecognitionHistoryWindow : AccessibleWindow
{
    private readonly List<RadioRecognizedTrackSettings> _entries;
    private List<RecognitionHistoryRow> _rows = [];
    private bool _updatingFilters;

    public RadioRecognitionHistoryWindow(List<RadioRecognizedTrackSettings> entries)
    {
        InitializeComponent();
        MenuAccessibility.NormalizeContextMenu(HistoryContextMenu);
        _entries = entries;
        RefreshStationFilters();
        RefreshRows();
    }

    public bool Changed { get; private set; }

    private void RefreshRows(string? preferredId = null, int fallbackIndex = 0)
    {
        var filterKey = (StationFilterCombo.SelectedItem as RecognitionStationFilter)?.Key;
        _rows = _entries
            .Where(entry => filterKey is null
                || string.Equals(RecognitionStationFilter.KeyFor(entry), filterKey, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(entry => entry.RecognizedUtcTicks)
            .Select(entry => new RecognitionHistoryRow(entry))
            .ToList();
        HistoryList.ItemsSource = _rows;
        if (_rows.Count == 0)
        {
            HistoryList.SelectedIndex = -1;
            HistoryStatus.Text = filterKey is null
                ? "Historia jest pusta"
                : "Brak wpisów dla wybranej stacji";
            return;
        }
        var index = preferredId is null
            ? Math.Clamp(fallbackIndex, 0, _rows.Count - 1)
            : _rows.FindIndex(row => string.Equals(row.Entry.Id, preferredId, StringComparison.Ordinal));
        HistoryList.SelectedIndex = index >= 0 ? index : Math.Clamp(fallbackIndex, 0, _rows.Count - 1);
        HistoryStatus.Text = filterKey is null
            ? $"Wpisów: {_rows.Count}, wszystkie stacje"
            : $"Wpisów: {_rows.Count}, {(StationFilterCombo.SelectedItem as RecognitionStationFilter)?.Label}";
    }

    private void RefreshStationFilters(string? preferredKey = null)
    {
        preferredKey ??= (StationFilterCombo.SelectedItem as RecognitionStationFilter)?.Key;
        var filters = new List<RecognitionStationFilter>
        {
            new(null, "Wszystkie stacje")
        };
        filters.AddRange(_entries
            .GroupBy(RecognitionStationFilter.KeyFor, StringComparer.OrdinalIgnoreCase)
            .Select(group => new RecognitionStationFilter(
                group.Key,
                group.OrderByDescending(entry => entry.RecognizedUtcTicks).First().StationName))
            .OrderBy(filter => filter.Label, StringComparer.CurrentCultureIgnoreCase));

        _updatingFilters = true;
        StationFilterCombo.ItemsSource = filters;
        StationFilterCombo.SelectedItem = filters.FirstOrDefault(filter =>
            string.Equals(filter.Key, preferredKey, StringComparison.OrdinalIgnoreCase)) ?? filters[0];
        _updatingFilters = false;
    }

    private void StationFilterCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_updatingFilters) return;
        RefreshRows();
    }

    private void Window_ContentRendered(object? sender, EventArgs e) => FocusSelectedRow();

    private void FocusSelectedRow()
    {
        HistoryList.UpdateLayout();
        if (HistoryList.SelectedIndex >= 0
            && HistoryList.ItemContainerGenerator.ContainerFromIndex(HistoryList.SelectedIndex)
                is ListBoxItem item)
        {
            item.Focus();
            Keyboard.Focus(item);
            return;
        }
        Keyboard.Focus(HistoryList);
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (Keyboard.Modifiers == ModifierKeys.Control && key == Key.C)
        {
            CopySelected(includeServiceLinks: false);
            e.Handled = true;
            return;
        }
        if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && key == Key.C)
        {
            CopySelected(includeServiceLinks: true);
            e.Handled = true;
            return;
        }
        if (Keyboard.Modifiers == ModifierKeys.None && key == Key.Delete)
        {
            DeleteSelected();
            e.Handled = true;
            return;
        }
        if (Keyboard.Modifiers == ModifierKeys.None && key == Key.Enter)
        {
            OpenProviderResult();
            e.Handled = true;
        }
    }

    private RecognitionHistoryRow[] SelectedRows() =>
        HistoryList.SelectedItems.OfType<RecognitionHistoryRow>().ToArray();

    private void CopySelected(bool includeServiceLinks)
    {
        var selected = SelectedRows();
        if (selected.Length == 0)
        {
            HistoryStatus.Announce("Brak wybranego wpisu");
            return;
        }
        var text = string.Join(
            Environment.NewLine + Environment.NewLine,
            selected.Select(row => includeServiceLinks ? row.RichText : row.CopyText));
        if (!ClipboardRetry.TrySetText(text, out var error))
        {
            HistoryStatus.Announce(error);
            return;
        }
        HistoryStatus.Announce(selected.Length == 1
            ? includeServiceLinks ? "Skopiowano opis i łącza" : "Skopiowano opis"
            : includeServiceLinks
                ? $"Skopiowano opisy i łącza: {selected.Length}"
                : $"Skopiowano opisy: {selected.Length}");
    }

    private void DeleteSelected()
    {
        var selected = SelectedRows();
        if (selected.Length == 0)
        {
            HistoryStatus.Announce("Brak wpisu do usunięcia");
            return;
        }
        var index = HistoryList.SelectedIndex;
        var ids = selected.Select(row => row.Entry.Id).ToHashSet(StringComparer.Ordinal);
        _entries.RemoveAll(entry => ids.Contains(entry.Id));
        Changed = true;
        RefreshStationFilters();
        RefreshRows(fallbackIndex: index);
        FocusSelectedRow();
        HistoryStatus.Announce(selected.Length == 1
            ? "Usunięto wpis z historii rozpoznawania"
            : $"Usunięto wpisy z historii rozpoznawania: {selected.Length}");
    }

    private void OpenProviderResult()
    {
        if (HistoryList.SelectedItem is not RecognitionHistoryRow row
            || string.IsNullOrWhiteSpace(row.Entry.ProviderUri))
        {
            HistoryStatus.Announce("Ten wynik nie zawiera łącza dostawcy rozpoznawania");
            return;
        }
        try
        {
            Process.Start(new ProcessStartInfo(row.Entry.ProviderUri) { UseShellExecute = true });
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            HistoryStatus.Announce("Nie udało się otworzyć łącza");
        }
    }

    private void Export()
    {
        if (_entries.Count == 0)
        {
            HistoryStatus.Announce("Historia jest pusta");
            return;
        }
        var dialog = new SaveFileDialog
        {
            Title = "Eksportuj rozpoznane utwory",
            Filter = "Plik JSON (*.json)|*.json|Plik CSV (*.csv)|*.csv",
            DefaultExt = ".json",
            AddExtension = true,
            FileName = $"AMC - rozpoznane utwory - {DateTime.Now:yyyy-MM-dd}"
        };
        if (dialog.ShowDialog(this) != true)
        {
            FocusSelectedRow();
            return;
        }
        try
        {
            var ordered = _entries.OrderByDescending(entry => entry.RecognizedUtcTicks).ToArray();
            if (string.Equals(Path.GetExtension(dialog.FileName), ".csv", StringComparison.OrdinalIgnoreCase))
            {
                var lines = new List<string>
                {
                    "data;stacja;tytuł;wykonawca;album;wydanie;Apple Music;Spotify;Tidal;YouTube Music;Discogs;MusicBrainz"
                };
                lines.AddRange(ordered.Select(entry => string.Join(';', new[]
                {
                    new DateTime(entry.RecognizedUtcTicks, DateTimeKind.Utc).ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"),
                    entry.StationName,
                    entry.Title,
                    entry.Artist,
                    entry.Album,
                    entry.ReleaseDate,
                    RecognitionLinks.AppleMusic(entry),
                    RecognitionLinks.Spotify(entry),
                    RecognitionLinks.Tidal(entry),
                    RecognitionLinks.YouTubeMusic(entry),
                    RecognitionLinks.Discogs(entry),
                    RecognitionLinks.MusicBrainz(entry)
                }.Select(Csv))));
                File.WriteAllLines(dialog.FileName, lines, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
            }
            else
            {
                var export = ordered.Select(entry => new
                {
                    recognizedAt = new DateTime(entry.RecognizedUtcTicks, DateTimeKind.Utc).ToLocalTime(),
                    station = entry.StationName,
                    title = entry.Title,
                    artist = entry.Artist,
                    album = entry.Album,
                    releaseDate = entry.ReleaseDate,
                    recognitionProviderUrl = entry.ProviderUri,
                    serviceSearch = new
                    {
                        appleMusic = RecognitionLinks.AppleMusic(entry),
                        spotify = RecognitionLinks.Spotify(entry),
                        tidal = RecognitionLinks.Tidal(entry),
                        youtubeMusic = RecognitionLinks.YouTubeMusic(entry)
                    },
                    catalogueSearch = new
                    {
                        discogs = RecognitionLinks.Discogs(entry),
                        musicBrainz = RecognitionLinks.MusicBrainz(entry)
                    }
                });
                File.WriteAllText(
                    dialog.FileName,
                    JsonSerializer.Serialize(export, new JsonSerializerOptions { WriteIndented = true }),
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
            }
            HistoryStatus.Announce($"Wyeksportowano historię: {Path.GetFileName(dialog.FileName)}");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            HistoryStatus.Announce($"Nie udało się zapisać pliku: {exception.Message}");
        }
        FocusSelectedRow();
    }

    private static string Csv(string value) => $"\"{value.Replace("\"", "\"\"")}\"";

    private void Copy_Click(object sender, RoutedEventArgs e) => CopySelected(includeServiceLinks: false);
    private void CopyWithLinks_Click(object sender, RoutedEventArgs e) => CopySelected(includeServiceLinks: true);
    private void OpenProvider_Click(object sender, RoutedEventArgs e) => OpenProviderResult();
    private void Export_Click(object sender, RoutedEventArgs e) => Export();
    private void Delete_Click(object sender, RoutedEventArgs e) => DeleteSelected();
}

internal sealed record RecognitionStationFilter(string? Key, string Label)
{
    public string NavigationText => Label;

    public static string KeyFor(RadioRecognizedTrackSettings entry) =>
        string.IsNullOrWhiteSpace(entry.StationId)
            ? $"name:{entry.StationName.Trim()}"
            : $"id:{entry.StationId.Trim()}";

    public override string ToString() => Label;
}

internal sealed class RecognitionHistoryRow(RadioRecognizedTrackSettings entry)
{
    public RadioRecognizedTrackSettings Entry { get; } = entry;
    private DateTime LocalTime => new DateTime(Entry.RecognizedUtcTicks, DateTimeKind.Utc).ToLocalTime();
    private string Track => string.Join(" — ", new[] { Entry.Title, Entry.Artist }
        .Where(value => !string.IsNullOrWhiteSpace(value)));

    public string Label => $"{Track}, {Entry.StationName}, {LocalTime:yyyy-MM-dd HH:mm}";
    public string NavigationText => $"{Entry.Title} {Entry.Artist} {Entry.Album} {Entry.StationName}";
    public string CopyText => string.Join(Environment.NewLine, new[]
    {
        $"Tytuł: {Entry.Title}",
        $"Wykonawca: {Entry.Artist}",
        string.IsNullOrWhiteSpace(Entry.Album) ? null : $"Album: {Entry.Album}",
        string.IsNullOrWhiteSpace(Entry.ReleaseDate) ? null : $"Wydanie: {Entry.ReleaseDate}",
        $"Stacja: {Entry.StationName}",
        $"Rozpoznano: {LocalTime:yyyy-MM-dd HH:mm:ss}"
    }.Where(line => line is not null));

    public string RichText => CopyText + Environment.NewLine
        + $"Apple Music: {RecognitionLinks.AppleMusic(Entry)}" + Environment.NewLine
        + $"Spotify: {RecognitionLinks.Spotify(Entry)}" + Environment.NewLine
        + $"Tidal: {RecognitionLinks.Tidal(Entry)}" + Environment.NewLine
        + $"YouTube Music: {RecognitionLinks.YouTubeMusic(Entry)}" + Environment.NewLine
        + $"Discogs: {RecognitionLinks.Discogs(Entry)}" + Environment.NewLine
        + $"MusicBrainz: {RecognitionLinks.MusicBrainz(Entry)}";

    public override string ToString() => Label;
}

internal static class RecognitionLinks
{
    private static string Query(RadioRecognizedTrackSettings entry) =>
        Uri.EscapeDataString(string.Join(' ', new[] { entry.Artist, entry.Title }
            .Where(value => !string.IsNullOrWhiteSpace(value))));

    public static string AppleMusic(RadioRecognizedTrackSettings entry) =>
        $"https://music.apple.com/search?term={Query(entry)}";

    public static string Spotify(RadioRecognizedTrackSettings entry) =>
        $"https://open.spotify.com/search/{Query(entry)}";

    public static string Tidal(RadioRecognizedTrackSettings entry) =>
        $"https://listen.tidal.com/search?q={Query(entry)}";

    public static string YouTubeMusic(RadioRecognizedTrackSettings entry) =>
        $"https://music.youtube.com/search?q={Query(entry)}";

    public static string Discogs(RadioRecognizedTrackSettings entry) =>
        $"https://www.discogs.com/search/?q={Query(entry)}&type=all";

    public static string MusicBrainz(RadioRecognizedTrackSettings entry) =>
        $"https://musicbrainz.org/search?query={Query(entry)}&type=recording&method=indexed";
}
