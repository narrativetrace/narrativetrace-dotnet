// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Cli.Doctor.Checks;

/// <summary>
/// <c>trap.approval-traces</c>: a <c>*.received.nt</c> file sitting in the
/// approval-trace directory means a test's structure diverged from its
/// <c>*.approved.nt</c> baseline and nobody has reviewed and promoted (or
/// rejected) the diff yet. Committing it by accident is the classic mistake
/// — see <c>documentation/what-to-commit.md</c>.
/// </summary>
public static class TrapApprovalTracesCheck
{
    private const string Id = "trap.approval-traces";
    private const string ReceivedSuffix = ".received.nt";

    /// <summary>Runs the check.</summary>
    public static DoctorFinding Run(DoctorSnapshot snapshot)
    {
        var received = snapshot.ApprovedDirFiles.Keys
            .Where(path => path.EndsWith(ReceivedSuffix, StringComparison.Ordinal))
            .ToList();
        if (received.Count == 0)
        {
            var approved = snapshot.ApprovedDirFiles.Count;
            return DoctorFinding.Pass(
                Id,
                approved == 0
                    ? "no approval traces configured yet — nothing to check"
                    : $"{approved} approved trace(s) found, no pending received diffs",
                DoctorDocUrls.StructuralTraceFormat);
        }

        return DoctorFinding.Fail(
            Id,
            $"{received.Count} stale received trace(s) found: {string.Join(", ", received)}",
            "Review each *.received.nt diff against its *.approved.nt baseline, then run " +
            "./build.sh Approve to promote it, or delete it if the change was wrong — never " +
            "commit a *.received.nt file.",
            DoctorDocUrls.StructuralTraceFormat);
    }
}
