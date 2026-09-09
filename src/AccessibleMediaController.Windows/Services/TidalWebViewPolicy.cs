using Microsoft.Web.WebView2.Core;

namespace AccessibleMediaController.Windows.Services;

internal static class TidalWebViewPolicy
{
    // The user's Play gesture happens in WPF, not in the non-focusable SDK
    // document. Enable host-requested playback in this dedicated environment
    // only. Do not change browser/system preferences or disable web security.
    internal static CoreWebView2EnvironmentOptions CreateOptions() => new()
    {
        AdditionalBrowserArguments = "--autoplay-policy=no-user-gesture-required"
    };
}
