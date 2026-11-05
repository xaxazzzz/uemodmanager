namespace UEManager.Core.Mods;

/// <summary>
/// The adoption half of <see cref="ModManager"/>: a scan of the mods already in the game that the manager does not
/// know, and the transaction that takes them over. Adoption never moves, copies or deletes a file in the game: it
/// copies the current bytes into the library and marks the existing deployment enabled, so disable, enable, update
/// and uninstall work from there on. When anything fails, only the newly created library state and records go,
/// and the game and the previous mod list stay as they were.
/// </summary>
public sealed partial class ModManager
{
    /// <summary>Scans the game for mods the manager does not manage. A preview: owning nothing yet.</summary>
    public InstalledScan ScanInstalled()
    {
        lock (gate)
        {
            return InstalledModScanner.Scan(Game, OwnedPaths(), ReadUe4ssRecord()?.Files);
        }
    }

    /// <summary>
    /// Takes the selected candidates over: their current bytes go into the library, and each becomes an enabled
    /// mod whose files stay where they are. Before anything is copied, every candidate is checked against a fresh
    /// scan and its checksums, and against the files of installed mods and the UE4SS manifest. A batch commits
    /// once or is undone entirely.
    /// </summary>
    /// <exception cref="ModException">A candidate changed after the scan, belongs to another mod, or could not be read.</exception>
    public IReadOnlyList<ModRecord> AdoptInstalled(InstalledScan scan, IReadOnlyCollection<InstalledCandidate> selected)
    {
        lock (gate)
        {
            EnsureCanChange();
            if (selected.Count == 0)
                throw new ModException($"Select at least one mod to adopt.");
            if (selected.Any(c => c.Files.Count == 0 || c.Files.Count != c.RelativePaths.Count)
                || selected.Select(c => c.Key).Distinct(StringComparer.OrdinalIgnoreCase).Count() != selected.Count)
                throw new ModException($"Select each complete mod only once.");
            // What the scan saw must still be there, unchanged: a file that changed since is a different decision.
            var fresh = InstalledModScanner.Scan(Game, OwnedPaths(), ReadUe4ssRecord()?.Files)
                .Candidates.ToDictionary(c => c.Key, StringComparer.OrdinalIgnoreCase);
            foreach (var candidate in selected)
            {
                if (!fresh.TryGetValue(candidate.Key, out var current) || !SameFiles(candidate, current))
                    throw new ModException($"\"{candidate.Name}\" has changed in the game since the scan. Scan again, then adopt it.");
            }
            var chosen = selected.Select(c => fresh[c.Key]).ToList();
            var uniquePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var candidate in chosen)
                for (var i = 0; i < candidate.Files.Count; i++)
                    if (!uniquePaths.Add(candidate.Files[i].Path)
                        || !string.Equals(ArchiveStager.SafeTarget(TargetFolder(candidate.Target), candidate.RelativePaths[i]),
                            candidate.Files[i].Path, StringComparison.OrdinalIgnoreCase))
                        throw new ModException($"The selected mods overlap or have unsafe target paths. Scan again.");

            var records = new List<ModRecord>();
            var created = new List<string>();
            void Undo()
            {
                foreach (var record in records)
                    mods.Remove(record);
                foreach (var library in created)
                    FileSystem.TryDeleteDirectory(library);
            }
            try
            {
                foreach (var candidate in chosen)
                {
                    var record = new ModRecord
                    {
                        Id = Guid.NewGuid().ToString("N")[..12],
                        Name = candidate.Name,
                        InstalledAt = DateTimeOffset.Now,
                        Enabled = true,
                    };
                    var libraryFolder = LibraryFolderOf(record);
                    created.Add(libraryFolder);
                    Directory.CreateDirectory(libraryFolder);
                    for (var i = 0; i < candidate.Files.Count; i++)
                    {
                        var file = candidate.Files[i];
                        AddAdoptedToLibrary(record, candidate.Target, candidate.RelativePaths[i], file);
                    }
                    records.Add(record);
                }
                var after = InstalledModScanner.Scan(Game, OwnedPaths(), ReadUe4ssRecord()?.Files).Candidates;
                if (chosen.Any(c => !after.Any(current => current.Key == c.Key && SameFiles(c, current))))
                    throw new ModException($"The game files changed while they were being copied. Nothing was adopted. Scan again.");
                // All copies are in: the records join the list, and the list is saved last, as with every change.
                mods.AddRange(records);
                Commit(undo: Undo);
            }
            catch (Exception)
            {
                Undo();
                throw;
            }
            Log.Info($"Adopted {records.Count} mod(s) already in the game: "
                + $"{string.Join(", ", records.Select(r => $"\"{r.Name}\""))}.");
            return records;
        }
    }

    /// <summary>Every game path a record of the installed mods owns, whether it is enabled now or not: where its
    /// files are deployed, where its disabled files would go, and the files of the UE4SS release the manager put in.
    /// Paths are canonical, so the install folder and the exe folder name the same file alike.</summary>
    private IReadOnlySet<string> OwnedPaths()
    {
        var owned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in mods.SelectMany(m => m.Files))
        {
            var path = file.DeployedPath
                ?? Path.Combine(TargetFolder(file.Target), Local(file.FileName));
            owned.Add(Path.GetFullPath(path));
        }
        return owned;
    }

    /// <summary>Whether two scans saw the same files in the same shape: paths, sizes and checksums alike.</summary>
    private static bool SameFiles(InstalledCandidate candidate, InstalledCandidate current) =>
        candidate.Target == current.Target && candidate.Kind == current.Kind && candidate.Uncertain == current.Uncertain
        && candidate.RelativePaths.SequenceEqual(current.RelativePaths, StringComparer.OrdinalIgnoreCase)
        && candidate.Files.Count == current.Files.Count
        && !candidate.Files.Zip(current.Files).Any(pair
            => !string.Equals(pair.First.Path, pair.Second.Path, StringComparison.OrdinalIgnoreCase)
                || pair.First.Size != pair.Second.Size
                || !string.Equals(pair.First.Sha256, pair.Second.Sha256, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Copies one adopted file into the library and records it as deployed where it is. A settings file keeps
    /// Kept set and its defaults unknown: the manager never saw the author's original, so the player's current
    /// version is what lives on, and an uninstall backs it up instead of throwing it away.
    /// </summary>
    private void AddAdoptedToLibrary(ModRecord record, DeployTarget target, string relativePath, InstalledFile file)
    {
        if (!File.Exists(file.Path))
            throw new ModException($"{relativePath} is no longer in the game, so \"{record.Name}\" cannot be adopted. Scan again.");
        var entry = new ModFileRecord
        {
            FileName = relativePath,
            Target = target,
            Size = file.Size,
            Sha256 = file.Sha256,
            DeployedPath = Path.GetFullPath(file.Path),
            Kept = SettingsFiles.Is(target, relativePath),
        };
        var copy = LibraryPath(record, entry);
        Directory.CreateDirectory(Path.GetDirectoryName(copy)!);
        File.Copy(file.Path, copy);
        // The library copy must hold the same bytes the game holds: a copy that went wrong is caught here, not later.
        var copied = new FileInfo(copy);
        if (copied.Length != file.Size || !string.Equals(FileSystem.Sha256(copy), file.Sha256, StringComparison.OrdinalIgnoreCase))
            throw new ModException($"{relativePath} could not be copied faithfully, so \"{record.Name}\" was not adopted.");
        record.Files.Add(entry);
    }
}
