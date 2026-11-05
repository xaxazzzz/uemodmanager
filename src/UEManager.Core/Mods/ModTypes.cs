namespace UEManager.Core.Mods;

/// <summary>Short type names for the interface: PAK, IoStore, Blueprint, UE4SS Lua, UE4SS DLL, UE4SS files, game files, exe files.</summary>
public static class ModTypes
{
    /// <summary>A folder for UE4SS's Mods folder without code (<see cref="PackageKind.Ue4ssFolder"/>), on its own.</summary>
    public const string Ue4ssFiles = "UE4SS files";

    /// <summary>A file the download places in one of the game's own folders, such as a keybinding preset.</summary>
    public const string GameFiles = "Game files";

    /// <summary>Files that go next to the game's exe, such as a proxy DLL and its add-on.</summary>
    public const string ExeFiles = "Exe files";

    /// <summary>A mod folder of PBZModLoader, in the project's PBZModLoader/mods folder.</summary>
    public const string LoaderMod = "PBZModLoader mod";

    /// <summary>A mod folder of the Mods Menu, below the project's script folder.</summary>
    public const string MenuMod = "Mods Menu mod";

    /// <summary>The runtime of a script loader: its bootstrap, manager and support files, without the mods.</summary>
    public const string LoaderRuntime = "Loader files";

    /// <param name="files">Each file's deploy target and path below that target.</param>
    public static string Label(IEnumerable<(DeployTarget Target, string Path)> files)
    {
        var list = files.ToList();
        var paths = list.Select(f => "/" + f.Path.Replace('\\', '/')).ToList();
        if (paths.Any(p => HasSegments(p, PackageScanner.LoaderFolder, PackageScanner.LoaderModsFolder)))
            return LoaderMod;
        if (paths.Any(p => HasSegments(p, PackageScanner.MenuFolder, PackageScanner.MenuModsFolder)))
            return MenuMod;
        if (paths.Any(p => HasSegments(p, PackageScanner.LoaderFolder) || HasSegments(p, PackageScanner.MenuFolder)))
            return LoaderRuntime;
        if (list.Count > 0 && list.All(f => f.Target == DeployTarget.InstallRoot))
            return GameFiles;
        if (list.Count > 0 && list.All(f => f.Target == DeployTarget.ExeFolder))
            return ExeFiles;
        var ue4ss = list.Where(f => f.Target == DeployTarget.Ue4ssMods).Select(f => "/" + f.Path.Replace('\\', '/')).ToList();
        var lua = ue4ss.Any(p => p.Contains("/Scripts/", StringComparison.OrdinalIgnoreCase));
        var native = ue4ss.Any(p => p.Contains("/dlls/", StringComparison.OrdinalIgnoreCase));
        if (lua || native)
            return (lua, native) switch
            {
                (true, true) => "UE4SS Lua + DLL",
                (false, true) => "UE4SS DLL",
                _ => "UE4SS Lua",
            };
        // A UE4SS folder without code, such as the one a Blueprint mod writes its settings to, says nothing of the type.
        if (list.Any(f => f.Target == DeployTarget.LogicMods))
            return "Blueprint";
        if (list.All(f => f.Target == DeployTarget.Ue4ssMods) && list.Count > 0)
            return Ue4ssFiles;
        return list.Any(f => FileSystem.HasExtension(f.Path, FileSystem.IoStoreExtensions)) ? "IoStore" : "PAK";
    }

    /// <summary>Whether the path holds these folders one after the other, whatever the case.</summary>
    private static bool HasSegments(string path, params string[] segments)
    {
        var parts = path.Split('/');
        for (var i = 0; i + segments.Length <= parts.Length; i++)
        {
            var found = true;
            for (var j = 0; j < segments.Length; j++)
            {
                if (!parts[i + j].Equals(segments[j], StringComparison.OrdinalIgnoreCase))
                {
                    found = false;
                    break;
                }
            }
            if (found)
                return true;
        }
        return false;
    }
}
