using System;
using System.Collections.Generic;
using System.Globalization;

namespace AccessibleMediaController.Core.Sonos;

/// <summary>
/// ETYKIETY PL listy urzadzen Sonos. Wydzielone z okna, zeby dalo sie je
/// zmierzyc bez WPF i bez pokazywania czegokolwiek.
///
/// Zasady, swiadome i sprawdzane testami:
///   * w ZWYKLEJ etykiecie nie ma identyfikatorow - czytnik ekranu ma czytac
///     nazwe, nie 24-znakowy playerId,
///   * dom bez nazwy dostaje kolejny numer PORZADKOWY ("Dom Sonos 1"), a nie
///     swoj householdId,
///   * stan odtwarzania jest po polsku i NIE jest przyciskiem - ten widok
///     niczego nie uruchamia,
///   * pusty dom, lista NIEPELNA i blad maja WLASNY, jawny tekst. Cisza jest
///     zabroniona.
/// </summary>
public static class SonosDeviceLabels
{
    /// <summary>PIERWSZA tresc okna. Jawnie mowi, ze widok nie zmienia odtwarzania.</summary>
    public const string ViewIntroduction =
        "Lista urządzeń; ten widok nie zmienia odtwarzania.";

    public const string PlaybackUnknown = "stan odtwarzania nieznany";

    /// <summary>Nazwa domu do ZWYKLEJ etykiety. Bez householdId.</summary>
    public static string DescribeHousehold(SonosHousehold household, int ordinal)
    {
        ArgumentNullException.ThrowIfNull(household);
        return household.HasName
            ? household.Name!
            : "Dom Sonos " + ordinal.ToString(CultureInfo.InvariantCulture);
    }

    public static string DescribePlaybackState(SonosPlaybackState state) => state switch
    {
        SonosPlaybackState.Playing => "odtwarza",
        SonosPlaybackState.Paused => "wstrzymane",
        SonosPlaybackState.Buffering => "buforuje",
        SonosPlaybackState.Idle => "bezczynne",
        _ => PlaybackUnknown
    };

    /// <summary>
    /// Etykieta grupy: nazwa, liczba glosnikow i stan po polsku. Bez groupId.
    /// </summary>
    public static string DescribeGroup(SonosGroup group)
    {
        ArgumentNullException.ThrowIfNull(group);
        var count = group.PlayerIds.Count;
        return group.Name
            + ", " + DescribeSpeakerCount(count)
            + ", " + DescribePlaybackState(group.PlaybackState);
    }

    /// <summary>
    /// Etykieta glosnika: nazwa plus UDZIAL W GRUPIE wyrazony nazwami grup, nie
    /// identyfikatorami. Glosnik poza grupami dostaje jawny tekst, nie pustke.
    /// </summary>
    public static string DescribePlayer(SonosPlayer player, SonosHouseholdTopology topology)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(topology);

        string? groupName = null;
        var coordinator = false;
        foreach (var group in topology.Groups)
        {
            if (!group.PlayerIds.Contains(player.Id))
            {
                continue;
            }

            groupName = group.Name;
            coordinator = string.Equals(group.CoordinatorId, player.Id, StringComparison.Ordinal);
            break;
        }

        if (groupName is null)
        {
            return player.Name + ", poza grupami";
        }

        return player.Name
            + ", w grupie " + groupName
            + (coordinator ? ", prowadzi grupę" : string.Empty);
    }

    private static string DescribeSpeakerCount(int count) => count switch
    {
        1 => "1 głośnik",
        >= 2 and <= 4 => count.ToString(CultureInfo.InvariantCulture) + " głośniki",
        _ => count.ToString(CultureInfo.InvariantCulture) + " głośników"
    };

    /// <summary>
    /// PODSUMOWANIE odczytu jednego domu. Pusty dom i lista NIEPELNA mowia to
    /// wprost; blad mowi swoim komunikatem i nie udaje pustej listy.
    /// </summary>
    public static string DescribeTopology(SonosGroupsReadResult result, string householdLabel)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (!result.Succeeded || result.Topology is null)
        {
            return result.Message;
        }

        var topology = result.Topology;
        if (topology.IsEmpty)
        {
            return "Dom " + householdLabel + " nie ma podłączonych głośników Sonos.";
        }

        var text = "Dom " + householdLabel
            + ": grup " + topology.Groups.Count.ToString(CultureInfo.InvariantCulture)
            + ", głośników " + topology.Players.Count.ToString(CultureInfo.InvariantCulture)
            + ".";

        if (topology.Partial)
        {
            text += " Lista jest NIEPEŁNA: Sonos pominął część grup lub głośników. "
                + "Odśwież po zakończeniu grupowania.";
        }

        return text;
    }

    /// <summary>Podsumowanie odczytu DOMOW. Brak domow to jawny tekst, nie cisza.</summary>
    public static string DescribeHouseholds(SonosHouseholdsReadResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (!result.Succeeded)
        {
            return result.Message;
        }

        return result.Households.Count switch
        {
            0 => "Konto Sonos nie ma żadnego domu z głośnikami.",
            1 => "Konto Sonos ma jeden dom; został wybrany.",
            _ => "Konto Sonos ma domów: "
                + result.Households.Count.ToString(CultureInfo.InvariantCulture)
                + ". Wybierz dom z listy."
        };
    }

    /// <summary>
    /// Nazwy domow z numeracja porzadkowa dla nienazwanych. Numer liczy
    /// POZYCJE na liscie, wiec dwa nienazwane domy nie zlewaja sie w jeden.
    /// </summary>
    public static IReadOnlyList<string> DescribeHouseholdChoices(IReadOnlyList<SonosHousehold> households)
    {
        ArgumentNullException.ThrowIfNull(households);
        var labels = new List<string>(households.Count);
        for (var index = 0; index < households.Count; index++)
        {
            labels.Add(DescribeHousehold(households[index], index + 1));
        }

        return labels;
    }
}
