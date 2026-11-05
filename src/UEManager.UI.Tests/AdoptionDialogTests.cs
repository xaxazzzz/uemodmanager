using System.IO;
using UEManager.Core.Games;
using UEManager.Core.Mods;
using UEManager.Core.Storage;
using UEManager.UI.Services;
using UEManager.UI.ViewModels;
using Xunit;

namespace UEManager.UI.Tests;

/// <summary>The adoption review: what the scan found, what starts ticked, and what adopting returns.</summary>
[Collection("Language")]
public class AdoptionDialogTests : IDisposable
{
    private readonly string folder = Path.Combine(Path.GetTempPath(), "uemm-adoption-test-" + Guid.NewGuid().ToString("N"));
    private readonly ModManager manager;
    private readonly GameInstall game;

    public AdoptionDialogTests()
    {
        // A fake Unreal install, as the Core tests' TestFolder.Game makes one.
        Directory.CreateDirectory(Path.Combine(folder, "Engine", "Binaries", "Win64"));
        Directory.CreateDirectory(Path.Combine(folder, "TestGame", "Content", "Paks"));
        Directory.CreateDirectory(Path.Combine(folder, "TestGame", "Binaries", "Win64"));
        File.WriteAllText(Path.Combine(folder, "TestGame", "Binaries", "Win64", "TestGame-Win64-Shipping.exe"), "");
        File.WriteAllText(Path.Combine(folder, "TestGame.exe"), "");
        game = GameLocator.Resolve(TestGameProfile(), folder, GameStore.Manual)!;
        manager = new ModManager(game, new AppPaths(Path.Combine(folder, "Data")));
    }

    public void Dispose() => TryDelete(folder);

    private static GameProfile TestGameProfile() => new()
    {
        Id = "test-game",
        DisplayName = "Test Game",
        SteamAppId = "999999",
        ProjectFolder = "TestGame",
    };

