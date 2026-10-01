using System.Threading;
using System.Threading.Tasks;

namespace AccessibleMediaController.Core.Sonos;

/// <summary>
/// OPCJONALNA granica ZMIANY SKLADU grup - dokladnie dwie operacje zapisu, zero
/// odczytu. Osobna i opcjonalna tak samo jak
/// <see cref="ISonosOwnStreamsSessionBackend"/>, zeby istniejace syntetyczne
/// zaplecza <see cref="ISonosGroupSessionBackend"/> nie musialy nagle umiec
/// przestawiac cudzych grup.
///
/// Odczyt topologii przed i po zapisie idzie ISTNIEJACYM
/// <see cref="ISonosGroupSessionBackend.ReadGroupsAsync"/> - nie dublujemy go tu.
/// </summary>
public interface ISonosGroupMembershipSessionBackend
{
    /// <summary>createGroup - z JAWNYM musicContextGroupId od wolajacego.</summary>
    Task<SonosGroupMembershipResult> CreateGroupAsync(
        string? householdId, SonosCreateGroupRequest? request, CancellationToken cancellationToken);

    /// <summary>setGroupMembers - ZASTAPIENIE skladu istniejacej grupy.</summary>
    Task<SonosGroupMembershipResult> SetGroupMembersAsync(
        string? groupId, SonosPlayerSet? players, CancellationToken cancellationToken);
}
