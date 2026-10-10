// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Cli;
using Xunit;

namespace NarrativeTrace.Cli.Tests.Init;

/// <summary>
/// The fourth skill through the REAL bundled carrier — no <c>--from</c>, so what is read is the
/// resource tree the built tool embeds — into a throwaway project named with <c>--dir</c>. The
/// existing verb tests use a stand-in carrier; these prove the shipped one carries the clarity page
/// through preview, install and removal.
/// </summary>
public sealed class ClaritySkillAdoptionTests : IDisposable
{
    private const string Skill = "add-narrativetrace-clarity";

    private readonly string _project = Path.Combine(Path.GetTempPath(), "nt-clarity-adopt-" + Guid.NewGuid().ToString("N"));
    private readonly StringWriter _out = new();
    private readonly StringWriter _err = new();

    public ClaritySkillAdoptionTests() => Directory.CreateDirectory(_project);

    public void Dispose() => Directory.Delete(_project, recursive: true);

    private int Run(params string[] args) => CliRunner.Run([.. args, "--dir", _project], _out, _err);

    private string Page(string flavour) => Path.Combine(_project, "." + flavour, "skills", Skill, "SKILL.md");

    [Fact]
    public void The_dry_run_shows_the_clarity_page_and_writes_nothing()
    {
        Assert.Equal(0, Run("init", "--dry-run"));

        Assert.Contains($"+++ b/.agents/skills/{Skill}/SKILL.md", _out.ToString(), StringComparison.Ordinal);
        Assert.False(Directory.Exists(Path.Combine(_project, ".agents")));
    }

    [Fact]
    public void Init_writes_the_clarity_page_and_uninstall_removes_exactly_it_along_with_the_rest()
    {
        Assert.Equal(0, Run("init"));
        Assert.True(File.Exists(Page("agents")));
        Assert.Contains("installed by narrativetrace init from", File.ReadAllText(Page("agents")), StringComparison.Ordinal);

        Assert.Equal(0, Run("uninstall"));

        Assert.False(Directory.Exists(Path.GetDirectoryName(Page("agents"))));
    }

    [Fact]
    public void A_clarity_page_already_in_the_tree_is_adopted_not_overwritten_a_second_time()
    {
        Assert.Equal(0, Run("init"));
        _out.GetStringBuilder().Clear();

        Assert.Equal(0, Run("init", "--dry-run"));

        Assert.DoesNotContain($"+++ b/.agents/skills/{Skill}/SKILL.md", _out.ToString(), StringComparison.Ordinal);
    }
}
