// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
#nullable enable
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace NarrativeTrace.Build;

/// <summary>
/// Backs the <c>Approve</c> target: promotes every <c>*.received.nt</c>
/// approval trace under the repository root onto its <c>*.approved.nt</c>
/// baseline.
/// </summary>
/// <remarks>
/// A thin, Nuke-independent mirror of
/// <c>NarrativeTrace.Core.NarrativeApproval.PromoteReceived</c> (kept
/// separate rather than referenced, like every other <c>*Support.cs</c> in
/// this project — the build script takes no dependency on the product it
/// builds) — walking the whole repository rather than one configured
/// directory, since a solution-wide <c>Approve</c> run has no single test
/// project's <c>NARRATIVETRACE_APPROVED_DIR</c> to read, and
/// <c>*.received.nt</c> is a distinctive, single-purpose filename pattern
/// that cannot collide with anything else tracked here.
/// </remarks>
internal static class ApproveNarrativesSupport
{
    private const string ReceivedSuffix = ".received.nt";
    private const string ApprovedSuffix = ".approved.nt";

    /// <summary>
    /// Promotes every <c>*.received.nt</c> found under <paramref name="root"/>
    /// to its <c>*.approved.nt</c> sibling.
    /// </summary>
    /// <returns>The approved traces written, in no guaranteed order; empty when there is nothing to promote.</returns>
    internal static IReadOnlyList<string> PromoteReceived(string root)
    {
        if (!Directory.Exists(root))
        {
            return [];
        }

        var promoted = new List<string>();
        foreach (var file in Directory.EnumerateFiles(root, "*" + ReceivedSuffix, SearchOption.AllDirectories))
        {
            var approved = ApprovedSibling(file);
            if (File.Exists(approved))
            {
                File.Delete(approved);
            }

            File.Move(file, approved);
            promoted.Add(approved);
        }

        return promoted;
    }

    private static string ApprovedSibling(string receivedFile)
    {
        var dir = Path.GetDirectoryName(receivedFile) ?? string.Empty;
        var name = Path.GetFileName(receivedFile).Replace(ReceivedSuffix, ApprovedSuffix);
        return Path.Combine(dir, name);
    }
}
