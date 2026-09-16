using System;
using System.Collections.Generic;
using System.Linq;
using AccessibleMediaController.Core.Sessions;

namespace AccessibleMediaController.Core.Tidal;

/// <summary>
/// Kolejka utworow TIDALa prowadzona PRZEZ AMC, a nie przez oryginalny TIDAL.
///
/// POWOD ISTNIENIA (zgloszenie uzytkownika 16.09.2026, doslownie): "nastepny
/// poprzedni dziala tak, jak on przekazuje w Tidalu, czyli przekazuje do
/// oryginalnego Tidala i potem juz idzie sciezka Tidal. [...] nie dla tego co
/// mam, co widzi moj fokus, co widzi AMC, co jest w bibliotece AMC, na
/// playliscie AMC, tylko tego co jest w oryginalnym Tidalu na sztywno".
///
/// Windowsowa sesja multimediow (SMTC) potrafi tylko powiedziec TIDALowi
/// "nastepny", a wtedy o kolejnosci decyduje TIDAL: przy utworze z albumu leci
/// dalszy album, przy singlu cokolwiek. Zeby kolejnosc byla ta z listy w AMC,
/// nie wolno prosic TIDALa o nastepny utwor - trzeba WSKAZAC mu konkretny
/// utwor, dokladnie tak jak przy odtwarzaniu z listy Enterem.
///
/// Dlatego ta klasa pamieta MIGAWKE listy z chwili uruchomienia utworu.
/// Migawka, nie zywa lista, bo uzytkownik moze w trakcie grania przejsc do
/// innego widoku albo przefiltrowac liste - a kolejnosc odtwarzania ma zostac
/// ta, ktora zaczal.
/// </summary>
public sealed class TidalDesktopTrackQueue
{
    private readonly List<MediaItem> tracks = new();
    private int position = -1;

    /// <summary>Nazwa listy, z ktorej pochodzi kolejka - do komunikatow.</summary>
    public string SourceName { get; private set; } = string.Empty;

    /// <summary>Czy jest po czym chodzic nastepnym i poprzednim.</summary>
    public bool HasQueue => tracks.Count > 0 && position >= 0;

    /// <summary>Ile utworow zapamietano.</summary>
    public int Count => tracks.Count;

    /// <summary>Numer grajacego utworu liczony od 1; 0 gdy kolejki nie ma.</summary>
    public int HumanPosition => HasQueue ? position + 1 : 0;

    /// <summary>
    /// Czy utwor da sie wskazac oryginalnemu TIDALowi. Bez identyfikatora albumu
    /// nie ma jak - TIDAL otwiera utwory przez strone albumu, co zmierzono
    /// 14.09.2026. Taki utwor NIE trafia do kolejki, bo inaczej nastepny
    /// zatrzymalby sie na nim na glucho.
    /// </summary>
    public static bool CanQueue(MediaItem? item) =>
        item is not null
        && item.Kind is MediaItemKind.Track
        && !string.IsNullOrWhiteSpace(item.Title)
        && TidalDesktopPlaybackPlan.NormalizeId(item.RelatedAlbumExternalId).Length > 0;

    /// <summary>
    /// Zapamietuje liste i utwor, ktory z niej zagral. Utwory bez albumu sa
    /// pomijane, ale grajacy utwor zostaje odnaleziony po tozsamosci, zeby
    /// numeracja zgadzala sie z tym, co uzytkownik slyszy.
    /// </summary>
    public void Capture(IEnumerable<MediaItem> listInOrder, MediaItem started, string sourceName)
    {
        ArgumentNullException.ThrowIfNull(listInOrder);
        ArgumentNullException.ThrowIfNull(started);

        tracks.Clear();
        position = -1;
        SourceName = sourceName ?? string.Empty;

        foreach (var item in listInOrder.Where(CanQueue))
        {
            if (SameItem(item, started)) position = tracks.Count;
            tracks.Add(item);
        }

        if (position >= 0) return;

        // Grajacego utworu nie bylo na liscie (np. wszedl z wyszukiwania).
        // Kolejka z jednym utworem jest uczciwsza od udawania, ze jest wieksza.
        tracks.Clear();
        if (!CanQueue(started)) return;
        tracks.Add(started);
        position = 0;
    }

    /// <summary>Zapomina kolejke - po przejsciu na sterowanie kolejka TIDALa.</summary>
    public void Clear()
    {
        tracks.Clear();
        position = -1;
        SourceName = string.Empty;
    }

    /// <summary>
    /// Nastepny albo poprzedni utwor Z LISTY AMC. Zwraca false na koncu listy -
    /// wolajacy ma wtedy powiedziec, ze to koniec, a nie milczec.
    /// </summary>
    public bool TryMove(bool forward, out MediaItem? track)
    {
        track = null;
        if (!HasQueue) return false;

        var next = forward ? position + 1 : position - 1;
        if (next < 0 || next >= tracks.Count) return false;

        position = next;
        track = tracks[position];
        return true;
    }

    /// <summary>Utwor grajacy teraz wedlug tej kolejki.</summary>
    public MediaItem? Current => HasQueue ? tracks[position] : null;

    /// <summary>
    /// Komunikat o koncu listy. Mowi TEZ z jakiej listy, bo uzytkownik moze miec
    /// otwarta inna niz ta, z ktorej gra.
    /// </summary>
    public string EndOfQueueMessage(bool forward)
    {
        var skad = SourceName.Length > 0 ? $" listy {SourceName}" : " listy";
        return forward
            ? $"To ostatni utwór{skad}"
            : $"To pierwszy utwór{skad}";
    }

    private static bool SameItem(MediaItem left, MediaItem right)
    {
        if (ReferenceEquals(left, right)) return true;
        if (!string.IsNullOrEmpty(left.ExternalId) && !string.IsNullOrEmpty(right.ExternalId))
            return string.Equals(left.ExternalId, right.ExternalId, StringComparison.Ordinal);
        return string.Equals(left.Id, right.Id, StringComparison.Ordinal);
    }
}
