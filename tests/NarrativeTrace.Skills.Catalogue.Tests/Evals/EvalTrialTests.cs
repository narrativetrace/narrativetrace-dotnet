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

    private EvalTrial NewTrial(RegistryDelivery delivery) =>
        new(_runner.Run, () => FixedClock, _runsPath, delivery);

    private bool RunWith(string? agentCommand, string prompt = "set up tracing") =>
        NewTrial().Run(Args, _root, _fixture, _caseDir, _scratch, OneTurn(agentCommand, prompt), 1);

    private static AgentTurns OneTurn(string? agentCommand, string prompt) =>
        agentCommand is null ? AgentTurns.Graded(prompt) : new AgentTurns(agentCommand, null, [prompt]);

    private static readonly AgentTurns Conversation = new(
        "claude -p \"{prompt}\" --session-id S",
        "claude -p \"{prompt}\" --resume S",
        ["report it", "yes, file it"]);

    private EvalTrial RecordingTrial(TrialEnvironment environment) =>
        new(_runner.Run, () => FixedClock, _runsPath, RegistryDelivery.None(), environment);

    private void WriteRenderedSkill() =>
        WriteFile(Path.Combine(_root, ".agents", "skills", "add-narrative-tracing", "SKILL.md"), "page\n");

    private static void WriteFile(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

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

    /// <summary>
    /// A fixture's root README describes the fixture for its maintainers — for a Phase 7 case, where
    /// the defect is hidden — so it never reaches the agent (Java's harness skips it too). A README
    /// anywhere below the root is project content and is copied.
    /// </summary>
    [Fact]
    public void The_fixtures_root_readme_is_never_copied_but_a_nested_one_is()
    {
        File.WriteAllText(Path.Combine(_fixture, "README.md"), "the trap is in the listener\n");
        Directory.CreateDirectory(Path.Combine(_fixture, "docs"));
        File.WriteAllText(Path.Combine(_fixture, "docs", "README.md"), "project docs\n");

        RunWith("claude -p \"{prompt}\"");

        Assert.False(File.Exists(Path.Combine(_scratch, "README.md")));
        Assert.True(File.Exists(Path.Combine(_scratch, "docs", "README.md")));
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

    [Fact]
    public void An_ordinary_case_copies_the_harnesss_own_rendered_pages_in()
    {
        WriteRenderedSkill();

        RunWith("claude -p \"{prompt}\"");

        Assert.True(File.Exists(Path.Combine(_scratch, ".agents", "skills", "add-narrative-tracing", "SKILL.md")));
    }

    /// <summary>
    /// The fresh-session case asks what the install ITSELF leaves for the next session; pages the
    /// harness seeded would answer that question before the install ran, and the case would pass vacuously.
    /// </summary>
    [Fact]
    public void A_case_that_declines_the_seeded_pages_receives_none_of_them()
    {
        WriteRenderedSkill();
        WriteFile(Path.Combine(_caseDir, "case.json"), "{ \"fixture\": \"f\", \"seedSkills\": false }");

        RunWith("claude -p \"{prompt}\"");

        Assert.False(Directory.Exists(Path.Combine(_scratch, ".agents", "skills")));
        Assert.False(Directory.Exists(Path.Combine(_scratch, ".claude", "skills")));
    }

    [Fact]
    public void A_registry_case_receives_none_of_the_harnesss_own_rendered_pages()
    {
        WriteRenderedSkill();
        var delivery = RegistryDelivery.Through(RegistryPreStep.NpxSkills, Path.Combine(_root, "work"));

        NewTrial(delivery).Run(Args, _root, _fixture, _caseDir, _scratch, OneTurn("claude -p \"{prompt}\"", "go"), 1);

        Assert.False(Directory.Exists(Path.Combine(_scratch, ".agents", "skills")));
    }

    [Fact]
    public void A_registry_case_runs_the_delivery_commands_before_the_agent_in_the_isolated_config()
    {
        var work = Path.Combine(_root, "work");
        var delivery = RegistryDelivery.Through(RegistryPreStep.NpxSkills, work);

        NewTrial(delivery).Run(Args, _root, _fixture, _caseDir, _scratch, OneTurn("claude -p \"{prompt}\"", "go"), 1);

        var preSteps = delivery.Commands(_root);
        Assert.Equal(preSteps[0], _runner.Calls[0].Argv);
        Assert.Equal("git", _runner.Calls[0].Argv[0]);
        Assert.Equal(IsolatedAgentConfig.ConfigDir(work), _runner.Calls[0].Env["CLAUDE_CONFIG_DIR"]);
        var agentCall = _runner.Calls.Single(c => c.Argv[0] == "claude" && c.Argv[1] == "-p");
        Assert.True(_runner.Calls.IndexOf(agentCall) > preSteps.Count - 1);
    }

    [Fact]
    public void A_failed_pre_step_crashes_the_trial_and_writes_no_ledger_row()
    {
        _runner.PreStepExit = 1;
        var delivery = RegistryDelivery.Through(RegistryPreStep.NpxSkills, Path.Combine(_root, "work"));

        Assert.Throws<InvalidOperationException>(() =>
            NewTrial(delivery).Run(Args, _root, _fixture, _caseDir, _scratch, OneTurn("claude -p \"{prompt}\"", "go"), 1));
        Assert.False(File.Exists(_runsPath));
        Assert.DoesNotContain(_runner.Calls, c => c.Argv[0] == "bash");
    }

    [Fact]
    public void The_grader_is_told_where_the_built_cli_is_when_it_exists()
    {
        var dll = Path.Combine(_root, "src", "NarrativeTrace.Cli", "bin", "Release", "net10.0", "dotnet-narrativetrace.dll");
        WriteFile(dll, "");

        RunWith("claude -p \"{prompt}\"");

        var graderCall = _runner.Calls.Single(c => c.Argv[0] == "bash");
        Assert.Equal(dll, graderCall.Env["NARRATIVETRACE_CLI_DLL"]);
    }

    [Fact]
    public void The_grader_is_told_no_cli_path_when_none_is_built()
    {
        RunWith("claude -p \"{prompt}\"");

        var graderCall = _runner.Calls.Single(c => c.Argv[0] == "bash");
        Assert.False(graderCall.Env.ContainsKey("NARRATIVETRACE_CLI_DLL"));
    }

    [Fact]
    public void Every_turn_is_driven_in_order_with_its_own_prompt_and_template()
    {
        RecordingTrial(TrialEnvironment.None()).Run(Args, _root, _fixture, _caseDir, _scratch, Conversation, 1);

        var agentCalls = _runner.Calls.Where(c => c.Argv[0] == "claude").ToList();
        Assert.Equal(2, agentCalls.Count);
        Assert.Equal(["claude", "-p", "report it", "--session-id", "S"], agentCalls[0].Argv);
        Assert.Equal(["claude", "-p", "yes, file it", "--resume", "S"], agentCalls[1].Argv);
        Assert.Equal("bash", _runner.Calls[^1].Argv[0]);
    }

    /// <summary>
    /// A second turn driven after a failed first is an approval answering a question that was never
    /// asked, and its grader would read "nothing was filed" as a pass — so the trial stops.
    /// </summary>
    [Fact]
    public void A_turn_that_exits_non_zero_stops_the_trial_before_any_later_turn_or_the_grader()
    {
        _runner.AgentExit = 1;

        var thrown = Assert.Throws<InvalidOperationException>(() =>
            RecordingTrial(TrialEnvironment.None()).Run(Args, _root, _fixture, _caseDir, _scratch, Conversation, 1));

        Assert.Contains("turn 1", thrown.Message, StringComparison.Ordinal);
        Assert.Single(_runner.Calls);
        Assert.False(File.Exists(_runsPath));
    }

    [Fact]
    public void A_recording_trial_writes_each_users_words_before_that_turn_and_sends_stdout_to_the_transcript()
    {
        var environment = TrialEnvironment.Under(Path.Combine(_root, "work"), _root);
        _runner.OnAgent = () => File.ReadAllLines(environment.Transcript()).Length;

        RecordingTrial(environment).Run(Args, _root, _fixture, _caseDir, _scratch, Conversation, 1);

        Assert.Equal([1, 2], _runner.TranscriptLinesSeenByEachTurn);
        Assert.All(_runner.Calls.Where(c => c.Argv[0] == "claude"), c => Assert.Equal(environment.Transcript(), c.StdoutTo));
        Assert.Contains("\"text\":\"yes, file it\"", File.ReadAllLines(environment.Transcript())[1], StringComparison.Ordinal);
    }

    [Fact]
    public void A_trial_that_records_nothing_captures_no_stdout()
    {
        RunWith("claude -p \"{prompt}\"");

        Assert.All(_runner.Calls, c => Assert.Null(c.StdoutTo));
    }

    [Fact]
    public void The_agent_runs_with_the_stand_ins_and_the_isolated_configuration_and_never_its_evidence()
    {
        var work = Path.Combine(_root, "work");
        var environment = TrialEnvironment.Under(work, _root);

        RecordingTrial(environment).Run(Args, _root, _fixture, _caseDir, _scratch, Conversation, 1);

        var agentCall = _runner.Calls.First(c => c.Argv[0] == "claude");
        Assert.StartsWith(Path.Combine(work, "bin") + ":", agentCall.Env["PATH"], StringComparison.Ordinal);
        Assert.Equal(IsolatedAgentConfig.ConfigDir(work), agentCall.Env["CLAUDE_CONFIG_DIR"]);
        Assert.False(agentCall.Env.ContainsKey("NARRATIVETRACE_TRANSCRIPT"));
        var graderCall = _runner.Calls.Single(c => c.Argv[0] == "bash");
        Assert.Equal(environment.Transcript(), graderCall.Env["NARRATIVETRACE_TRANSCRIPT"]);
        Assert.Equal(environment.BlockedInvocations(), graderCall.Env["NARRATIVETRACE_GH_LOG"]);
        Assert.Equal(_scratch, graderCall.Env["PROJECT_DIR"]);
    }

    /// <summary>
    /// The capture is proven at the actual OS process boundary: a fake that "captured" into a string
    /// would prove nothing about a child process's file descriptors. Errors stay where whoever started
    /// the trial sees them; only standard output is the transcript.
    /// </summary>
    [Fact]
    public void The_real_process_seam_appends_standard_output_and_only_it_to_the_transcript()
    {
        var transcript = Path.Combine(_root, "transcript.jsonl");
        File.WriteAllText(transcript, "{\"nt_turn\":1}\n");

        var exit = EvalTrial.RealProcess(
            ["sh", "-c", "echo '{\"type\":\"result\"}'; echo oops >&2; exit 3"],
            _root,
            new Dictionary<string, string>(),
            transcript);

        Assert.Equal(3, exit);
        Assert.Equal(["{\"nt_turn\":1}", "{\"type\":\"result\"}"], File.ReadAllLines(transcript));
    }

    private sealed class FakeRunner
    {
        public int AgentExit { get; set; }

        public int GraderExit { get; set; }

        public int PreStepExit { get; set; }

        public Func<int>? OnAgent { get; set; }

        public List<int> TranscriptLinesSeenByEachTurn { get; } = [];

        public List<(IReadOnlyList<string> Argv, string Cwd, IReadOnlyDictionary<string, string> Env, string? StdoutTo)> Calls { get; } = [];

        public int Run(IReadOnlyList<string> argv, string cwd, IReadOnlyDictionary<string, string> env, string? stdoutTo)
        {
            Calls.Add((argv, cwd, env, stdoutTo));
            if (argv[0] == "bash")
            {
                return GraderExit;
            }

            if (IsPreStep(argv))
            {
                return PreStepExit;
            }

            if (OnAgent is not null)
            {
                TranscriptLinesSeenByEachTurn.Add(OnAgent());
            }

            return AgentExit;
        }

        // A pre-step is a registry tool line: git/tar staging, npx, or `claude plugin …` — never
        // the agent's own `claude -p …` turn.
        private static bool IsPreStep(IReadOnlyList<string> argv) =>
            argv[0] is "git" or "tar" or "npx" || (argv[0] == "claude" && argv[1] == "plugin");
    }
}
