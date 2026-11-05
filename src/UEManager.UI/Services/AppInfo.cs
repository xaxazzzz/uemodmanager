using System.Reflection;

namespace UEManager.UI.Services;

internal static class AppInfo
{
    public const string Name = "PBZ Mod Hub";

    /// <summary>How the release zips start, as publish.ps1 names them.</summary>
    public const string ReleaseFilePrefix = "uemm";

    /// <summary>Where the original lives; works based on it keep linking here (NOTICE.md, term 1).</summary>
    public const string ProjectUrl = "https://github.com/KintaroModding/uemodmanager";

    /// <summary>GitHub's answer for the newest release.</summary>
    public const string LatestReleaseUrl = "https://api.github.com/repos/KintaroModding/uemodmanager/releases/latest";

    /// <summary>
    /// The newest release, read at every start for the update check, in order: copies of GitHub's answer with a copy of
    /// the zip on Cloudflare Pages (the KintaroModding/metadata repo, deployed on every push), then GitHub itself.
    /// </summary>
    // This modified build has no binary update channel; upstream updates would discard its adoption features.
    public static readonly IReadOnlyList<string> LatestReleaseUrls = [];

    public const string ReleasesPageUrl = ProjectUrl + "/releases/latest";

    /// <summary>The invite to the community's Discord server.</summary>
    public const string DiscordUrl = "https://discord.gg/8vtph8SCFe";

    public static string Version { get; } =
        Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0]
        ?? "0.0.0";

    /// <summary>A pre-release build, as set by PreRelease in Directory.Build.props.</summary>
    public static bool IsPreRelease { get; } = IsSwitchedOn("PreRelease");

    /// <summary>The openmods page is shown, as set by Catalog in Directory.Build.props. Its code is always built in.</summary>
    public static bool IsCatalogOn { get; } = IsSwitchedOn("Catalog");

    private static bool IsSwitchedOn(string name) =>
        Assembly.GetExecutingAssembly().GetCustomAttributes<AssemblyMetadataAttribute>()
            .Any(attribute => attribute.Key == name && string.Equals(attribute.Value, "true", StringComparison.OrdinalIgnoreCase));
}
