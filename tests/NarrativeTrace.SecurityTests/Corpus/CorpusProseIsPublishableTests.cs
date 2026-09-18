// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Text.RegularExpressions;
using Xunit;

namespace NarrativeTrace.SecurityTests.Corpus;

/// <summary>
/// The corpus's own PROSE — the fields a reader reads, not the bytes a case plants — must be
/// publishable as it stands.
/// </summary>
/// <remarks>
/// <para>
/// INTENT: the hostile corpus is the cross-port master copy — every runtime mirrors these files
/// byte-identically (see <see cref="HostileCorpusMasterSyncTests"/>, <see cref="RedactionCorpusMasterSyncTests"/>),
/// and they ship in the public snapshot of each. A row written during private work therefore
/// carries that work's vocabulary straight into four public repositories — a commit SHA nobody
/// outside can resolve, or the name of an internal process — and the publish reference gate
/// rejects the mirrored file downstream, in a repository whose author cannot fix the text.
/// Catching it here, in the master copy's own mirror, keeps the fix at the source.
/// </para>
/// <para>
/// @llmNote Scans <c>Id</c>, <c>Kind</c>, <c>Description</c> and (for a graph row) <c>Member</c>
/// ONLY. The payload fields — a canary, a value, a field name — are the hostile data itself: a
/// national-id shape or a token fixture may legitimately be a long run of hex digits, and a
/// deny-list vocabulary row may legitimately name any field an application ever declared. The
/// prose fields are the ones written for a human, and they are the ones that must read as a
/// statement of the rule the row pins.
/// </para>
/// <para>
/// @edgeCase The hex rule is deliberately bounded at seven characters, the shortest abbreviated
/// SHA git resolves. Shorter runs — <c>cafe</c>, <c>dead</c>, a four-digit year — are ordinary
/// English and ordinary data.
/// </para>
/// </remarks>
public sealed partial class CorpusProseIsPublishableTests
{
    /// <summary>An abbreviated or full commit SHA: what a public reader cannot resolve.</summary>
    [GeneratedRegex(@"\b[0-9a-f]{7,40}\b")]
    private static partial Regex CommitSha();

    /// <summary>Vocabulary of the private process, never of the rule a row pins.</summary>
    [GeneratedRegex(@"pair\s*#|\bagent\b|\bcoordinator\b", RegexOptions.IgnoreCase)]
    private static partial Regex PrivateProcess();

    [Fact]
    public void No_graph_row_prose_carries_a_commit_sha_or_the_vocabulary_of_the_private_process()
    {
        var offending = new List<string>();
        foreach (var graphCase in HostileCorpus.Graphs())
        {
            CollectOffenses(
                graphCase.Id,
                [graphCase.Id, graphCase.Kind, graphCase.Description, graphCase.Member],
                offending);
        }

        Assert.True(offending.Count == 0, string.Join("\n", offending));
    }

    [Fact]
    public void No_redaction_row_prose_carries_a_commit_sha_or_the_vocabulary_of_the_private_process()
    {
        var offending = new List<string>();
        foreach (var redactionCase in HostileCorpus.Redactions())
        {
            CollectOffenses(
                redactionCase.Id,
                [redactionCase.Id, redactionCase.Kind, redactionCase.Description],
                offending);
        }

        Assert.True(offending.Count == 0, string.Join("\n", offending));
    }

    private static void CollectOffenses(
        string id, IReadOnlyList<string?> fields, List<string> offending)
    {
        foreach (var field in fields)
        {
            if (field is null)
            {
                continue;
            }

            CollectMatches(id, field, CommitSha(), offending);
            CollectMatches(id, field, PrivateProcess(), offending);
        }
    }

    private static void CollectMatches(
        string id, string field, Regex pattern, List<string> offending)
    {
        foreach (Match match in pattern.Matches(field))
        {
            offending.Add(
                $"{id}: corpus prose ships publicly and must name the rule, not the private work — \"{match.Value}\"");
        }
    }
}
