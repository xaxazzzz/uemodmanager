using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using UEManager.Core.Games;

namespace UEManager.Core.Mods;

public enum LoaderRuntimeState { None, Unverified, Loaded, OffInLoader, NotDiscovered, Discovered, Failed }

public sealed record ScriptLoaderResult(LoaderRuntimeState State, DateTime At);
public sealed record ScriptLoaderDiscovery(DateTime WrittenAt, IReadOnlyDictionary<string, ScriptLoaderResult> Mods);

/// <summary>Discovery describes configuration; only timestamped import results prove that an entry loaded.</summary>
public static partial class ScriptLoaders
{
    public enum Which { None, Menu, Loader }

    public static Which Of(ModRecord mod) => Identity(mod).Loader;
    public static string? ModId(ModRecord mod) => Identity(mod).Id;

    private static (Which Loader, string? Id) Identity(ModRecord mod)
    {
        foreach (var file in mod.Files.Where(f => f.Target == DeployTarget.InstallRoot))
        {
            var parts = file.FileName.Split('/', '\\');
            for (var i = 0; i + 2 < parts.Length; i++)
            {
                if (parts[i].Equals(PackageScanner.MenuFolder, StringComparison.OrdinalIgnoreCase)
                    && parts[i + 1].Equals(PackageScanner.MenuModsFolder, StringComparison.OrdinalIgnoreCase))
                    return (Which.Menu, parts[i + 2]);
                if (parts[i].Equals(PackageScanner.LoaderFolder, StringComparison.OrdinalIgnoreCase)
                    && parts[i + 1].Equals(PackageScanner.LoaderModsFolder, StringComparison.OrdinalIgnoreCase))
                    return (Which.Loader, parts[i + 2]);
            }
        }
        return (Which.None, null);
    }

    private static string Key(Which loader, string id) => $"{loader}:{id}";

