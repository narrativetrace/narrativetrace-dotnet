// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Diagnostics;
using NarrativeTrace.SkillsEvals;
using Xunit;

namespace NarrativeTrace.SkillsEvals.Tests;

/// <summary>
/// Adversarial pass over the multi-turn runner: evidence recording and kept evidence, the curl and
/// gh stand-ins against hostile argv, case.json turn declarations, the agent argv seam, platform
/// presets, and the EvalTrial conversation loop. Each test states the behaviour the code SHOULD have;
/// a failing one is a finding, not a reason to weaken the assertion.
/// </summary>
public sealed class AdversarialPhase5RunnerTests : IDisposable
{
    private const string Skill = "narrativetrace-feedback";
    private const string Session = "6bd1f7fb-2d90-441a-be7e-ac5a42b60a40";
    private const string Site = "https://narrativetrace.ai/dotnet/llms.txt";

    private readonly string _dir = Directory.CreateTempSubdirectory("nt-adv5-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private string Work => Path.Combine(_dir, "work");

    private TrialEnvironment Under() => TrialEnvironment.Under(Work, _dir);

    private void Manifest(string json) => File.WriteAllText(Path.Combine(_dir, "case.json"), json);

    [Fact]
    public void Recording_an_empty_reply_writes_a_marker_with_empty_text()
    {
        var environment = Under();

        environment.RecordUserTurn(2, string.Empty);

        Assert.Equal("{\"nt_turn\":2,\"role\":\"user\",\"text\":\"\"}", File.ReadAllText(environment.Transcript()).TrimEnd('\n'));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Recording_a_turn_below_one_is_refused_before_anything_is_written(int turn)
    {
        var environment = Under();

        Assert.Throws<ArgumentOutOfRangeException>(() => environment.RecordUserTurn(turn, "hello"));
        Assert.False(File.Exists(environment.Transcript()));
    }

    [Fact]
    public void Recording_a_null_reply_is_refused_and_leaves_no_partial_marker()
    {
        var environment = Under();

        Assert.Throws<ArgumentNullException>(() => environment.RecordUserTurn(1, null!));
        Assert.False(File.Exists(environment.Transcript()));
    }

    /// <summary>The stand-in quotes its log path with single quotes, which leave $, backticks and double quotes inert.</summary>
    [Fact]
    public void A_work_directory_full_of_shell_metacharacters_still_logs_to_its_own_file()
    {
        var odd = Path.Combine(_dir, "wo rk $HOME \"x\" `y` (z)");
        var environment = TrialEnvironment.Under(odd, _dir);

        var (exit, _) = Run(StandIn(environment, "gh"), NoEnv, "issue", "create");

        Assert.Equal(0, exit);
        Assert.Equal("gh issue create", Assert.Single(File.ReadAllLines(environment.BlockedInvocations())));
    }

    /// <summary>
    /// Hosts are case-insensitive, but the stand-in matches the site's one spelling on purpose: a
    /// spelling it does not know FAILS CLOSED (exit 6), the safe direction for a containment check.
    /// </summary>
    [Fact]
    public void The_curl_stand_in_fails_closed_on_another_case_of_the_sites_host()
    {
        var (exit, output) = Curl(Under(), "https://NarrativeTrace.AI/dotnet/llms.txt");

        Assert.Equal(6, exit);
        Assert.DoesNotContain("REAL", output, StringComparison.Ordinal);
    }

    /// <summary>The ordinary ways an agent reads the published file are served.</summary>
    [Theory]
    [InlineData("-sSL", Site)]
    [InlineData("-s", "-o", "llms.txt", Site)]
    [InlineData("--silent", "--location", "--url", Site)]
    [InlineData("--url=" + Site)]
    [InlineData("-fsSL", "-H", "Accept: text/plain", Site)]
    public void The_curl_stand_in_serves_ordinary_read_only_requests_for_the_site(params string[] args)
    {
        var (exit, output) = Curl(Under(), args);

        Assert.Equal(0, exit);
        Assert.Contains("REAL", output, StringComparison.Ordinal);
    }

    /// <summary>Anything that could send data, read a config of URLs, or that the allow-list does not know fails closed.</summary>
    [Theory]
    [InlineData("-d", "@secret.txt", Site)]
    [InlineData("--data=x", Site)]
    [InlineData("-K", "urls.cfg", Site)]
    [InlineData("-T", "file", Site)]
    [InlineData("-sx", "proxy", Site)]
    [InlineData("--url")]
    [InlineData(Site, "-o")]
    public void The_curl_stand_in_fails_closed_on_any_option_outside_its_read_only_allow_list(params string[] args)
    {
        var (exit, output) = Curl(Under(), args);

        Assert.Equal(6, exit);
        Assert.DoesNotContain("REAL", output, StringComparison.Ordinal);
    }

    /// <summary>A trailing-dot FQDN or an explicit port names the site but is not the exact string the stand-in matches; refusing it fails closed.</summary>
    [Theory]
    [InlineData("https://narrativetrace.ai./llms.txt")]
    [InlineData("https://narrativetrace.ai:443/llms.txt")]
    public void The_curl_stand_in_fails_closed_on_a_trailing_dot_or_port_spelling_of_the_site(string url)
    {
        var (exit, output) = Curl(Under(), url);

        Assert.Equal(6, exit);
        Assert.DoesNotContain("REAL", output, StringComparison.Ordinal);
    }

    [Fact]
    public void The_curl_stand_in_serves_a_request_mixing_http_and_https_URLs_on_the_site()
    {
        var (exit, output) = Curl(Under(), "http://narrativetrace.ai/a", "https://narrativetrace.ai/b");

        Assert.Equal(0, exit);
        Assert.Contains("REAL", output, StringComparison.Ordinal);
    }

    /// <summary>A foreign URL written as <c>--url=VALUE</c> is still a URL the real curl will fetch, so it must block the request.</summary>
    [Fact]
    public void The_curl_stand_in_refuses_a_foreign_URL_passed_in_the_url_equals_form()
    {
        var (exit, output) = Curl(Under(), "--url=https://evil.example/x", Site);

        Assert.Equal(6, exit);
        Assert.DoesNotContain("REAL", output, StringComparison.Ordinal);
    }

    /// <summary>curl fetches scheme-less hosts and ftp:// URLs too; neither may ride along with a served URL.</summary>
    [Theory]
    [InlineData("evil.example/x")]
    [InlineData("ftp://evil.example/x")]
    public void The_curl_stand_in_refuses_a_foreign_URL_without_an_http_scheme_beside_a_served_one(string foreign)
    {
        var (exit, output) = Curl(Under(), Site, foreign);

        Assert.Equal(6, exit);
        Assert.DoesNotContain("REAL", output, StringComparison.Ordinal);
    }

    [Fact]
    public void The_curl_stand_in_refuses_a_foreign_URL_given_after_the_url_flag()
    {
        var (exit, output) = Curl(Under(), "--url", "https://evil.example/x");

        Assert.Equal(6, exit);
        Assert.DoesNotContain("REAL", output, StringComparison.Ordinal);
    }

    /// <summary>With no real curl anywhere on PATH, even a served request ends blocked, and the attempt is still recorded.</summary>
    [Fact]
    public void A_served_request_with_no_real_curl_behind_the_stand_in_exits_blocked_and_is_logged()
    {
        var environment = Under();
        var stubDir = Path.GetDirectoryName(StandIn(environment, "curl"))!;

        var (exit, _) = Run(StandIn(environment, "curl"), new Dictionary<string, string> { ["PATH"] = stubDir }, Site);

        Assert.Equal(6, exit);
        Assert.Contains("curl " + Site, File.ReadAllText(environment.BlockedInvocations()), StringComparison.Ordinal);
    }

    [Fact]
    public void Keeping_evidence_into_an_existing_directory_replaces_an_older_transcript()
    {
        var environment = Under();
        var kept = Directory.CreateDirectory(Path.Combine(_dir, "keep")).FullName;
        File.WriteAllText(Path.Combine(kept, "transcript.jsonl"), "older trial\n");
        environment.RecordUserTurn(1, "newer");

        environment.KeepEvidenceUnder(kept);

        Assert.Equal("{\"nt_turn\":1,\"role\":\"user\",\"text\":\"newer\"}\n", File.ReadAllText(Path.Combine(kept, "transcript.jsonl")));
    }

    /// <summary>A trial that ran no gh stand-in must not leave an older trial's log in the kept directory for a reader to attribute to this one.</summary>
    [Fact]
    public void Keeping_evidence_does_not_leave_an_older_trials_stand_in_log_beside_this_trials_transcript()
    {
        var environment = Under();
        environment.RecordUserTurn(1, "report it");
        var kept = Directory.CreateDirectory(Path.Combine(_dir, "keep")).FullName;
        File.WriteAllText(Path.Combine(kept, "gh-invocations.log"), "gh issue create\n");

        environment.KeepEvidenceUnder(kept);

        Assert.False(File.Exists(Path.Combine(kept, "gh-invocations.log")));
    }

    /// <summary>A reply that is a legal JSON key path to the same turn must not silently overwrite a sibling.</summary>
    [Fact]
    public void Two_keys_naming_the_same_turn_are_refused_rather_than_one_reply_silently_dropped()
    {
        Manifest("{ \"turns\": { \"2\": \"yes\", \"02\": \"no\" } }");

        Assert.Throws<InvalidOperationException>(() => CaseTurns.ScriptedRepliesFor(_dir));
    }

    [Fact]
    public void A_duplicated_turn_key_in_the_same_object_is_refused_rather_than_last_one_wins()
    {
        Manifest("{ \"turns\": { \"2\": \"yes\", \"2\": \"no\" } }");

        Assert.Throws<InvalidOperationException>(() => CaseTurns.ScriptedRepliesFor(_dir));
    }

    /// <summary>Only the canonical decimal spelling names a turn; a signed or zero-padded key is a second spelling.</summary>
    [Theory]
    [InlineData("+2")]
    [InlineData("02")]
    public void A_non_canonical_spelling_of_a_turn_number_is_refused(string key)
    {
        Manifest("{ \"turns\": { \"" + key + "\": \"yes\" } }");

        Assert.Throws<InvalidOperationException>(() => CaseTurns.ScriptedRepliesFor(_dir));
    }

    [Fact]
    public void A_turn_key_too_large_for_an_int_is_refused()
    {
        Manifest("{ \"turns\": { \"2\": \"yes\", \"99999999999\": \"later\" } }");

        Assert.Throws<InvalidOperationException>(() => CaseTurns.ScriptedRepliesFor(_dir));
    }

    /// <summary>A manifest that is valid JSON but not an object must still say which file is wrong.</summary>
    [Fact]
    public void A_case_manifest_that_is_a_json_array_names_case_json_when_refused()
    {
        Manifest("[ { \"turns\": { \"2\": \"yes\" } } ]");

        var thrown = Assert.Throws<InvalidOperationException>(() => CaseTurns.ScriptedRepliesFor(_dir));

        Assert.Contains("case.json", thrown.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("{ \"turns\": null }")]
    [InlineData("{ \"turns\": { \"2\": null } }")]
    [InlineData("{ \"turns\": { \"2\": { \"text\": \"yes\" } } }")]
    public void A_null_or_nested_reply_is_refused_as_not_a_string(string manifest)
    {
        Manifest(manifest);

        Assert.Throws<InvalidOperationException>(() => CaseTurns.ScriptedRepliesFor(_dir));
    }

    /// <summary>A whitespace-only first template is an empty command, which the agent seam would only reject at run time.</summary>
    [Fact]
    public void A_whitespace_first_turn_template_is_refused_when_the_trial_is_built()
    {
        Assert.Throws<ArgumentException>(() => new AgentTurns("   ", null, ["report it"]));
    }

    /// <summary>With no first-turn template the trial drives no agent, so a later turn would silently never run.</summary>
    [Fact]
    public void A_trial_with_no_first_turn_command_refuses_several_turns_rather_than_driving_none_of_them()
    {
        Assert.Throws<ArgumentException>(() => new AgentTurns(null, "claude --resume {session}", ["one", "two"]));
    }

    /// <summary>A model id with a space would split into extra argv elements in the preset, so it must be refused up front.</summary>
    [Fact]
    public void A_model_id_containing_whitespace_is_refused_rather_than_split_into_extra_argv()
    {
        Assert.Throws<ArgumentException>(() =>
            AgentTurns.Of(Platform.Claude, "haiku --dangerously-skip-permissions", Skill, null, "report it", [], null));
    }

    /// <summary>Enum.TryParse accepts numeric text, so "--platform 1" would silently select Codex.</summary>
    [Theory]
    [InlineData("0")]
    [InlineData("1")]
    [InlineData("3")]
    public void Numeric_text_is_not_a_platform_name(string value)
    {
        Assert.Null(Platforms.Parse(value));
    }

    /// <summary>The single-turn preset throws for an undefined platform; the multi-turn one should not quietly answer null.</summary>
    [Fact]
    public void An_undefined_platform_is_refused_by_the_multi_turn_preset_as_it_is_by_the_single_turn_one()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ((Platform)99).PresetFirstTurnCommand("m", Skill));
    }

    /// <summary>A real process must hand over a large transcript whole, not truncated at a pipe-sized buffer.</summary>
    [Fact]
    public void The_real_process_seam_appends_a_megabyte_of_standard_output_in_full()
    {
        var transcript = Path.Combine(_dir, "transcript.jsonl");

        var exit = EvalTrial.RealProcess(
            ["sh", "-c", "head -c 1000000 /dev/zero | tr '\\0' 'a'"],
            _dir,
            new Dictionary<string, string>(),
            transcript);

        Assert.Equal(0, exit);
        Assert.Equal(1_000_000, new FileInfo(transcript).Length);
    }

    /// <summary>A registry delivery names its own config directory; the trial's isolated one must still win for the agent.</summary>
    [Fact]
    public void The_agent_keeps_its_trials_config_directory_even_when_the_delivery_names_another()
    {
        var environment = Under();
        var otherWork = Path.Combine(_dir, "other-work");
        var delivery = RegistryDelivery.Through(RegistryPreStep.NpxSkills, otherWork);
        var agentCalls = new List<IReadOnlyDictionary<string, string>>();

        EvalTrial.ProcessRunner run = (argv, _, env, _) =>
        {
            if (argv[0] == "claude")
            {
                agentCalls.Add(env);
            }

            return 0;
        };

        RunTrial(run, delivery, environment, Conversation());

        var agent = agentCalls[0];
        Assert.Equal(IsolatedAgentConfig.ConfigDir(Work), agent["CLAUDE_CONFIG_DIR"]);
        Assert.Equal(IsolatedAgentConfig.NpmCacheDir(otherWork), agent["npm_config_cache"]);
    }

    /// <summary>A failing second turn stops the trial and the message says which turn failed.</summary>
    [Fact]
    public void A_second_turn_that_exits_non_zero_names_turn_2_and_the_grader_never_runs()
    {
        var grader = new List<string>();
        EvalTrial.ProcessRunner run = (argv, _, _, _) =>
        {
            if (argv[0] == "bash") { grader.Add(argv[0]); }
            return argv.Contains("--resume") ? 1 : 0;
        };

        var thrown = Assert.Throws<InvalidOperationException>(() => RunTrial(run, RegistryDelivery.None(), TrialEnvironment.None(), Conversation()));

        Assert.Contains("turn 2", thrown.Message, StringComparison.Ordinal);
        Assert.Empty(grader);
    }

    private static AgentTurns Conversation() =>
        new("claude -p \"{prompt}\" --session-id " + Session, "claude -p \"{prompt}\" --resume " + Session, ["report it", "yes, file it"]);

    private bool RunTrial(
        EvalTrial.ProcessRunner run, RegistryDelivery delivery, TrialEnvironment environment, AgentTurns turns)
    {
        var fixture = Directory.CreateDirectory(Path.Combine(_dir, "fixture")).FullName;
        File.WriteAllText(Path.Combine(fixture, "Program.cs"), "// program\n");
        var scratch = Directory.CreateDirectory(Path.Combine(_dir, "scratch")).FullName;
        var args = new EvalRunnerArgs("add-narrative-tracing", "init-prompt-empty-project", Platform.Claude, "haiku", null, 1);
        var trial = new EvalTrial(
            run, () => new DateTimeOffset(2026, 10, 8, 7, 0, 0, TimeSpan.Zero),
            Path.Combine(_dir, "ledger", "runs.jsonl"), delivery, environment);
        return trial.Run(args, _dir, fixture, Path.Combine(_dir, "case"), scratch, turns, 1);
    }

    private (int Exit, string Output) Curl(TrialEnvironment environment, params string[] args)
    {
        var realBin = Directory.CreateDirectory(Path.Combine(_dir, "real-bin")).FullName;
        var realCurl = Path.Combine(realBin, "curl");
        File.WriteAllText(realCurl, "#!/bin/sh\nprintf 'REAL'\nfor a in \"$@\"; do printf ' %s' \"$a\"; done\nprintf '\\n'\n");
        File.SetUnixFileMode(realCurl, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        var stubDir = Path.GetDirectoryName(StandIn(environment, "curl"))!;
        return Run(StandIn(environment, "curl"), new Dictionary<string, string> { ["PATH"] = stubDir + ":" + realBin }, args);
    }

    private static readonly IReadOnlyDictionary<string, string> NoEnv = new Dictionary<string, string>();

    private static string StandIn(TrialEnvironment environment, string name) =>
        Path.Combine(environment.AgentEnvironment()["PATH"].Split(':')[0], name);

    /// <summary>Runs <paramref name="file"/> with <paramref name="args"/>; <paramref name="env"/> overrides the inherited environment.</summary>
    private static (int Exit, string Output) Run(string file, IReadOnlyDictionary<string, string> env, params string[] args)
    {
        var psi = new ProcessStartInfo(file)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var arg in args)
        {
            psi.ArgumentList.Add(arg);
        }

        foreach (var (key, value) in env)
        {
            psi.Environment[key] = value;
        }

        using var process = StartRetryingTextFileBusy(psi);
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(TimeSpan.FromSeconds(20)))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException($"{file} did not exit");
        }

        return (process.ExitCode, stdout.Result + stderr.Result);
    }

    /// <summary>
    /// ETXTBSY: a parallel test's fork() can briefly hold a just-written stand-in's write handle
    /// open until that child execs, and exec'ing the file meanwhile fails with "Text file busy".
    /// A bounded retry, never an unbounded one — a stand-in that stays busy is a real failure.
    /// </summary>
    private static Process StartRetryingTextFileBusy(ProcessStartInfo psi)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return Process.Start(psi)!;
            }
            catch (System.ComponentModel.Win32Exception e) when (attempt < 10 && e.NativeErrorCode == 26)
            {
                Thread.Sleep(20 * attempt);
            }
        }
    }
}
