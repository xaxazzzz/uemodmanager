using UEManager.Core.Games;
using UEManager.Core.Mods;
using UEManager.Core.Storage;

namespace UEManager.Core.Tests;

/// <summary>
/// PBZ's two script loaders: PBZModLoader (runtime below the project folder, bootstrap below the game's script
/// folder, mods in PBZModLoader/mods/&lt;id&gt;) and Mods Menu (below the script folder's PBZModsMenu, mods in
/// Mods/&lt;id&gt;). Each mod folder is one part, the loader's own runtime is another, and the proxy DLLs that
/// start a loader are alternatives: one of them, not all.
/// </summary>
public class PbzLoaderTests : IDisposable
{
    private readonly TestFolder folder = new();
    private readonly ModManager manager;
    private readonly GameInstall game;

    public PbzLoaderTests()
    {
        game = GameLocator.Resolve(TestFolder.Profile(), folder.Game(), GameStore.Manual)!;
        manager = new ModManager(game, new AppPaths(folder.Combine("Data")));
    }

    public void Dispose() => folder.Dispose();

    /// <summary>The test game's exe folder, as the download names it, with Windows separators.</summary>
    private string ExeFolder =>
        Path.GetRelativePath(game.RootPath, game.BinariesPath).Replace('/', '\\');

    /// <summary>A PBZModLoader mod: main.js and the files it loads, as Map Collectible Tracker ships.</summary>
    private string LoaderModDownload()
    {
        folder.File(@"Downloads\Tracker\TestGame\PBZModLoader\mods\pbz-map-tracker\main.js", "the start");
        folder.File(@"Downloads\Tracker\TestGame\PBZModLoader\mods\pbz-map-tracker\PbzMapState.js", "state");
        folder.File(@"Downloads\Tracker\TestGame\PBZModLoader\mods\pbz-map-tracker\PbzMapTracker.js", "tracker");
        folder.File(@"Downloads\Tracker\README.txt", "instructions");
        return folder.Combine("Downloads", "Tracker");
    }

    /// <summary>The loader itself: bootstrap scripts, its runtime and its compiler, as PBZModLoader 1.0.0 ships.</summary>
    private string LoaderDownload()
    {
        folder.File(@"Downloads\Loader\TestGame\Content\JavaScript\PBZModLoader\bootstrap.js", "the start");
        folder.File(@"Downloads\Loader\TestGame\Content\JavaScript\PBZModLoader\manager.js", "the manager");
        folder.File(@"Downloads\Loader\TestGame\PBZModLoader\mods.ini", "switches");
        folder.File(@"Downloads\Loader\TestGame\PBZModLoader\compiler\tsc.exe", "the compiler");
        folder.File(@"Downloads\Loader\TestGame\PBZModLoader\TOOLCHAIN-LICENSE.txt", "a license");
        folder.File($@"Downloads\Loader\{ExeFolder}\version.dll", "the first entry");
        folder.File($@"Downloads\Loader\{ExeFolder}\alt\version.dll", "the second entry");
        folder.File(@"Downloads\Loader\备用入口\dsound.dll", "backup dsound");
        folder.File(@"Downloads\Loader\备用入口\dxgi.dll", "backup dxgi");
        folder.File(@"Downloads\Loader\备用入口\hid.dll", "backup hid");
        folder.File(@"Downloads\Loader\备用入口\winmm.dll", "backup winmm");
        return folder.Combine("Downloads", "Loader");
    }

    /// <summary>The Mods Menu loader and its own mod, as Mods Menu 0.7.1 ships.</summary>
    private string MenuDownload()
    {
        folder.File(@"Downloads\Menu\TestGame\Content\JavaScript\PBZModsMenu\bootstrap.js", "the start");
        folder.File(@"Downloads\Menu\TestGame\Content\JavaScript\PBZModsMenu\manager.js", "the manager");
        folder.File(@"Downloads\Menu\TestGame\Content\JavaScript\PBZModsMenu\manifest.js", "the manifest");
        folder.File(@"Downloads\Menu\TestGame\Content\JavaScript\PBZModsMenu\Mods\PBZModsMenu\main.js", "the menu");
        folder.File($@"Downloads\Menu\{ExeFolder}\hid.dll", "the entry");
        return folder.Combine("Downloads", "Menu");
    }

    /// <summary>A loader mod with a pak-side settings file, as PVLW ships: the mod reads BossRush.json.</summary>
    private string ModWithPakSettings()
    {
        folder.File(@"Downloads\Pvlw\TestGame\PBZModLoader\mods\PVLW_2_GauntletOfMasters\main.js", "the start");
        folder.File(@"Downloads\Pvlw\TestGame\PBZModLoader\mods\PVLW_2_GauntletOfMasters\Gameplay\BossRush.js", "a mode");
        folder.File(@"Downloads\Pvlw\TestGame\Content\Paks\~mods\BossRush.json", "{ \"rush\": true }");
        return folder.Combine("Downloads", "Pvlw");
    }