    public static ScriptLoaderDiscovery? Read(GameInstall game)
    {
        var project = game.ProjectFolder ?? game.Profile.ProjectFolder;
        if (string.IsNullOrWhiteSpace(project)) return null;
        var scripts = Path.Combine(game.RootPath, project, "Content", "JavaScript");
        var found = new Dictionary<string, ScriptLoaderResult>(StringComparer.OrdinalIgnoreCase);
        var outcomes = new Dictionary<string, ScriptLoaderResult>(StringComparer.OrdinalIgnoreCase);
        var menuRoutes = new Dictionary<string, Which>(StringComparer.OrdinalIgnoreCase);
        ReadDiscovery(Path.Combine(scripts, PackageScanner.MenuFolder, "discovered.txt"), Which.Menu, found, menuRoutes);
        ReadDiscovery(Path.Combine(scripts, PackageScanner.LoaderFolder, "generated-config.js"), Which.Loader, found, menuRoutes);
        foreach (var saved in new[] { Path.Combine(game.RootPath, project, "Saved"),
                     Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), project, "Saved") }
                     .Distinct(StringComparer.OrdinalIgnoreCase))
            ReadLog(Path.Combine(saved, "PBZModsMenuLoader-log.txt"), Which.Menu, outcomes, menuRoutes);
        var evidence = Path.Combine(game.RootPath, project, PackageScanner.LoaderFolder, "evidence");
        try
        {
            if (Directory.Exists(evidence))
            {
                var latest = Directory.EnumerateFiles(evidence, "loader.log", new EnumerationOptions
                {
                    RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint,
                }).MaxBy(File.GetLastWriteTimeUtc);
                if (latest is not null) ReadLog(latest, Which.Loader, outcomes, menuRoutes);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        foreach (var (key, outcome) in outcomes) found[key] = outcome;
        return found.Count == 0 ? null : new ScriptLoaderDiscovery(found.Values.Max(r => r.At), found);
    }

    public static LoaderRuntimeState StateOf(ModRecord mod, ScriptLoaderDiscovery? discovery, DateTime sessionStart)
    {
        var (loader, id) = Identity(mod);
        if (!mod.Enabled || loader == Which.None) return LoaderRuntimeState.None;
        if (discovery is null || id is null) return LoaderRuntimeState.Unverified;
        if (!discovery.Mods.TryGetValue(Key(loader, id), out var result)
            && !discovery.Mods.TryGetValue(Key(loader, "*"), out result)) return LoaderRuntimeState.Unverified;
        if (discovery.Mods.TryGetValue(Key(loader, "*"), out var failure) && failure.State == LoaderRuntimeState.Failed
            && (result.State is LoaderRuntimeState.Discovered or LoaderRuntimeState.NotDiscovered || failure.At >= result.At))
            result = failure;
        if (result.At < sessionStart || result.At.AddSeconds(1) < mod.InstalledAt.LocalDateTime)
            return LoaderRuntimeState.Unverified;
        return result.State;
    }

    private static void ReadDiscovery(string path, Which loader, Dictionary<string, ScriptLoaderResult> found,
        Dictionary<string, Which> menuRoutes)
    {
        try
        {
            if (!File.Exists(path) || new FileInfo(path).Length > 65536) return;
            var text = File.ReadAllText(path).Trim();
            if (loader == Which.Loader)
            {
                var start = text.IndexOf('{');
                if (start < 0) return;
                text = text[start..].TrimEnd(';').Trim();
            }
            using var document = JsonDocument.Parse(text);
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("mods", out var mods) || mods.ValueKind != JsonValueKind.Array) return;
            var at = File.GetLastWriteTime(path);
            found[Key(loader, "*")] = new(LoaderRuntimeState.NotDiscovered, at);
            foreach (var mod in mods.EnumerateArray())
            {
                if (mod.ValueKind != JsonValueKind.Object || !mod.TryGetProperty("id", out var name)
                    || name.ValueKind != JsonValueKind.String || name.GetString() is not { Length: > 0 } id) continue;
                var layout = loader;
                if (loader == Which.Menu)
                {
                    if (mod.TryGetProperty("entry", out var entry) && entry.ValueKind == JsonValueKind.String
                        && entry.GetString()!.Replace('\\', '/').Contains("PBZModLoader/mods/", StringComparison.OrdinalIgnoreCase))
                        layout = Which.Loader;
                    menuRoutes[id] = layout;
                }
                var off = mod.TryGetProperty("enabled", out var enabled) && enabled.ValueKind == JsonValueKind.False;
                found[Key(layout, id)] = new(off ? LoaderRuntimeState.OffInLoader : LoaderRuntimeState.Discovered, at);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { }
    }

    private static void ReadLog(string path, Which loader, Dictionary<string, ScriptLoaderResult> outcomes,
        IReadOnlyDictionary<string, Which> menuRoutes)
    {
        try
        {
            if (!File.Exists(path)) return;
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            // Keep log reads bounded; a partial first line cannot match a timestamped event.
            if (stream.Length > 2 * 1024 * 1024) stream.Seek(-2 * 1024 * 1024, SeekOrigin.End);
            using var reader = new StreamReader(stream);
            while (reader.ReadLine() is { } line)
            {
                var skipped = loader == Which.Loader && line.Contains("MODs skipped:", StringComparison.OrdinalIgnoreCase);
                if (loader == Which.Loader && !skipped && !line.Contains("[PBZMOD]", StringComparison.OrdinalIgnoreCase)) continue;
                var match = Outcome().Match(line);
                if (!match.Success && !skipped) continue;
                DateTime at;
                var firstSpace = line.IndexOf(' ');
                if (firstSpace > 0 && DateTimeOffset.TryParse(line[..firstSpace], CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeLocal, out var timestamp)) at = timestamp.LocalDateTime;
                else if (loader == Which.Loader && line.StartsWith('[') && line.IndexOf(']') is var end && end > 1
                         && TimeSpan.TryParse(line[1..end], CultureInfo.InvariantCulture, out var time))
                    at = File.GetLastWriteTime(path).Date + time;
                else continue;
                if (at > DateTime.Now.AddMinutes(5)) continue;
                var id = skipped ? "*" : match.Groups["id"].Value;
                var layout = loader == Which.Menu && menuRoutes.TryGetValue(id, out var route) ? route : loader;
                var key = Key(layout, id);
                var state = skipped ? LoaderRuntimeState.Failed : match.Groups["state"].Value.ToLowerInvariant() switch
                {
                    "loaded" => LoaderRuntimeState.Loaded,
                    "failed" => LoaderRuntimeState.Failed,
                    _ => LoaderRuntimeState.OffInLoader,
                };
                if (!outcomes.TryGetValue(key, out var previous) || at >= previous.At) outcomes[key] = new(state, at);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    [GeneratedRegex(@"(?:^|\s)(?<state>loaded|failed|disabled)\s+(?<id>[A-Za-z0-9_-]{1,128})(?=$|[\s:(])", RegexOptions.IgnoreCase)]
    private static partial Regex Outcome();
}
