// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.SkillsEvals;
using Xunit;

namespace NarrativeTrace.SkillsEvals.Tests;

/// <summary>
/// Adversarial pass over the milestone-4 registry classes (a cheap model wrote these, each kept
/// only after reading what it asserts). It pins behaviour the main suite leaves uncovered: the
/// vocabulary's refusal to trim or case-fold a registry id, the case.json regex's tolerance of
/// whitespace and newlines but not of an unescaped-quote or padded value, argv safety for paths
/// with spaces, the login-overwrite on a re-seed, and the config-dir precedence. No finding exposed
/// a bug — every test documents the current, intended behaviour.
/// </summary>
public sealed class AdversarialRegistryTests : IDisposable
{
    private readonly string _work = Path.Combine(Path.GetTempPath(), "nt-adv-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_work))
        {
            Directory.Delete(_work, recursive: true);
        }
    }

    private string NewCase(string caseJson)
    {
        var caseDir = Path.Combine(_work, "case");
        Directory.CreateDirectory(caseDir);
        File.WriteAllText(Path.Combine(caseDir, "case.json"), caseJson);
        return caseDir;
    }

    // ---- RegistryPreStep.Parse: an id is matched exactly, never trimmed or case-folded ----

    [Fact]
    public void Parse_of_null_is_null_not_a_crash()
    {
        Assert.Null(RegistryPreSteps.Parse(null));
    }

    [Theory]
    [InlineData("CLAUDE-MARKETPLACE")]
    [InlineData("Npx-Skills")]
    public void Parse_is_case_sensitive(string id)
    {
        Assert.Null(RegistryPreSteps.Parse(id));
    }

    [Theory]
    [InlineData("npx-skills ")]
    [InlineData(" npx-skills")]
    [InlineData("\tnpx-skills")]
    public void Parse_does_not_trim_surrounding_whitespace(string id)
    {
        Assert.Null(RegistryPreSteps.Parse(id));
    }

    // ---- StagedSnapshot: argv safety and the shared archive path ----

    [Fact]
    public void Staging_keeps_paths_with_spaces_whole_across_both_commands()
    {
        const string repoRoot = "/repo with spaces";
        const string into = "/work/staged dir";
        var commands = StagedSnapshot.StagingCommands(repoRoot, into);

        Assert.Equal(repoRoot, commands[0][2]);
        Assert.Equal(into + ".tar", commands[0][6]);
        Assert.Equal(into + ".tar", commands[1][2]);
        Assert.Equal(into, commands[1][4]);
    }

    // ---- IsolatedAgentConfig ----

    [Fact]
    public void RealConfigDir_prefers_the_override_then_home_then_the_runtime_home()
    {
        Assert.Equal("/override", IsolatedAgentConfig.RealConfigDir("/override", "/home", "/fallback"));
        Assert.Equal(Path.Combine("/home", ".claude"), IsolatedAgentConfig.RealConfigDir(null, "/home", "/fallback"));
        Assert.Equal(Path.Combine("/fallback", ".claude"), IsolatedAgentConfig.RealConfigDir(null, null, "/fallback"));
    }

    [Fact]
    public void Re_seeding_overwrites_the_previous_login_copy()
    {
        var realConfig = Path.Combine(_work, "real");
        Directory.CreateDirectory(realConfig);
        File.WriteAllText(Path.Combine(realConfig, ".credentials.json"), "{\"old\":\"data\"}");
        Assert.True(IsolatedAgentConfig.SeedLogin(realConfig, _work));

        File.WriteAllText(Path.Combine(realConfig, ".credentials.json"), "{\"new\":\"data\"}");
        Assert.True(IsolatedAgentConfig.SeedLogin(realConfig, _work));

        var copy = File.ReadAllText(Path.Combine(IsolatedAgentConfig.ConfigDir(_work), ".credentials.json"));
        Assert.Contains("new", copy, StringComparison.Ordinal);
    }

    [Fact]
    public void The_registry_env_is_a_superset_of_the_agent_config_env()
    {
        var agentEnv = IsolatedAgentConfig.AgentConfigEnv(_work);
        var fullEnv = IsolatedAgentConfig.Env(_work);

        foreach (var (key, value) in agentEnv)
        {
            Assert.Equal(value, fullEnv[key]);
        }
    }

    // ---- RegistryDelivery ----

    [Fact]
    public void An_empty_work_dir_is_allowed_only_null_is_refused()
    {
        Assert.True(RegistryDelivery.Through(RegistryPreStep.NpxSkills, "").DeliversTheSkills());
        Assert.Throws<ArgumentNullException>(() => RegistryDelivery.Through(RegistryPreStep.NpxSkills, null!));
    }

    [Fact]
    public void A_delivery_never_leaks_project_dir_into_its_environment()
    {
        var env = RegistryDelivery.Through(RegistryPreStep.NpxSkills, "/work").Environment();
        Assert.False(env.ContainsKey("PROJECT_DIR"));
    }

    // ---- CaseRegistry: the one-token regex and its fault paths ----

    [Fact]
    public void A_null_case_dir_is_a_crash_not_a_silent_null()
    {
        Assert.Throws<ArgumentNullException>(() => CaseRegistry.RegistryFor(null!));
    }

    [Fact]
    public void An_empty_manifest_is_an_ordinary_case()
    {
        Assert.Null(CaseRegistry.RegistryFor(NewCase("")));
    }

    [Fact]
    public void Whitespace_around_the_colon_is_tolerated()
    {
        Assert.Equal(RegistryPreStep.NpxSkills, CaseRegistry.RegistryFor(NewCase("{ \"registry\" : \"npx-skills\" }")));
    }

    [Fact]
    public void A_registry_field_on_its_own_line_is_found()
    {
        var json = "{\n  \"fixture\": \"some/path\",\n  \"registry\": \"claude-marketplace\"\n}";
        Assert.Equal(RegistryPreStep.ClaudeMarketplace, CaseRegistry.RegistryFor(NewCase(json)));
    }

    [Fact]
    public void The_first_registry_field_wins_when_the_manifest_repeats_it()
    {
        var json = "{ \"registry\": \"claude-marketplace\", \"registry\": \"npx-skills\" }";
        Assert.Equal(RegistryPreStep.ClaudeMarketplace, CaseRegistry.RegistryFor(NewCase(json)));
    }

    [Theory]
    [InlineData("{ \"registry\": \" npx-skills \" }")]   // padded value: no trimming, so unknown
    [InlineData("{ \"registry\": \"   \" }")]            // whitespace-only value
    [InlineData("{ \"registry\": \"npx\\\"skills\" }")]  // the regex stops at the first quote
    public void A_value_outside_the_vocabulary_is_an_error_not_a_skip(string caseJson)
    {
        var ex = Assert.Throws<InvalidOperationException>(() => CaseRegistry.RegistryFor(NewCase(caseJson)));
        Assert.Contains("unknown registry", ex.Message, StringComparison.Ordinal);
    }
}
