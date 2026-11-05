using System.Globalization;
using UEManager.Core.Mods;

namespace UEManager.UI.Services;

internal static class Format
{
    public static string Size(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => string.Format(CultureInfo.CurrentCulture, "{0:0.#} KB", bytes / 1024.0),
        < 1024L * 1024 * 1024 => string.Format(CultureInfo.CurrentCulture, "{0:0.#} MB", bytes / (1024.0 * 1024)),
        _ => string.Format(CultureInfo.CurrentCulture, "{0:0.##} GB", bytes / (1024.0 * 1024 * 1024)),
    };

    /// <summary>A mod's type for the player: Blueprint, UE4SS files, game files and exe files are translated; PAK, IoStore and UE4SS are names.</summary>
    public static string Kind(string typeLabel) => typeLabel switch
    {
        "Blueprint" => L.T("Blueprint"),
        ModTypes.Ue4ssFiles => L.T("UE4SS files"),
        ModTypes.GameFiles => L.T("Game files"),
        ModTypes.ExeFiles => L.T("Exe files"),
        ModTypes.LoaderRuntime => L.T("Loader files"),
        _ => typeLabel,
    };

    /// <summary>What the loaders' notes say of a script mod at the last game start, apart from deployed and enabled.</summary>
    public static (string Text, string Tone, string Glyph, string Hint) Runtime(LoaderRuntimeState state) => state switch
    {
        LoaderRuntimeState.Discovered => (L.T("Discovered"), "warn", "\uE9CE",
            L.T("The loader discovered this mod, but no successful load was recorded.")),
        LoaderRuntimeState.Failed => (L.T("Load failed"), "bad", "\uE7BA",
            L.T("The loader reported a failure for this mod. Check its log for details.")),
        LoaderRuntimeState.Loaded => (L.T("Loaded"), "good", "\uE73E",
            L.T("The script loader found this mod and loaded it at the last game start.")),
        LoaderRuntimeState.OffInLoader => (L.T("Off in the loader"), "warn", "\uE7BA",
            L.T("The script loader knows this mod, but has it switched off. Switch it on in the loader, or enable the mod here.")),
        LoaderRuntimeState.NotDiscovered => (L.T("Not discovered"), "warn", "\uE7BA",
            L.T("The script loader did not find this mod at the last game start. It may not load where the mod's files are.")),
        LoaderRuntimeState.Unverified => (L.T("Not verified"), "", "\uE9CE",
            L.T("No note of the last game start was found, so whether the loader loaded the mod is not known. Start the game once to find out.")),
        _ => ("", "", "", ""),
    };

    /// <summary>
    /// Wide letter spacing for labels, which WPF text cannot do by itself.
    /// Letters are joined with narrow no-break spaces so a word never wraps inside.
    /// Chinese and Japanese text, which has no capitals and no spaces between words, stays as it is.
    /// </summary>
    public static string Tracked(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return "";
        if (text.Any(c => c >= '⺀'))
            return text;
        var words = text.ToUpper(CultureInfo.CurrentCulture).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return string.Join("  ", words.Select(word => string.Join(' ', word.ToCharArray())));
    }
}
