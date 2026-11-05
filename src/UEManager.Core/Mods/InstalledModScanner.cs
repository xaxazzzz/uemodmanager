using UEManager.Core.Games;

namespace UEManager.Core.Mods;

/// <summary>The kinds of mods the scan finds already in the game.</summary>
public enum InstalledKind
{
    /// <summary>A pak group in ~mods or LogicMods: a .pak with its IoStore companions.</summary>
    Pak,

    /// <summary>A UE4SS mod folder with Lua scripts or a native DLL.</summary>
    Ue4ss,

    /// <summary>A script mod folder of PBZModLoader or the Mods Menu.</summary>
    Script,

    /// <summary>A DLL next to the game's exe, such as a proxy of a loader installed by hand.</summary>
    ExeFiles,
}

/// <summary>One file of a candidate: where it is now, how big it is, and its checksum.</summary>
/// <param name="Path">The file's full path in the game.</param>
public sealed record InstalledFile(string Path, long Size, string Sha256);

/// <summary>
/// A mod already in the game that the manager does not know. A preview, not ownership: adopting is a separate,
/// explicit step. An uncertain candidate (a DLL next to the exe) starts unselected and needs the player's choice.
/// </summary>
/// <param name="Name">The mod's name: a pak's stem, or the mod folder's name.</param>
/// <param name="Uncertain">The player must pick this one explicitly, as for a DLL that stands in for a Windows library.</param>
/// <param name="Target">The folder the mod's record would deploy into.</param>
/// <param name="Files">The files as they are now, with their checksums.</param>
/// <param name="RelativePaths">Each file's path below its target folder, in the order of <paramref name="Files"/>.</param>
public sealed record InstalledCandidate(
    string Name,
    InstalledKind Kind,
    bool Uncertain,
    DeployTarget Target,
    IReadOnlyList<InstalledFile> Files,
    IReadOnlyList<string> RelativePaths)
{
    /// <summary>The identity that ties a candidate to a later scan of the same files.</summary>
    internal string Key => string.Join('|', Kind, Name, Path.GetDirectoryName(Files[0].Path) ?? "");
}

/// <param name="Ignored">Files the scan saw and left alone: documents, logs, shared runtime, and files of other tools.</param>
public sealed record InstalledScan(IReadOnlyList<InstalledCandidate> Candidates, IReadOnlyList<string> Ignored);

