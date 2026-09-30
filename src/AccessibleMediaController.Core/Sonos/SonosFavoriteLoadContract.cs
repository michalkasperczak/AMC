using System;
using System.Threading;
using System.Threading.Tasks;

namespace AccessibleMediaController.Core.Sonos;

/// <summary>
/// Sposob, w jaki gloshnik wstawia ulubione do WSPOLNEJ kolejki grupy
/// (schema "queueAction" z definicji OpenAPI 3.0.3 "Sonos Control API (cloud)"
/// v1.56.0-alpha.1-1-gc264f93f-production-cloud, operacja
/// Favorites-LoadFavorite-GroupId).
///
/// Wartosci sa DOKLADNIE te, ktore wymienia definicja - wszystkie piec, takze
/// PLAY_NOW. Definicja opisuje pole jako opcjonalne z domyslnym "append", ale
/// AMC domyslnej wartosci NIE przyjmuje: wolajacy podaje akcje JAWNIE, bo
/// roznica miedzy dopisaniem a zastapieniem kolejki uzytkownika jest
/// nieodwracalna i nie moze zalezec od milczenia.
///
/// Wybor polityki (co robi konkretny przycisk albo skrot) nalezy do warstw
/// wyzszych; transport zaden wariant nie jest tu uprzywilejowany.
/// </summary>
public enum SonosFavoriteQueueAction
{
    /// <summary>REPLACE - zastepuje zawartosc wspolnej kolejki.</summary>
    Replace,

    /// <summary>APPEND - dopisuje na koncu wspolnej kolejki.</summary>
    Append,

    /// <summary>INSERT - wstawia do wspolnej kolejki.</summary>
    Insert,

    /// <summary>INSERT_NEXT - wstawia jako nastepna pozycje.</summary>
    InsertNext,

    /// <summary>PLAY_NOW - wstawia i przechodzi do tej pozycji.</summary>
    PlayNow
}

/// <summary>
/// Jedno zrodlo prawdy dla wartosci przesylanych w polu action. Napis wchodzi
/// do ciala zadania, wiec nie wolno go sklejac ani zgadywac w kilku miejscach.
/// </summary>
public static class SonosFavoriteQueueActions
{
    /// <summary>Wartosc pola "action" wprost z definicji; nieznany enum to blad wolajacego.</summary>
    public static string WireValue(SonosFavoriteQueueAction action) => action switch
    {
        SonosFavoriteQueueAction.Replace => "REPLACE",
        SonosFavoriteQueueAction.Append => "APPEND",
        SonosFavoriteQueueAction.Insert => "INSERT",
        SonosFavoriteQueueAction.InsertNext => "INSERT_NEXT",
        SonosFavoriteQueueAction.PlayNow => "PLAY_NOW",
        _ => throw new ArgumentOutOfRangeException(nameof(action))
    };

    /// <summary>
    /// Czy wartosc pochodzi z definicji. Rzutowanie dowolnej liczby na enum jest
    /// w C# legalne, wiec transport musi to sprawdzic ZANIM cokolwiek wysle.
    /// </summary>
    public static bool IsDefined(SonosFavoriteQueueAction action) =>
        action is SonosFavoriteQueueAction.Replace
            or SonosFavoriteQueueAction.Append
            or SonosFavoriteQueueAction.Insert
            or SonosFavoriteQueueAction.InsertNext
            or SonosFavoriteQueueAction.PlayNow;
}

/// <summary>
/// WASKA granica ladowania ulubionego - dokladnie jedna operacja zapisu
/// (POST /groups/{groupId}/favorites). Osobna od <see cref="ISonosFavoritesApi"/>,
/// zeby odczyt ulubionych pozostal odczytem, a dziesiatki istniejacych atrap
/// odczytu nie musialy nagle umiec pisac.
///
/// Zadnego konta, presetu, koordynatora ani UI tu nie ma.
/// </summary>
public interface ISonosFavoriteLoadApi
{
    Task<SonosGroupCommandOutcome> LoadFavoriteAsync(
        string? accessToken,
        string? groupId,
        string? favoriteId,
        SonosFavoriteQueueAction action,
        bool playOnCompletion,
        CancellationToken cancellationToken);
}
