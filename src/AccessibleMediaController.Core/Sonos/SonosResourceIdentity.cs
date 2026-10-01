using System;

namespace AccessibleMediaController.Core.Sonos;

/// <summary>
/// Limity schematu universalMusicObjectId z definicji OpenAPI 3.0.3
/// "Sonos Control API (cloud)" v1.56.0-alpha.1-1-gc264f93f-production-cloud
/// (components.schemas.universalMusicObjectId). Nie sa to domysly AMC.
/// </summary>
public static class SonosResourceIdentityLimits
{
    /// <summary>objectId: maxLength 256, w definicji WYMAGANY wewnatrz obiektu id.</summary>
    public const int MaxObjectIdLength = 256;

    /// <summary>serviceId: maxLength 20, nullable.</summary>
    public const int MaxServiceIdLength = 20;

    /// <summary>accountId: maxLength 128, nullable.</summary>
    public const int MaxAccountIdLength = 128;
}

/// <summary>
/// Tozsamosc materialu Sonos (schema universalMusicObjectId) - ta sama trojka
/// pol wystepuje w favorites.items[].resource.id i w
/// playbackMetadata.container.id. Niemutowalna.
///
/// To NIE jest identyfikator wiersza katalogu. Pole favorite.id z poziomu listy
/// ulubionych to INNY klucz (klucz katalogu domu) i NIGDY nie wolno go
/// porownywac z objectId.
///
/// Rozpoznanie wymaga KOMPLETNEJ trojki po obu stronach i DOKLADNEJ rownosci
/// porzadkowej (Ordinal) wszystkich trzech pol. Brak pola to "nie wiem", a nie
/// dopasowanie do czegokolwiek - dlatego nie ma tu rownowaznosci czesciowej,
/// wieloznacznika ani Equals/GetHashCode: zgodnosc liczy wylacznie
/// <see cref="Matches"/>.
///
/// Wartosci zrodlowe zachowujemy LITERALNIE: bez trim, bez zmiany wielkosci
/// liter, bez normalizacji URI i bez dekodowania procentowego.
///
/// Te identyfikatory (zwlaszcza accountId) sa danymi prywatnymi uzytkownika.
/// Zyja w pamieci procesu; nie wolno ich zapisywac do AppSettings, cache ani
/// logow - dlatego <see cref="ToString"/> podaje wylacznie obecnosc pol.
/// </summary>
public sealed class SonosResourceIdentity
{
    public SonosResourceIdentity(string? serviceId, string? objectId, string? accountId)
    {
        if (serviceId is not null && serviceId.Length > SonosResourceIdentityLimits.MaxServiceIdLength)
        {
            throw new ArgumentOutOfRangeException(nameof(serviceId));
        }

        if (objectId is not null && objectId.Length > SonosResourceIdentityLimits.MaxObjectIdLength)
        {
            throw new ArgumentOutOfRangeException(nameof(objectId));
        }

        if (accountId is not null && accountId.Length > SonosResourceIdentityLimits.MaxAccountIdLength)
        {
            throw new ArgumentOutOfRangeException(nameof(accountId));
        }

        ServiceId = serviceId;
        ObjectId = objectId;
        AccountId = accountId;
    }

    /// <summary>universalMusicObjectId.serviceId - maxLength 20, moze nie byc podany.</summary>
    public string? ServiceId { get; }

    /// <summary>universalMusicObjectId.objectId - maxLength 256.</summary>
    public string? ObjectId { get; }

    /// <summary>universalMusicObjectId.accountId - maxLength 128, moze nie byc podany.</summary>
    public string? AccountId { get; }

    /// <summary>
    /// Czy trojka jest kompletna. Niekompletna tozsamosc jest LEGALNA w
    /// odpowiedzi (serviceId i accountId sa nullable), ale nie wystarcza do
    /// rozpoznania materialu.
    /// </summary>
    public bool IsComplete =>
        !string.IsNullOrEmpty(ServiceId)
        && !string.IsNullOrEmpty(ObjectId)
        && !string.IsNullOrEmpty(AccountId);

    /// <summary>
    /// Fabryka tolerancyjna: zwraca null, gdy nie ma ANI JEDNEGO pola albo gdy
    /// ktorakolwiek wartosc lamie limit definicji. Uzywa jej parser, zeby
    /// niepoprawna CZESC OPCJONALNA wylaczala rozpoznanie tej jednej pozycji,
    /// a nie kasowala calego katalogu.
    /// </summary>
    public static SonosResourceIdentity? TryCreate(string? serviceId, string? objectId, string? accountId)
    {
        if (serviceId is null && objectId is null && accountId is null)
        {
            return null;
        }

        if (serviceId is not null && serviceId.Length > SonosResourceIdentityLimits.MaxServiceIdLength)
        {
            return null;
        }

        if (objectId is not null && objectId.Length > SonosResourceIdentityLimits.MaxObjectIdLength)
        {
            return null;
        }

        if (accountId is not null && accountId.Length > SonosResourceIdentityLimits.MaxAccountIdLength)
        {
            return null;
        }

        return new SonosResourceIdentity(serviceId, objectId, accountId);
    }

    /// <summary>
    /// Rozpoznanie materialu: WSZYSTKIE TRZY pola rowne porzadkowo i obie
    /// trojki kompletne. Nazwa, rodzaj, service.name, identyfikator wiersza
    /// katalogu, wersja kolejki ani kod 200 NIE wchodza do tego porownania.
    /// </summary>
    public bool Matches(SonosResourceIdentity? other)
    {
        if (other is null || !IsComplete || !other.IsComplete)
        {
            return false;
        }

        return string.Equals(ServiceId, other.ServiceId, StringComparison.Ordinal)
            && string.Equals(ObjectId, other.ObjectId, StringComparison.Ordinal)
            && string.Equals(AccountId, other.AccountId, StringComparison.Ordinal);
    }

    /// <summary>
    /// Wygodna postac statyczna: brak ktorejkolwiek tozsamosci to BRAK
    /// dopasowania, nigdy dopasowanie domyslne.
    /// </summary>
    public static bool AreSameMaterial(SonosResourceIdentity? left, SonosResourceIdentity? right) =>
        left is not null && left.Matches(right);

    /// <summary>Nie wypisuje zadnej wartosci - tylko obecnosc i kompletnosc pol.</summary>
    public override string ToString() =>
        "Tożsamość materiału Sonos: serwis "
        + (ServiceId is null ? "brak" : "jest")
        + ", obiekt "
        + (ObjectId is null ? "brak" : "jest")
        + ", konto "
        + (AccountId is null ? "brak" : "jest")
        + (IsComplete ? ", kompletna" : ", niekompletna");
}
