// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.SkillsEvals;
using Xunit;

namespace NarrativeTrace.SkillsEvals.Tests;

public sealed class EvidenceDirectoryTests
{
    private static readonly DateTimeOffset At = new(2026, 10, 9, 21, 5, 7, TimeSpan.FromHours(2));

    [Fact]
    public void A_failed_trials_record_is_named_by_run_stamp_and_trial()
    {
        Assert.Equal(
            Path.Combine("/repo", "artifacts", "skills-evals", "narrativetrace-verify", "verify-skip", "20261009T190507Z-trial-2"),
            EvidenceDirectory.For("/repo", "narrativetrace-verify", "verify-skip", 2, pass: false, At));
    }

    [Fact]
    public void A_passing_trials_record_is_kept_too_and_says_so()
    {
        Assert.EndsWith(
            "20261009T190507Z-trial-3-pass",
            EvidenceDirectory.For("/repo", "narrativetrace-debug", "debug-value-divergence", 3, pass: true, At));
    }
}
