// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Core;

/// <summary>
/// Approval traces over the structural trace (ADR-002): committed
/// <c>*.approved.nt</c> baselines are the behavioral contract, and a run whose
/// structure differs fails with a readable diff.
/// </summary>
/// <remarks>
/// INTENT: the approval-testing idea, applied to traces — a mismatch writes
/// the current render as <c>*.received.nt</c> beside the approved trace for
/// review, and approving is promoting the received trace over the approved
/// one. Byte comparison only: the renderer is deterministic, so
/// byte-identical is behaviorally identical. Anything smarter (semantic
/// diff, review workflow) is deliberately out of scope here.
/// </remarks>
public static class NarrativeApproval
{
    private const string ApprovedSuffix = ".approved.nt";
    private const string ReceivedSuffix = ".received.nt";

    /// <summary>Verifies the scenario's structure against its approved trace.</summary>
    /// <param name="trace">The captured trace.</param>
    /// <param name="scenario">
    /// The value-free scenario title — <see cref="ArtifactIdentity.StructuralScenario"/>, never a
    /// display name that may carry an interpolated argument.
    /// </param>
    /// <param name="approvedFile">The approved trace's path (see <see cref="ApprovedFile(string, ArtifactIdentity)"/>).</param>
    /// <exception cref="NarrativeApprovalException">
    /// No approved trace exists yet (the current render is written as the
    /// received trace to review and approve) or the structure differs from
    /// it (received trace written, message carries the readable diff).
    /// </exception>
    /// <exception cref="IOException">The approved or received trace could not be read or written.</exception>
    public static void Verify(TraceTree trace, string scenario, string approvedFile)
    {
        var current = StructuralTraceRenderer.RenderDocument(trace, scenario);
        var receivedFile = ReceivedSibling(approvedFile);
        if (!File.Exists(approvedFile))
        {
            throw NoApprovedTrace(scenario, current, approvedFile, receivedFile);
        }

        var delta = StructuralDelta.Between(File.ReadAllText(approvedFile), current);
        if (!delta.Unchanged)
        {
            throw StructureChanged(delta, current, receivedFile);
        }

        if (File.Exists(receivedFile))
        {
            File.Delete(receivedFile);
        }
    }

    private static NarrativeApprovalException NoApprovedTrace(
        string scenario, string current, string approvedFile, string receivedFile)
    {
        TraceFileWriter.Write(receivedFile, current);
        return new NarrativeApprovalException(
            "No approved trace for scenario \"" + scenario + "\".\n"
            + "Received: " + receivedFile
            + "\nReview it and approve via the Approve build target (or rename it to "
            + Path.GetFileName(approvedFile) + ").");
    }

    private static NarrativeApprovalException StructureChanged(
        StructuralDelta delta, string current, string receivedFile)
    {
        TraceFileWriter.Write(receivedFile, current);
        return new NarrativeApprovalException(
            "Trace changed against the approved trace (" + delta.Summary() + "):\n"
            + delta.Diff()
            + "Received: " + receivedFile
            + "\nIf this change is intended, approve it via the Approve build target.");
    }

    /// <summary>
    /// The committed baseline's location for one test:
    /// <c>&lt;approvedDir&gt;/&lt;SimpleClassName&gt;/&lt;method_slug&gt;.approved.nt</c> — the
    /// same class-directory and slug rules as every other per-test artifact,
    /// so baseline and build artifact line up by name.
    /// </summary>
    public static string ApprovedFile(string approvedDir, string testClassName, string testMethodName)
    {
        return ApprovedFile(approvedDir, ArtifactIdentity.OfMethod(testClassName, testMethodName));
    }

    /// <summary>
    /// The committed baseline of one invocation, keyed by the full artifact identity.
    /// </summary>
    /// <remarks>
    /// A method that runs more than once has one baseline per invocation.
    /// Keying by the method alone would make every invocation share one
    /// <c>.approved.nt</c>, so the last invocation's structure would silently
    /// become the contract for all of them.
    /// </remarks>
    public static string ApprovedFile(string approvedDir, ArtifactIdentity identity)
    {
        return Path.Combine(
            OutputDirectoryResolver.ClassDirectory(approvedDir, identity.TestClassName),
            identity.FileSlug() + ApprovedSuffix);
    }

    private static string ReceivedSibling(string approvedFile)
    {
        var dir = Path.GetDirectoryName(approvedFile) ?? string.Empty;
        var name = Path.GetFileName(approvedFile).Replace(ApprovedSuffix, ReceivedSuffix);
        return Path.Combine(dir, name);
    }

    /// <summary>
    /// Promotes every <c>*.received.nt</c> under the root to its
    /// <c>*.approved.nt</c> baseline — the whole of "approving": reviewed
    /// received files become the new contract. Backs the <c>Approve</c> Nuke
    /// target.
    /// </summary>
    /// <returns>
    /// The approved files written, in no guaranteed order; empty when there
    /// is nothing to promote or the root does not exist yet.
    /// </returns>
    public static IReadOnlyList<string> PromoteReceived(string root)
    {
        if (!Directory.Exists(root))
        {
            return [];
        }

        var promoted = new List<string>();
        foreach (var file in Directory.EnumerateFiles(root, "*" + ReceivedSuffix, SearchOption.AllDirectories))
        {
            var dir = Path.GetDirectoryName(file) ?? string.Empty;
            var approved = Path.Combine(
                dir, Path.GetFileName(file).Replace(ReceivedSuffix, ApprovedSuffix));

            // File.Move's 3-arg overwrite overload is unavailable on
            // netstandard2.0, which this assembly still targets.
            if (File.Exists(approved))
            {
                File.Delete(approved);
            }

            File.Move(file, approved);
            promoted.Add(approved);
        }

        return promoted;
    }
}
