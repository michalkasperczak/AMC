namespace AccessibleMediaController.Core.Updates;

public enum UpdateComponentKind
{
    Application,
    ServiceAdapter
}

public sealed record UpdateComponent(
    string Id,
    string Version,
    UpdateComponentKind Kind,
    Uri PackageUri,
    string Sha256,
    string Signature);

public sealed record UpdateCheckResult(
    DateTimeOffset CheckedAt,
    IReadOnlyList<UpdateComponent> AvailableComponents,
    string? Error = null);

public interface IUpdateService
{
    Task<UpdateCheckResult> CheckAsync(CancellationToken cancellationToken = default);
    Task DownloadAsync(UpdateComponent component, CancellationToken cancellationToken = default);
    Task StageAsync(UpdateComponent component, CancellationToken cancellationToken = default);
}

public sealed class UnconfiguredUpdateService : IUpdateService
{
    public Task<UpdateCheckResult> CheckAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new UpdateCheckResult(
            DateTimeOffset.UtcNow,
            [],
            "Serwer aktualizacji nie został jeszcze skonfigurowany."));
    }

    public Task DownloadAsync(UpdateComponent component, CancellationToken cancellationToken = default) =>
        Task.FromException(new InvalidOperationException("Serwer aktualizacji nie został jeszcze skonfigurowany."));

    public Task StageAsync(UpdateComponent component, CancellationToken cancellationToken = default) =>
        Task.FromException(new InvalidOperationException("Serwer aktualizacji nie został jeszcze skonfigurowany."));
}
