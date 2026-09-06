// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using FsCheck;
using FsCheck.Xunit;
using NarrativeTrace.Clarity;
using Xunit;

namespace NarrativeTrace.Clarity.Tests;

/// <summary>
/// Property-based invariants for the name scorers (CLARITY-14): whatever
/// identifier the framework throws at them — including empty, unicode, and
/// pathological casing — every dimension must produce a score in [0, 1].
/// </summary>
public class ScorerPropertyTests
{
    [Property(MaxTest = 500)]
    public void Method_name_score_is_within_unit_interval(NonNull<string> name)
    {
        AssertInUnitInterval(MethodNameScorer.Score(name.Get));
    }

    [Property(MaxTest = 500)]
    public void Class_name_score_is_within_unit_interval(NonNull<string> name)
    {
        AssertInUnitInterval(ClassNameScorer.Score(name.Get));
    }

    [Property(MaxTest = 500)]
    public void Parameter_name_score_is_within_unit_interval(NonNull<string> name)
    {
        AssertInUnitInterval(ParameterNameScorer.Score(name.Get));
    }

    private static void AssertInUnitInterval(double score)
    {
        Assert.InRange(score, 0.0, 1.0);
    }
}
