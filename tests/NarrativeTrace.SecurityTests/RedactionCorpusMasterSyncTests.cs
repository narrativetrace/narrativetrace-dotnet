// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using Xunit;

namespace NarrativeTrace.SecurityTests;

/// <summary>
/// §6.7 (the rendering rule: the master is the source of truth, mirrors are byte-identical
/// with zero gaps preferred), extended to <c>redaction.json</c>: a corpus row is byte-identical
/// across every runtime or it is not the same row. This runtime's <c>redaction.json</c> is
/// already a full, byte-identical copy of the master file, so <see cref="DocumentedGaps"/> stays
/// declared, and empty — a future drift fails loudly here rather than silently reopening a gap
/// this class stopped watching for. Never assumed present — a checkout without the canonical Java
/// repo mounted stays green with a loud <see cref="SkippableFactAttribute"/> skip, matching
/// <see cref="MasterCorpusPath"/>'s own remarks.
/// </summary>
public sealed class RedactionCorpusMasterSyncTests
{
    private const string SkipReason =
        "master corpus not mounted — set JAVA_REPO, or mount the canonical Java repo at " +
        "/workspace-java or check it out as a host sibling, to run the redaction.json " +
        "byte-identity check against the master";

    /// <summary>
    /// Every master <c>redaction.json</c> row this runtime does not mirror byte-for-byte, with the
    /// reason — empty today. Kept as a named, typed seam rather than deleted: a future gap is
    /// documented here with a reason instead of silently widening the assertion below.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> DocumentedGaps =
        new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>
    /// The whole-file diff: every master row is either present locally and byte-identical, or
    /// named in <see cref="DocumentedGaps"/> — and every documented gap must actually still
    /// diverge (a resolved entry left behind here would hide a future, real drift on that row).
    /// </summary>
    [SkippableFact]
    public void Every_master_row_is_mirrored_or_a_documented_gap()
    {
        var masterFile = MasterCorpusPath.ResolveMasterRedactionFile();
        Skip.If(masterFile is null, SkipReason);
        Console.WriteLine($"master redaction.json compared against: {masterFile!.Value.ComparedRef}");

        var master = CorpusFileRows.ById(masterFile.Value.Content);
        var local = CorpusFileRows.ById(LocalRedactionPath());

        var undocumentedDrift = new List<string>();
        var resolvedGaps = new List<string>();

        foreach (var (id, masterText) in master)
        {
            var matches = local.TryGetValue(id, out var localText)
                && string.Equals(masterText, localText, StringComparison.Ordinal);
            if (!matches && !DocumentedGaps.ContainsKey(id))
                undocumentedDrift.Add($"{id}: differs from master and is not in DocumentedGaps");
        }

        foreach (var id in DocumentedGaps.Keys)
        {
            var stillDiverges = !master.TryGetValue(id, out var masterText)
                || !local.TryGetValue(id, out var localText)
                || !string.Equals(masterText, localText, StringComparison.Ordinal);
            if (!stillDiverges)
                resolvedGaps.Add($"{id}: now matches the master — remove its DocumentedGaps entry");
        }

        var extraLocal = local.Keys.Where(id => !master.ContainsKey(id)).ToList();

        Assert.True(undocumentedDrift.Count == 0, string.Join("\n", undocumentedDrift));
        Assert.True(resolvedGaps.Count == 0, string.Join("\n", resolvedGaps));
        Assert.True(extraLocal.Count == 0, "local-only rows not in the master: " + string.Join(", ", extraLocal));
    }

    private static string LocalRedactionPath() =>
        Path.Combine(AppContext.BaseDirectory, "HostileCorpus", "redaction.json");
}
