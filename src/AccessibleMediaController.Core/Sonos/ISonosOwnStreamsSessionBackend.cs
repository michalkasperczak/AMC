using System.Threading;
using System.Threading.Tasks;

namespace AccessibleMediaController.Core.Sonos;

/// <summary>Opcjonalna granica własnego radia, bez nowego właściciela konta.</summary>
public interface ISonosOwnStreamsSessionBackend
{
    Task<SonosSessionCreateResult> CreateSessionAsync(string? groupId,
        SonosSessionRequest request, CancellationToken cancellationToken);
    Task<SonosStreamUrlLoadResult> LoadStreamUrlAsync(string? sessionId, string? streamUrl,
        bool playOnCompletion, string? itemId, CancellationToken cancellationToken);
}
