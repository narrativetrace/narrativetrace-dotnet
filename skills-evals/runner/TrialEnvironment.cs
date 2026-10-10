// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace NarrativeTrace.SkillsEvals;

/// <summary>
/// The environment every command of one trial runs with, and the evidence a grader reads
/// afterwards — the transcript and the log the recording stand-ins write.
///
/// <para>All of it lives in a work directory OUTSIDE the scaffolded project. That is the whole
/// point: the agent cannot read what it is being graded on, and cannot write it either, so a
/// transcript is evidence rather than something the subject produced about itself. Only the grader
/// is told where the two files are.</para>
/// </summary>
public sealed class TrialEnvironment
{
    /// <summary>
    /// The commands a trial may not really run, each with the exit code its stand-in answers.
    /// <c>gh</c> exits 0: the point is to record that the agent tried to FILE, so the stand-in
    /// behaves as if it had worked and files nothing. <c>curl</c> exits 6 — curl's own "could not
    /// resolve host" — because the truthful answer to a request from a sandbox with no network is a
    /// network failure, where exiting 0 with empty output would read as "that repository has nothing
    /// in it" and send the agent down a wrong path. Both are outside every skill's closed command
    /// vocabulary by ruling, so nothing a skill legitimately instructs loses anything by this.
    /// </summary>
    private static readonly (string Name, int ExitCode)[] BlockedCommands = [("gh", 0), ("curl", 6)];

    /// <summary>
    /// One log for every stand-in, so "what did this trial try to reach?" is one file. Its name
    /// matches the grader variable that reads it for "was anything filed?".
    /// </summary>
    private const string BlockedLogName = "gh-invocations.log";

    /// <summary>
    /// The one host a <c>curl</c> stand-in passes through to the real curl: the published site.
    /// The init prompt's first step is "read llms.txt first", and an agent that fetches it with
    /// <c>curl</c> rather than a summarising page tool is doing exactly what the product asks.
    /// </summary>
    public const string ServedHost = "narrativetrace.ai";

    /// <summary>Set by a stand-in before it hands over, so a stand-in reached again exits instead.</summary>
    private const string RecursionGuardVariable = "NARRATIVETRACE_CURL_STANDIN";

    /// <summary>Where <c>./build.sh SkillsEvalFeed</c> leaves every package it packed from HEAD.</summary>
    public const string FeedPath = "artifacts/skills-evals-feed";

    /// <summary>How a fixture's <c>nuget.config</c> finds that feed from a temp directory.</summary>
    private const string FeedVariable = "NARRATIVETRACE_TEST_FEED";

    /// <summary>What the grader is told, and the agent is not.</summary>
    private const string TranscriptVariable = "NARRATIVETRACE_TRANSCRIPT";

    private const string GhLogVariable = "NARRATIVETRACE_GH_LOG";

    /// <summary>
    /// No <c>__pycache__</c>: a grader that imports a shared helper would otherwise write one INTO
    /// the committed case directory — a trial leaving build output inside the repository it tests.
    /// </summary>
    private const string NoBytecodeVariable = "PYTHONDONTWRITEBYTECODE";

    /// <summary>
    /// Every non-ASCII character kept as itself (a reply is somebody's sentence, in their language),
    /// every control character escaped — so a marker is one line whatever the turn's text carries.
    /// </summary>
    private static readonly JsonSerializerOptions OneLineJson =
        new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private static readonly TrialEnvironment NoEnvironment =
        new(null, new Dictionary<string, string>(StringComparer.Ordinal));

    private readonly string? _workDir;
    private readonly IReadOnlyDictionary<string, string> _agentEnvironment;

    private TrialEnvironment(string? workDir, IReadOnlyDictionary<string, string> agentEnvironment)
    {
        _workDir = workDir;
        _agentEnvironment = agentEnvironment;
    }

    /// <summary>A trial that records nothing and adds nothing — what a unit test of the flow wires.</summary>
    public static TrialEnvironment None() => NoEnvironment;

