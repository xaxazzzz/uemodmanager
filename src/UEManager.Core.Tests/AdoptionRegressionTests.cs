using UEManager.Core.Games;
using UEManager.Core.Mods;
using UEManager.Core.Storage;

namespace UEManager.Core.Tests;

public sealed class AdoptionRegressionTests : IDisposable
{
    private readonly TestFolder folder = new();
    private readonly GameInstall game;
    private readonly AppPaths paths;
    private readonly ModManager manager;

    public AdoptionRegressionTests()
    {
        game = GameLocator.Resolve(TestFolder.Profile(), folder.Game(), GameStore.Manual)!;
        paths = new AppPaths(folder.Combine("Data"));
        manager = new ModManager(game, paths, _ => false);
    }

    public void Dispose() => folder.Dispose();

    private string Put(string relative, string text = "mod")
    {
        var path = Path.Combine(game.RootPath, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
        return path;
    }

    [Fact]
    public void Ue4ss_round_trip_preserves_the_original_layout_and_settings()
    {
        Put("TestGame/Binaries/Win64/ue4ss/UE4SS.dll");
        var main = Put("TestGame/Binaries/Win64/ue4ss/Mods/LuaMod/Scripts/main.lua");
        var config = Put("TestGame/Binaries/Win64/ue4ss/Mods/LuaMod/Scripts/config.lua", "my setting");
        Put("TestGame/Binaries/Win64/ue4ss/Mods/LuaMod/enabled.txt", "");
        var scan = manager.ScanInstalled();
        var candidate = Assert.Single(scan.Candidates);
        Assert.Contains("LuaMod/Scripts/main.lua", candidate.RelativePaths);
        var mod = Assert.Single(manager.AdoptInstalled(scan, [candidate]));
        File.WriteAllText(config, "my new setting");
        manager.SetEnabled(mod.Id, false);
        manager.SetEnabled(mod.Id, true);
        Assert.True(File.Exists(main));
        Assert.Equal("my new setting", File.ReadAllText(config));
        Assert.False(Directory.Exists(Path.Combine(game.BinariesPath, "ue4ss/Mods/LuaMod/LuaMod")));
    }

    [Fact]
    public void Nested_paks_with_the_same_name_stay_separate_and_keep_their_paths()
    {
        var a = Put("TestGame/Content/Paks/~mods/A/zMod_P.pak", "A");
        var b = Put("TestGame/Content/Paks/~mods/B/zMod_P.pak", "B");
        var scan = manager.ScanInstalled();
        Assert.Equal(2, scan.Candidates.Count);
        var records = manager.AdoptInstalled(scan, scan.Candidates);
        foreach (var mod in records) manager.SetEnabled(mod.Id, false);
        foreach (var mod in records) manager.SetEnabled(mod.Id, true);
        Assert.Equal("A", File.ReadAllText(a));
        Assert.Equal("B", File.ReadAllText(b));
        Assert.False(File.Exists(Path.Combine(game.ModsPath, "zMod_P.pak")));
    }

    [Theory]
    [InlineData("../../escaped.pak")]
    [InlineData("different.pak")]
    [InlineData("absolute")]
    [InlineData("zMod_P.pak:stream")]
    public void Adoption_rejects_changed_target_mappings(string relative)
    {
        if (relative == "absolute") relative = folder.Combine("outside.pak");
        var source = Put("TestGame/Content/Paks/~mods/zMod_P.pak");
        var scan = manager.ScanInstalled();
        var original = Assert.Single(scan.Candidates);
        Assert.Throws<ModException>(() => manager.AdoptInstalled(scan,
            [original with { RelativePaths = [relative] }]));
        Assert.Empty(manager.Mods);
        Assert.Equal("mod", File.ReadAllText(source));
    }

    [Fact]
    public void Adoption_rejects_changed_targets_and_duplicate_candidates()
    {
        Put("TestGame/Content/Paks/~mods/zMod_P.pak");
        var scan = manager.ScanInstalled();
        var original = Assert.Single(scan.Candidates);
        Assert.Throws<ModException>(() => manager.AdoptInstalled(scan,
            [original with { Target = DeployTarget.ExeFolder }]));
        Assert.Throws<ModException>(() => manager.AdoptInstalled(scan, [original, original]));
        Assert.Empty(manager.Mods);
    }

    [Fact]
    public void Native_game_libraries_and_nested_dlls_are_not_mod_candidates()
    {
        Put("TestGame/Binaries/Win64/game-library.dll");
        Put("TestGame/Binaries/Win64/vendor/version.dll");
        Put("TestGame/Binaries/Win64/version.dll");
        Assert.Equal("version.dll", Assert.Single(manager.ScanInstalled().Candidates).Name);
    }

    [Fact]
    public void A_shared_folder_and_script_folders_without_an_entry_are_not_mods()
    {
        Put("TestGame/Binaries/Win64/ue4ss/UE4SS.dll");
        Put("TestGame/Binaries/Win64/ue4ss/Mods/shared/Scripts/main.lua");
        Put("TestGame/Content/JavaScript/PBZModsMenu/Mods/Docs/README.md");
        Put("TestGame/Content/JavaScript/PBZModsMenu/Mods/Logs/crash.log");
        Assert.Empty(manager.ScanInstalled().Candidates);
    }

    [Fact]
    public void A_pak_whose_matching_settings_are_owned_is_not_partially_adopted()
    {
        var config = Put("TestGame/Content/Paks/~mods/zMod_P.json", "mine");
        Put("TestGame/Content/Paks/~mods/zMod_P.pak");
        JsonFile.Write(paths.StateFile(game.Profile.Id), new ModState
        {
            Mods = [new ModRecord
            {
                Id = "config-owner", Name = "Config owner", Enabled = true,
                Files = [new ModFileRecord { FileName = "zMod_P.json", Target = DeployTarget.Mods, DeployedPath = config }],
            }],
        });
        Assert.Empty(new ModManager(game, paths, _ => false).ScanInstalled().Candidates);
    }

    [Fact]
    public void Updating_an_adopted_mod_keeps_its_existing_config_when_requested()
    {
        Put("TestGame/Content/Paks/~mods/zMod_P.pak", "old code");
        var config = Put("TestGame/Content/Paks/~mods/zMod_P.json", "my pre-adoption settings");
        var scan = manager.ScanInstalled();
        manager.AdoptInstalled(scan, scan.Candidates);
        folder.File("Downloads/Update/zMod_P.pak", "new code");
        folder.File("Downloads/Update/zMod_P.json", "new author defaults");
        using var preview = manager.Inspect(folder.Combine("Downloads", "Update"));
        manager.Install(preview, preview.Packages, "Updated mod", "2", replace: true, keepSettings: true);
        Assert.Equal("my pre-adoption settings", File.ReadAllText(config));
    }
    [Fact]
    public void Shared_menu_framework_is_not_an_ordinary_adoption_candidate()
    {
        Put("TestGame/Content/JavaScript/PBZModsMenu/Mods/PBZModsMenu/main.js");
        Assert.Empty(manager.ScanInstalled().Candidates);
    }

    [Fact]
    public void Adopted_settings_remain_unknown_and_backed_up_after_reverting_an_edit()
    {
        Put("TestGame/Content/JavaScript/PBZModsMenu/Mods/example/main.js");
        var config = Put("TestGame/Content/JavaScript/PBZModsMenu/Mods/example/config.json", "A");
        var scan = manager.ScanInstalled();
        var record = Assert.Single(manager.AdoptInstalled(scan, scan.Candidates));
        File.WriteAllText(config, "B");
        manager.SetEnabled(record.Id, false);
        manager.SetEnabled(record.Id, true);
        File.WriteAllText(config, "A");
        manager.SetEnabled(record.Id, false);
        Assert.Null(record.Files.Single(f => f.IsSettingsFile).DefaultSha256);
        manager.Uninstall(record.Id);
        Assert.Contains(Directory.GetFiles(paths.BackupDir(game.Profile.Id), "config.json", SearchOption.AllDirectories),
            f => File.ReadAllText(f) == "A");
    }
    [Fact]
    public void Unknown_download_can_be_mapped_explicitly_with_a_reviewable_destination()
    {
        var source = folder.File("Download/custom.asset", "asset");
        using var preview = manager.Inspect(Path.GetDirectoryName(source)!);
        Assert.Empty(preview.Packages);
        manager.MapUnrecognized(preview, "TestGame/CustomMods/example");
        var package = Assert.Single(preview.Packages);
        Assert.Equal("TestGame/CustomMods/example/custom.asset", Assert.Single(package.RelativePaths));
        var record = manager.Install(preview, preview.Packages, "Custom", null);
        var deployed = Path.Combine(game.RootPath, "TestGame/CustomMods/example/custom.asset");
        Assert.Equal("asset", File.ReadAllText(deployed));
        manager.SetEnabled(record.Id, false);
        Assert.False(File.Exists(deployed));
        manager.SetEnabled(record.Id, true);
        Assert.Equal("asset", File.ReadAllText(deployed));
    }

    [Theory]
    [InlineData("../outside")]
    [InlineData("E:/absolute")]
    [InlineData("TestGame:stream")]
    public void Unknown_download_rejects_unsafe_destinations(string destination)
    {
        var source = folder.File("Download/custom.asset");
        using var preview = manager.Inspect(Path.GetDirectoryName(source)!);
        Assert.Throws<ModException>(() => manager.MapUnrecognized(preview, destination));
        Assert.Empty(preview.Packages);
    }
    [Fact]
    public void Executable_and_install_root_aliases_have_one_owner()
    {
        var source = folder.File("Alias/TestGame/Binaries/Win64/version.dll", "proxy1");
        using var first = manager.Inspect(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(source)))!);
        var owner = manager.Install(first, first.Packages, "Owner", null);
        var replacement = folder.File("Loose/version.dll", "proxy2");
        using var second = manager.Inspect(Path.GetDirectoryName(replacement)!);
        Assert.Equal(owner.Id, manager.Check(second, second.Packages).Replaces?.Id);
    }
    [Theory]
    [InlineData("TestGame/Saved/SaveGames", "slot.sav")]
    [InlineData("TestGame", "Saved/SaveGames/slot.sav")]
    [InlineData(".", "TestGame/Saved/Logs/slot.sav")]
    [InlineData("TestGame/Saved/Crashes", "slot.sav")]
    [InlineData("TestGame/Saved/./SaveGames", "slot.sav")]
    [InlineData("TestGame/Saved/SaveGames.", "slot.sav")]
    public void Manual_mapping_never_takes_ownership_of_saves_or_generated_reports(string destination, string file)
    {
        var save = Put("TestGame/Saved/SaveGames/slot.sav", "current progress");
        folder.File("Unknown/" + file, "old progress");
        using var preview = manager.Inspect(folder.Combine("Unknown"));
        Assert.Throws<ModException>(() => manager.MapUnrecognized(preview, destination));
        Assert.Equal("current progress", File.ReadAllText(save));
        Assert.Empty(preview.Packages);
    }
}