/// <summary>
/// Finds the mods already in the game that the manager does not manage: pak groups in the mods folders, UE4SS mod
/// folders, script mods of PBZModLoader and the Mods Menu, and DLLs next to the exe. Files the manager deployed,
/// the UE4SS runtime and its shared libraries, saves, logs and documents stay out. A group in which any file
/// belongs to another mod is refused whole, never adopted in part.
/// </summary>
public static class InstalledModScanner
{
    public static InstalledScan Scan(GameInstall game, IReadOnlySet<string> owned, IReadOnlyDictionary<string, string>? ue4ssFiles)
    {
        // Files of the UE4SS release the manager installed are owned through its manifest, wherever they sit.
        if (ue4ssFiles is not null)
        {
            var withManifest = new HashSet<string>(owned, StringComparer.OrdinalIgnoreCase);
            foreach (var key in ue4ssFiles.Keys)
                withManifest.Add(Path.GetFullPath(Path.Combine(game.BinariesPath, key.Replace('/', '\\'))));
            owned = withManifest;
        }

        var ue4ss = Ue4ssRuntime.Detect(game);
        var project = game.ProjectFolder ?? game.Profile.ProjectFolder;
        var roots = new List<(string Root, ScanKind Kind)>();
        void AddRoot(string path, ScanKind kind)
        {
            if (path.Length > 0 && Directory.Exists(path))
                roots.Add((Path.TrimEndingDirectorySeparator(path) + Path.DirectorySeparatorChar, kind));
        }
        AddRoot(game.ModsPath, ScanKind.PakFolder);
        AddRoot(game.LogicModsPath, ScanKind.PakFolder);
        AddRoot(ue4ss.ModsPath, ScanKind.Ue4ssFolder);
        if (!string.IsNullOrWhiteSpace(project))
        {
            AddRoot(Path.Combine(game.RootPath, project, PackageScanner.LoaderFolder, PackageScanner.LoaderModsFolder), ScanKind.LoaderMods);
            AddRoot(Path.Combine(game.RootPath, project, "Content", "JavaScript", PackageScanner.MenuFolder,
                PackageScanner.MenuModsFolder), ScanKind.LoaderMods);
        }
        AddRoot(game.BinariesPath, ScanKind.ExeFolder);
        foreach (var (root, _) in roots)
            EnsureNoReparseAbove(root);

        var ignored = new List<string>();
        var candidates = new List<InstalledCandidate>();
        var checksums = new Dictionary<string, InstalledFile>(StringComparer.OrdinalIgnoreCase);
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = false,
            AttributesToSkip = FileAttributes.ReparsePoint,
        };
        foreach (var (root, kind) in roots)
        {
            options.RecurseSubdirectories = kind != ScanKind.ExeFolder;
            var files = Directory.EnumerateFiles(Path.TrimEndingDirectorySeparator(root), "*", options)
                .Select(Path.GetFullPath)
                .ToList();
            switch (kind)
            {
                case ScanKind.PakFolder:
                    PakCandidates(root, files, owned, candidates, ignored, checksums,
                        string.Equals(Path.TrimEndingDirectorySeparator(root), Path.TrimEndingDirectorySeparator(game.LogicModsPath),
                            StringComparison.OrdinalIgnoreCase) ? DeployTarget.LogicMods : DeployTarget.Mods);
                    break;
                case ScanKind.Ue4ssFolder:
                    Ue4ssCandidates(root, files, owned, candidates, ignored, checksums);
                    break;
                case ScanKind.LoaderMods:
                    ScriptCandidates(game, root, files, owned, candidates, ignored, checksums);
                    break;
                case ScanKind.ExeFolder:
                    ExeCandidates(game, ue4ss, files, owned, candidates, ignored, checksums);
                    break;
            }
        }
        return new InstalledScan(candidates, ignored.Order(NaturalOrder.Instance).ToList());
    }

    /// <summary>Which files a scan root holds, so each is grouped by its own rules.</summary>
    private enum ScanKind
    {
        PakFolder,
        Ue4ssFolder,
        LoaderMods,
        ExeFolder,
    }

    /// <summary>
    /// Pak groups of one mods folder: a .pak with its IoStore companions, plus the settings file that carries the
    /// group's own stem. A group in which any file belongs to another mod is refused whole. Everything else of the
    /// folder, such as another mod's settings file, stays out and is shown as ignored.
    /// </summary>
    private static void PakCandidates(string root, List<string> files, IReadOnlySet<string> owned,
        List<InstalledCandidate> candidates, List<string> ignored, Dictionary<string, InstalledFile> checksums, DeployTarget target)
    {
        var adopted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in files.Where(f => FileSystem.HasExtension(f, FileSystem.PackageExtensions))
                     .GroupBy(f => Path.Combine(Path.GetDirectoryName(f)!, Stem(f)), StringComparer.OrdinalIgnoreCase))
        {
            var siblings = files.Where(f => string.Equals(Path.GetDirectoryName(f), Path.GetDirectoryName(group.First()),
                    StringComparison.OrdinalIgnoreCase) && string.Equals(Stem(f), Stem(group.First()), StringComparison.OrdinalIgnoreCase)
                && (FileSystem.HasExtension(f, FileSystem.PackageExtensions) || SettingsFiles.IsPakSettings(f))).ToList();
            if (siblings.Any(owned.Contains))
                continue;
            var ordered = siblings.Order(StringComparer.OrdinalIgnoreCase).ToList();
            adopted.UnionWith(ordered);
            candidates.Add(new InstalledCandidate(Stem(ordered[0]), InstalledKind.Pak, false,
                target,
                [.. ordered.Select(f => ChecksumOf(f, checksums))],
                [.. ordered.Select(f => RelativeTo(root, f))]));
        }
        ignored.AddRange(files.Where(f => !adopted.Contains(f) && !owned.Contains(f)));
    }

    /// <summary>UE4SS mod folders: one candidate per folder with Scripts\main.lua or dlls; UE4SS's shared libraries,
    /// loose files and folders without code stay out.</summary>
    private static void Ue4ssCandidates(string root, List<string> files, IReadOnlySet<string> owned,
        List<InstalledCandidate> candidates, List<string> ignored, Dictionary<string, InstalledFile> checksums)
    {
        var adopted = new List<string>();
        foreach (var folder in Directory.EnumerateDirectories(Path.TrimEndingDirectorySeparator(root)))
        {
            var name = Path.GetFileName(folder);
            if (name.Equals(SharedFiles.Folder, StringComparison.OrdinalIgnoreCase))
                continue;
            var sep = Path.TrimEndingDirectorySeparator(folder) + Path.DirectorySeparatorChar;
            var inside = files.Where(f => f.StartsWith(sep, StringComparison.OrdinalIgnoreCase)).ToList();
            var hasLua = inside.Any(f => Path.GetFileName(f).Equals("main.lua", StringComparison.OrdinalIgnoreCase)
                && Path.GetFileName(Path.GetDirectoryName(f)!).Equals("Scripts", StringComparison.OrdinalIgnoreCase));
            var hasNative = inside.Any(f => FileSystem.HasExtension(f, [".dll"])
                && Path.GetFileName(Path.GetDirectoryName(f)!).Equals("dlls", StringComparison.OrdinalIgnoreCase));
            if (!hasLua && !hasNative)
                continue;
            // A partially owned folder is refused whole: adopting the rest would make the mod look whole when it is not.
            if (inside.Any(owned.Contains))
                continue;
            var modFiles = inside.Where(IsModFile).Order(StringComparer.OrdinalIgnoreCase).ToList();
            adopted.AddRange(modFiles);
            candidates.Add(new InstalledCandidate(name, InstalledKind.Ue4ss, false, DeployTarget.Ue4ssMods,
                [.. modFiles.Select(f => ChecksumOf(f, checksums))],
                [.. modFiles.Select(f => RelativeTo(root, f))]));
        }
        foreach (var file in files.Where(f => !adopted.Contains(f) && !owned.Contains(f) && !ignored.Contains(f)))
            ignored.Add(file);
    }

    /// <summary>Script mod folders of one loader's mods folder, each kept whole below the install folder.</summary>
    private static void ScriptCandidates(GameInstall game, string root, List<string> files, IReadOnlySet<string> owned,
        List<InstalledCandidate> candidates, List<string> ignored, Dictionary<string, InstalledFile> checksums)
    {
        var adopted = new List<string>();
        foreach (var folder in Directory.EnumerateDirectories(Path.TrimEndingDirectorySeparator(root)))
        {
            var name = Path.GetFileName(folder);
            if (name.Equals(PackageScanner.MenuFolder, StringComparison.OrdinalIgnoreCase))
                continue; // The menu's own entry shares its runtime with every script mod.
            var sep = Path.TrimEndingDirectorySeparator(folder) + Path.DirectorySeparatorChar;
            var inside = files.Where(f => f.StartsWith(sep, StringComparison.OrdinalIgnoreCase)).ToList();
            if (inside.Any(owned.Contains)
                || !inside.Any(f => string.Equals(Path.GetDirectoryName(f), folder, StringComparison.OrdinalIgnoreCase)
                    && Path.GetFileName(f).ToLowerInvariant() is "main.js" or "main.ts"))
                continue;
            var modFiles = inside.Where(IsModFile)
                .Order(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (modFiles.Count == 0)
                continue;
            adopted.AddRange(modFiles);
            candidates.Add(new InstalledCandidate(name, InstalledKind.Script, false, DeployTarget.InstallRoot,
                [.. modFiles.Select(f => ChecksumOf(f, checksums))],
                [.. modFiles.Select(f => RelativeTo(game.RootPath, f))]));
        }
        foreach (var file in files.Where(f => !adopted.Contains(f) && !owned.Contains(f) && !ignored.Contains(f)))
            ignored.Add(file);
    }

    /// <summary>DLLs next to the game's exe: each is its own uncertain candidate, and UE4SS's own files stay out.</summary>
    private static void ExeCandidates(GameInstall game, Ue4ssRuntime ue4ss, List<string> files, IReadOnlySet<string> owned,
        List<InstalledCandidate> candidates, List<string> ignored, Dictionary<string, InstalledFile> checksums)
    {
        var modsSep = Path.TrimEndingDirectorySeparator(ue4ss.ModsPath) + Path.DirectorySeparatorChar;
        foreach (var file in files.Order(StringComparer.OrdinalIgnoreCase))
        {
            if (owned.Contains(file))
                continue;
            // UE4SS's own mod folders are scanned for their own candidates; its runtime files are never a mod.
            if (file.StartsWith(modsSep, StringComparison.OrdinalIgnoreCase) || IsUe4ssRuntimeFile(file, ue4ss)
                || !(FileSystem.HasExtension(file, [".asi", ".addon64", ".addon32"])
                    || FileSystem.HasExtension(file, [".dll"]) && PackageScanner.ProxyDllNames.Contains(
                        Path.GetFileNameWithoutExtension(file), StringComparer.OrdinalIgnoreCase)))
            {
                if (!ignored.Contains(file))
                    ignored.Add(file);
                continue;
            }
            candidates.Add(new InstalledCandidate(Path.GetFileName(file), InstalledKind.ExeFiles, true,
                DeployTarget.ExeFolder, [ChecksumOf(file, checksums)], [Path.GetFileName(file)]));
        }
    }

    /// <summary>UE4SS's own files: the proxy DLL, the runtime folder and its settings, whoever installed them.</summary>
    private static bool IsUe4ssRuntimeFile(string path, Ue4ssRuntime ue4ss)
    {
        if (Path.GetFileName(ue4ss.Folder).Equals("ue4ss", StringComparison.OrdinalIgnoreCase)
            && path.StartsWith(Path.TrimEndingDirectorySeparator(ue4ss.Folder) + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase))
            return true;
        var name = Path.GetFileName(path);
        return name.Equals("dwmapi.dll", StringComparison.OrdinalIgnoreCase)
            || name.Equals("xinput1_3.dll", StringComparison.OrdinalIgnoreCase)
            || name.Equals("UE4SS.dll", StringComparison.OrdinalIgnoreCase)
            || name.Equals("UE4SS.pdb", StringComparison.OrdinalIgnoreCase)
            || name.Equals("UE4SS-settings.ini", StringComparison.OrdinalIgnoreCase)
            || name.Equals("UE4SS.log", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>One reparse point above a scan root could lead anywhere: the scan refuses instead of following it.</summary>
    private static void EnsureNoReparseAbove(string root)
    {
        for (var folder = Path.TrimEndingDirectorySeparator(root); folder.Length > 3; folder = Path.GetDirectoryName(folder)!)
        {
            if (Directory.Exists(folder) && File.GetAttributes(folder).HasFlag(FileAttributes.ReparsePoint))
                throw new ModException($"The folder '{folder}' is a link to somewhere else, so the mods there cannot be scanned safely. Remove the link, or scan the real folder.");
        }
    }

    /// <summary>The stem a pak group is known by: the file name without its last extension.</summary>
    private static string Stem(string path) => Path.GetFileNameWithoutExtension(path);

    private static bool IsModFile(string path) => !SettingsFiles.IsDocument(path)
        && !FileSystem.HasExtension(path, [".log", ".tmp"]);

    private static string RelativeTo(string root, string path) =>
        Path.GetRelativePath(root, path).Replace('\\', '/');

    private static InstalledFile ChecksumOf(string path, Dictionary<string, InstalledFile> checksums)
    {
        if (checksums.TryGetValue(path, out var known))
            return known;
        var info = new FileInfo(path);
        var file = new InstalledFile(path, info.Length, FileSystem.Sha256(path));
        checksums[path] = file;
        return file;
    }
}