    /// <summary>Inspects a download and keeps its packages and ignored files for the assertions.</summary>
    private InstallPreview Inspect(string source)
    {
        var preview = manager.Inspect(source);
        packages = preview.Packages.ToList();
        ignoredFiles = preview.IgnoredFiles.ToList();
        return preview;
    }

    private List<PackageGroup> packages = [];
    private List<string> ignoredFiles = [];

    [Fact]
    public void A_PBZModLoader_mod_folder_is_one_part_with_all_of_its_files()
    {
        using var _ = Inspect(LoaderModDownload());

        // The route to the Mods Menu comes beside the original; the mod itself stays one part with all of its files.
        var mod = Assert.Single(packages, p => !p.IsCompatibilityRoute);
        Assert.Equal(PackageKind.ScriptMod, mod.Kind);
        Assert.Equal("pbz-map-tracker", mod.Name);
        Assert.Equal(DeployTarget.InstallRoot, mod.Target);
        Assert.Equal(
        [
            "TestGame/PBZModLoader/mods/pbz-map-tracker/main.js",
            "TestGame/PBZModLoader/mods/pbz-map-tracker/PbzMapState.js",
            "TestGame/PBZModLoader/mods/pbz-map-tracker/PbzMapTracker.js",
        ], mod.RelativePaths);
        Assert.Equal(["Tracker/README.txt"], ignoredFiles);
    }

    [Fact]
    public void The_loader_runtime_is_its_own_part_separate_from_mods()
    {
        folder.File(@"Downloads\Both\TestGame\Content\JavaScript\PBZModLoader\bootstrap.js", "the start");
        folder.File(@"Downloads\Both\TestGame\Content\JavaScript\PBZModLoader\manager.js", "the manager");
        folder.File(@"Downloads\Both\TestGame\PBZModLoader\mods.ini", "switches");
        folder.File(@"Downloads\Both\TestGame\PBZModLoader\compiler\tsc.exe", "the compiler");
        folder.File(@"Downloads\Both\TestGame\PBZModLoader\mods\pbz-map-tracker\main.js", "the mod");
        using var _ = Inspect(folder.Combine("Downloads", "Both"));

        Assert.Equal(3, packages.Count); // the runtime, the mod, and the mod's route to the menu
        var runtime = Assert.Single(packages, p => p.Kind == PackageKind.ScriptRuntime);
        Assert.Equal("PBZModLoader", runtime.Name);
        Assert.Equal(DeployTarget.InstallRoot, runtime.Target);
        Assert.Equal(
        [
            "TestGame/Content/JavaScript/PBZModLoader/bootstrap.js",
            "TestGame/Content/JavaScript/PBZModLoader/manager.js",
            "TestGame/PBZModLoader/compiler/tsc.exe",
            "TestGame/PBZModLoader/mods.ini",
        ], runtime.RelativePaths);
        var mod = Assert.Single(packages, p => p.Kind == PackageKind.ScriptMod && !p.IsCompatibilityRoute);
        Assert.Equal("pbz-map-tracker", mod.Name);
    }

    [Fact]
    public void Each_menu_mod_is_its_own_part_and_the_menu_runtime_is_another()
    {
        using var _ = Inspect(MenuDownload());

        Assert.Equal(3, packages.Count);
        var entry = Assert.Single(packages, p => p.Name == "hid.dll");
        Assert.Equal(DeployTarget.InstallRoot, entry.Target);
        Assert.Equal([$"{ExeFolder.Replace('\\', '/')}/hid.dll"], entry.RelativePaths);
        var runtime = Assert.Single(packages, p => p.Kind == PackageKind.ScriptRuntime);
        Assert.Equal("PBZModsMenu", runtime.Name);
        Assert.Equal(
        [
            "TestGame/Content/JavaScript/PBZModsMenu/bootstrap.js",
            "TestGame/Content/JavaScript/PBZModsMenu/manager.js",
            "TestGame/Content/JavaScript/PBZModsMenu/manifest.js",
        ], runtime.RelativePaths);
        var menu = Assert.Single(packages, p => p.Kind == PackageKind.ScriptMod);
        Assert.Equal("PBZModsMenu", menu.Name);
        Assert.Equal(["TestGame/Content/JavaScript/PBZModsMenu/Mods/PBZModsMenu/main.js"], menu.RelativePaths);
    }

    [Fact]
    public void A_file_straight_in_the_mods_folder_is_not_a_mod()
    {
        folder.File(@"Downloads\Orphan\TestGame\PBZModLoader\mods\orphan.js", "not inside a mod folder");
        using var _ = Inspect(folder.Combine("Downloads", "Orphan"));

        Assert.Empty(packages);
        Assert.Equal(["Orphan/TestGame/PBZModLoader/mods/orphan.js"], ignoredFiles);
    }

