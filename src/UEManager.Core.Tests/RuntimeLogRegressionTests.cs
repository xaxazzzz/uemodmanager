using UEManager.Core.Games;
using UEManager.Core.Mods;

namespace UEManager.Core.Tests;

public sealed class RuntimeLogRegressionTests : IDisposable
{
    private readonly TestFolder folder = new();
    private readonly GameInstall game;
    private readonly DateTime now = DateTime.Now;

    public RuntimeLogRegressionTests() => game = GameLocator.Resolve(TestFolder.Profile(), folder.Game(), GameStore.Manual)!;
    public void Dispose() => folder.Dispose();

    private string Write(string relative, string text)
    {
        var path = Path.Combine(game.RootPath, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
        return path;
    }

    private ModRecord Mod(string id = "example", bool menu = true) => new()
    {
        Name = id, Enabled = true,
        Files = [new ModFileRecord
        {
            Target = DeployTarget.InstallRoot,
            FileName = menu ? $"TestGame/Content/JavaScript/PBZModsMenu/Mods/{id}/main.js"
                : $"TestGame/PBZModLoader/mods/{id}/main.js",
        }],
    };

    private void Discover(bool menu = true, string? entry = null, bool enabled = true)
    {
        var text = System.Text.Json.JsonSerializer.Serialize(new { version = 1, mods = new[]
        {
            new { id = "example", entry = entry ?? "PBZModsMenu/Mods/example/main", enabled },
        }});
        Write(menu ? "TestGame/Content/JavaScript/PBZModsMenu/discovered.txt"
            : "TestGame/Content/JavaScript/PBZModLoader/generated-config.js", menu ? text : "module.exports = " + text + ";");
    }

    private string MenuLog(string lines) => Write("TestGame/Saved/PBZModsMenuLoader-log.txt", lines);
    private string Line(string result, DateTime? time = null) => $"{(time ?? now).ToUniversalTime():O} {result}\n";
    private LoaderRuntimeState State(ModRecord mod) => ScriptLoaders.StateOf(mod, ScriptLoaders.Read(game), now.AddMinutes(-5));

    [Fact]
    public void Discovery_is_not_proof_of_successful_initialization()
    {
        Discover();
        Assert.NotEqual(LoaderRuntimeState.Loaded, State(Mod()));
    }

    [Fact]
    public void A_real_loaded_line_proves_the_entry_loaded_without_a_manifest()
    {
        MenuLog(Line("loaded example (no start confirmation)"));
        Assert.Equal(LoaderRuntimeState.Loaded, State(Mod()));
    }

    [Fact]
    public void A_failed_entry_is_not_reported_as_loaded_just_because_it_was_discovered()
    {
        Discover();
        MenuLog(Line("failed example: missing dependency") + Line("runtime degraded: 1 loaded, 1 failed"));
        Assert.Equal("Failed", State(Mod()).ToString());
    }

    [Fact]
    public void Two_loaders_with_the_same_id_do_not_share_a_runtime_result()
    {
        Discover();
        Discover(menu: false);
        MenuLog(Line("failed example: menu-only failure"));
        Write("TestGame/PBZModLoader/evidence/current/loader.log", $"[{now:HH:mm:ss.fff}] [PBZMOD] loaded example\n");
        Assert.Equal("Failed", State(Mod()).ToString());
        Assert.Equal(LoaderRuntimeState.Loaded, State(Mod(menu: false)));
    }

    [Fact]
    public void A_menu_manifest_for_another_loader_layout_does_not_match_by_id_alone()
    {
        Discover();
        MenuLog(Line("loaded example"));
        Assert.NotEqual(LoaderRuntimeState.Loaded, State(Mod(menu: false)));
    }

    [Fact]
    public void Explicit_external_menu_entries_report_their_actual_original_layout()
    {
        Discover(entry: "../../../PBZModLoader/mods/example/main");
        MenuLog(Line("loaded example"));
        Assert.Equal(LoaderRuntimeState.Loaded, State(Mod(menu: false)));
        Assert.NotEqual(LoaderRuntimeState.Loaded, State(Mod()));
    }

    [Fact]
    public void Old_event_timestamps_are_stale_even_when_the_file_was_written_recently()
    {
        MenuLog(Line("loaded example", now.AddHours(-1)));
        Assert.NotEqual(LoaderRuntimeState.Loaded, State(Mod()));
    }

    [Fact]
    public void A_rebuilt_shorter_log_is_read_without_an_old_offset()
    {
        MenuLog(Line("failed example: " + new string('x', 2000)));
        Assert.Equal("Failed", State(Mod()).ToString());
        MenuLog(Line("loaded example"));
        Assert.Equal(LoaderRuntimeState.Loaded, State(Mod()));
    }

    [Fact]
    public void An_update_after_the_runtime_event_is_not_verified_by_the_old_event()
    {
        MenuLog(Line("loaded example"));
        var updated = Mod();
        updated.InstalledAt = new DateTimeOffset(now.AddMinutes(1));
        Assert.NotEqual(LoaderRuntimeState.Loaded, State(updated));
    }

    [Fact]
    public void Native_evidence_without_an_import_result_does_not_prove_loading()
    {
        Discover(menu: false);
        Write("TestGame/PBZModLoader/evidence/current/loader.log", $"[{now:HH:mm:ss.fff}] MODs skipped: Instruction signature mismatch\n");
        Assert.NotEqual(LoaderRuntimeState.Loaded, State(Mod(menu: false)));
    }
    [Fact]
    public void Native_signature_skip_marks_discovered_native_mods_failed()
    {
        Discover(menu: false);
        Write("TestGame/PBZModLoader/evidence/current/loader.log",
            $"[{now:HH:mm:ss.fff}] MODs skipped: Script validation signatures missing or ambiguous; original game continues\n");
        Assert.Equal(LoaderRuntimeState.Failed, State(Mod(menu: false)));
    }
}
