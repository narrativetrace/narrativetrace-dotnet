// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Globalization;
using NarrativeTrace.SkillsEvals;
using Xunit;

namespace NarrativeTrace.SkillsEvals.Tests;

/// <summary>
/// The Tier B trial flow with a fake process spawner, a fixed clock and a temp ledger: agent
/// pass/fail/crash, grader exit codes, prompt safety end-to-end, the no-agent path, and the ledger
/// row appended. No real subprocess, no real CLI.
/// </summary>
public sealed class EvalTrialTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "nt-trial-" + Guid.NewGuid().ToString("N"));
    private readonly string _fixture;
    private readonly string _caseDir;
    private readonly string _scratch;
    private readonly string _runsPath;
    private readonly FakeRunner _runner = new();

    private static readonly DateTimeOffset FixedClock =
        DateTimeOffset.Parse("2026-09-26T07:00:00Z", CultureInfo.InvariantCulture);

    private static readonly EvalRunnerArgs Args = new(
        "add-narrative-tracing", "init-prompt-empty-project", Platform.Claude, "haiku", null, 1);

    public EvalTrialTests()
    {
        _fixture = Path.Combine(_root, "fixture");
        _caseDir = Path.Combine(_root, "case");
        _scratch = Path.Combine(_root, "scratch");
        _runsPath = Path.Combine(_root, "ledger", "runs.jsonl");
        Directory.CreateDirectory(_fixture);
        Directory.CreateDirectory(_scratch);
        File.WriteAllText(Path.Combine(_fixture, "Program.cs"), "// program\n");
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private EvalTrial NewTrial() => new(_runner.Run, () => FixedClock, _runsPath);

    private bool RunWith(string? agentCommand, string prompt = "set up tracing") =>
        NewTrial().Run(Args, _root, _fixture, _caseDir, _scratch, prompt, agentCommand, 1);

    [Fact]
    public void A_passing_agent_and_a_passing_grader_is_a_pass()
    {
        _runner.AgentExit = 0;
        _runner.GraderExit = 0;

        Assert.True(RunWith("claude -p \"{prompt}\""));
        Assert.Contains("\"result\":\"pass\"", File.ReadAllText(_runsPath), StringComparison.Ordinal);
    }

    [Fact]
    public void A_passing_agent_but_a_failing_grader_is_a_fail_with_a_row_kept()
    {
        _runner.GraderExit = 1;

        Assert.False(RunWith("claude -p \"{prompt}\""));
        Assert.Contains("\"result\":\"fail\"", File.ReadAllText(_runsPath), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(127)]
    public void Any_non_zero_grader_exit_is_a_fail(int graderExit)
    {
        _runner.GraderExit = graderExit;

        Assert.False(RunWith("claude -p \"{prompt}\""));
    }

    [Fact]
    public void A_crashing_agent_throws_and_writes_no_ledger_row()
    {
        _runner.AgentExit = 3;

        Assert.Throws<InvalidOperationException>(() => RunWith("claude -p \"{prompt}\""));
        Assert.False(File.Exists(_runsPath));
    }

    [Fact]
    public void With_no_agent_command_the_agent_step_is_skipped_and_the_fixture_graded_as_is()
    {
        _runner.GraderExit = 0;

        Assert.True(RunWith(agentCommand: null));
        Assert.Single(_runner.Calls);
        Assert.Equal("bash", _runner.Calls[0].Argv[0]);
    }

    [Fact]
    public void The_prompt_reaches_the_agent_as_one_verbatim_argv_element()
    {
        var hostile = "check `id` and $(whoami); done";

        RunWith("claude -p \"{prompt}\" --model haiku", hostile);

        var agentCall = _runner.Calls[0];
        Assert.Equal(hostile, agentCall.Argv[2]);
    }

    [Fact]
    public void The_grader_runs_in_the_scratch_dir_with_project_dir_set()
    {
        RunWith("claude -p \"{prompt}\"");

        var graderCall = _runner.Calls.Single(c => c.Argv[0] == "bash");
        Assert.Equal(_scratch, graderCall.Cwd);
        Assert.Equal(_scratch, graderCall.Env["PROJECT_DIR"]);
    }

    [Fact]
    public void The_fixture_is_scaffolded_into_the_scratch_directory()
    {
        RunWith("claude -p \"{prompt}\"");

        Assert.True(File.Exists(Path.Combine(_scratch, "Program.cs")));
    }

    [Fact]
    public void The_ledger_row_carries_the_fixed_clock_and_the_case_identity()
    {
        RunWith("claude -p \"{prompt}\"");

        var row = RunLedgerRow.ParseJsonLine(File.ReadAllText(_runsPath).Trim());
        Assert.Equal("add-narrative-tracing", row.Skill);
        Assert.Equal("init-prompt-empty-project", row.CaseName);
        Assert.Equal(Platform.Claude, row.Platform);
        Assert.StartsWith("2026-09-26T07:00:00", row.Date, StringComparison.Ordinal);
    }

    private sealed class FakeRunner
    {
        public int AgentExit { get; set; }

        public int GraderExit { get; set; }

        public List<(IReadOnlyList<string> Argv, string Cwd, IReadOnlyDictionary<string, string> Env)> Calls { get; } = [];

        public int Run(IReadOnlyList<string> argv, string cwd, IReadOnlyDictionary<string, string> env)
        {
            Calls.Add((argv, cwd, env));
            return argv[0] == "bash" ? GraderExit : AgentExit;
        }
    }
}
