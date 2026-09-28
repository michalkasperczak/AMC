using System;
using System.Threading;
using System.Threading.Tasks;

namespace AccessibleMediaController.Core.Sonos;

/// <summary>
/// WASKIE zaplecze sesji Sonos widziane przez UI: trzy odczyty i podstawowe
/// polecenia GRUPY. Nie ma tu tokenu, brokera, magazynu ani niczego, co
/// pozwoliloby oknu dotknac poswiadczen - te zostaja u wlasciciela konta.
///
/// Ten interfejs jest JEDYNA granica API/transportu dla sesji Sonos i celowo
/// jest FAKTYCZNA granica, a nie tylko zamiana konfiguracji: dzieki temu pomiar
/// prawdziwego okna moze podstawic zaplecze BEZ konta, bez sieci i bez DPAPI.
/// Produkcyjnie implementuje go adapter nad istniejacym wlascicielem konta.
/// </summary>
public interface ISonosGroupSessionBackend
{
    Task<SonosGroupReadResult<SonosGroupPlaybackStatus>> ReadGroupPlaybackAsync(
        string? groupId, CancellationToken cancellationToken);

    Task<SonosGroupReadResult<SonosGroupMetadata>> ReadGroupMetadataAsync(
        string? groupId, CancellationToken cancellationToken);

    Task<SonosGroupReadResult<SonosGroupVolume>> ReadGroupVolumeAsync(
        string? groupId, CancellationToken cancellationToken);

    Task<SonosGroupCommandResult> SendGroupCommandAsync(
        string? groupId, SonosGroupCommand command, CancellationToken cancellationToken);

    Task<SonosGroupCommandResult> SeekRelativeAsync(
        string? groupId, int deltaMillis, string? itemId, CancellationToken cancellationToken);

    Task<SonosGroupCommandResult> SetGroupVolumeAsync(
        string? groupId, int volume, CancellationToken cancellationToken);

    Task<SonosGroupCommandResult> SetGroupMuteAsync(
        string? groupId, bool muted, CancellationToken cancellationToken);

    /// <summary>ODCZYT domow - potrzebny, by sesja miala droge do konta bez menu Plik.</summary>
    Task<SonosHouseholdsReadResult> ReadHouseholdsAsync(CancellationToken cancellationToken);

    Task<SonosGroupsReadResult> ReadGroupsAsync(string householdId, CancellationToken cancellationToken);
}
