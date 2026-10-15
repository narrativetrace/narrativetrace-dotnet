// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.SkillsEvals;
using Xunit;

namespace NarrativeTrace.SkillsEvals.Tests;

public sealed class CaseFixtureTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "nt-case-" + Guid.NewGuid().ToString("N"));

    public CaseFixtureTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void Reads_the_fixture_field_from_case_json()
    {
        File.WriteAllText(Path.Combine(_dir, "case.json"), "{ \"fixture\": \"skills-evals/fixtures/empty-project\" }");

        Assert.Equal("skills-evals/fixtures/empty-project", CaseFixture.FixtureFor(_dir));
    }

    [Fact]
    public void Defaults_to_the_sixty_seconds_fixture_when_there_is_no_case_json()
    {
        Assert.Equal("examples/NarrativeTrace.Examples.SixtySeconds", CaseFixture.FixtureFor(_dir));
    }

    [Fact]
    public void A_case_json_without_a_fixture_field_is_an_error()
    {
        File.WriteAllText(Path.Combine(_dir, "case.json"), "{ \"other\": 1 }");

        Assert.Throws<InvalidOperationException>(() => CaseFixture.FixtureFor(_dir));
    }

    [Fact]
    public void A_case_seeds_the_harnesss_rendered_pages_unless_it_says_otherwise()
    {
        Assert.True(CaseFixture.SeedsTheRenderedSkills(_dir));
        File.WriteAllText(Path.Combine(_dir, "case.json"), "{ \"fixture\": \"f\" }");
        Assert.True(CaseFixture.SeedsTheRenderedSkills(_dir));
    }

    [Fact]
    public void A_case_can_decline_the_seeded_pages_so_the_install_is_what_the_agent_finds()
    {
        File.WriteAllText(Path.Combine(_dir, "case.json"), "{ \"fixture\": \"f\", \"seedSkills\": false }");

        Assert.False(CaseFixture.SeedsTheRenderedSkills(_dir));
    }

    [Theory]
    [InlineData("\"no\"")]
    [InlineData("0")]
    [InlineData("null")]
    public void Refuses_a_seedSkills_that_is_not_a_boolean(string declared)
    {
        File.WriteAllText(Path.Combine(_dir, "case.json"), "{ \"fixture\": \"f\", \"seedSkills\": " + declared + " }");

        Assert.Throws<InvalidOperationException>(() => CaseFixture.SeedsTheRenderedSkills(_dir));
    }
}