    [Fact]
    public void Unrelated_project_files_stay_out()
    {
        folder.File(@"Downloads\Mixed\TestGame\PBZModLoader\mods\real-mod\main.js", "the mod");
        folder.File(@"Downloads\Mixed\TestGame\Saved\SaveGames\slot.sav", "the player's save");
        folder.File(@"Downloads\Mixed\TestGame\Content\Movies\intro.mp4", "a movie");
        using var _ = Inspect(folder.Combine("Downloads", "Mixed"));

        var mod = Assert.Single(packages, p => !p.IsCompatibilityRoute);
        Assert.Equal("real-mod", mod.Name);
        Assert.Equal(
        [
            "Mixed/TestGame/Content/Movies/intro.mp4",
            "Mixed/TestGame/Saved/SaveGames/slot.sav",
        ], ignoredFiles);
    }

    [Fact]
    public void A_pak_settings_file_goes_with_the_sole_mod_that_reads_it()
    {
        using var _ = Inspect(ModWithPakSettings());

        var mod = Assert.Single(packages, p => !p.IsCompatibilityRoute);
        Assert.Equal("PVLW_2_GauntletOfMasters", mod.Name);
        Assert.Contains("TestGame/Content/Paks/~mods/BossRush.json", mod.RelativePaths);
    }

    [Fact]
    public void The_proxy_DLLs_of_one_download_are_alternatives_of_each_other()
    {
        using var _ = Inspect(LoaderDownload());

        var entries = packages.Where(p => p.AlternativeOf is not null).ToList();
        Assert.Equal(6, entries.Count);
        Assert.All(entries, p => Assert.Equal(entries[0].AlternativeOf, p.AlternativeOf));
        Assert.All(entries, p => Assert.True(p.IsProxyEntry));
        // The loader's own files are not an alternative of anything.
        Assert.DoesNotContain(packages, p => p.AlternativeOf is not null && p.Kind is PackageKind.ScriptMod or PackageKind.ScriptRuntime);
    }

    [Fact]
    public void A_proxy_DLL_next_to_the_exe_is_a_known_loader_not_an_unknown_file()
    {
        using var _ = Inspect(MenuDownload());

        var entry = Assert.Single(packages, p => p.Name == "hid.dll");
        Assert.False(entry.IsUncertain);
        Assert.True(entry.IsProxyEntry);
    }

    [Fact]
    public void A_main_js_mod_can_be_routed_to_the_menu_instead_of_its_loader()
    {
        using var _ = Inspect(LoaderModDownload());

        var original = Assert.Single(packages, p => !p.IsCompatibilityRoute);
        var route = Assert.Single(packages, p => p.IsCompatibilityRoute);
        Assert.Equal(original.AlternativeOf, route.AlternativeOf);
        Assert.Equal(
        [
            "TestGame/Content/JavaScript/PBZModsMenu/Mods/pbz-map-tracker/main.js",
            "TestGame/Content/JavaScript/PBZModsMenu/Mods/pbz-map-tracker/PbzMapState.js",
            "TestGame/Content/JavaScript/PBZModsMenu/Mods/pbz-map-tracker/PbzMapTracker.js",
        ], route.RelativePaths);
        Assert.Equal(original.Files.Count, route.Files.Count);
    }

    [Fact]
    public void A_mod_without_main_js_keeps_its_original_place_only()
    {
        folder.File(@"Downloads\TsMod\TestGame\PBZModLoader\mods\ts-mod\index.ts", "export const mod = 1;");
        using var _ = Inspect(folder.Combine("Downloads", "TsMod"));

        var mod = Assert.Single(packages);
        Assert.Null(mod.AlternativeOf);
        Assert.False(mod.IsCompatibilityRoute);
    }

    [Fact]
    public void Selecting_two_alternatives_together_is_refused()
    {
        using var preview = Inspect(LoaderDownload());

        var entries = packages.Where(p => p.AlternativeOf is not null).Take(2).ToList();
        var check = manager.Check(preview, entries);
        Assert.False(check.CanInstall);
        Assert.Contains(check.Problems, p => p.ToString().Contains("alternative", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void An_unedited_script_config_is_handled_as_settings()
    {
        folder.File(@"Downloads\Configured\TestGame\PBZModLoader\mods\configured\main.js", "the mod");
        folder.File(@"Downloads\Configured\TestGame\PBZModLoader\mods\configured\config.json", "{ \"on\": true }");
        using var preview = Inspect(folder.Combine("Downloads", "Configured"));
        // The original place, not its route to the menu: the two are alternatives.
        var selected = preview.Packages.Where(p => !p.IsCompatibilityRoute).ToList();
        var mod = manager.Install(preview, selected, "Configured", null);

        var config = Assert.Single(mod.Files, f => f.FileName.EndsWith("config.json", StringComparison.OrdinalIgnoreCase));
        Assert.True(config.IsSettingsFile);
    }
}
