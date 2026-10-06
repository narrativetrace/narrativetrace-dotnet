// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.SkillsEvals;
using Xunit;

namespace NarrativeTrace.SkillsEvals.Tests;

public sealed class TierPreconditionTests
{
    [Fact]
    public void The_command_is_the_closed_build_sh_vocabulary()
    {
        Assert.Equal(["./build.sh", "SkillsLint", "SkillsReplay"], TierPrecondition.Command());
    }

    [Fact]
    public void Green_tiers_pass_the_precondition_and_run_the_command_at_the_repo_root()
    {
        IReadOnlyList<string>? seenCommand = null;
        string? seenCwd = null;

        TierPrecondition.AssertDeterministicTiersGreen("/repo", (command, cwd) =>
        {
            seenCommand = command;
            seenCwd = cwd;
        });

        Assert.Equal(TierPrecondition.Command(), seenCommand);
        Assert.Equal("/repo", seenCwd);
    }

    [Fact]
    public void A_red_tier_is_wrapped_in_a_quota_preserving_explanation()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            TierPrecondition.AssertDeterministicTiersGreen("/repo",
                (_, _) => throw new IOException("build exited 1")));

        Assert.Contains("refuses to start", ex.Message, StringComparison.Ordinal);
        Assert.IsType<IOException>(ex.InnerException);
    }
}
