using AccessibleMediaController.Core.LocalMedia;
using AccessibleMediaController.Core.Sessions;
using AccessibleMediaController.Windows.Services;

namespace AccessibleMediaController.Windows;

public partial class MainWindow
{
    private bool SaveLocalPlaybackCheckpoint(DemoMediaSession local)
    {
        // Catalogue/metadata changes retain CaptureLocalMediaState and a full
        // snapshot. The timer only updates playback fields of existing records.
        var savedById = _state.LocalMedia.Items.ToDictionary(item => item.Id, StringComparer.Ordinal);
        if (_localItems.Any(item => !savedById.ContainsKey(item.Id))) return TrySaveLocalMediaState(false);
        CaptureCurrentSessionNavigationState();
        local.RememberCurrentPosition();
        var normalizer = new LocalFolderPathNormalizer();
        foreach (var item in _localItems)
        {
            var saved = savedById[item.Id];
            saved.ResumePositionTicks = ShouldRememberLocalPosition(saved, item, normalizer)
                ? Math.Max(0, local.RememberedPositions.GetValueOrDefault(item.Id).Ticks)
                : 0;
        }
        _state.LocalMedia.CurrentItemId = local.HasCurrentItem ? local.CurrentItem.Id : null;
        if (local.HasCurrentItem)
        {
            if (UsesSystemDefaultOutput("local")) _state.LocalMedia.Volume = local.Volume;
            if (savedById.GetValueOrDefault(local.CurrentItem.Id)?.PlaybackRateOverride is null)
                _state.LocalMedia.PlaybackRate = local.PlaybackRate;
        }
        return QueuePlaybackCheckpoint(PlaybackStateCheckpoint.CaptureLocal(_state));
    }
}
