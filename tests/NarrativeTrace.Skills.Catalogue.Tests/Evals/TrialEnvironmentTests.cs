// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Diagnostics;
using NarrativeTrace.SkillsEvals;
using Xunit;

namespace NarrativeTrace.SkillsEvals.Tests;

/// <summary>
/// The environment every command of a trial runs with, and the evidence a grader reads afterwards:
/// the transcript, the recording <c>gh</c>/<c>curl</c> stand-ins and their log. All of it lives in a
/// work directory OUTSIDE the scaffolded project, so the agent cannot read what it is being graded
/// on and cannot write it either.
/// </summary>
public sealed class TrialEnvironmentTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("nt-trialenv-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private string Work => Path.Combine(_dir, "work");

    private TrialEnvironment Under() => TrialEnvironment.Under(Work, _dir);

    [Fact]
    public void A_trial_that_records_nothing_has_no_evidence_and_adds_no_environment()
    {
        var environment = TrialEnvironment.None();

        Assert.False(environment.IsRecording);
        Assert.Empty(environment.AgentEnvironment());
        Assert.Empty(environment.GraderEnvironment());
    }

    [Fact]
    public void The_evidence_lives_in_the_work_directory_and_never_in_the_project()
    {
        var project = Directory.CreateDirectory(Path.Combine(_dir, "scratch")).FullName;

        var environment = Under();

        Assert.True(environment.IsRecording);
        Assert.StartsWith(Work + Path.DirectorySeparatorChar, environment.Transcript(), StringComparison.Ordinal);
        Assert.False(File.Exists(environment.Transcript()));
        Assert.StartsWith(Work + Path.DirectorySeparatorChar, environment.BlockedInvocations(), StringComparison.Ordinal);
        Assert.DoesNotContain(project, environment.Transcript(), StringComparison.Ordinal);
    }

    /// <summary>
    /// The whole reason a stand-in is installed rather than relying on <c>gh</c> being absent:
    /// absence makes "tried to file" and "did not try" the same observation. It also means no trial
    /// can create a real issue anywhere, whatever an agent does.
    /// </summary>
    [Fact]
    public void Installs_a_gh_stand_in_that_records_what_it_was_asked_and_files_nothing()
    {
        var environment = Under();

        var (exit, _) = Run(StandIn(environment, "gh"), NoEnv, "issue", "create", "--title", "doctor: trap.redaction-proof is wrong");

        Assert.Equal(0, exit);
        Assert.Contains(
            "gh issue create --title doctor: trap.redaction-proof is wrong",
            File.ReadAllText(environment.BlockedInvocations()),
            StringComparison.Ordinal);
    }

    [Fact]
    public void The_stand_ins_are_first_on_the_agents_path_with_the_inherited_path_behind_them()
    {
        var path = Under().AgentEnvironment()["PATH"];

        Assert.StartsWith(Path.Combine(Work, "bin") + ":", path, StringComparison.Ordinal);
        Assert.EndsWith(Environment.GetEnvironmentVariable("PATH")!, path, StringComparison.Ordinal);
    }

    /// <summary>
    /// An agent told where its own transcript is could read what it is being graded on, and an agent
    /// told where the stand-in log is could rewrite it. The grader is told; the agent is not.
    /// </summary>
    [Fact]
    public void The_agent_is_never_told_where_its_own_evidence_is()
    {
        var environment = Under();

        Assert.False(environment.AgentEnvironment().ContainsKey("NARRATIVETRACE_TRANSCRIPT"));
        Assert.False(environment.AgentEnvironment().ContainsKey("NARRATIVETRACE_GH_LOG"));
        Assert.DoesNotContain(
            environment.AgentEnvironment().Values,
            value => value.Contains(environment.Transcript(), StringComparison.Ordinal)
                || value.Contains(environment.BlockedInvocations(), StringComparison.Ordinal));
    }

    [Fact]
    public void The_grader_is_told_where_both_pieces_of_evidence_are()
    {
        var environment = Under();

        var grader = environment.GraderEnvironment();

        Assert.Equal(environment.Transcript(), grader["NARRATIVETRACE_TRANSCRIPT"]);
        Assert.Equal(environment.BlockedInvocations(), grader["NARRATIVETRACE_GH_LOG"]);
        Assert.True(grader.ContainsKey("PATH"));
    }

    /// <summary>
    /// EVERY trial runs against a throwaway agent configuration, not only a registry one. A trial
    /// that inherits the operator's configuration is driven against the operator's own skills — the
    /// skill under test one line among forty — and a red row would not reproduce anywhere else.
    /// </summary>
    [Fact]
    public void Every_trial_runs_against_a_throwaway_agent_configuration()
    {
        var environment = Under();

        Assert.Equal(IsolatedAgentConfig.ConfigDir(Work), environment.AgentEnvironment()["CLAUDE_CONFIG_DIR"]);
        Assert.True(environment.GraderEnvironment().ContainsKey("CLAUDE_CONFIG_DIR"));
    }

    [Fact]
    public void A_trial_that_records_nothing_refuses_to_be_asked_for_its_evidence()
    {
        var environment = TrialEnvironment.None();

        Assert.Throws<InvalidOperationException>(environment.Transcript);
        Assert.Throws<InvalidOperationException>(environment.BlockedInvocations);
        Assert.Throws<InvalidOperationException>(() => environment.RecordUserTurn(1, "anything"));
    }

    /// <summary>
    /// The stand-in embeds its log path in a POSIX single-quoted string, so a path carrying a single
    /// quote would close it and turn the rest into shell words.
    /// </summary>
    [Fact]
    public void Refuses_a_work_directory_whose_path_could_break_out_of_the_stand_ins_own_quoting()
    {
        var thrown = Assert.Throws<ArgumentException>(() => TrialEnvironment.Under(Path.Combine(_dir, "wo'rk"), _dir));

        Assert.Contains("'", thrown.Message, StringComparison.Ordinal);
        Assert.False(Directory.Exists(Path.Combine(_dir, "wo'rk")));
    }

    [Fact]
    public void Records_each_turn_as_one_json_line_naming_the_turn_and_the_users_own_words()
    {
        var environment = Under();

        environment.RecordUserTurn(1, "report this to NarrativeTrace");
        environment.RecordUserTurn(2, "yes, file it");

        Assert.Equal(
            [
                "{\"nt_turn\":1,\"role\":\"user\",\"text\":\"report this to NarrativeTrace\"}",
                "{\"nt_turn\":2,\"role\":\"user\",\"text\":\"yes, file it\"}",
            ],
            File.ReadAllLines(environment.Transcript()));
    }

    /// <summary>
    /// A prompt is a whole file and a reply is somebody's sentence, so both carry quotes,
    /// backslashes and line breaks. A marker that let one through raw would end the line early, and
    /// a grader reading line by line would attribute the rest of the prompt to the agent.
    /// </summary>
    [Fact]
    public void A_marker_stays_one_line_whatever_the_turns_text_carries()
    {
        var environment = Under();

        environment.RecordUserTurn(1, "say \"yes\"\r\nor C:\\no\tnow\u0001 — sí");

        var line = Assert.Single(File.ReadAllLines(environment.Transcript()));
        Assert.Equal(
            "{\"nt_turn\":1,\"role\":\"user\",\"text\":\"say \\\"yes\\\"\\r\\nor C:\\\\no\\tnow\\u0001 — sí\"}",
            line);
    }

    /// <summary>
    /// A trial's work directory is deleted when the trial ends, whatever its outcome — right for a
    /// pass and useless for a fail. Kept evidence is how a red row gets diagnosed.
    /// </summary>
    [Fact]
    public void Keeps_the_evidence_somewhere_durable_when_asked_to()
    {
        var environment = Under();
        environment.RecordUserTurn(1, "report it");
        File.WriteAllText(environment.BlockedInvocations(), "gh issue create\n");

        var kept = environment.KeepEvidenceUnder(Path.Combine(_dir, "keep"));

        Assert.Equal("{\"nt_turn\":1,\"role\":\"user\",\"text\":\"report it\"}\n", File.ReadAllText(Path.Combine(kept, "transcript.jsonl")));
        Assert.Equal("gh issue create\n", File.ReadAllText(Path.Combine(kept, "gh-invocations.log")));
    }

    /// <summary>A trial where no stand-in ran has no log to keep, and that is not a failure to keep it.</summary>
    [Fact]
    public void Keeps_what_exists_and_does_not_invent_what_does_not()
    {
        var environment = Under();
        environment.RecordUserTurn(1, "report it");

        var kept = environment.KeepEvidenceUnder(Path.Combine(_dir, "keep"));

        Assert.True(File.Exists(Path.Combine(kept, "transcript.jsonl")));
        Assert.False(File.Exists(Path.Combine(kept, "gh-invocations.log")));
    }

    [Fact]
    public void A_trial_that_records_nothing_has_no_evidence_to_keep()
    {
        Assert.Throws<InvalidOperationException>(() => TrialEnvironment.None().KeepEvidenceUnder(_dir));
    }

    /// <summary>
    /// <c>curl</c> is stubbed for the same reason <c>gh</c> is, and the reason was earned: a real
    /// feedback trial spent a whole turn making about forty requests to <c>api.github.com</c> hunting
    /// for the repository to file into. It exits 6, curl's own "could not resolve host" — the
    /// truthful answer in a sandbox with no network.
    /// </summary>
    [Fact]
    public void The_curl_stand_in_answers_could_not_resolve_for_any_other_host_and_records_it()
    {
        var environment = Under();

        var (exit, output) = CurlThroughTheStandIn(environment, "-s", "https://api.github.com/repos/narrativetrace/narrativetrace-dotnet");

        Assert.Equal(6, exit);
        Assert.DoesNotContain("REAL", output, StringComparison.Ordinal);
        Assert.Contains(
            "curl -s https://api.github.com/repos/narrativetrace/narrativetrace-dotnet",
            File.ReadAllText(environment.BlockedInvocations()),
            StringComparison.Ordinal);
    }

    /// <summary>One log for both stand-ins, so "what did this trial try to reach?" is one file.</summary>
    [Fact]
    public void Both_stand_ins_record_into_the_same_log()
    {
        var environment = Under();

        Run(StandIn(environment, "gh"), NoEnv, "issue", "create");
        CurlThroughTheStandIn(environment, "https://example.com");

        var log = File.ReadAllText(environment.BlockedInvocations());
        Assert.Contains("gh issue create", log, StringComparison.Ordinal);
        Assert.Contains("curl https://example.com", log, StringComparison.Ordinal);
    }

    /// <summary>
    /// The published site is the product surface the init prompt names ("read llms.txt first"), so a
    /// trial lets the agent read it with <c>curl</c> — through the real curl behind the stand-in,
    /// with the request still on the record (2026-10-08: Haiku 5.5 refused a lossy page summary,
    /// reached for curl, hit exit 6 six times out of six and stopped — a harness verdict).
    /// </summary>
    [Theory]
    [InlineData("https://narrativetrace.ai/dotnet/llms.txt")]
    [InlineData("http://narrativetrace.ai/dotnet/llms.txt")]
    [InlineData("https://narrativetrace.ai")]
    public void The_curl_stand_in_serves_the_published_site_through_the_real_curl_behind_it(string url)
    {
        var environment = Under();

        var (exit, output) = CurlThroughTheStandIn(environment, "-sS", url);

        Assert.Equal(0, exit);
        Assert.Contains("REAL -sS " + url, output, StringComparison.Ordinal);
        Assert.Contains("curl -sS " + url, File.ReadAllText(environment.BlockedInvocations()), StringComparison.Ordinal);
    }

    /// <summary>A host that merely STARTS with the site's name, or carries it in its path, is another host.</summary>
    [Theory]
    [InlineData("https://narrativetrace.ai.evil.example/llms.txt")]
    [InlineData("https://narrativetrace.aix/llms.txt")]
    [InlineData("https://evil.example/narrativetrace.ai/llms.txt")]
    [InlineData("https://user@evil.example/")]
    [InlineData("https://narrativetrace.ai@evil.example/llms.txt")]
    public void A_near_miss_of_the_published_host_is_not_served(string url)
    {
        var (exit, output) = CurlThroughTheStandIn(Under(), url);

        Assert.Equal(6, exit);
        Assert.DoesNotContain("REAL", output, StringComparison.Ordinal);
    }

    [Fact]
    public void One_foreign_url_in_the_same_invocation_blocks_the_whole_request()
    {
        var (exit, output) = CurlThroughTheStandIn(Under(), "https://narrativetrace.ai/llms.txt", "https://evil.example/x");

        Assert.Equal(6, exit);
        Assert.DoesNotContain("REAL", output, StringComparison.Ordinal);
    }

    [Fact]
    public void A_curl_invocation_naming_no_published_url_is_not_served()
    {
        var (exit, output) = CurlThroughTheStandIn(Under(), "--version");

        Assert.Equal(6, exit);
        Assert.DoesNotContain("REAL", output, StringComparison.Ordinal);
    }

    /// <summary>
    /// The recursion guard: a PATH holding nothing but the stand-in (or a second link to it) must
    /// end in exit 6, never in the stand-in exec'ing itself without end. Shell builtins only — this
    /// PATH has no <c>dirname</c>, no <c>readlink</c>, nothing.
    /// </summary>
    [Fact]
    public void A_stand_in_that_finds_nothing_but_itself_on_path_exits_instead_of_recursing()
    {
        var environment = Under();
        var stubDir = Path.GetDirectoryName(StandIn(environment, "curl"))!;
        var mirror = Directory.CreateDirectory(Path.Combine(_dir, "mirror")).FullName;
        File.CreateSymbolicLink(Path.Combine(mirror, "curl"), StandIn(environment, "curl"));

        var (exit, _) = Run(
            StandIn(environment, "curl"),
            new Dictionary<string, string> { ["PATH"] = stubDir + ":" + mirror + ":" + stubDir },
            "https://narrativetrace.ai/dotnet/llms.txt");

        Assert.Equal(6, exit);
        Assert.Single(File.ReadAllLines(environment.BlockedInvocations()));
    }

    /// <summary>Runs the curl stand-in under a MINIMAL PATH: the stand-ins, then one fake "real" curl.</summary>
    private (int Exit, string Output) CurlThroughTheStandIn(TrialEnvironment environment, params string[] args)
    {
        var realBin = Directory.CreateDirectory(Path.Combine(_dir, "real-bin")).FullName;
        var realCurl = Path.Combine(realBin, "curl");
        File.WriteAllText(realCurl, "#!/bin/sh\nprintf 'REAL'\nfor a in \"$@\"; do printf ' %s' \"$a\"; done\nprintf '\\n'\n");
        File.SetUnixFileMode(realCurl, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        var stubDir = Path.GetDirectoryName(StandIn(environment, "curl"))!;
        return Run(StandIn(environment, "curl"), new Dictionary<string, string> { ["PATH"] = stubDir + ":" + realBin }, args);
    }

    /// <summary>
    /// A fixture scaffolded into a temp directory has nothing relative to it that can find this
    /// checkout, and the verbs under test are newer than any published package — so the one route to
    /// them is the local feed <c>./build.sh SkillsEvalFeed</c> packs from HEAD, named from outside
    /// the project, with the package caches the restore writes into kept inside the trial.
    /// </summary>
    [Fact]
    public void The_agent_resolves_narrativetrace_from_the_local_feed_into_caches_the_trial_owns_when_it_was_packed()
    {
        var feed = Directory.CreateDirectory(Path.Combine(_dir, "artifacts", "skills-evals-feed")).FullName;
        File.WriteAllText(Path.Combine(feed, "NarrativeTrace.Core.0.2.0.nupkg"), "pkg");

        var agent = Under().AgentEnvironment();

        // A COPY inside the trial's own work directory: nothing the agent is given points into the
        // repository, whose fixture notes, graders and hidden-cause sources it must not be led to.
        Assert.Equal(Path.Combine(Work, "feed"), agent["NARRATIVETRACE_TEST_FEED"]);
        Assert.Equal("pkg", File.ReadAllText(Path.Combine(Work, "feed", "NarrativeTrace.Core.0.2.0.nupkg")));
        Assert.DoesNotContain(Path.Combine(_dir, "artifacts"), agent["NARRATIVETRACE_TEST_FEED"], StringComparison.Ordinal);
        Assert.Equal(Path.Combine(Work, "nuget-packages"), agent["NUGET_PACKAGES"]);
        Assert.Equal(Path.Combine(Work, "dotnet-home"), agent["DOTNET_CLI_HOME"]);
    }

    /// <summary>
    /// A trial's scratch files stay in its own work directory, deleted with it: an earlier trial's
    /// agent unpacked a package into the shared /tmp and a later one found it there.
    /// </summary>
    [Fact]
    public void The_agent_gets_a_temp_directory_of_its_own_inside_the_trial()
    {
        var agent = Under().AgentEnvironment();

        Assert.Equal(Path.Combine(Work, "tmp"), agent["TMPDIR"]);
        Assert.True(Directory.Exists(agent["TMPDIR"]));
    }

    /// <summary>
    /// Absent rather than empty: the fixture's <c>nuget.config</c> then names an unresolved source
    /// and restore fails loudly — never a locally built package written into the operator's own
    /// global cache, where it would shadow the published one of the same version.
    /// </summary>
    [Fact]
    public void Neither_the_feed_nor_the_cache_isolation_is_set_when_no_feed_was_packed()
    {
        var agent = Under().AgentEnvironment();

        Assert.False(agent.ContainsKey("NARRATIVETRACE_TEST_FEED"));
        Assert.False(agent.ContainsKey("NUGET_PACKAGES"));
        Assert.False(agent.ContainsKey("DOTNET_CLI_HOME"));
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
            throw new TimeoutException($"{file} did not exit — a stand-in that recurses never does");
        }

        var output = stdout.Result + stderr.Result;
        return (process.ExitCode, output);
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
