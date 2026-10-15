// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.SkillsEvals;

/// <summary>
/// The sporadic-lanes precondition gate as one decision: Claude is exempt from all of it;
/// Codex/Antigravity/Cursor refuse to start a trial unless the deterministic tiers are green at HEAD AND the
/// weekly allowance is not spent. Fully testable without a real build or ledger file — the tier
/// check's <see cref="TierPrecondition.RunCommand"/> is injected and the quota check reads an
/// already-parsed <see cref="QuotaLedger"/>.
/// </summary>
public static class SporadicPolicy
{
    /// <summary>
    /// The one line a lane prints INSTEAD of running when its weekly allowance is zero, or
    /// <see langword="null"/> when the lane should go on to <see cref="BeforeTrial"/>.
    /// </summary>
    /// <remarks>
    /// A zero allowance row is the owner's own "the CLI is not installed or signed in yet" (the ledger
    /// convention for Antigravity and Cursor), so the lane skips and the run exits green — it never fails a
    /// build over a tool nobody has installed. A MISSING row, a spent allowance and a red tier are not
    /// that state: they stay refusals.
    /// </remarks>
    public static string? SkipReason(Platform platform, QuotaLedger ledger)
    {
        if (!platform.IsSporadic())
        {
            return null;
        }

        // The FIRST row for the platform, the one CheckQuota reads — two decisions about one ledger agree.
        var notInstalled = ledger.Allowances.FirstOrDefault(a => a.Platform == platform)?.WeeklyAllowance == 0;
        return notInstalled
            ? $"SKIP: the {platform.WireName()} lane did not run — its `{platform.CliTool()}` CLI is not installed or signed in " +
                "(weekly allowance 0 in ledger/quota.md); the owner raises the allowance once it is"
            : null;
    }

    /// <summary>
    /// Throws when <paramref name="platform"/> is sporadic and either precondition fails; does
    /// nothing for a non-sporadic platform (Claude), which is exempt from both checks.
    /// </summary>
    public static void BeforeTrial(
        Platform platform,
        string repoRoot,
        TierPrecondition.RunCommand runCommand,
        QuotaLedger ledger,
        DateOnly today)
    {
        if (!platform.IsSporadic())
        {
            return;
        }

        TierPrecondition.AssertDeterministicTiersGreen(repoRoot, runCommand);
        var decision = QuotaMarkdown.CheckQuota(ledger, platform, today);
        if (!decision.Allowed)
        {
            throw new InvalidOperationException(decision.Reason);
        }
    }
}
