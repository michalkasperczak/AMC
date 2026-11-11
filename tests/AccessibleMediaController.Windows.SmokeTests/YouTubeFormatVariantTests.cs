using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Windows.Services;

internal static class YouTubeFormatVariantTests
{
    internal static void Run()
    {
        const string offer = """
        {"formats":[
          {"format_id":"18","ext":"mp4","vcodec":"avc1.42001E","acodec":"mp4a.40.2","width":640,"height":360,"tbr":500.5,"filesize":10000000},
          {"format_id":"137","ext":"mp4","vcodec":"avc1.640028","acodec":"none","width":1920,"height":1080,"tbr":1800.2,"filesize":100000000},
          {"format_id":"140","ext":"m4a","vcodec":"none","acodec":"mp4a.40.2","abr":128.3,"filesize_approx":10000000},
          {"format_id":"story","ext":"mhtml","vcodec":"none","acodec":"none"},
          {"format_id":"drm","ext":"mp4","vcodec":"avc1","acodec":"mp4a","has_drm":true}
        ]}
        """;
        var variants = YouTubeDownloadVariantCatalog.Parse(offer);
        if (variants.Count != 4)
            throw new Exception("Katalog nie rozdziela połączonego pliku, obrazu, audio i jawnego połączenia.");
        var merged = variants.Single(item => item.Selector == "137+140");
        if (merged.Extension != "mkv" || !merged.NeedsMerge
            || merged.Kind != "videoAudio" || merged.SizeBytes != 110_000_000
            || !merged.SizeApproximate || merged.Height != 1080)
            throw new Exception("Wariant połączenia nie zachowuje rzeczywistych parametrów strumieni.");
        if (YouTubeDownloadVariantCatalog.Match(variants, merged.Selector, merged.Signature) is null
            || YouTubeDownloadVariantCatalog.Match(variants, merged.Selector, "stale") is not null)
            throw new Exception("Podpis wyboru nie odrzuca nieaktualnej oferty.");
        var changed = YouTubeDownloadVariantCatalog.Parse(
            offer.Replace("\"height\":1080", "\"height\":720"));
        if (YouTubeDownloadVariantCatalog.Match(changed, merged.Selector, merged.Signature) is not null)
            throw new Exception("Zmiana jakości nie unieważnia wcześniejszego wyboru.");
        var otherVideo = YouTubeDownloadVariantCatalog.Parse(offer, "https://youtu.be/other");
        if (YouTubeDownloadVariantCatalog.Match(otherVideo, merged.Selector, merged.Signature) is not null)
            throw new Exception("Ten sam format innego filmu nie może użyć poprzedniego wyboru.");

        var selectedArgs = YouTubeMediaDownloader.BuildVideoArguments(
            "media.%(ext)s", "ffmpeg.exe", YouTubeVideoDownloadFormat.Original,
            "https://www.youtube.com/watch?v=example", merged.Selector);
        var selectorIndex = selectedArgs.ToList().IndexOf("--format");
        if (selectorIndex < 0 || selectedArgs[selectorIndex + 1] != "137+140"
            || !selectedArgs.Contains("--merge-output-format")
            || selectedArgs.Contains("--remux-video"))
            throw new Exception("Pobranie wybranego wariantu może przejść na inny format lub niechciany remuks MP4.");
        var ordinaryArgs = YouTubeMediaDownloader.BuildVideoArguments(
            "media.%(ext)s", "ffmpeg.exe", YouTubeVideoDownloadFormat.Mp4,
            "https://www.youtube.com/watch?v=example");
        var ordinaryIndex = ordinaryArgs.ToList().IndexOf("--format");
        if (ordinaryIndex < 0 || ordinaryArgs[ordinaryIndex + 1] != "bv*+ba/b"
            || !ordinaryArgs.Contains("--remux-video"))
            throw new Exception("Zmienił się dotychczasowy prosty wybór pobierania MP4.");
    }
}