    private static void TryDelete(string path)
    {
        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    /// <summary>A pak group in ~mods and a DLL next to the exe, as a hand-installed pair would sit.</summary>
    private InstalledScan Scan() => new(
    [
        new InstalledCandidate("zMod_P", InstalledKind.Pak, false, DeployTarget.Mods,
        [
            new InstalledFile(Path.Combine(game.ModsPath, "zMod_P.pak"), 3, "00"),
        ], ["zMod_P.pak"]),
        new InstalledCandidate("tool.dll", InstalledKind.ExeFiles, true, DeployTarget.ExeFolder,
        [
            new InstalledFile(Path.Combine(game.BinariesPath, "tool.dll"), 4, "01"),
        ], ["tool.dll"]),
    ], [Path.Combine(game.ModsPath, "README.md")]);

    [Fact]
    public void Whole_mods_start_ticked_and_a_dll_next_to_the_exe_waits()
    {
        var dialog = new AdoptionDialogViewModel(manager, Scan());

        Assert.Equal(2, dialog.Candidates.Count);
        Assert.True(Assert.Single(dialog.Candidates, c => c.Candidate.Kind == InstalledKind.Pak).IsSelected);
        var dll = Assert.Single(dialog.Candidates, c => c.Candidate.Kind == InstalledKind.ExeFiles);
        Assert.False(dll.IsSelected);
        Assert.True(dll.IsUncertain);
        Assert.NotEqual("", dll.UncertainHint);
    }

    [Fact]
    public async Task Adopt_returns_only_the_ticked_candidates()
    {
        var dialog = new AdoptionDialogViewModel(manager, Scan());
        // The pak starts ticked, so adopting is possible; with nothing ticked it is not.
        Assert.True(dialog.AdoptCommand.CanExecute(null));
        foreach (var candidate in dialog.Candidates)
            candidate.IsSelected = false;
        Assert.False(dialog.AdoptCommand.CanExecute(null));
        foreach (var candidate in dialog.Candidates)
            candidate.IsSelected = true;
        Assert.True(dialog.AdoptCommand.CanExecute(null));

        dialog.AdoptCommand.Execute(null);

        var choice = await dialog.Result;
        Assert.NotNull(choice);
        Assert.Equal(2, choice!.Candidates.Count);
    }

    [Fact]
    public void The_dialog_shows_the_type_the_destination_and_the_ignored_files()
    {
        var dialog = new AdoptionDialogViewModel(manager, Scan());
        var pak = Assert.Single(dialog.Candidates, c => c.Candidate.Kind == InstalledKind.Pak);

        Assert.Equal(Format.Kind(ModTypes.Label([(DeployTarget.Mods, "zMod_P.pak")])), pak.KindText);
        Assert.Contains("~mods", pak.DestinationText);
        Assert.Contains("README.md", dialog.IgnoredText);
        Assert.Contains("found in the game", dialog.Summary);
        Assert.Contains("file left alone", dialog.Summary);
    }

    [Fact]
    public void The_loader_states_get_their_own_texts_and_tones()
    {
        Assert.NotEqual("", Format.Runtime(LoaderRuntimeState.Loaded).Text);
        Assert.NotEqual("", Format.Runtime(LoaderRuntimeState.Discovered).Text);
        Assert.NotEqual("", Format.Runtime(LoaderRuntimeState.Failed).Text);
        Assert.NotEqual("", Format.Runtime(LoaderRuntimeState.OffInLoader).Text);
        Assert.NotEqual("", Format.Runtime(LoaderRuntimeState.NotDiscovered).Text);
        Assert.NotEqual("", Format.Runtime(LoaderRuntimeState.Unverified).Text);
        Assert.Equal(("", "", "", ""), Format.Runtime(LoaderRuntimeState.None));
        // A loaded mod and an unverified one must not share a tone: one is proven, the other is not.
        Assert.NotEqual(Format.Runtime(LoaderRuntimeState.Loaded).Tone, Format.Runtime(LoaderRuntimeState.Unverified).Tone);
    }

    [Fact]
    public void Script_compatibility_routes_are_explicit_and_mutually_exclusive()
    {
        var download = Path.Combine(folder, "Downloads", "Script", "TestGame", "PBZModLoader", "mods", "example");
        Directory.CreateDirectory(download);
        File.WriteAllText(Path.Combine(download, "main.js"), "exports.ready = true;");
        using var preview = manager.Inspect(Path.Combine(folder, "Downloads", "Script"));
        var dialog = new InstallDialogViewModel(manager, preview, () => { });
        var native = Assert.Single(dialog.Packages, p => !p.Package.IsCompatibilityRoute);
        var route = Assert.Single(dialog.Packages, p => p.Package.IsCompatibilityRoute);
        Assert.True(native.IsSelected);
        Assert.False(route.IsSelected);
        Assert.True(dialog.CanInstall);
        Assert.NotEqual(native.Label, route.Label);
        route.IsSelected = true;
        Assert.False(native.IsSelected);
        Assert.True(dialog.CanInstall);
    }

    [Fact]
    public void A_loader_download_selects_only_one_proxy_entry()
    {
        var download = Path.Combine(folder, "Downloads", "Loader");
        Directory.CreateDirectory(Path.Combine(download, "TestGame", "PBZModLoader"));
        File.WriteAllText(Path.Combine(download, "TestGame", "PBZModLoader", "mods.ini"), "[Loader]\nEnabled=1");
        Directory.CreateDirectory(Path.Combine(download, "TestGame", "Binaries", "Win64"));
        File.WriteAllText(Path.Combine(download, "TestGame", "Binaries", "Win64", "version.dll"), "proxy");
        Directory.CreateDirectory(Path.Combine(download, "alternate"));
        File.WriteAllText(Path.Combine(download, "alternate", "hid.dll"), "alternate proxy");
        using var preview = manager.Inspect(download);
        var dialog = new InstallDialogViewModel(manager, preview, () => { });
        Assert.Equal(DeployTarget.InstallRoot, Assert.Single(dialog.Packages, p => p.Package.IsProxyEntry && p.IsSelected).Package.Target);
        Assert.True(dialog.CanInstall);
        var alternative = dialog.Packages.First(p => p.Package.IsProxyEntry && !p.IsSelected);
        alternative.IsSelected = true;
        Assert.Single(dialog.Packages, p => p.Package.IsProxyEntry && p.IsSelected);
        Assert.True(dialog.CanInstall);
    }
    [Fact]
    public void Unknown_layout_requires_an_explicit_destination_before_install()
    {
        var download = Path.Combine(folder, "Downloads", "Custom");
        Directory.CreateDirectory(download);
        File.WriteAllText(Path.Combine(download, "custom.asset"), "asset");
        using var preview = manager.Inspect(download);
        var dialog = new InstallDialogViewModel(manager, preview, () => { });
        Assert.True(dialog.CanMapManually);
        Assert.False(dialog.CanInstall);
        dialog.ManualDestination = "../outside";
        dialog.MapManuallyCommand.Execute(null);
        Assert.False(dialog.CanInstall);
        dialog.ManualDestination = "TestGame/CustomMods";
        dialog.MapManuallyCommand.Execute(null);
        Assert.False(dialog.CanMapManually);
        Assert.True(dialog.CanInstall);
        Assert.Single(dialog.Packages);
    }
}
