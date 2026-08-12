namespace AccessibleMediaController.Core.Commands;

public static class CommandCatalog
{
    public static string GetDisplayName(string commandId)
    {
        const string sessionSlotPrefix = "session.slot.";
        if (commandId.StartsWith(sessionSlotPrefix, StringComparison.Ordinal)
            && int.TryParse(commandId.AsSpan(sessionSlotPrefix.Length), out var slot))
        {
            return $"Wybierz sesję {slot}";
        }

        return commandId switch
        {
            CommandIds.PlayPause => "Odtwarzaj lub wstrzymaj",
            CommandIds.Previous => "Poprzedni element",
            CommandIds.Next => "Następny element",
            CommandIds.SeekBackward10 => "Cofnij o 10 sekund",
            CommandIds.SeekForward10 => "Przewiń o 10 sekund",
            CommandIds.SeekBackward60 => "Cofnij o minutę",
            CommandIds.SeekForward60 => "Przewiń o minutę",
            CommandIds.VolumeUp5 => "Głośniej o 5%",
            CommandIds.VolumeDown5 => "Ciszej o 5%",
            CommandIds.VolumeUp1 => "Głośniej o 1%",
            CommandIds.VolumeDown1 => "Ciszej o 1%",
            CommandIds.TrackStart => "Początek utworu",
            CommandIds.TrackEnd => "Koniec utworu",
            CommandIds.TimeElapsed => "Czas od początku",
            CommandIds.TimeRemaining => "Czas pozostały",
            CommandIds.TimeTotal => "Czas całkowity",
            CommandIds.SessionList => "Lista sesji",
            CommandIds.SessionPrevious => "Poprzednia sesja",
            CommandIds.SessionNext => "Następna sesja",
            CommandIds.ViewFavorites => "Pokaż ulubione",
            CommandIds.ToggleFavorite => "Dodaj lub usuń z ulubionych",
            CommandIds.ViewPlaylists => "Pokaż playlisty",
            CommandIds.ManagePlaylists => "Zmień przynależność do playlist",
            CommandIds.SearchCurrent => "Szukaj w bieżącej usłudze",
            CommandIds.SearchAll => "Szukaj we wszystkich usługach",
            CommandIds.ViewLibrary => "Pokaż bibliotekę",
            CommandIds.ToggleLibrary => "Dodaj lub usuń z biblioteki",
            CommandIds.ViewQueue => "Pokaż kolejkę",
            CommandIds.AddQueue => "Dodaj lub usuń z kolejki",
            CommandIds.TogglePlayNext => "Ustaw lub usuń odtwarzanie jako następne",
            CommandIds.ViewAlbums => "Pokaż albumy",
            CommandIds.ViewRadio => "Pokaż radio i rekomendacje",
            CommandIds.StartRadio => "Uruchom radio na podstawie elementu",
            CommandIds.ViewMixes => "Pokaż miksy",
            CommandIds.ViewHistory => "Pokaż historię",
            CommandIds.ViewNowPlaying => "Pokaż teraz odtwarzane",
            CommandIds.OpenOfficialApp => "Otwórz w oficjalnej aplikacji",
            CommandIds.ItemInformation => "Informacje o elemencie",
            CommandIds.ExtendedInformation => "Rozszerzone informacje o elemencie",
            CommandIds.ViewOutputs => "Pokaż wyjścia i urządzenia",
            CommandIds.ViewDownloads => "Pokaż pobrane",
            CommandIds.Help => "Pomoc dotycząca skrótów",
            _ => commandId
        };
    }
}
