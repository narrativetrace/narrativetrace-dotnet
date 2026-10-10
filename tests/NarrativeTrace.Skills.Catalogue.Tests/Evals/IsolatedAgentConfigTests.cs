// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.SkillsEvals;
using Xunit;

namespace NarrativeTrace.SkillsEvals.Tests;

/// <summary>
/// A throwaway vendor-tool configuration a trial owns: every command runs against
/// <c>CLAUDE_CONFIG_DIR</c> under the trial's own work directory, with the subscription login seeded
/// in. The directory is resolved from the <c>HOME</c> environment variable (never the runtime's own
/// "user home", which a container whose uid has no passwd entry hands as nonsense), so a trial
/// neither inherits nor writes the real configuration of the person running it.
/// </summary>
public sealed class IsolatedAgentConfigTests : IDisposable
{
    private readonly string _work = Path.Combine(Path.GetTempPath(), "nt-cfg-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_work))
        {
            Directory.Delete(_work, recursive: true);
        }
    }

    [Fact]
    public void The_config_and_cache_directories_sit_inside_the_trials_work_directory()
    {
        Assert.Equal(Path.Combine(_work, "config"), IsolatedAgentConfig.ConfigDir(_work));
        Assert.Equal(Path.Combine(_work, "npm-cache"), IsolatedAgentConfig.NpmCacheDir(_work));
    }

    [Fact]
    public void Every_trials_agent_environment_points_the_vendor_cli_at_the_isolated_config()
    {
        var env = IsolatedAgentConfig.AgentConfigEnv(_work);

        Assert.Equal(IsolatedAgentConfig.ConfigDir(_work), env["CLAUDE_CONFIG_DIR"]);
        Assert.Single(env);
    }

    [Fact]
    public void A_registry_trial_also_gets_a_writable_cache_and_a_telemetry_opt_out()
    {
        var env = IsolatedAgentConfig.Env(_work);

        Assert.Equal(IsolatedAgentConfig.ConfigDir(_work), env["CLAUDE_CONFIG_DIR"]);
        Assert.Equal(IsolatedAgentConfig.NpmCacheDir(_work), env["npm_config_cache"]);
        Assert.Equal("1", env["DO_NOT_TRACK"]);
    }

    [Fact]
    public void The_config_override_wins_when_it_is_set()
    {
        Assert.Equal("/custom", IsolatedAgentConfig.RealConfigDir("/custom", "/home/dev", "/jvm/home"));
    }

    [Fact]
    public void Home_is_asked_before_the_runtimes_own_user_home()
    {
        Assert.Equal(
            Path.Combine("/home/dev", ".claude"),
            IsolatedAgentConfig.RealConfigDir(null, "/home/dev", "/jvm/home"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void A_blank_home_falls_back_to_the_runtimes_user_home(string? home)
    {
        Assert.Equal(
            Path.Combine("/jvm/home", ".claude"),
            IsolatedAgentConfig.RealConfigDir(null, home, "/jvm/home"));
    }

    [Fact]
    public void Seeding_copies_the_login_when_one_is_there_and_says_so()
    {
        var realConfig = Path.Combine(_work, "real");
        Directory.CreateDirectory(realConfig);
        File.WriteAllText(Path.Combine(realConfig, ".credentials.json"), "{\"token\":\"x\"}");

        Assert.True(IsolatedAgentConfig.SeedLogin(realConfig, _work));
        Assert.True(File.Exists(Path.Combine(IsolatedAgentConfig.ConfigDir(_work), ".credentials.json")));
        Assert.True(Directory.Exists(IsolatedAgentConfig.NpmCacheDir(_work)));
    }

    [Fact]
    public void Seeding_does_not_fail_when_there_is_no_login_but_says_so()
    {
        var realConfig = Path.Combine(_work, "real");
        Directory.CreateDirectory(realConfig);

        Assert.False(IsolatedAgentConfig.SeedLogin(realConfig, _work));
        Assert.True(Directory.Exists(IsolatedAgentConfig.ConfigDir(_work)));
    }
}
