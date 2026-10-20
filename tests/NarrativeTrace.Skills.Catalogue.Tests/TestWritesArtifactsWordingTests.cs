// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Skills.Catalogue;
using Xunit;

namespace NarrativeTrace.Skills.Tests;

/// <summary>
/// B-102: an xUnit test that runs its body through <c>NarrativeFixture.Run</c> writes its own files
/// when it ends, so no page may teach that only an explicit <c>WriteArtifacts</c> call writes them.
/// </summary>
public sealed class TestWritesArtifactsWordingTests
{
    public static TheoryData<string, string> NoFilesCauses()
    {
        var data = new TheoryData<string, string>();
        foreach (var skill in SkillCatalogue.Skills)
        {
            foreach (var note in skill.Steps.SelectMany(step => step.Failure ?? []))
            {
                if (note.Cause.Contains("WriteArtifacts", StringComparison.Ordinal))
                {
                    data.Add(skill.CanonicalName, note.Cause);
                }
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(NoFilesCauses))]
    public void A_cause_that_names_WriteArtifacts_also_names_the_Run_path_that_writes_on_its_own(
        string skill, string cause)
    {
        Assert.True(
            cause.Contains("NarrativeFixture.Run", StringComparison.Ordinal),
            $"{skill}: \"{cause}\"");
    }

    [Fact]
    public void The_reading_guide_says_both_integrations_write_when_the_test_ends()
    {
        var guide = TraceReading.Flavours.Markdown;

        Assert.Contains("`NarrativeFixture.Run`", guide, StringComparison.Ordinal);
        Assert.Contains("`NarrativeTestBase`", guide, StringComparison.Ordinal);
    }
}
