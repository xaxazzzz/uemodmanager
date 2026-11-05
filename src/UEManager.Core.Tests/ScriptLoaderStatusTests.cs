using UEManager.Core.Games;
using UEManager.Core.Mods;
using UEManager.Core.Storage;

namespace UEManager.Core.Tests;

/// <summary>Discovery proves configuration, never successful script initialization.</summary>
public class ScriptLoaderStatusTests : IDisposable
{
    private readonly TestFolder folder = new();
    private readonly GameInstall game;

    public ScriptLoaderStatusTests()
    {
        game = GameLocator.Resolve(TestFolder.Profile(), folder.Game(), GameStore.Manual)!;
    }

    public void Dispose() => folder.Dispose();

    private string MenuFile(string text, DateTime written)
    {
        var path = folder.Combine("Game", "TestGame", "Content", "JavaScript", "PBZModsMenu", "discovered.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
        File.SetLastWriteTime(path, written);
        return path;
    }

    private string LoaderFile(string text, DateTime written)
    {
        var path = folder.Combine("Game", "TestGame", "Content", "JavaScript", "PBZModLoader", "generated-config.js");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
        File.SetLastWriteTime(path, written);
        return path;
    }

    private ModRecord ScriptMod(string id, bool menu = true) => new()
    {
        Name = id,
        Enabled = true,
        Files =
        [
            new ModFileRecord
            {
                FileName = menu ? $"TestGame/Content/JavaScript/PBZModsMenu/Mods/{id}/main.js" : $"TestGame/PBZModLoader/mods/{id}/main.js",
                Target = DeployTarget.InstallRoot,
                DeployedPath = folder.Combine("Game", "TestGame", "PBZModLoader", "mods", id, "main.js"),
            },
        ],
    };

    [Fact]
    public void A_fresh_menu_discovery_that_names_the_mod_says_it_was_discovered()
    {
        var now = DateTime.Now;
        MenuFile("""{"version":1,"mods":[{"id":"my-mod","entry":"PBZModsMenu/Mods/my-mod/main","enabled":true}]}""", now);
        var read = ScriptLoaders.Read(game);

        Assert.NotNull(read);
        Assert.Equal(LoaderRuntimeState.Discovered, ScriptLoaders.StateOf(ScriptMod("my-mod"), read, now.AddMinutes(-5)));
    }

    [Fact]
    public void The_loader_config_is_read_though_it_is_javascript()
    {
        var now = DateTime.Now;
        LoaderFile("'use strict';\nmodule.exports = {\"version\":1,\"mods\":[{\"id\":\"compiled\",\"enabled\":true,\"entry\":\"mods/compiled/main.js\"}]};", now);
        var read = ScriptLoaders.Read(game);

        Assert.Equal(LoaderRuntimeState.Discovered, ScriptLoaders.StateOf(ScriptMod("compiled", menu: false), read, now.AddMinutes(-5)));
    }

    [Fact]
    public void A_mod_the_loader_found_but_kept_off_says_so()
    {
        var now = DateTime.Now;
        MenuFile("""{"version":1,"mods":[{"id":"my-mod","entry":"PBZModsMenu/Mods/my-mod/main","enabled":false}]}""", now);

        Assert.Equal(LoaderRuntimeState.OffInLoader,
            ScriptLoaders.StateOf(ScriptMod("my-mod"), ScriptLoaders.Read(game), now.AddMinutes(-5)));
    }

    [Fact]
    public void A_fresh_discovery_without_the_mod_says_it_was_not_discovered()
    {
        var now = DateTime.Now;
        MenuFile("""{"version":1,"mods":[{"id":"other","entry":"PBZModsMenu/Mods/other/main","enabled":true}]}""", now);

        Assert.Equal(LoaderRuntimeState.NotDiscovered,
            ScriptLoaders.StateOf(ScriptMod("my-mod"), ScriptLoaders.Read(game), now.AddMinutes(-5)));
    }

    [Fact]
    public void A_discovery_from_before_the_game_start_is_stale_and_says_nothing()
    {
        var start = DateTime.Now.AddMinutes(-30);
        MenuFile("""{"version":1,"mods":[{"id":"my-mod","entry":"PBZModsMenu/Mods/my-mod/main","enabled":true}]}""", start.AddMinutes(-10));

        Assert.Equal(LoaderRuntimeState.Unverified,
            ScriptLoaders.StateOf(ScriptMod("my-mod"), ScriptLoaders.Read(game), start));
    }

    [Fact]
    public void Without_any_discovery_the_mod_is_unverified()
    {
        Assert.Null(ScriptLoaders.Read(game));
        Assert.Equal(LoaderRuntimeState.Unverified, ScriptLoaders.StateOf(ScriptMod("my-mod"), null, DateTime.Now.AddMinutes(-5)));
    }

    [Fact]
    public void A_disabled_mod_has_no_loader_state_and_a_pak_mod_neither()
    {
        var now = DateTime.Now;
        MenuFile("""{"version":1,"mods":[{"id":"my-mod","entry":"PBZModsMenu/Mods/my-mod/main","enabled":true}]}""", now);
        var read = ScriptLoaders.Read(game);
        var off = ScriptMod("my-mod");
        off.Enabled = false;
        var pak = new ModRecord
        {
            Name = "pak mod",
            Enabled = true,
            Files = [new ModFileRecord { FileName = "zMod_P.pak", Target = DeployTarget.Mods }],
        };

        Assert.Equal(LoaderRuntimeState.None, ScriptLoaders.StateOf(off, read, now.AddMinutes(-5)));
        Assert.Equal(LoaderRuntimeState.None, ScriptLoaders.StateOf(pak, read, now.AddMinutes(-5)));
    }

    [Fact]
    public void The_menu_mod_folder_of_an_adopted_menu_mod_is_recognized()
    {
        var now = DateTime.Now;
        MenuFile("""{"version":1,"mods":[{"id":"menu-mod","entry":"PBZModsMenu/Mods/menu-mod/main","enabled":true}]}""", now);
        var menu = new ModRecord
        {
            Name = "menu-mod",
            Enabled = true,
            Files =
            [
                new ModFileRecord
                {
                    FileName = "TestGame/Content/JavaScript/PBZModsMenu/Mods/menu-mod/main.js",
                    Target = DeployTarget.InstallRoot,
                },
            ],
        };

        Assert.Equal(LoaderRuntimeState.Discovered, ScriptLoaders.StateOf(menu, ScriptLoaders.Read(game), now.AddMinutes(-5)));
    }
}
