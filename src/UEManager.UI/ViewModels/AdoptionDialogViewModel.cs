using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UEManager.Core.Mods;
using UEManager.UI.Services;

namespace UEManager.UI.ViewModels;

/// <summary>The mods the player chose to take over, as the adoption dialog closes.</summary>
public sealed record AdoptionChoice(IReadOnlyList<InstalledCandidate> Candidates);

/// <summary>
/// The review step between scanning the game for mods installed by hand and adopting them: each candidate with its
/// type, its place in the game and its files, and a box per mod. A DLL next to the exe waits unticked for the
/// player's own choice, since a proxy DLL can belong to any tool.
/// </summary>
public sealed partial class AdoptionDialogViewModel : LocalizedObservableObject
{
    private readonly ModManager manager;
    private readonly InstalledScan scan;
    private readonly TaskCompletionSource<AdoptionChoice?> result = new();
    private bool selecting;

    public AdoptionDialogViewModel(ModManager manager, InstalledScan scan)
    {
        this.manager = manager;
        this.scan = scan;
        // Everything that looks like a whole mod is ticked; a lone DLL next to the exe waits for the player.
        selecting = true;
        Candidates = [.. scan.Candidates.Select(c => new InstalledCandidateViewModel(manager, c, OnOptionChanged) { IsSelected = !c.Uncertain })];
        selecting = false;
        RefreshCounts();
    }

    /// <summary>The scan this dialog shows, for the adoption that follows.</summary>
    public InstalledScan Scan => scan;

    public Task<AdoptionChoice?> Result => result.Task;

    public IReadOnlyList<InstalledCandidateViewModel> Candidates { get; }

    public string Summary
    {
        get
        {
            var parts = new List<string> { L.Plural(Candidates.Count, "{0} mod found in the game", "{0} mods found in the game") };
            if (scan.Ignored.Count > 0)
                parts.Add(L.Plural(scan.Ignored.Count, "{0} file left alone", "{0} files left alone"));
            return string.Join("  ·  ", parts);
        }
    }

    [ObservableProperty]
    private string adoptText = L.T("Adopt");

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AdoptCommand))]
    private string selectionText = "";

    [RelayCommand(CanExecute = nameof(CanAdopt))]
    private void Adopt() => result.TrySetResult(
        new AdoptionChoice(Candidates.Where(p => p.IsSelected).Select(p => p.Candidate).ToList()));

    private bool CanAdopt => Candidates.Any(p => p.IsSelected);

    [RelayCommand]
    private void Cancel() => result.TrySetResult(null);

    /// <summary>Alternatives of one group are one choice: selecting one clears the others. The candidates of the
    /// installed scan share no files, so nothing else has to give way.</summary>
    private void OnOptionChanged(InstalledCandidateViewModel option)
    {
        if (selecting)
            return;
        RefreshCounts();
    }

    private void RefreshCounts()
    {
        var selected = Candidates.Count(p => p.IsSelected);
        SelectionText = L.Plural(selected, "{0} selected", "{0} selected");
        OnPropertyChanged(nameof(Summary));
    }

    /// <summary>The files the scan saw and left alone, by name, so nothing hidden can go unnoticed.</summary>
    public string? IgnoredText
    {
        get
        {
            const int shown = 8;
            var ignored = scan.Ignored;
            if (ignored.Count == 0)
                return null;
            var names = string.Join(", ", ignored.Take(shown).Select(Path.GetFileName));
            if (ignored.Count > shown)
                names += "  " + L.Plural(ignored.Count - shown, "…and {0} more file", "…and {0} more files");
            return L.F($"Left alone: {names}");
        }
    }
}

/// <summary>One candidate in the adoption dialog: a mod already in the game, waiting to be taken over.</summary>
public sealed partial class InstalledCandidateViewModel(ModManager manager, InstalledCandidate candidate,
    Action<InstalledCandidateViewModel> changed) : LocalizedObservableObject
{
    public InstalledCandidate Candidate => candidate;

    public string Label => candidate.Name;

    /// <summary>The type the adopted mod would have, as the Mods list shows it.</summary>
    public string KindText => Format.Kind(ModTypes.Label(candidate.RelativePaths.Select(p => (candidate.Target, p))));

    /// <summary>Where the mod's files are now: the first file's folder in the game.</summary>
    public string DestinationText
    {
        get
        {
            var first = candidate.Files[0].Path;
            var folder = Path.GetDirectoryName(first)!;
            var root = Path.TrimEndingDirectorySeparator(manager.Game.RootPath) + Path.DirectorySeparatorChar;
            return folder.StartsWith(root, StringComparison.OrdinalIgnoreCase) ? folder[root.Length..] : folder;
        }
    }

    public string FilesText => string.Join("   ", candidate.RelativePaths.Select(p
        => p.StartsWith(candidate.Name + "/", StringComparison.OrdinalIgnoreCase) ? p[(candidate.Name.Length + 1)..] : p));

    public string SizeText => Format.Size(candidate.Files.Sum(f => f.Size));

    /// <summary>A DLL that stands in for a Windows library could belong to any tool, so the player picks it themselves.</summary>
    public bool IsUncertain => candidate.Uncertain;

    public string UncertainHint => L.T("This file stands next to the game's exe, where Windows loads whatever is there. Only tick it if you know which tool it belongs to.");

    [ObservableProperty]
    private bool isSelected;

    partial void OnIsSelectedChanged(bool value) => changed(this);
}