    /// <summary>A trial recording into <paramref name="workDir"/>.</summary>
    public static TrialEnvironment Under(string workDir, string repoRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workDir);
        ArgumentException.ThrowIfNullOrWhiteSpace(repoRoot);
        RequireQuotable(workDir);
        var binDir = Directory.CreateDirectory(Path.Combine(workDir, "bin")).FullName;
        var log = Path.Combine(workDir, BlockedLogName);
        foreach (var (name, exitCode) in BlockedCommands)
        {
            WriteStandIn(Path.Combine(binDir, name), name, exitCode, log);
        }

        var environment = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["PATH"] = binDir + ":" + Environment.GetEnvironmentVariable("PATH"),
            [NoBytecodeVariable] = "1",
            // The agent's own scratch space, deleted with the trial: a shared /tmp let one trial's
            // unpacked package reach the next trial's agent.
            ["TMPDIR"] = Directory.CreateDirectory(Path.Combine(workDir, "tmp")).FullName,
        };
        foreach (var (key, value) in IsolatedAgentConfig.AgentConfigEnv(workDir).Concat(LocalFeed(workDir, repoRoot)))
        {
            environment[key] = value;
        }

        return new TrialEnvironment(workDir, environment);
    }

    /// <summary>Whether this trial keeps a transcript and a stand-in log at all.</summary>
    public bool IsRecording => _workDir is not null;

    /// <summary>Every turn's prompt and the agent's own stdout, in order — the grader's record of the run.</summary>
    public string Transcript() => Path.Combine(RequireRecording(), "transcript.jsonl");

    /// <summary>One line per invocation of a blocked command the stand-ins recorded, argv included.</summary>
    public string BlockedInvocations() => Path.Combine(RequireRecording(), BlockedLogName);

    /// <summary>What the agent's own turns run with.</summary>
    public IReadOnlyDictionary<string, string> AgentEnvironment() => _agentEnvironment;

    /// <summary>The agent's environment, plus where the two pieces of evidence are.</summary>
    public IReadOnlyDictionary<string, string> GraderEnvironment()
    {
        if (!IsRecording)
        {
            return _agentEnvironment;
        }

        return new Dictionary<string, string>(_agentEnvironment, StringComparer.Ordinal)
        {
            [TranscriptVariable] = Transcript(),
            [GhLogVariable] = BlockedInvocations(),
        };
    }

    /// <summary>
    /// The route to the packages <c>./build.sh SkillsEvalFeed</c> packed from HEAD, when it has run:
    /// the feed a fixture's <c>nuget.config</c> names as <c>%NARRATIVETRACE_TEST_FEED%</c>, plus a
    /// package cache and a CLI home the TRIAL owns. The isolation is not optional once the feed is
    /// used: a package built here carries the same version as the published one, and written into the
    /// operator's global cache it would silently stand in for it in every other project.
    /// </summary>
    private static Dictionary<string, string> LocalFeed(string workDir, string repoRoot)
    {
        var feed = Path.Combine(repoRoot, FeedPath);
        return !Directory.Exists(feed)
            ? []
            : new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [FeedVariable] = CopyOfTheFeed(feed, workDir),
                ["NUGET_PACKAGES"] = Path.Combine(workDir, "nuget-packages"),
                ["DOTNET_CLI_HOME"] = Path.Combine(workDir, "dotnet-home"),
            };
    }

    /// <summary>
    /// The feed's packages copied into the trial's own work directory: the agent is told where its
    /// feed is, and a path into the repository would lead it to the fixture notes, the graders and the
    /// sources of a dependency whose behaviour a case hides on purpose (found on the first Phase 7
    /// verify trial: the agent followed the package name into the repository and read the source).
    /// </summary>
    private static string CopyOfTheFeed(string feed, string workDir)
    {
        var copy = Directory.CreateDirectory(Path.Combine(workDir, "feed")).FullName;
        foreach (var package in Directory.EnumerateFiles(feed, "*.nupkg"))
        {
            File.Copy(package, Path.Combine(copy, Path.GetFileName(package)), overwrite: true);
        }

        return copy;
    }

    /// <summary>
    /// Marks the start of <paramref name="turn"/> in the transcript, with the words the user is
    /// given there. The marker is what lets a grader say WHICH turn it is reading — every agent line
    /// after it and before the next marker belongs to that turn — and it carries the user's own
    /// words, so a grader compares the agent's behaviour against the reply that was actually
    /// scripted rather than against a copy of it.
    /// </summary>
    public void RecordUserTurn(int turn, string text)
    {
        var transcript = Transcript();
        ArgumentOutOfRangeException.ThrowIfLessThan(turn, 1);
        ArgumentNullException.ThrowIfNull(text);
        File.AppendAllText(
            transcript,
            "{\"nt_turn\":" + turn.ToString(CultureInfo.InvariantCulture)
            + ",\"role\":\"user\",\"text\":" + JsonSerializer.Serialize(text, OneLineJson) + "}\n");
    }

    /// <summary>
    /// Copies whatever evidence this trial produced into <paramref name="destination"/>, and returns
    /// it. A trial's work directory is a temp directory the runner deletes when the trial ends,
    /// whatever the outcome — right for a pass and useless for a fail, where the grader's one-line
    /// reason is on the console and the record it read is gone.
    /// </summary>
    public string KeepEvidenceUnder(string destination)
    {
        var evidence = new[] { Transcript(), BlockedInvocations() };
        Directory.CreateDirectory(destination);
        foreach (var file in evidence)
        {
            var kept = Path.Combine(destination, Path.GetFileName(file));
            if (File.Exists(file))
            {
                File.Copy(file, kept, overwrite: true);
            }
            else
            {
                // Never leave an older trial's record beside this one, for a reader to attribute to it.
                File.Delete(kept);
            }
        }

        return destination;
    }

    /// <summary>
    /// A recording stand-in: it writes down what it was asked to do, and does none of it. Each
    /// argument is appended on the same line, so one invocation is one greppable line and a grader
    /// can both count them and search them for a planted value.
    /// </summary>
    private static void WriteStandIn(string stub, string name, int exitCode, string log)
    {
        File.WriteAllText(
            stub,
            "#!/bin/sh\n"
            + $"# A recording stand-in for `{name}`, installed first on this trial's PATH by the eval\n"
            + "# runner. It records its argv and does NOTHING ELSE, so no trial can file an issue or\n"
            + "# reach a network - and so 'the agent tried to' is distinguishable from 'it did not',\n"
            + "# which the command merely being absent is not.\n"
            + "{\n"
            + $"  printf '{name}'\n"
            + "  for arg in \"$@\"; do printf ' %s' \"$arg\"; done\n"
            + "  printf '\\n'\n"
            + $"}} >> '{log}'\n"
            + ServedRequestOrExit(name, exitCode));
        File.SetUnixFileMode(stub, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }

    /// <summary>
    /// The stand-in's tail: <c>exit N</c> for every command but <c>curl</c>; for curl, a request is
    /// passed to the first real curl behind the stand-in on PATH only when EVERY argument is either a
    /// URL on <see cref="ServedHost"/> or an option from a short read-only allow-list, and at least one
    /// URL is given. Anything else — a foreign URL in any spelling (<c>--url=</c>, no scheme, another
    /// scheme), an upload or data option, a config file, an option this list does not know — exits
    /// with the blocked code. An allow-list rather than a deny-list because curl fetches EVERY
    /// non-option argument: a deny-list of URL shapes let <c>evil.example/x</c> ride along beside a
    /// served URL (found by the milestone's adversarial pass; Java's stand-in has the same hole).
    /// Matching is exact and case-sensitive on purpose: a spelling it does not know fails closed.
    /// </summary>
    private static string ServedRequestOrExit(string name, int exitCode)
    {
        if (name != "curl")
        {
            return $"exit {exitCode}\n";
        }

        return ServedOnlyWhenEveryArgumentIsAllowed(exitCode) + HandToTheRealCurlBehind(exitCode);
    }

    /// <summary>One pass over argv: a served URL sets <c>served</c>; an allowed option passes; anything else exits.</summary>
    private static string ServedOnlyWhenEveryArgumentIsAllowed(int exitCode) =>
        "served=0\n"
        + "value=\n"
        + "for arg in \"$@\"; do\n"
        + "  if [ -n \"$value\" ]; then\n"
        + "    [ \"$value\" = url ] && { case \"$arg\" in " + ServedUrls + ") served=1 ;; *) exit " + exitCode + " ;; esac; }\n"
        + "    value=; continue\n"
        + "  fi\n"
        + "  case \"$arg\" in\n"
        + "    " + ServedUrls + ") served=1 ;;\n"
        + "    --url) value=url ;;\n"
        + "    --url=*) case \"${arg#--url=}\" in " + ServedUrls + ") served=1 ;; *) exit " + exitCode + " ;; esac ;;\n"
        + "    " + OptionsTakingAValue + ") value=other ;;\n"
        + "    " + OptionsWithAnInlineValue + ") ;;\n"
        + "    " + LongFlags + ") ;;\n"
        + "    -*[!" + ShortFlagLetters + "]*) exit " + exitCode + " ;;\n"
        + "    -?*) ;;\n"
        + "    *) exit " + exitCode + " ;;\n"
        + "  esac\n"
        + "done\n"
        + "[ -z \"$value\" ] && [ \"$served\" = 1 ] || exit " + exitCode + "\n";

    /// <summary>The site, by both schemes, bare or with a path — nothing else, in no other spelling.</summary>
    private const string ServedUrls =
        "http://" + ServedHost + "|http://" + ServedHost + "/*|https://" + ServedHost + "|https://" + ServedHost + "/*";

    /// <summary>Read-only options whose value is the next argument (where to write, what to show, how long to wait).</summary>
    private const string OptionsTakingAValue =
        "-o|--output|-H|--header|-A|--user-agent|-m|--max-time|--connect-timeout|-w|--write-out|--retry";

    /// <summary>The same options with their value attached.</summary>
    private const string OptionsWithAnInlineValue =
        "--output=*|--header=*|--user-agent=*|--max-time=*|--connect-timeout=*|--write-out=*|--retry=*";

    /// <summary>Read-only long flags: quiet, errors, redirects, failure codes, headers in the output.</summary>
    private const string LongFlags =
        "--silent|--show-error|--location|--fail|--fail-with-body|--compressed|--include|--head|--no-progress-meter";

    /// <summary>Short flags that may be combined (<c>-sSL</c>): silent, show-error, location, fail, include, head.</summary>
    private const string ShortFlagLetters = "sSLfiI";

    /// <summary>Execs the first curl on PATH that is not this stand-in; exits blocked when there is none.</summary>
    private static string HandToTheRealCurlBehind(int exitCode) =>
        $"[ -n \"${RecursionGuardVariable}\" ] && exit {exitCode}\n"
        + $"export {RecursionGuardVariable}=1\n"
        + "self=${0%/*}\n"
        + "IFS=:\n"
        + "for dir in $PATH; do\n"
        + "  [ \"$dir\" = \"$self\" ] && continue\n"
        + "  [ \"$dir/curl\" -ef \"$0\" ] && continue\n"
        + "  [ -x \"$dir/curl\" ] && exec \"$dir/curl\" \"$@\"\n"
        + "done\n"
        + $"exit {exitCode}\n";

    /// <summary>The stand-in embeds its log path in a POSIX single-quoted string, which one quote would end.</summary>
    private static void RequireQuotable(string workDir)
    {
        if (workDir.Contains('\'', StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "a trial's work directory may not carry a ' — the stand-ins quote their log path: " + workDir,
                nameof(workDir));
        }
    }

    private string RequireRecording() =>
        _workDir ?? throw new InvalidOperationException("a trial that records nothing has no evidence to point at");
}
