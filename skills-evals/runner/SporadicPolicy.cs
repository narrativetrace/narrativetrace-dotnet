// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.SkillsEvals;

/// <summary>
/// The sporadic-lanes precondition gate as one decision: Claude is exempt from all of it;
/// Codex/Gemini refuse to start a trial unless the deterministic tiers are green at HEAD AND the
/// weekly allowance is not spent. Fully testable without a real build or ledger file — the tier
/// check's <see cref="TierPrecondition.RunCommand"/> is injected and the quota check reads an
/// already-parsed <see cref="QuotaLedger"/>.
/// </summary>
public static class SporadicPolicy
{
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
