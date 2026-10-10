// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using Xunit;

namespace NarrativeTrace.SecurityTests;

/// <summary>
/// §6.7 (the master is the source of truth, mirrors byte-identical): this port's
/// <c>feedback.json</c> is a verbatim copy of the Java master, or it is not the same corpus. Never
/// assumed present — a checkout without the canonical Java repo mounted stays green with a loud skip,
/// matching <see cref="MasterCorpusPath"/>'s own remarks.
/// </summary>
public sealed class FeedbackCorpusMasterSyncTests
{
    private const string SkipReason =
        "master corpus not mounted — set JAVA_REPO, or mount the canonical Java repo at " +
        "/workspace-java, to run the feedback.json byte-identity check against the master";

    [SkippableFact]
    public void Every_master_row_is_mirrored_byte_for_byte()
    {
        var masterFile = MasterCorpusPath.ResolveMasterFeedbackFile();
        Skip.If(masterFile is null, SkipReason);
        Console.WriteLine($"master feedback.json compared against: {masterFile!.Value.ComparedRef}");

        var master = CorpusFileRows.ById(masterFile.Value.Content);
        var local = CorpusFileRows.ById(LocalFeedbackPath());

        var drift = new List<string>();
        foreach (var (id, masterText) in master)
        {
            if (!local.TryGetValue(id, out var localText) || !string.Equals(masterText, localText, StringComparison.Ordinal))
            {
                drift.Add($"{id}: differs from master");
            }
        }

        var extraLocal = local.Keys.Where(id => !master.ContainsKey(id)).ToList();

        Assert.True(drift.Count == 0, string.Join("\n", drift));
        Assert.True(extraLocal.Count == 0, "local-only rows not in the master: " + string.Join(", ", extraLocal));
    }

    private static string LocalFeedbackPath() =>
        Path.Combine(AppContext.BaseDirectory, "HostileCorpus", "feedback.json");
}
