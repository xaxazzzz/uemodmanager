using UEManager.Core.Games;
using UEManager.Core.Mods;
using UEManager.Core.Storage;

namespace UEManager.Core.Tests;

/// <summary>
/// Mods the player installed by hand are already in the game, and the manager must take them over without moving or
/// redeploying a single file: a scan shows them as candidates (a preview, not ownership), and adopting copies their
/// current bytes into the library and marks the existing deployment enabled. Nothing in the game changes.
/// </summary>
public class ManualAdoptionTests : IDisposable
{
    private readonly TestFolder folder = new();
    private readonly ModManager manager;
    private readonly GameInstall game;

    public ManualAdoptionTests()
    {
        game = GameLocator.Resolve(TestFolder.Profile(), folder.Game(), GameStore.Manual)!;
        manager = new ModManager(game, new AppPaths(folder.Combine("Data")));
    }

    public void Dispose() => folder.Dispose();

    /// <summary>Writes a file with binary-ish content and returns its full path.</summary>
    private string GameFile(string relative, string content = "data")
    {
        var path = Path.Combine([game.RootPath, .. relative.Split('\\', '/')]);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    private string ModsFolder => game.ModsPath;

    private InstalledScan Scan()
    {
        scan = manager.ScanInstalled();
        return scan;
    }

    private InstalledScan scan = null!;

    [Fact]
    public void A_hand_installed_pak_group_is_one_candidate()
    {
        GameFile(@"TestGame\Content\Paks\~mods\zMod_P.pak", "the pak");
        GameFile(@"TestGame\Content\Paks\~mods\zMod_P.utoc", "the toc");
        GameFile(@"TestGame\Content\Paks\~mods\zMod_P.ucas", "the cas");

        var candidates = Scan().Candidates;

        var candidate = Assert.Single(candidates);
        Assert.Equal("zMod_P", candidate.Name);
        Assert.False(candidate.Uncertain);
        Assert.Equal(3, candidate.Files.Count);
        Assert.True(candidate.Files.All(f => f.Path.StartsWith(ModsFolder, StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void A_settings_file_next_to_its_pak_joins_it_and_another_stems_does_not()
    {
        GameFile(@"TestGame\Content\Paks\~mods\zMod_P.pak", "the pak");
        GameFile(@"TestGame\Content\Paks\~mods\zMod_P.json", "{ \"on\": true }");
        GameFile(@"TestGame\Content\Paks\~mods\other.json", "{ \"not\": \"mine\" }");

        var candidate = Assert.Single(Scan().Candidates);

        Assert.Equal(2, candidate.Files.Count);
        Assert.Contains(candidate.Files, f => f.Path.EndsWith("zMod_P.json", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(candidate.Files, f => f.Path.EndsWith("other.json", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void A_ue4ss_mod_folder_is_a_candidate_with_all_of_its_files()
    {
        GameFile(@"TestGame\Binaries\Win64\ue4ss\UE4SS.dll", "ue4ss");
        var modFolder = Path.Combine(Ue4ssRuntime.Detect(game).ModsPath, "LuaMod");
        Directory.CreateDirectory(Path.Combine(modFolder, "Scripts"));
        File.WriteAllText(Path.Combine(modFolder, "Scripts", "main.lua"), "the mod");
        File.WriteAllText(Path.Combine(modFolder, "Scripts", "config.lua"), "-- settings");
        File.WriteAllText(Path.Combine(modFolder, "enabled.txt"), "");

        var candidate = Assert.Single(Scan().Candidates);

        Assert.Equal("LuaMod", candidate.Name);
        Assert.Equal(3, candidate.Files.Count);
        Assert.Contains(candidate.Files, f => f.Path.EndsWith("main.lua", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(candidate.Files, f => f.Path.EndsWith("enabled.txt", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Loader_mod_folders_are_candidates_and_stay_separate()
    {
        GameFile(@"TestGame\PBZModLoader\mods\pbz-map-tracker\main.js", "the tracker");
        GameFile(@"TestGame\PBZModLoader\mods\pbz-map-tracker\PbzMapState.js", "state");
        GameFile(@"TestGame\Content\JavaScript\PBZModsMenu\Mods\menu-mod\main.js", "the menu mod");

        var candidates = Scan().Candidates;

        Assert.Equal(2, candidates.Count);
        Assert.All(candidates, c => Assert.Equal(InstalledKind.Script, c.Kind));
        var tracker = Assert.Single(candidates, c => c.Name == "pbz-map-tracker");
        Assert.Equal(2, tracker.Files.Count);
        Assert.Single(candidates, c => c.Name == "menu-mod");
    }

    [Fact]
    public void Managed_files_are_not_candidates()
    {
        folder.File(@"Downloads\Pak\zManaged_P.pak", "managed pak");
        using (var preview = manager.Inspect(folder.Combine("Downloads", "Pak")))
        {
            manager.Install(preview, preview.Packages.ToList(), preview.SuggestedName, preview.SuggestedVersion);
        }

        Assert.Empty(Scan().Candidates);
    }

    [Fact]
    public void The_ue4ss_runtime_is_never_a_candidate_whether_the_manager_installed_it_or_not()
    {
        GameFile(@"TestGame\Binaries\Win64\ue4ss\UE4SS.dll", "ue4ss");
        GameFile(@"TestGame\Binaries\Win64\ue4ss\Mods\shared\json\json.lua", "a library");
        GameFile(@"TestGame\Binaries\Win64\dwmapi.dll", "the proxy");
        Directory.CreateDirectory(folder.Combine("Data", "games", "test-game"));
        File.WriteAllText(folder.Combine("Data", "games", "test-game", "ue4ss.json"),
            """{"Version":"v3.0.1","Files":{"dwmapi.dll":"0000","ue4ss/UE4SS.dll":"0000"}}""");

        Assert.Empty(Scan().Candidates);
    }

    [Fact]
    public void Documents_logs_and_saved_files_are_ignored()
    {
        GameFile(@"TestGame\Content\Paks\~mods\README.md", "a readme");
        GameFile(@"TestGame\Content\Paks\~mods\crash.log", "a log");
        GameFile(@"TestGame\Saved\SaveGames\slot.sav", "a save");

        var result = Scan();

        Assert.Empty(result.Candidates);
        Assert.Contains(result.Ignored, f => f.EndsWith("README.md", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Ignored, f => f.EndsWith("crash.log", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(result.Ignored, f => f.EndsWith("slot.sav", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Known_proxy_DLLs_next_to_the_exe_wait_as_uncertain_candidates()
    {
        GameFile(@"TestGame\Binaries\Win64\tool.dll", "some tool");
        GameFile(@"TestGame\Binaries\Win64\version.dll", "a known proxy");
        GameFile(@"TestGame\Binaries\Win64\dwmapi.dll", "unrecorded proxy of a hand-installed UE4SS");
        GameFile(@"TestGame\Binaries\Win64\ue4ss\UE4SS.dll", "hand-installed ue4ss");

        var candidates = Scan().Candidates;

        var candidate = Assert.Single(candidates);
        Assert.Equal(InstalledKind.ExeFiles, candidate.Kind);
        Assert.True(candidate.Uncertain);
        Assert.Equal("version.dll", candidate.Name);
    }

    [Fact]
    public void A_reparse_point_at_a_scan_root_stops_the_scan()
    {
        var real = folder.Combine("real-mods");
        Directory.CreateDirectory(real);
        File.WriteAllText(Path.Combine(real, "zMod_P.pak"), "the pak");
        var link = ModsFolder;
        Directory.CreateDirectory(Path.GetDirectoryName(link)!);
        try
        {
            Run($"mklink /J \"{link}\" \"{real}\"");

            // A junction above or at a scan root could lead anywhere: the scan refuses instead of following it.
            Assert.Throws<ModException>(Scan);
        }
        finally
        {
            // Only the link goes; the folder it points to stays, so the test folder can go too.
            Run($"rmdir \"{link}\"");
        }
    }

    [Fact]
    public void Adopting_copies_the_files_and_marks_the_deployment_enabled_without_touching_the_game()
    {
        var pak = GameFile(@"TestGame\Content\Paks\~mods\zMod_P.pak", "the pak");
        var before = File.ReadAllBytes(pak);
        var candidates = Scan().Candidates;

        var adopted = Assert.Single(manager.AdoptInstalled(scan, candidates));

        Assert.True(adopted.Enabled);
        Assert.Equal(pak, adopted.Files.Single().DeployedPath);
        Assert.Null(adopted.Files.Single().ReplacedSha256);
        Assert.Equal(before, File.ReadAllBytes(pak));
        var copy = File.ReadAllText(Path.Combine(manager.LibraryFolderOf(adopted), "zMod_P.pak"));
        Assert.Equal("the pak", copy);
        Assert.Empty(manager.ScanInstalled().Candidates);
    }

    [Fact]
    public void Adopting_a_batch_that_fails_halfway_changes_nothing()
    {
        var first = GameFile(@"TestGame\Content\Paks\~mods\zFirst_P.pak", "the first");
        var second = GameFile(@"TestGame\Content\Paks\~mods\zSecond_P.pak", "the second");
        var candidates = Scan().Candidates;
        Assert.Equal(2, candidates.Count);
        // The second file is held open exclusively, so copying it into the library fails.
        using (File.Open(second, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            Assert.ThrowsAny<Exception>(() => manager.AdoptInstalled(scan, candidates));
        }

        Assert.Empty(manager.Mods);
        var library = Path.Combine(folder.Combine("Data"), "games", "test-game", "library");
        Assert.True(!Directory.Exists(library) || !Directory.EnumerateFileSystemEntries(library).Any());
        Assert.Equal("the first", File.ReadAllText(first));
        Assert.Equal("the second", File.ReadAllText(second));
    }

    [Fact]
    public void A_group_partly_owned_by_another_mod_is_refused()
    {
        folder.File(@"Downloads\Pak\zMixed_P.pak", "managed pak");
        using (var preview = manager.Inspect(folder.Combine("Downloads", "Pak")))
        {
            manager.Install(preview, preview.Packages.ToList(), preview.SuggestedName, preview.SuggestedVersion);
        }
        GameFile(@"TestGame\Content\Paks\~mods\zMixed_P.json", "{ \"on\": true }");

        Assert.Empty(Scan().Candidates);
    }

    [Fact]
    public void A_disabled_mod_still_owns_its_targets()
    {
        folder.File(@"Downloads\Pak\zOff_P.pak", "managed pak");
        string id;
        using (var preview = manager.Inspect(folder.Combine("Downloads", "Pak")))
        {
            id = manager.Install(preview, preview.Packages.ToList(), preview.SuggestedName, preview.SuggestedVersion).Id;
        }
        manager.SetEnabled(id, false);
        File.WriteAllText(Path.Combine(ModsFolder, "zOff_P.pak"), "the player put it back by hand");

        Assert.Empty(Scan().Candidates);
    }

    [Fact]
    public void Adoption_refuses_candidates_that_changed_after_the_scan()
    {
        var pak = GameFile(@"TestGame\Content\Paks\~mods\zMod_P.pak", "the pak");
        var candidates = Scan().Candidates;
        File.WriteAllText(pak, "changed by hand after the scan");

        Assert.Throws<ModException>(() => manager.AdoptInstalled(scan, candidates));
    }

    [Fact]
    public void Adoption_refuses_a_group_that_grew_after_the_scan()
    {
        GameFile(@"TestGame\Content\Paks\~mods\zMod_P.pak", "the pak");
        var candidates = Scan().Candidates;
        GameFile(@"TestGame\Content\Paks\~mods\zMod_P.json", "{ \"late\": true }");

        Assert.Throws<ModException>(() => manager.AdoptInstalled(scan, candidates));
    }

    [Fact]
    public void Disable_enable_and_uninstall_touch_only_the_adopted_mod()
    {
        var first = GameFile(@"TestGame\Content\Paks\~mods\zFirst_P.pak", "the first");
        var second = GameFile(@"TestGame\Content\Paks\~mods\zSecond_P.pak", "the second");
        var candidates = Scan().Candidates;
        var adopted = Assert.Single(manager.AdoptInstalled(scan, candidates.Where(c => c.Name == "zFirst_P").ToList()));

        manager.SetEnabled(adopted.Id, false);
        Assert.False(File.Exists(first));
        Assert.True(File.Exists(second));

        manager.SetEnabled(adopted.Id, true);
        Assert.Equal("the first", File.ReadAllText(first));

        manager.Uninstall(adopted.Id);
        Assert.False(File.Exists(first));
        Assert.True(File.Exists(second));
        Assert.False(Directory.Exists(manager.LibraryFolderOf(adopted)));
    }

    [Fact]
    public void An_adopted_config_survives_an_uninstall()
    {
        var config = GameFile(@"TestGame\PBZModLoader\mods\configured\config.json", "{ \"the player's\": 1 }");
        GameFile(@"TestGame\PBZModLoader\mods\configured\main.js", "the mod");
        var candidates = Scan().Candidates;
        var adopted = Assert.Single(manager.AdoptInstalled(scan, candidates));

        var file = Assert.Single(adopted.Files, f => f.FileName.EndsWith("config.json", StringComparison.OrdinalIgnoreCase));
        Assert.True(file.IsPlayerFile);

        manager.Uninstall(adopted.Id);

        // The mod is gone from the game, but the player's version of the config was backed up first.
        Assert.False(File.Exists(config));
        var backups = Directory.EnumerateFiles(folder.Combine("Data", "games", "test-game", "backups"), "*",
            SearchOption.AllDirectories).ToList();
        Assert.Contains(backups, f => f.EndsWith("config.json", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("{ \"the player's\": 1 }",
            File.ReadAllText(Assert.Single(backups, f => f.EndsWith("config.json", StringComparison.OrdinalIgnoreCase))));
    }

    [Fact]
    public void A_blocked_save_rolls_the_adoption_back()
    {
        var pak = GameFile(@"TestGame\Content\Paks\~mods\zMod_P.pak", "the pak");
        var candidates = Scan().Candidates;
        Directory.CreateDirectory(folder.Combine("Data", "games", "test-game"));
        using var hold = File.Open(folder.Combine("Data", "games", "test-game", "mods.json"), FileMode.OpenOrCreate,
            FileAccess.Read, FileShare.None);

        Assert.ThrowsAny<Exception>(() => manager.AdoptInstalled(scan, candidates));

        Assert.Empty(manager.Mods);
        Assert.Equal("the pak", File.ReadAllText(pak));
    }

    private static void Run(string arguments)
    {
        var start = new System.Diagnostics.ProcessStartInfo("cmd.exe", "/c " + arguments)
        {
            CreateNoWindow = true,
            UseShellExecute = false,
        };
        using var process = System.Diagnostics.Process.Start(start);
        process!.WaitForExit();
    }
}
