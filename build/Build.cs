// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml.Linq;
using NarrativeTrace.Build;
using Nuke.Common;
using Nuke.Common.Execution;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.Git;
using Serilog;
using static Nuke.Common.Tools.DotNet.DotNetTasks;
using static Nuke.Common.Tools.Git.GitTasks;

class Build : NukeBuild
{
    public static int Main() => Execute<Build>(x => x.Compile);

    [Solution("NarrativeTrace.sln")] readonly Solution Solution;

    [Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")]
    readonly Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;

    [Parameter("Demo: example to run (ecommerce|clarity|minecraft|library); omitted = interactive picker")]
    readonly string Example;

    [Parameter("Demo: play straight through, no stop points")]
    readonly bool NoPause;

    [Parameter("Security scanners: fail (not just warn) when a scanner binary is absent — always true under CI")]
    readonly bool SecurityRequired;

    AbsolutePath ArtifactsDirectory => RootDirectory / "artifacts";
    AbsolutePath TestResultsDirectory => ArtifactsDirectory / "test-results";
    AbsolutePath CoverageDirectory => ArtifactsDirectory / "coverage";
    AbsolutePath MutationDirectory => ArtifactsDirectory / "mutation";
    AbsolutePath MetricsDirectory => ArtifactsDirectory / "metrics";
    AbsolutePath BenchmarkArtifactsDir => ArtifactsDirectory / "benchmarks";
    AbsolutePath BenchmarkBaselineFile => RootDirectory / "benchmarks" / "benchmark-baseline.json";
    AbsolutePath SecurityDirectory => ArtifactsDirectory / "security";
    AbsolutePath SecurityScanStatusDirectory => SecurityDirectory / "scan-status";

    /// <summary>
    /// Whether a missing scanner binary must fail its target rather than warn-and-pass — always
    /// true under CI (the bare <c>CI</c> environment variable, which GitLab CI and GitHub Actions
    /// both set automatically — matching the java runtime's own check), or when
    /// <see cref="SecurityRequired"/> is passed explicitly. Checked directly rather than via
    /// <see cref="NukeBuild.IsLocalBuild"/>: that
    /// property recognizes specific CI hosts by their own provider variables, which is narrower
    /// than the bare <c>CI</c> convention this gate is meant to catch. See
    /// <see cref="ScannerGateSupport"/>.
    /// </summary>
    bool SecurityScannersRequired =>
        SecurityRequired || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CI"));
    AbsolutePath FuzzDirectory => ArtifactsDirectory / "fuzz";
    AbsolutePath FuzzProject => RootDirectory / "fuzz" / "NarrativeTrace.Fuzz" / "NarrativeTrace.Fuzz.csproj";
    AbsolutePath FuzzSeeds => RootDirectory / "fuzz" / "NarrativeTrace.Fuzz" / "seeds";
    AbsolutePath VerifyAllLogDirectory => ArtifactsDirectory / "verifyAll-logs";
    AbsolutePath VerificationReportsDirectory => RootDirectory / "reports" / "verification";

    [Parameter("VerifyAll: 1-minute host load average above which benchmarks/allocation are " +
        "skipped rather than measured — this shared host has a documented pattern of phantom " +
        "benchmark regressions under load. Default 6.0 (this container's own core count).")]
    readonly double VerifyAllLoadThreshold = 6.0;

    [Parameter("VerifyAll: skip the benchmarks/allocation categories outright, independent of the " +
        "load-average check — for a run where a timing measurement is known to be pointless.")]
    readonly bool VerifyAllSkipTiming;

    [Parameter("Fuzz: seconds afl-fuzz spends on each target (renderer, json) — default 60")]
    readonly int FuzzSeconds = 60;

    [Parameter("Stress: repetitions per race in the long sweep — default 200000")]
    readonly int StressSweepIterations = 200_000;

    [Parameter("VerifyPublication: package version to verify (default: this build's own VersionPrefix)")]
    readonly string? VerifyVersion;

    [Parameter("VerifyPublication: check the local feed under artifacts/ instead of the real nuget.org — rehearsal only, never the default")]
    readonly bool LocalRehearsal;

    [Parameter("VerifyPublication: print the coordinates/URLs that would be checked; no network calls, no smoke test")]
    readonly bool VerifyDryRun;

    [Parameter("VerifyPublication: overall polling deadline in seconds — nuget.org sync is minutes to hours (default: 7200)")]
    readonly int VerifyTimeoutSeconds = 7200;

    [Parameter("VerifyPublication: steady-state wait between polling rounds once backoff has ramped up (default: 60)")]
    readonly int VerifyIntervalSeconds = 60;

    /// <summary>
    /// The projects the <c>Test</c> and <c>Coverage</c> sweeps run. Selection
    /// is by name — see <see cref="TestProjectSelection"/>, whose check keeps
    /// every project under <c>tests/</c> inside the gate.
    /// </summary>
    Project[] TestProjects => Solution.AllProjects
        .Where(x => TestProjectSelection.IsSelected(x.Name))
        .ToArray();

    // Per-test-project line-coverage floors and the explicit, written-reason
    // exemptions from them live in CoverageAccounting.Thresholds/Exemptions —
    // see that class for both maps and the default-deny check (run at the
    // top of Coverage, below) that every project in TestProjects is in one
    // or the other.

    /// <summary>
    /// Tests that re-invoke <c>./build.sh</c> (BuildScript.Tests). Excluded
    /// from the project-wide Test and Coverage sweeps, which would otherwise
    /// recurse into themselves; the BuildScriptTests target runs them.
    /// </summary>
    const string SpawnsBuildFilter = "Category!=SpawnsBuild";

    /// <summary>
    /// Process logger for the handful of invocations below whose healthy,
    /// informational chatter lands on stderr instead of stdout — gitleaks'
    /// own "no leaks found" progress line, <c>dotnet format</c>'s
    /// workspace-loading grumble, semgrep's scan-status banner. Nuke's
    /// default logger (used whenever a <c>logger</c> argument is omitted)
    /// treats every stderr line as a Serilog error, so a fully green run
    /// reads back as a wall of [ERR]. This routes both streams to
    /// Information instead; it changes nothing about failure detection —
    /// <c>AssertZeroExitCode</c> (or the task's own exit-code check) remains
    /// the only signal a run failed.
    /// </summary>
    static readonly Action<OutputType, string> QuietProcessLogger = (_, text) => Log.Information(text);

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    Target Clean => _ => _
        .Before(Restore)
        .Executes(() =>
        {
            if (Directory.Exists(ArtifactsDirectory))
                Directory.Delete(ArtifactsDirectory, true);
            Directory.CreateDirectory(ArtifactsDirectory);
        });

    Target Restore => _ => _
        .Executes(() => DotNetRestore(s => s.SetProjectFile(Solution)));

    Target Compile => _ => _
        .DependsOn(Restore)
        .Executes(() =>
        {
            DotNetBuild(s => s
                .SetProjectFile(Solution)
                .SetConfiguration(Configuration)
                .EnableNoRestore());
        });

    // ── Quality gates ─────────────────────────────────────────────────────────

    Target Analyze => _ => _
        .DependsOn(Restore)
        .Executes(() =>
        {
            DotNetBuild(s => s
                .SetProjectFile(Solution)
                .SetConfiguration(Configuration)
                .EnableNoRestore()
                .SetProperty("RunAnalyzers", "true"));
        });

    AbsolutePath FormatSolution => RootDirectory / "NarrativeTrace.Format.sln";

    // whitespace + style only: bare `dotnet format` also runs the ANALYZERS
    // pass at warn severity, turning every Sonar/CA suggestion into a format
    // failure — analyzers are Analyze's gate (RunAnalyzers=true there), not
    // the formatter's.
    Target Format => _ => _
        .DependsOn(Restore)
        .Executes(() =>
        {
            var solution = WriteFormatSolution();
            DotNet($"format whitespace \"{solution}\" --no-restore", logger: QuietProcessLogger);
            DotNet($"format style \"{solution}\" --no-restore", logger: QuietProcessLogger);
        });

    Target FormatCheck => _ => _
        .DependsOn(Restore)
        .Executes(() =>
        {
            var solution = WriteFormatSolution();
            DotNet($"format whitespace \"{solution}\" --no-restore --verify-no-changes", logger: QuietProcessLogger);
            DotNet($"format style \"{solution}\" --no-restore --verify-no-changes", logger: QuietProcessLogger);
        });

    /// <summary>
    /// Copies the solution and removes the F# project, returning the copy's
    /// path. A .slnf filter is not enough: dotnet format loads the underlying
    /// solution's whole workspace regardless of the filter, and the SDK
    /// regression the remarks below describe makes the F# load fatal — so the
    /// solution format opens must genuinely not contain it. The copy sits at
    /// the root (gitignored) so the projects' relative paths stay valid.
    /// </summary>
    AbsolutePath WriteFormatSolution()
    {
        File.Copy(Solution.Path!, FormatSolution, overwrite: true);
        // The workspace loader follows ProjectReferences, so removing the
        // F# projects alone is not enough: any C# project referencing one
        // (the F# example's test project) drags it back in. Remove to a
        // fixpoint, derived from the project files — never hand-listed.
        var removed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        bool ProjectPullsInFsharp(Nuke.Common.ProjectModel.Project p) =>
            p.Path.ToString().EndsWith(".fsproj", StringComparison.OrdinalIgnoreCase)
            || File.ReadAllText(p.Path).Contains(".fsproj", StringComparison.OrdinalIgnoreCase);
        foreach (var project in Solution.AllProjects.Where(ProjectPullsInFsharp))
        {
            if (removed.Add(project.Path))
            {
                DotNet($"sln \"{FormatSolution}\" remove \"{project.Path}\"");
            }
        }
        return FormatSolution;
    }


    /// <summary>
    /// Fails when a translated public document has drifted from its English
    /// source, is incomplete for a language declared <c>complete</c>, has
    /// lost structural parity (headings, code samples, tables, links), or its
    /// language menu/index has drifted — see the i18n terminology conventions
    /// and <c>documentation/i18n/manifest.json</c>. Editing an English public
    /// doc therefore requires refreshing its translations in the same change;
    /// that consequence is the point of the gate. Absent a manifest, this
    /// degrades to the staleness-only check with one warning saying so.
    /// </summary>
    Target TranslationCheck => _ => _
        .Executes(() =>
        {
            var result = TranslationPlatformSupport.RunAll(RootDirectory);
            foreach (var warning in result.Warnings)
                Console.WriteLine($"  {warning}");
            foreach (var failure in result.Failures)
                Console.WriteLine($"  {failure}");

            if (result.Failures.Count > 0)
                throw new InvalidOperationException(
                    $"Translation check failed: {result.Failures.Count} problem(s). "
                    + "Refresh the translation and restamp its line-1 header.");

            Console.WriteLine("Translation check passed");
        });

    /// <summary>
    /// Prints the full translation coverage and review-field matrix, one line
    /// per manifest language. A human dashboard, not a gate — deliberately
    /// not a <see cref="Verify"/> dependency, since publish-gating on review
    /// status is a later owner decision.
    /// </summary>
    Target TranslationStatus => _ => _
        .Executes(() =>
        {
            var manifest = I18nManifestSupport.LoadOrNull(RootDirectory);
            Console.WriteLine(TranslationReviewSupport.StatusReport(RootDirectory, manifest));
        });

    /// <summary>
    /// Fails when a tracked source file carries a license header. Owner
    /// ruling 2026-09-01: in-tree license headers must not exist in this
    /// private repository — <c>scripts/publish-public.sh</c> stamps them
    /// only at publish time, over a throwaway snapshot, and never commits
    /// the result here. The inverse of that stamping step.
    /// </summary>
    Target HeaderAbsenceCheck => _ => _
        .Executes(() =>
        {
            var problems = HeaderAbsenceSupport.Check(RootDirectory);
            foreach (var problem in problems)
                Console.WriteLine($"  {problem}");

            if (problems.Count > 0)
                throw new InvalidOperationException(
                    $"Header absence check failed: {problems.Count} file(s) carry an in-tree license "
                    + "header. Headers are stamped by scripts/publish-public.sh at publish time only — "
                    + "remove the header from the source file.");

            Console.WriteLine("Header absence check passed: no tracked source file carries a license header");
        });

    /// <summary>
    /// Verifies the <c>legal:*</c> marked regions in README.md and its root
    /// translations (<c>legal.properties</c>, <c>scripts/legal-check.sh</c>)
    /// are well-formed and, when the sibling golden Java repo is checked out
    /// next to this one, still match its golden copies (LICENSE included).
    /// Delegates entirely to the shell script: default mode WARNs — a
    /// checkout without the sibling, the common CI case, stays green. Set
    /// <c>LEGAL_CHECK_STRICT=1</c> to fail instead for a deliberate local or
    /// CI verification run; deliberately <b>not</b> wired into
    /// <c>scripts/publish-public.sh</c> as a strict preflight, because
    /// <c>legal:trademark</c> wraps each repo's own paraphrase and is
    /// expected to differ from the golden copy — a strict preflight would
    /// fail every publish where the sibling happens to be checked out.
    /// </summary>
    /// <remarks>
    /// Bash-only, like <see cref="Demo"/>'s non-Windows branch and
    /// <c>install-security-tools.sh</c> — skipped with a note where bash is
    /// not on PATH rather than failing the gate over a missing shell.
    /// </remarks>
    Target LegalCheck => _ => _
        .Executes(() =>
        {
            const string tool = "bash";
            if (!IsOnPath(tool))
            {
                Console.WriteLine($"{tool} not found on PATH: legal check skipped "
                    + "(Windows without Git Bash/WSL). CI and *nix dev containers run it.");
                return;
            }

            ProcessTasks.StartProcess(tool, "scripts/legal-check.sh", RootDirectory)
                .AssertZeroExitCode();
        });

    /// <summary>
    /// Fails when a scenario printed by an example has no wiring note in
    /// <c>examples/demo/wiring.awk</c>, or a note names no scenario — the
    /// demo launcher must be able to explain every scenario it walks.
    /// </summary>
    Target DemoWiringCheck => _ => _
        .Executes(() =>
        {
            var problems = DemoWiringSupport.Check(RootDirectory);
            foreach (var problem in problems)
                Console.WriteLine($"  {problem}");

            if (problems.Count > 0)
                throw new InvalidOperationException(
                    $"demo.sh cannot explain every scenario (see examples/demo/wiring.awk): "
                    + $"{problems.Count} problem(s).");

            Console.WriteLine("Demo wiring check passed: every example scenario has a wiring note");
        });

    // ── Examples ──────────────────────────────────────────────────────────────

    /// <summary>Runs every example in sequence, non-interactively (the CI smoke run).</summary>
    /// <remarks>
    /// <c>.After(Test)</c> is an ordering constraint, not a dependency (see
    /// <see cref="Verify"/>'s remarks): it keeps a <c>RunExamples</c> failure
    /// from preempting <see cref="Test"/>, which shares its
    /// <see cref="Compile"/> dependency but is otherwise unordered relative
    /// to it.
    /// </remarks>
    Target RunExamples => _ => _
        .DependsOn(Compile)
        .After(Test)
        .Executes(() =>
        {
            foreach (var name in new[] { "ECommerce", "Clarity", "Minecraft", "Library" })
            {
                var project = RootDirectory / "examples" / $"NarrativeTrace.Examples.{name}";
                if (!Directory.Exists(project))
                    continue;
                Console.WriteLine($"── {name} ──");
                DotNetRun(s => s
                    .SetProjectFile(project)
                    .SetConfiguration(Configuration)
                    .EnableNoBuild());
            }
        });

    /// <summary>
    /// Launches <c>demo.sh</c> (<c>demo.ps1</c> on Windows). Pass
    /// <c>--example NAME</c> and <c>--no-pause</c> for a non-interactive run;
    /// the interactive picker needs a terminal, so prefer the script directly
    /// for that.
    /// </summary>
    Target Demo => _ => _
        .Executes(() =>
        {
            var arguments = new List<string>();
            if (!string.IsNullOrEmpty(Example))
                arguments.AddRange(["--example", Example]);
            if (NoPause)
                arguments.Add("--no-pause");

            var (tool, script) = EnvironmentInfo.IsWin
                ? ("pwsh", "demo.ps1")
                : ("bash", "demo.sh");
            ProcessTasks.StartProcess(
                    tool,
                    string.Join(' ', new[] { script }.Concat(arguments)),
                    RootDirectory)
                .AssertZeroExitCode();
        });

    // ── Tests ─────────────────────────────────────────────────────────────────

    Target Test => _ => _
        .DependsOn(Compile)
        .Executes(() =>
        {
            Directory.CreateDirectory(TestResultsDirectory);
            var failures = new List<string>();
            foreach (var project in TestProjects)
            {
                try
                {
                    DotNetTest(s => s
                        .SetProjectFile(project.Path)
                        .SetConfiguration(Configuration)
                        .EnableNoBuild()
                        .SetFilter(SpawnsBuildFilter)
                        .SetResultsDirectory(TestResultsDirectory)
                        .SetLoggers($"trx;LogFileName={project.Name}.trx"));
                }
                catch (Exception ex)
                {
                    // A failing project must not stop the sweep: the ORIGINAL shape aborted the
                    // loop on the first failing project (DotNetTest asserts a zero exit code),
                    // leaving every alphabetically-later project's .trx file unwritten (or stale
                    // from a previous run) — invisible to anything reading test-results/*.trx
                    // afterward, including VerifyAll's unit-tests/property/fuzz-tier-a/
                    // architecture/conformance/stress-short rows, all sliced from this same run.
                    // Found writing VerifyAll.
                    failures.Add(project.Name);
                    Console.WriteLine($"Test: {project.Name} failed ({ex.Message}) — continuing with the remaining projects");
                }
            }

            if (failures.Count > 0)
            {
                throw new Exception(
                    $"Test: {failures.Count} of {TestProjects.Length} project(s) had failing tests: "
                    + $"{string.Join(", ", failures)}. Every project still ran — see {TestResultsDirectory} "
                    + "for each one's .trx.");
            }
        });

    /// <summary>
    /// Runs the tests that shell out to <c>./build.sh</c> itself.
    /// </summary>
    /// <remarks>
    /// Deliberately outside <c>Verify</c>: each test spawns a nested build, so
    /// folding it into the gate would multiply every gate run by the cost of a
    /// full build. Run it when <c>build/</c> or the MSBuild targets change.
    /// </remarks>
    Target BuildScriptTests => _ => _
        .DependsOn(Compile)
        .Executes(() =>
        {
            Directory.CreateDirectory(TestResultsDirectory);
            var project = Solution.AllProjects
                .First(x => x.Name == "BuildScript.Tests");
            DotNetTest(s => s
                .SetProjectFile(project.Path)
                .SetConfiguration(Configuration)
                .EnableNoBuild()
                .SetFilter("Category=SpawnsBuild")
                .SetResultsDirectory(TestResultsDirectory)
                .SetLoggers($"trx;LogFileName={project.Name}.spawning.trx"));
        });

    /// <summary>
    /// Fails the build when any package in the graph has a known advisory.
    /// </summary>
    /// <remarks>
    /// Runs NuGet's audit over direct <em>and</em> transitive packages,
    /// forcing re-evaluation so a cached restore cannot hide a newly published
    /// advisory. The teeth come from Directory.Build.props, which promotes
    /// NU1902/NU1903 to errors for every project (passing that list here would
    /// need the semicolon escaped, and duplicating it invites drift).
    /// Requires network access — offline runs fail loudly rather than passing
    /// blind.
    /// </remarks>
    Target DependencyAudit => _ => _
        .Executes(() =>
        {
            // The build project is outside the solution, so auditing only the
            // solution would leave the toolchain that produces the artifacts
            // unaudited — which is where the first real finding turned up.
            foreach (var target in new[]
                { Solution.Path.ToString(), (RootDirectory / "build" / "_build.csproj").ToString() })
            {
                DotNetRestore(s => s
                    .SetProjectFile(target)
                    .SetForce(true)
                    .SetProperty("NuGetAudit", "true")
                    .SetProperty("NuGetAuditMode", "all"));
            }
        });

    // ── Security tooling (shared cross-runtime convention) ─────────────────────────

    /// <summary>
    /// Full-history secrets sweep (gitleaks). Git-log mode reads only
    /// already-committed objects — no network — and is fast enough for this
    /// repo (~2 s over ~800 commits) to ride in <see cref="Verify"/> rather
    /// than the scheduled tier <see cref="Mutation"/>/<see cref="Fuzz"/>
    /// occupy for cost reasons. Covers what is already committed; the
    /// pre-commit hook's <c>gitleaks protect --staged</c> covers what is
    /// about to be — together the two close the gap a git-log-only scan
    /// leaves (uncommitted/staged content it cannot see).
    /// </summary>
    /// <remarks>
    /// A missing binary follows <see cref="ScannerGateSupport"/>: WARN and a recorded
    /// <c>skipped</c> status locally (THIN-CI keeps tool installation out of the gate itself; CI
    /// provisions the binary before calling this target), a hard failure under CI or
    /// <see cref="SecurityRequired"/> — the exact class that bit this repository's own first
    /// release, where this same graceful skip went unnoticed.
    /// </remarks>
    Target SecretsScan => _ => _
        .After(Clean)
        .Executes(() =>
        {
            const string tool = "gitleaks";
            if (!IsOnPath(tool))
            {
                var decision = ScannerGateSupport.OnMissingBinary(
                    tool, SecurityScannersRequired,
                    "https://github.com/gitleaks/gitleaks/releases");
                ScannerGateSupport.RecordSkipped(SecurityScanStatusDirectory, tool, "binary not on PATH");
                if (decision.Fail)
                    throw new InvalidOperationException($"SecretsScan: {decision.Message}");
                Console.WriteLine($"SecretsScan: {decision.Message}");
                return;
            }

            Directory.CreateDirectory(SecurityDirectory);
            var reportPath = SecurityDirectory / "gitleaks-report.json";
            ProcessTasks.StartProcess(
                    tool,
                    $"detect --source \"{RootDirectory}\" --redact --no-banner --report-format json "
                        + $"--report-path \"{reportPath}\"",
                    logger: QuietProcessLogger)
                .AssertZeroExitCode();
            ScannerGateSupport.RecordRanClean(SecurityScanStatusDirectory, tool);
        });

    /// <summary>
    /// Runs semgrep's OSS community C# security ruleset (<c>p/csharp</c>)
    /// over the source tree. Custom rules are out of scope by owner ruling —
    /// community ruleset only.
    /// </summary>
    /// <remarks>
    /// Deliberately <b>not</b> a dependency of <see cref="Verify"/>: the
    /// ruleset is fetched from the semgrep registry over the network on
    /// every run, so — like <see cref="DependencyAudit"/> — it cannot sit in
    /// an offline per-commit gate. CI runs it on merge-request and scheduled
    /// pipelines only (the private CI configuration), never on an ordinary push.
    /// A missing binary follows <see cref="ScannerGateSupport"/>, the same
    /// shape as <see cref="SecretsScan"/>.
    /// </remarks>
    Target Semgrep => _ => _
        .Executes(() =>
        {
            const string tool = "semgrep";
            if (!IsOnPath(tool))
            {
                var decision = ScannerGateSupport.OnMissingBinary(
                    tool, SecurityScannersRequired, "pip install semgrep");
                ScannerGateSupport.RecordSkipped(SecurityScanStatusDirectory, tool, "binary not on PATH");
                if (decision.Fail)
                    throw new InvalidOperationException($"Semgrep: {decision.Message}");
                Console.WriteLine($"Semgrep: {decision.Message}");
                return;
            }

            Directory.CreateDirectory(SecurityDirectory);
            var reportPath = SecurityDirectory / "semgrep-csharp.json";
            // semgrep writes its scan-status banner ("Scanning N files...",
            // "Scan Summary") to stderr even with --json --output routing the
            // findings to a file — same chatty-stderr habit as gitleaks and
            // dotnet format above.
            ProcessTasks.StartProcess(
                    tool,
                    $"--config=p/csharp --metrics=off --error --json --output \"{reportPath}\" \"{RootDirectory}\"",
                    logger: QuietProcessLogger)
                .AssertZeroExitCode();
            ScannerGateSupport.RecordRanClean(SecurityScanStatusDirectory, tool);
        });

    /// <summary>
    /// Scans every manifest (<c>*.csproj</c>, <c>Directory.Build.props</c>)
    /// for known-vulnerable dependencies against the OSV database.
    /// </summary>
    /// <remarks>
    /// Needs network (the osv.dev API), so — like <see cref="DependencyAudit"/>
    /// — it runs on schedule/web pipelines only, never per commit.
    /// <see cref="VulnerablePackages"/> runs beside it in the same tier as a
    /// second, NuGet-native source for the same question. A missing binary
    /// follows <see cref="ScannerGateSupport"/>, the same shape as
    /// <see cref="SecretsScan"/>.
    /// </remarks>
    Target OsvScan => _ => _
        .Executes(() =>
        {
            const string tool = "osv-scanner";
            if (!IsOnPath(tool))
            {
                var decision = ScannerGateSupport.OnMissingBinary(
                    tool, SecurityScannersRequired,
                    "https://github.com/google/osv-scanner/releases");
                ScannerGateSupport.RecordSkipped(SecurityScanStatusDirectory, tool, "binary not on PATH");
                if (decision.Fail)
                    throw new InvalidOperationException($"OsvScan: {decision.Message}");
                Console.WriteLine($"OsvScan: {decision.Message}");
                return;
            }

            Directory.CreateDirectory(SecurityDirectory);
            var reportPath = SecurityDirectory / "osv-scanner-report.json";
            ProcessTasks.StartProcess(
                    tool,
                    $"scan source --recursive --format json --output-file \"{reportPath}\" \"{RootDirectory}\"")
                .AssertZeroExitCode();
            ScannerGateSupport.RecordRanClean(SecurityScanStatusDirectory, tool);
        });

    /// <summary>
    /// NuGet-native cross-check for <see cref="OsvScan"/>:
    /// <c>dotnet list package --vulnerable --include-transitive</c> against
    /// every project in the solution. Needs network (queries nuget.org), so
    /// it sits in the same schedule/web tier.
    /// </summary>
    /// <remarks>
    /// The command always exits 0, even with findings, so the gate reads its
    /// own report text for the CLI's "has the following vulnerable packages"
    /// phrasing rather than trusting the exit code.
    /// </remarks>
    Target VulnerablePackages => _ => _
        .Executes(() =>
        {
            var output = DotNet($"list \"{Solution.Path}\" package --vulnerable --include-transitive")
                .Select(line => line.Text)
                .ToList();

            if (output.Any(line => line.Contains("has the following vulnerable packages", StringComparison.Ordinal)))
                throw new InvalidOperationException(
                    "dotnet list package --vulnerable found advisory(ies) in the output above. Upgrade " +
                    "the affected package where a fixed version exists, or record the deferral where " +
                    "your team tracks work, with a reason (test-only, no fixed release, breaking).");
        });

    // ── Coverage ──────────────────────────────────────────────────────────────

    Target Coverage => _ => _
        .DependsOn(Compile)
        .Executes(() =>
        {
            CheckCoverageAccounting();

            if (Directory.Exists(CoverageDirectory))
                Directory.Delete(CoverageDirectory, true);
            Directory.CreateDirectory(CoverageDirectory);
            Directory.CreateDirectory(TestResultsDirectory);

            var failures = new List<string>();
            foreach (var project in TestProjects)
            {
                var dir = (AbsolutePath)Path.Combine(CoverageDirectory, project.Name);
                Directory.CreateDirectory(dir);

                var settings = new DotNetTestSettings()
                    .SetProjectFile(project.Path)
                    .SetConfiguration(Configuration)
                    .EnableNoBuild()
                    .SetFilter(SpawnsBuildFilter)
                    .SetResultsDirectory(TestResultsDirectory)
                    .SetLoggers($"trx;LogFileName={project.Name}.coverage.trx")
                    .SetProperty("CollectCoverage", "true")
                    .SetProperty("CoverletOutputFormat", "cobertura")
                    .SetProperty("CoverletOutput", dir / "coverage");

                if (CoverageAccounting.Thresholds.TryGetValue(project.Name, out var gate))
                {
                    settings = settings
                        .SetProperty(
                            "Threshold",
                            gate.Threshold.ToString(
                                System.Globalization.CultureInfo
                                    .InvariantCulture))
                        .SetProperty("ThresholdType", "line");
                    if (gate.Include is not null)
                        settings = settings.SetProperty("Include", gate.Include);
                }

                try
                {
                    DotNetTest(settings);
                }
                catch (Exception ex)
                {
                    // Same fix as Test above, same reason: a project under its coverage threshold
                    // (or with a failing test) must not stop every alphabetically-later project
                    // from ever producing its .cobertura.xml — SummarizeCoverage()/CoverageReport
                    // would otherwise silently total fewer projects than TestProjects actually has.
                    failures.Add(project.Name);
                    Console.WriteLine($"Coverage: {project.Name} failed ({ex.Message}) — continuing with the remaining projects");
                }
            }

            SummarizeCoverage();

            if (failures.Count > 0)
            {
                throw new Exception(
                    $"Coverage: {failures.Count} of {TestProjects.Length} project(s) failed their "
                    + $"tests or coverage threshold: {string.Join(", ", failures)}. Every project "
                    + $"still ran — see {CoverageDirectory} for each one's report.");
            }
        });

    /// <summary>
    /// Fails fast, before running a single test, when a project
    /// <see cref="TestProjects"/> would sweep is present in neither
    /// <see cref="CoverageAccounting.Thresholds"/> nor
    /// <see cref="CoverageAccounting.Exemptions"/> — default-deny, so adding
    /// a test project forces a decision instead of inheriting an ungated
    /// run. Also run standalone as <see cref="CoverageAccountingCheck"/>,
    /// for a cheap check that needs no compiled output.
    /// </summary>
    void CheckCoverageAccounting()
    {
        var names = TestProjects.Select(p => p.Name).ToArray();
        var doubly = CoverageAccounting.DoublyAccounted(
            CoverageAccounting.Thresholds.Keys.ToList(), CoverageAccounting.Exemptions.Keys.ToList());
        if (doubly.Count > 0)
        {
            throw new InvalidOperationException(
                $"Coverage accounting: {string.Join(", ", doubly)} appear in BOTH "
                + "CoverageAccounting.Thresholds and CoverageAccounting.Exemptions — a project is "
                + "either gated or exempted, never both. Remove whichever row is the mistake.");
        }

        var unaccounted = CoverageAccounting.Unaccounted(
            names, CoverageAccounting.Thresholds.Keys.ToList(), CoverageAccounting.Exemptions.Keys.ToList());
        if (unaccounted.Count > 0)
        {
            throw new InvalidOperationException(
                $"Coverage accounting: {unaccounted.Count} test project(s) are in neither "
                + $"CoverageAccounting.Thresholds nor CoverageAccounting.Exemptions: "
                + $"{string.Join(", ", unaccounted)}. Add a gate (threshold + the Include filter "
                + "naming the product assembly it owns) or an exemption (a written reason) in "
                + "build/CoverageAccounting.cs — coverage must never run silently ungated.");
        }
    }

    /// <summary>
    /// Standalone entry point for <see cref="CheckCoverageAccounting"/> — no
    /// <see cref="Compile"/> dependency, so it is cheap enough to run on its
    /// own rather than only as a side effect of the full <see cref="Coverage"/>
    /// sweep.
    /// </summary>
    Target CoverageAccountingCheck => _ => _
        .Executes(() =>
        {
            CheckCoverageAccounting();
            Console.WriteLine(
                $"Coverage accounting check passed: {TestProjects.Length} test project(s), "
                + $"{CoverageAccounting.Thresholds.Count} gated, "
                + $"{CoverageAccounting.Exemptions.Count} exempted.");
        });

    Target CoverageReport => _ => _
        .DependsOn(Coverage)
        .Executes(() =>
        {
            var reports = Directory.EnumerateFiles(
                CoverageDirectory, "*.cobertura.xml", SearchOption.AllDirectories).ToList();
            var rows = new List<(int Missed, int Covered, double Rate, string Name)>();

            foreach (var report in reports)
                ExtractClassCoverage(report, rows);

            var ordered = rows.OrderByDescending(x => x.Missed).ToList();
            var reportFile = CoverageDirectory / "coverage-report.txt";
            using var writer = new StreamWriter(reportFile);
            writer.WriteLine("PER-CLASS COVERAGE (sorted by missed lines descending)");
            writer.WriteLine(new string('=', 80));
            writer.WriteLine($"{"Missed",8}  {"Covered",8}  {"Rate%",6}  Class");
            writer.WriteLine(new string('-', 80));
            foreach (var r in ordered)
                writer.WriteLine($"{r.Missed,8}  {r.Covered,8}  {r.Rate,5:F1}%  {r.Name}");
            writer.WriteLine();
            writer.WriteLine($"Total classes: {ordered.Count}");
            writer.WriteLine($"Total missed: {ordered.Sum(x => x.Missed)}");
            writer.WriteLine($"Total covered: {ordered.Sum(x => x.Covered)}");

            Console.WriteLine($"Coverage report written: {reportFile}");
        });

    // ── Mutation ──────────────────────────────────────────────────────────────

    [Parameter("Mutation/VerifyAll: comma-separated stryker-config module names to SKIP (e.g. " +
        "\"core\") — a validation-run time-box only. The default (omitted) runs every discovered " +
        "stryker-config*.json, which is what ./build.sh Mutation and ./build.sh VerifyAll do with " +
        "no flags.")]
    readonly string? MutationExclude;

    Target Mutation => _ => _
        .DependsOn(Compile)
        .Executes(() =>
        {
            CheckMutationAccounting();

            if (Directory.Exists(MutationDirectory))
                Directory.Delete(MutationDirectory, true);
            Directory.CreateDirectory(MutationDirectory);

            DotNet("tool restore");

            var excluded = (MutationExclude ?? "")
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var configs = MutationAccounting.ConfigFiles(RootDirectory)
                .Where(config => !excluded.Contains(ExtractStrykerModuleName(config)))
                .ToList();

            // Every module runs regardless of an earlier one's own outcome — collected below,
            // never thrown mid-loop. The original shape (DotNet(...), which asserts a zero exit
            // code) threw on the FIRST module whose mutation score missed its break threshold,
            // aborting the loop before later modules' StrykerOutput was ever moved into
            // MutationDirectory — silently dropping their reports, the exact defect class Java's
            // own verifyAll first run exposed (2 of 5 modules missing). Found writing VerifyAll,
            // which reads every module's report and would otherwise have inherited the gap.
            var failures = new List<string>();
            foreach (var config in configs)
            {
                var moduleName = ExtractStrykerModuleName(config);
                var strykerOutput = (AbsolutePath)(RootDirectory / "StrykerOutput");
                if (Directory.Exists(strykerOutput))
                    Directory.Delete(strykerOutput, true);

                // Stryker auto-detects the solution file when exactly one .sln/.slnx sits at the
                // repo root; -s pins it explicitly instead, because that assumption breaks the
                // moment NarrativeTrace.Format.sln (WriteFormatSolution's gitignored scratch copy,
                // left behind by any prior Format/FormatCheck run — an everyday ambient state, not
                // a contrived one) is still on disk: Stryker then refuses with "found more than
                // one" and every module fails to mutate. Found running Mutation right after Verify.
                var process = ProcessTasks.StartProcess(
                    "dotnet",
                    $"tool run dotnet-stryker -- --config-file \"{config}\" --solution \"{Solution.Path}\"",
                    RootDirectory);
                process.AssertWaitForExit();
                if (process.ExitCode != 0)
                    failures.Add($"{moduleName} (exit {process.ExitCode})");

                if (Directory.Exists(strykerOutput))
                {
                    var dest = (AbsolutePath)(MutationDirectory / moduleName);
                    if (Directory.Exists(dest))
                        Directory.Delete(dest, true);
                    Directory.Move(strykerOutput, dest);
                }
                else
                {
                    Console.WriteLine(
                        $"Mutation: {moduleName} produced no StrykerOutput — its report will be "
                        + $"absent from {MutationDirectory}");
                }
            }

            if (failures.Count > 0)
            {
                throw new Exception(
                    $"Mutation: {failures.Count} of {configs.Count} module(s) missed their break "
                    + $"threshold or crashed: {string.Join(", ", failures)}. Every module still ran "
                    + $"— see {MutationDirectory} for each one's report.");
            }
        });

    /// <summary>
    /// Fails fast, before Stryker runs at all, when a project in the solution is present in none —
    /// or more than one — of <see cref="MutationAccounting"/>'s three buckets (mutation-tested,
    /// test suite, exempted). Default-deny, the inverse of what a project absent from every bucket
    /// used to get: nothing enforced or reported the gap. Also run standalone as
    /// <see cref="MutationAccountingCheck"/>, cheap enough (name/config checks only, no Stryker) to
    /// ride <see cref="Verify"/> every commit even though the real mutation sweep never does.
    /// </summary>
    void CheckMutationAccounting()
    {
        var names = Solution.AllProjects.Select(p => p.Name).ToArray();
        var tested = MutationAccounting.TestedProjectNames(RootDirectory).ToList();
        var exempted = MutationAccounting.Exemptions.Keys.ToList();

        var doubly = MutationAccounting.DoublyAccounted(names, tested, exempted);
        if (doubly.Count > 0)
        {
            throw new InvalidOperationException(
                $"Mutation accounting: {string.Join(", ", doubly)} land in more than one of "
                + "mutation-tested / test-suite / MutationAccounting.Exemptions — a project is "
                + "exactly one of those. Remove whichever classification is stale.");
        }

        var unaccounted = MutationAccounting.Unaccounted(names, tested, exempted);
        if (unaccounted.Count > 0)
        {
            throw new InvalidOperationException(
                $"Mutation accounting: {unaccounted.Count} project(s) are mutation-tested by "
                + $"nothing, no test-suite name pattern, and not exempted: "
                + $"{string.Join(", ", unaccounted)}. Add a stryker-config targeting it, or an "
                + "exemption (a written reason) in build/MutationAccounting.cs — mutation coverage "
                + "must never grow silently ungated.");
        }
    }

    /// <summary>
    /// Standalone entry point for <see cref="CheckMutationAccounting"/> — no
    /// <see cref="Compile"/> dependency, so it is cheap enough to run on its own, and it is what
    /// rides <see cref="Verify"/>: config/name checks only, it never invokes Stryker.
    /// </summary>
    Target MutationAccountingCheck => _ => _
        .Executes(() =>
        {
            CheckMutationAccounting();
            var tested = MutationAccounting.TestedProjectNames(RootDirectory);
            Console.WriteLine(
                $"Mutation accounting check passed: {Solution.AllProjects.Count()} project(s), "
                + $"{tested.Count} mutation-tested, {MutationAccounting.Exemptions.Count} exempted, "
                + "the rest test suites.");
        });

    // ── Metrics ───────────────────────────────────────────────────────────────

    /// <remarks>
    /// Ordered after <see cref="Clean"/> because it writes under
    /// <c>artifacts/</c>, which Clean deletes wholesale: scheduled first, as
    /// NUKE is free to do, it ran the gate and then had its report thrown away.
    /// </remarks>
    Target MetricsReport => _ => _
        .After(Clean)
        .Executes(() =>
        {
            if (Directory.Exists(MetricsDirectory))
                Directory.Delete(MetricsDirectory, true);
            Directory.CreateDirectory(MetricsDirectory);

            var sourceFiles = Directory.EnumerateFiles(RootDirectory / "src", "*.cs", SearchOption.AllDirectories)
                .Where(x => !x.Contains("/bin/") && !x.Contains("/obj/"))
                .ToList();

            var fileRows = new List<(int Ncss, int Methods, string File)>();
            var methodRows = new List<(int Ncss, string Class, string Method, string File, int Line)>();

            foreach (var file in sourceFiles)
            {
                var lines = File.ReadAllLines(file);
                var rel = Path.GetRelativePath(RootDirectory, file);
                fileRows.Add((CountNcss(lines), CountMethods(lines), rel));
                methodRows.AddRange(ExtractMethodMetrics(lines, rel));
            }

            WriteFileMetrics(fileRows);
            WriteMethodMetrics(methodRows);
        });

    /// <remarks>
    /// Ordered after <see cref="MetricsReport"/>, which empties the metrics
    /// directory the two of them share — running first cost this report its
    /// file. That also puts it after <see cref="Clean"/>, transitively.
    /// </remarks>
    Target CouplingReport => _ => _
        .After(MetricsReport)
        .Executes(() =>
        {
            Directory.CreateDirectory(MetricsDirectory);

            var sourceFiles = Directory.EnumerateFiles(RootDirectory / "src", "*.cs", SearchOption.AllDirectories)
                .Where(x => !x.Contains("/bin/") && !x.Contains("/obj/"))
                .ToList();

            var data = CollectNamespaceData(sourceFiles);
            WriteCouplingReport(data);
        });

    // ── Benchmarks ────────────────────────────────────────────────────────────

    /// <summary>
    /// Runs the benchmark suite and gates it against the baseline.
    /// </summary>
    /// <remarks>
    /// Ordered <em>after</em> every other <see cref="Verify"/> stage. NUKE is
    /// free to schedule an unordered target first, and it scheduled this one
    /// first: a benchmark failure — the stage most sensitive to the host, and
    /// the slowest by far — left format, analysis, tests and coverage all
    /// reporting <c>NotRun</c>, so the gate said nothing about the code. These
    /// are ordering constraints, not dependencies, so <c>./build.sh Benchmark</c>
    /// on its own still runs nothing but the benchmarks.
    /// </remarks>
    Target Benchmark => _ => _
        .After(Clean)
        .After(FormatCheck)
        .After(Analyze)
        .After(TranslationCheck)
        .After(Test)
        .After(Coverage)
        .After(DependencyAudit)
        .After(MetricsReport)
        .After(CouplingReport)
        .Executes(() =>
        {
            PrepareBenchmarkArtifacts();
            RunBenchmarks();
            var results = BenchmarkGate.LoadResults(BenchmarkArtifactsDir);
            if (File.Exists(BenchmarkBaselineFile))
                BenchmarkGate.CheckRegressions(results, BenchmarkBaselineFile);
            else
                Console.WriteLine("No benchmark baseline found. Run: ./build.sh BenchmarkBaseline");
        });

    Target BenchmarkBaseline => _ => _
        .Executes(() =>
        {
            PrepareBenchmarkArtifacts();
            RunBenchmarks();
            BenchmarkGate.SaveBaseline(
                BenchmarkGate.LoadResults(BenchmarkArtifactsDir), BenchmarkBaselineFile);
        });

    // ── Tier B fuzzing (security-testing.md) ────────────────────────────────────

    /// <summary>
    /// Coverage-guided fuzzing (Tier B): instruments the top two targets from the security-testing
    /// document's list (the value renderer, JSON emission) with SharpFuzz and drives them under
    /// <c>afl-fuzz</c> for <see cref="FuzzSeconds"/> each.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Deliberately <b>not</b> a dependency of <see cref="Verify"/> — it is the scheduled/manual CI
    /// job's target, the same shape as <see cref="Benchmark"/>'s relationship to the gate. Tier A
    /// (the FsCheck properties in <c>NarrativeTrace.SecurityTests</c>) is what runs on every commit.
    /// </para>
    /// <para>
    /// <b>@edgeCase</b> Two independent tools are needed and this target verifies each honestly
    /// rather than assuming both: <c>sharpfuzz</c> (a .NET global/local tool, IL-rewrites the
    /// published <c>NarrativeTrace.Core.dll</c> to report coverage the way AFL expects — pure .NET,
    /// restored via <c>dotnet tool restore</c>, works in any container with NuGet access) and
    /// <c>afl-fuzz</c>, provisioned in <c>.devcontainer/Dockerfile</c> (Ubuntu's <c>afl++</c>
    /// package — earlier absence was a stale/never-updated apt cache, not an unpackaged platform;
    /// <c>apt-get update</c> as root surfaces it fine on this container's arm64 host). Confirmed on
    /// the container this runtime was built in: <c>sharpfuzz</c> instrumentation succeeds (the
    /// published DLL grows from ~186 KB to ~312 KB — real IL added, verified by byte comparison).
    /// </para>
    /// <para>
    /// <b>@edgeCase</b> Once <c>afl-fuzz</c> is on <c>PATH</c>, a second gap surfaces: AFL's own
    /// <c>check_binary()</c> inspects the invoked binary — here, the generic <c>dotnet</c> host, not
    /// the IL-instrumented DLL it loads — for compile-time AFL instrumentation markers, and always
    /// aborts with "No instrumentation detected" because SharpFuzz's coverage signal lives in the
    /// managed IL, communicated to afl-fuzz over the shared-memory forkserver protocol
    /// <c>SharpFuzz.Fuzzer.OutOfProcess.Run</c> implements (see <c>fuzz/NarrativeTrace.Fuzz/Program.cs</c>),
    /// never in the native host binary. This is SharpFuzz's own documented shape, not a defect in this
    /// harness: its README's own afl-fuzz invocation sets <c>AFL_SKIP_BIN_CHECK=1</c> for exactly this
    /// reason, so this target sets it too. Proven for real against the renderer target in this
    /// container (20-second run, default corpus): AFL's own <c>fuzzer_stats</c> reported
    /// <c>execs_done: 187372</c>, <c>execs_per_sec: 9363.92</c>, <c>edges_found: 76</c>,
    /// <c>bitmap_cvg: 0.12%</c> — a real coverage-guided loop actually executing the instrumented
    /// target at thousands of runs per second, not a binary-check no-op.
    /// </para>
    /// <para>
    /// This target still performs the instrumentation half unconditionally, and only attempts the
    /// actual fuzzing loop when it finds a driver on <c>PATH</c> — logging a clear, actionable
    /// message and returning (not failing) otherwise, for any environment that provisions this image
    /// without its <c>afl++</c> layer.
    /// </para>
    /// <para>
    /// Each target gets its own instrumented copy in <c>artifacts/fuzz/&lt;target&gt;/publish/</c> —
    /// never the shared <c>src/*/bin</c> output, which every other gate step reads; instrumenting
    /// that in place was tried once while writing this target and it corrupted the DLL other steps
    /// were mid-read of.
    /// </para>
    /// </remarks>
    Target Fuzz => _ => _
        .DependsOn(Compile)
        .Executes(() =>
        {
            if (Directory.Exists(FuzzDirectory))
                Directory.Delete(FuzzDirectory, true);
            Directory.CreateDirectory(FuzzDirectory);

            DotNet("tool restore");

            var driver = Environment.GetEnvironmentVariable("AFL_FUZZ") ?? "afl-fuzz";
            var driverFound = IsOnPath(driver);

            foreach (var target in new[] { "renderer", "json" })
                RunFuzzTarget(target, driver, driverFound);

            if (!driverFound)
            {
                Console.WriteLine(
                    $"afl-fuzz not found on PATH: instrumented both targets under {FuzzDirectory}, "
                    + "but did not run the coverage-guided loop. Install AFL++ (or point AFL_FUZZ at "
                    + "another driver) to run it for real.");
            }
        });

    static bool IsOnPath(string executable)
    {
        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
        return pathEnv.Split(Path.PathSeparator)
            .Where(dir => !string.IsNullOrWhiteSpace(dir))
            .Select(dir => Path.Combine(dir, executable))
            .Any(File.Exists);
    }

    void RunFuzzTarget(string target, string driver, bool driverFound)
    {
        var targetDir = FuzzDirectory / target;
        var publishDir = targetDir / "publish";
        DotNet($"publish \"{FuzzProject}\" -c Release -o \"{publishDir}\"");

        var coreDll = publishDir / "NarrativeTrace.Core.dll";
        var before = new FileInfo(coreDll).Length;
        DotNet($"tool run sharpfuzz \"{coreDll}\"");
        var after = new FileInfo(coreDll).Length;
        Console.WriteLine($"[fuzz:{target}] instrumented NarrativeTrace.Core.dll ({before} -> {after} bytes)");

        if (!driverFound)
            return;

        var inDir = targetDir / "in";
        var outDir = targetDir / "out";
        Directory.CreateDirectory(inDir);
        foreach (var seed in Directory.EnumerateFiles(FuzzSeeds))
            File.Copy(seed, inDir / Path.GetFileName(seed), overwrite: true);

        var harness = publishDir / "NarrativeTrace.Fuzz.dll";
        Environment.SetEnvironmentVariable("AFL_SKIP_CPUFREQ", "1");
        // SharpFuzz's coverage signal is IL rewritten into the target DLL, carried to afl-fuzz over
        // its shared-memory forkserver protocol (SharpFuzz.Fuzzer.OutOfProcess) — never compiled
        // into the `dotnet` host binary afl-fuzz actually invokes. Without this, AFL's own
        // check_binary() aborts every run with "No instrumentation detected" before a single
        // execution happens; SharpFuzz's own README sets this for the identical reason.
        Environment.SetEnvironmentVariable("AFL_SKIP_BIN_CHECK", "1");
        ProcessTasks.StartProcess(
                driver,
                $"-V {FuzzSeconds} -i \"{inDir}\" -o \"{outDir}\" -- dotnet \"{harness}\" {target} @@")
            .AssertZeroExitCode();

        ReportAndVerifyAflRun(target, outDir);
    }

    /// <summary>
    /// Reads afl-fuzz's own <c>fuzzer_stats</c> after a run and prints the metrics that prove (or
    /// disprove) it actually fuzzed — <c>execs_done</c>/<c>execs_per_sec</c>/<c>edges_found</c>/
    /// <c>bitmap_cvg</c> — instead of trusting a zero exit code alone. A fuzz job that returns
    /// almost instantly against a multi-minute budget is not proof it fuzzed, just proof it exited
    /// zero; this fails the build loudly on zero executions instead of reporting success, per the
    /// governing rule that a job which did not really run must say so.
    /// </summary>
    static void ReportAndVerifyAflRun(string target, AbsolutePath outDir)
    {
        var statsFile = outDir / "default" / "fuzzer_stats";
        if (!File.Exists(statsFile))
            throw new Exception($"[fuzz:{target}] afl-fuzz exited cleanly but wrote no fuzzer_stats — treat as not having fuzzed.");

        var stats = File.ReadAllLines(statsFile)
            .Select(line => line.Split(':', 2))
            .Where(parts => parts.Length == 2)
            .ToDictionary(parts => parts[0].Trim(), parts => parts[1].Trim());

        var execs = long.Parse(
            stats.GetValueOrDefault("execs_done", "0"), System.Globalization.CultureInfo.InvariantCulture);
        Console.WriteLine(
            $"[fuzz:{target}] afl-fuzz ran {execs} executions ({stats.GetValueOrDefault("execs_per_sec", "?")}/s), "
            + $"edges {stats.GetValueOrDefault("edges_found", "?")}/{stats.GetValueOrDefault("total_edges", "?")} "
            + $"({stats.GetValueOrDefault("bitmap_cvg", "?")} coverage), "
            + $"{stats.GetValueOrDefault("saved_crashes", "0")} crashes, {stats.GetValueOrDefault("saved_hangs", "0")} hangs.");

        if (execs == 0)
            throw new Exception($"[fuzz:{target}] afl-fuzz reported 0 executions — instrumentation ran but the fuzzing loop never did.");
    }

    // ── Stress (concurrency invariants shared across runtimes) ──────────────────

    /// <summary>
    /// Long randomized sweep of <c>NarrativeTrace.StressTests</c> — the same
    /// jcstress-invariant races the <c>Test</c>/<c>Verify</c> sweeps already
    /// run in short, bounded mode (every <c>[Fact]</c> there reads its
    /// repetition count from <c>StressIterations</c>, which defaults small).
    /// This target re-runs the identical project with
    /// <c>NARRATIVETRACE_STRESS_ITERATIONS</c> raised to
    /// <see cref="StressSweepIterations"/>.
    /// </summary>
    /// <remarks>
    /// Deliberately <b>not</b> a dependency of <see cref="Verify"/> — the same
    /// split as <see cref="Mutation"/> and <see cref="Fuzz"/>: a short,
    /// deterministic slice runs on every commit, the long sweep runs on the
    /// scheduled/manual job, because racing the same interleaving hundreds of
    /// thousands of times is minutes, not seconds.
    /// </remarks>
    Target Stress => _ => _
        .DependsOn(Compile)
        .Executes(() =>
        {
            Directory.CreateDirectory(TestResultsDirectory);
            var project = Solution.AllProjects
                .First(x => x.Name == "NarrativeTrace.StressTests");
            Environment.SetEnvironmentVariable(
                "NARRATIVETRACE_STRESS_ITERATIONS",
                StressSweepIterations.ToString(System.Globalization.CultureInfo.InvariantCulture));
            DotNetTest(s => s
                .SetProjectFile(project.Path)
                .SetConfiguration(Configuration)
                .EnableNoBuild()
                .SetResultsDirectory(TestResultsDirectory)
                .SetLoggers($"trx;LogFileName={project.Name}.sweep.trx"));
        });

    // ── Packaging ─────────────────────────────────────────────────────────────

    Target Pack => _ => _
        .DependsOn(Test)
        .Executes(() =>
        {
            DotNetPack(s => s
                .SetProject(Solution)
                .SetConfiguration(Configuration)
                .SetOutputDirectory(ArtifactsDirectory)
                .EnableNoBuild());
        });

    // ── Post-publish verification ────────────────────────────────────────────

    /// <summary>
    /// Proves a nuget.org release actually landed, from OUTSIDE the pipeline that built it — the
    /// same two checks java's <c>scripts/verify-publication.sh</c> makes, native to this build
    /// rather than shelled out: (1) every package the solution's own projects say they publish is
    /// present at the given version, and no package a project explicitly does NOT publish is
    /// present anyway; (2) a consumer smoke test that scaffolds this repository's own documented
    /// first-10-minutes recipe into a temp directory, with a cold isolated NuGet package cache and
    /// a <c>NuGet.config</c> pinning nuget.org as the only source, and asserts the traced test
    /// actually wrote its trace file with the expected content. "It resolves and compiles" is
    /// explicitly not the bar.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Never a per-commit or nightly-gating target — it makes real network calls against a
    /// registry whose own sync is documented as minutes to hours, so a run that fails this minute
    /// can legitimately pass the next. <c>.github/workflows/verify-publication.yml</c> runs it as
    /// a non-gating scheduled canary, the same shape as java's workflow of the same name.
    /// </para>
    /// <para>
    /// <see cref="LocalRehearsal"/> checks the local feed under <see cref="ArtifactsDirectory"/>
    /// (populated by <see cref="Pack"/>) and resolves the smoke test from it instead of the real
    /// nuget.org — rehearse the recipe before a release exists anywhere public. Never the
    /// default: a rehearsal that silently became the real check would prove nothing.
    /// </para>
    /// </remarks>
    Target VerifyPublication => _ => _
        .Executes(() =>
        {
            var version = string.IsNullOrWhiteSpace(VerifyVersion) ? ReadVersionPrefix() : VerifyVersion!;
            var manifest = PublicationVerificationSupport.BuildManifest(DerivePackageProjects());
            PrintPublicationManifest(manifest, version);

            if (VerifyDryRun)
            {
                PrintPublicationDryRun(manifest, version);
                return;
            }

            var presentResults = PollExpectedPresent(manifest.ExpectedOnRegistry, version);
            var absentResults = CheckExpectedAbsent(manifest.ExpectedOffRegistry, version);
            var smoke = RunConsumerSmokeTest(version);

            var problems = new List<string>();
            problems.AddRange(manifest.Warnings.Select(w => $"WARNING (structural): {w}"));
            problems.AddRange(presentResults
                .Where(r => r.Verdict != PublicationVerificationSupport.Presence.Present)
                .Select(r => $"MISSING expected package: {r.PackageId} {version} — {r.Verdict}"));
            problems.AddRange(absentResults
                .Where(r => r.Verdict == PublicationVerificationSupport.Presence.Present)
                .Select(r => $"UNEXPECTED package present: {r.PackageId} {version} is live on the registry, "
                    + "but its project is not packable in this build. Either it should never have been "
                    + "published, or this build's packability no longer matches what already shipped."));

            Console.WriteLine();
            Console.WriteLine("=== VerifyPublication report ===");
            if (problems.Count == 0)
                Console.WriteLine("  (no problems)");
            foreach (var problem in problems)
                Console.WriteLine($"  {problem}");
            Console.WriteLine($"Consumer smoke test: {smoke.Verdict}{(smoke.Detail is null ? "" : $" ({smoke.Detail})")}");

            var hardFailures = problems.Count(p => !p.StartsWith("WARNING", StringComparison.Ordinal));
            if (hardFailures > 0 || smoke.Verdict != SmokeVerdict.Passed)
            {
                throw new Exception(
                    $"VerifyPublication failed: {hardFailures} registry problem(s), "
                    + $"smoke test {smoke.Verdict}. See the report above.");
            }
        });

    const int VerifyPublicationInitialBackoffSeconds = 15;
    static readonly HttpClient VerifyPublicationHttp = new() { Timeout = TimeSpan.FromSeconds(30) };

    enum SmokeVerdict { Passed, Failed }

    sealed record PackageCheckResult(string PackageId, PublicationVerificationSupport.Presence Verdict);

    sealed record SmokeResult(SmokeVerdict Verdict, string? Detail);

    string ReadVersionPrefix()
    {
        var text = File.ReadAllText(RootDirectory / "Directory.Build.props");
        var match = Regex.Match(text, "<VersionPrefix>([^<]+)</VersionPrefix>");
        if (!match.Success)
        {
            throw new InvalidOperationException(
                "Directory.Build.props has no <VersionPrefix> — pass --verify-version explicitly.");
        }
        return match.Groups[1].Value;
    }

    /// <summary>
    /// Asks the build, never a hand-kept list: evaluates every solution project's real
    /// <c>IsPackable</c>/<c>PackageId</c> via <c>dotnet msbuild -getProperty</c> — the same
    /// evaluation <see cref="Pack"/>'s <c>dotnet pack</c> itself performs — so a newly added
    /// project can never be silently missed either way.
    /// </summary>
    List<PublicationVerificationSupport.ProjectPackability> DerivePackageProjects()
    {
        var results = new List<PublicationVerificationSupport.ProjectPackability>();
        foreach (var project in Solution.AllProjects.OrderBy(p => p.Name, StringComparer.Ordinal))
        {
            var (isPackable, packageId) = EvaluatePackability(project.Path);
            var relative = Path.GetRelativePath(RootDirectory, project.Path).Replace('\\', '/');
            results.Add(new(relative, packageId, isPackable));
        }
        return results;
    }

    static (bool IsPackable, string PackageId) EvaluatePackability(string projectPath)
    {
        var joined = string.Join(
            '\n',
            DotNet($"msbuild \"{projectPath}\" -nologo -getProperty:IsPackable -getProperty:PackageId")
                .Select(o => o.Text));
        var braceIndex = joined.IndexOf('{');
        if (braceIndex < 0)
        {
            throw new InvalidOperationException(
                $"dotnet msbuild -getProperty produced no JSON for {projectPath}: {joined}");
        }
        using var document = JsonDocument.Parse(joined[braceIndex..]);
        var properties = document.RootElement.GetProperty("Properties");
        var isPackable = !string.Equals(
            properties.TryGetProperty("IsPackable", out var packableValue) ? packableValue.GetString() : "true",
            "false", StringComparison.OrdinalIgnoreCase);
        var packageId = properties.TryGetProperty("PackageId", out var idValue) ? idValue.GetString() : null;
        return (isPackable, packageId ?? Path.GetFileNameWithoutExtension(projectPath));
    }

    void PrintPublicationManifest(PublicationVerificationSupport.PublicationManifest manifest, string version)
    {
        Console.WriteLine(
            $"VerifyPublication: derived {manifest.ExpectedOnRegistry.Count} packable project(s) for version "
            + $"{version} ({manifest.ExpectedOffRegistry.Count} non-packable project(s) in the solution).");
        foreach (var project in manifest.ExpectedOnRegistry)
            Console.WriteLine($"  PACKABLE  {project.PackageId,-40} {project.RelativeProjectPath}");
        foreach (var warning in manifest.Warnings)
            Console.WriteLine($"  WARNING   {warning}");
    }

    void PrintPublicationDryRun(PublicationVerificationSupport.PublicationManifest manifest, string version)
    {
        Console.WriteLine();
        Console.WriteLine("Dry run — no network calls, no smoke test. Would check for PRESENCE:");
        foreach (var project in manifest.ExpectedOnRegistry)
        {
            Console.WriteLine($"  {PublicationVerificationSupport.NupkgUrl(PublicationVerificationSupport.NuGetOrgFlatContainerBase, project.PackageId, version)}");
            Console.WriteLine($"  {PublicationVerificationSupport.NuspecUrl(PublicationVerificationSupport.NuGetOrgFlatContainerBase, project.PackageId, version)}");
        }
        Console.WriteLine("Would check for ABSENCE (a non-packable project's package must not exist):");
        foreach (var project in manifest.ExpectedOffRegistry)
            Console.WriteLine($"  {project.PackageId}");
        Console.WriteLine();
        Console.WriteLine(
            $"Smoke test would restore NarrativeTrace.Core/Runtime/Proxy/Testing.Xunit {version} from "
            + (LocalRehearsal ? $"the local feed at {ArtifactsDirectory}." : "nuget.org (the only configured source)."));
    }

    static int HeadStatus(string url)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Head, url);
            using var response = VerifyPublicationHttp.Send(request, HttpCompletionOption.ResponseHeadersRead);
            return (int)response.StatusCode;
        }
        catch
        {
            // No response at all (DNS/connect/timeout failure) reads the same as curl's own "000"
            // fallback in the java script: not explained by ordinary sync lag, so MISSING.
            return 0;
        }
    }

    PublicationVerificationSupport.Presence RemoteCheckOne(string packageId, string version) =>
        PublicationVerificationSupport.Worst(new[]
        {
            PublicationVerificationSupport.ClassifyHttpStatus(HeadStatus(
                PublicationVerificationSupport.NupkgUrl(PublicationVerificationSupport.NuGetOrgFlatContainerBase, packageId, version))),
            PublicationVerificationSupport.ClassifyHttpStatus(HeadStatus(
                PublicationVerificationSupport.NuspecUrl(PublicationVerificationSupport.NuGetOrgFlatContainerBase, packageId, version))),
        });

    PublicationVerificationSupport.Presence LocalCheckOne(string packageId, string version)
    {
        var found = Directory.Exists(ArtifactsDirectory)
            && Directory.EnumerateFiles(ArtifactsDirectory, "*.nupkg")
                .Any(file => string.Equals(
                    Path.GetFileNameWithoutExtension(file), $"{packageId}.{version}",
                    StringComparison.OrdinalIgnoreCase));
        return found ? PublicationVerificationSupport.Presence.Present : PublicationVerificationSupport.Presence.Missing;
    }

    PublicationVerificationSupport.Presence CheckOne(string packageId, string version) =>
        LocalRehearsal ? LocalCheckOne(packageId, version) : RemoteCheckOne(packageId, version);

    /// <summary>
    /// Polls every expected package until each reads PRESENT, backing off between rounds up to
    /// one overall <see cref="VerifyTimeoutSeconds"/> deadline — not a separate timeout per
    /// package, the same reasoning java's script documents for Central.
    /// </summary>
    List<PackageCheckResult> PollExpectedPresent(
        IReadOnlyList<PublicationVerificationSupport.ProjectPackability> expected, string version)
    {
        var status = expected.ToDictionary(
            p => p.PackageId, _ => PublicationVerificationSupport.Presence.Missing);
        var deadline = DateTimeOffset.UtcNow.AddSeconds(VerifyTimeoutSeconds);
        var backoff = VerifyPublicationInitialBackoffSeconds;
        while (true)
        {
            var pending = new List<string>();
            foreach (var project in expected)
            {
                if (status[project.PackageId] == PublicationVerificationSupport.Presence.Present)
                    continue;
                var verdict = CheckOne(project.PackageId, version);
                status[project.PackageId] = verdict;
                if (verdict != PublicationVerificationSupport.Presence.Present)
                    pending.Add($"{project.PackageId}({verdict})");
            }
            if (pending.Count == 0 || LocalRehearsal || DateTimeOffset.UtcNow >= deadline)
            {
                return status.Select(kv => new PackageCheckResult(kv.Key, kv.Value)).ToList();
            }
            Console.WriteLine($">> not yet propagated, {pending.Count} pending, retrying in {backoff}s: {string.Join(' ', pending)}");
            Thread.Sleep(TimeSpan.FromSeconds(backoff));
            backoff = PublicationVerificationSupport.NextBackoffSeconds(backoff, VerifyIntervalSeconds);
        }
    }

    /// <summary>
    /// Checks once, no polling, that every non-packable project's package is NOT live — the
    /// negative half of the same derivation, and the exact check that would have caught
    /// <c>NarrativeTrace.Benchmarks</c> shipping in 0.1.0.
    /// </summary>
    List<PackageCheckResult> CheckExpectedAbsent(
        IReadOnlyList<PublicationVerificationSupport.ProjectPackability> notExpected, string version) =>
        notExpected
            .Select(project => new PackageCheckResult(project.PackageId, CheckOne(project.PackageId, version)))
            .ToList();

    /// <summary>
    /// Scaffolds <c>documentation/first-10-minutes.md</c>'s exact recipe into a fresh temp
    /// directory — a cold isolated <c>NUGET_PACKAGES</c>, a <c>NuGet.config</c> pinning nuget.org
    /// (or, under <see cref="LocalRehearsal"/>, the local feed) as the only source — and asserts
    /// the traced test wrote its trace artifact with the expected content. Resolving and
    /// compiling is not the bar tested here.
    /// </summary>
    SmokeResult RunConsumerSmokeTest(string version)
    {
        var workDir = Directory.CreateTempSubdirectory("nt-verify-publication-").FullName;
        var packagesDir = Directory.CreateTempSubdirectory("nt-verify-publication-packages-").FullName;
        var httpCacheDir = Directory.CreateTempSubdirectory("nt-verify-publication-httpcache-").FullName;
        try
        {
            WriteSmokeProject(workDir, version);
            var outputDir = Path.Combine(workDir, "narrativetrace-output");
            var env = OverrideEnvironment(
                ("NUGET_PACKAGES", packagesDir),
                ("NUGET_HTTP_CACHE_PATH", httpCacheDir),
                ("NARRATIVETRACE_OUTPUT", "true"),
                ("NARRATIVETRACE_OUTPUT_DIR", outputDir),
                ("DOTNET_NOLOGO", "1"),
                ("DOTNET_CLI_TELEMETRY_OPTOUT", "1"));

            var process = ProcessTasks.StartProcess(
                "dotnet", "test --nologo",
                workingDirectory: workDir,
                environmentVariables: env,
                logOutput: false);
            process.AssertWaitForExit();

            if (process.ExitCode != 0)
            {
                var logPath = Path.Combine(workDir, "dotnet-test-output.log");
                File.WriteAllLines(logPath, process.Output.Select(o => o.Text));
                return new SmokeResult(SmokeVerdict.Failed, $"dotnet test exited {process.ExitCode} — see {logPath} (work dir kept)");
            }

            var traceFile = Path.Combine(outputDir, "traces", "OrderServiceTests", "places_an_order.md");
            if (!File.Exists(traceFile))
                return new SmokeResult(SmokeVerdict.Failed, $"dotnet test passed but no trace file at {traceFile} (work dir kept: {workDir})");

            var content = File.ReadAllText(traceFile);
            var missingFragments = new[] { "PlaceOrder", "cust-1", "book-123" }
                .Where(fragment => !content.Contains(fragment, StringComparison.Ordinal))
                .ToList();
            if (missingFragments.Count > 0)
            {
                return new SmokeResult(
                    SmokeVerdict.Failed,
                    $"trace file at {traceFile} is missing expected content: {string.Join(", ", missingFragments)} (work dir kept)");
            }
            if (content.Contains("gift wrap", StringComparison.Ordinal))
            {
                return new SmokeResult(
                    SmokeVerdict.Failed,
                    $"trace file at {traceFile} leaked the [NotTraced] argument instead of redacting it (work dir kept: {workDir})");
            }

            var detail = $"trace file present with expected content: {traceFile}";
            Directory.Delete(workDir, recursive: true);
            Directory.Delete(packagesDir, recursive: true);
            Directory.Delete(httpCacheDir, recursive: true);
            return new SmokeResult(SmokeVerdict.Passed, detail);
        }
        catch (Exception ex)
        {
            return new SmokeResult(SmokeVerdict.Failed, $"{ex.GetType().Name}: {ex.Message} (work dir: {workDir})");
        }
    }

    void WriteSmokeProject(string workDir, string version)
    {
        File.WriteAllText(
            Path.Combine(workDir, "NuGet.config"),
            LocalRehearsal ? LocalRehearsalNuGetConfig() : RealRegistryNuGetConfig());

        File.WriteAllText(Path.Combine(workDir, "nt-verify-publication-smoke.csproj"), $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
                <IsPackable>false</IsPackable>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="NarrativeTrace.Core" Version="{version}" />
                <PackageReference Include="NarrativeTrace.Runtime" Version="{version}" />
                <PackageReference Include="NarrativeTrace.Proxy" Version="{version}" />
                <PackageReference Include="NarrativeTrace.Testing.Xunit" Version="{version}" />
                <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
                <PackageReference Include="xunit" Version="2.9.2" />
                <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2">
                  <PrivateAssets>all</PrivateAssets>
                  <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
                </PackageReference>
              </ItemGroup>
            </Project>
            """);

        // Verbatim from documentation/first-10-minutes.md steps 2 and 3 — this smoke test IS that
        // documented recipe, run for real against whatever the target version actually published.
        File.WriteAllText(Path.Combine(workDir, "OrderService.cs"), SmokeOrderServiceSource);
        File.WriteAllText(Path.Combine(workDir, "OrderServiceTests.cs"), SmokeOrderServiceTestsSource);
    }

    /// <summary>
    /// Real verification: nuget.org is the only source, exactly like a consumer's own
    /// restore — every dependency of the smoke project, NarrativeTrace's own packages
    /// included, comes from there.
    /// </summary>
    static string RealRegistryNuGetConfig() => """
        <?xml version="1.0" encoding="utf-8"?>
        <configuration>
          <packageSources>
            <clear />
            <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
          </packageSources>
        </configuration>
        """;

    /// <summary>
    /// Rehearsal: the smoke project also restores ordinary third-party test-host
    /// packages (<c>Microsoft.NET.Test.Sdk</c>, <c>xunit</c>, its VS runner) that the
    /// local <see cref="ArtifactsDirectory"/> feed never carries — a bare local-only
    /// source left those NU1101-ing with no source for them (found running this exact
    /// target: the cold, isolated <c>NUGET_PACKAGES</c> this smoke test always uses had
    /// nothing to fall back to). Keeping nuget.org as a second source without package
    /// source mapping would let a same-numbered <c>NarrativeTrace.*</c> package already
    /// on nuget.org quietly satisfy the restore instead of the local build under test —
    /// exactly the failure mode a rehearsal exists to rule out. Mapping pins
    /// <c>NarrativeTrace.*</c> to <c>local</c> only and leaves every other id free to
    /// resolve from nuget.org as usual.
    /// </summary>
    string LocalRehearsalNuGetConfig() => $"""
        <?xml version="1.0" encoding="utf-8"?>
        <configuration>
          <packageSources>
            <clear />
            <add key="local" value="{ArtifactsDirectory}" />
            <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
          </packageSources>
          <packageSourceMapping>
            <packageSource key="local">
              <package pattern="NarrativeTrace.*" />
            </packageSource>
            <packageSource key="nuget.org">
              <package pattern="*" />
            </packageSource>
          </packageSourceMapping>
        </configuration>
        """;

    const string SmokeOrderServiceSource = """
        using NarrativeTrace.Core.Annotation;

        public interface IOrderService
        {
            string PlaceOrder(
                string customerId, string productId, int quantity,
                [NotTraced] string internalNote);
        }

        public sealed class OrderService : IOrderService
        {
            public string PlaceOrder(
                string customerId, string productId, int quantity, string internalNote)
                => $"confirmed:{customerId}:{productId}:{quantity}";
        }
        """;

    const string SmokeOrderServiceTestsSource = """
        using NarrativeTrace.Proxy;
        using NarrativeTrace.TestingXunit;
        using Xunit;

        public sealed class OrderServiceTests : IClassFixture<NarrativeFixture>
        {
            private readonly NarrativeFixture _fixture;
            public OrderServiceTests(NarrativeFixture fixture) => _fixture = fixture;

            [Fact]
            public void Places_an_order()
            {
                _fixture.Run(nameof(Places_an_order), ctx =>
                {
                    var orders = NarrativeTraceProxy.Create<IOrderService>(new OrderService(), ctx);
                    orders.PlaceOrder("cust-1", "book-123", 2, "gift wrap");
                });

                _fixture.WriteArtifacts(nameof(OrderServiceTests), nameof(Places_an_order), failed: false);
            }
        }
        """;

    /// <summary>
    /// The current process environment plus <paramref name="overrides"/>. NUKE's
    /// <c>ProcessTasks.StartProcess</c> <b>clears</b> the child's environment entirely whenever
    /// <c>environmentVariables</c> is non-null, rather than merging — so every call site that
    /// needs to override one or two variables must start from a full copy of this process's own
    /// environment (PATH, HOME, etc.) or the child process cannot even find <c>dotnet</c>.
    /// </summary>
    static IReadOnlyDictionary<string, string> OverrideEnvironment(params (string Key, string Value)[] overrides)
    {
        var env = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (System.Collections.DictionaryEntry entry in Environment.GetEnvironmentVariables())
            env[(string)entry.Key] = (string)entry.Value!;
        foreach (var (key, value) in overrides)
            env[key] = value;
        return env;
    }

    // ── VerifyAll ─────────────────────────────────────────────────────────────
    //
    // pro repo TODO §35E: "one command that runs everything and reports numbers." Mirrors the
    // family's golden Java implementation (`./gradlew verifyAll`) and TypeScript's `verify:all` —
    // see reports/verification/SCHEMA.md for the cross-port contract this target's output commits
    // to (field names, the four statuses, the 21 category ids). Runs EVERY verification this repo
    // has, gate and heavy alike, as a sequence of fresh `./build.sh <Target>` subprocesses — never
    // this repo's own already-built targets called in-process, for the same reason Java's
    // `runGradleSubprocess` never calls its Gradle tasks directly: a category's own failure (an
    // exception thrown deep inside, say, Analyze) must not abort the whole run, and NUKE's
    // in-process target graph has no `--continue` equivalent. A category's status always comes
    // from what actually happened (the subprocess's real exit code, a report the tool itself
    // wrote) — never invented, zeroed, or estimated; a metric this port's tooling cannot honestly
    // produce is left out of that row's `metrics` with a `note` explaining why, matching SCHEMA.md's
    // own rule.
    //
    // Reads FIVE existing real invocations twice each, at zero extra cost, rather than re-running
    // them: `Analyze` (lint + complexity + half of sast), the one repo-wide `Test` sweep (unit-tests
    // + property + fuzz-tier-a + the slices of architecture/conformance/stress-short), and
    // `Benchmark`'s own BenchmarkDotNet run (benchmarks + allocation, since MemoryDiagnoser already
    // captures both mean time and bytes-allocated-per-operation together — unlike JMH, which needs
    // a second GC-profiler pass for the Java port).

    Target VerifyAll => _ => _
        .Executes(() =>
        {
            Directory.CreateDirectory(VerifyAllLogDirectory);
            var startedAt = DateTimeOffset.UtcNow;
            var results = new List<CategoryResult>();

            void AddRow(CategoryResult row)
            {
                results.Add(row);
                var elapsed = (DateTimeOffset.UtcNow - startedAt).TotalSeconds;
                Console.WriteLine(
                    $"[{elapsed,5:F0}s elapsed] {row.Category,-14} {row.Status,-15} "
                    + $"({row.DurationSeconds,8:F1}s)  {row.Note}");
            }

            Console.WriteLine(new string('=', 100));
            Console.WriteLine("VerifyAll: running every verification this repo has, gate and heavy alike.");
            Console.WriteLine("This is LONG-RUNNING BY DESIGN (mutation across every Stryker module, the");
            Console.WriteLine("budgeted fuzz-tier-b sweep, and the long stress sweep are each historically");
            Console.WriteLine("many minutes on this project's own dev container). A category's failure never");
            Console.WriteLine("aborts the run — see reports/verification/SCHEMA.md for how to read the report.");
            Console.WriteLine(new string('=', 100));

            // Each phase is its own safety boundary: an exception this code did not anticipate
            // (a parser choking on a tool's future output shape, a report file that never
            // materializes) must never take the whole run down with it — RunSafely logs it and
            // moves on, and the reconciliation pass below fills in a failed row for whatever
            // category(ies) that phase never got to add, so the report this run writes is always
            // complete even when something inside one phase broke.
            RunSafely("static/security", () => VerifyAllStaticAndSecurityRows(AddRow));
            RunSafely("test sweep", () => VerifyAllTestSweepRows(AddRow));
            RunSafely("coverage", () => VerifyAllCoverageRow(AddRow));
            RunSafely("mutation", () => VerifyAllMutationRow(AddRow));
            RunSafely("fuzz-tier-b", () => VerifyAllFuzzTierBRow(AddRow));
            RunSafely("benchmarks/allocation", () => VerifyAllBenchmarkRows(AddRow));
            RunSafely("stress-long", () => VerifyAllStressLongRow(AddRow));

            foreach (var category in VerificationSchema.Categories)
            {
                if (results.Any(r => r.Category == category))
                    continue;
                AddRow(new CategoryResult(
                    category, "none", "failed", new Dictionary<string, object>(), 0.0,
                    "this category's own phase threw before adding it — see the console output above"));
            }

            var run = new VerificationRun(
                "dotnet", ReadVersionPrefix(), VerifyAllExec.ShortCommit(RootDirectory),
                VerifyAllExec.HostDescriptor(), startedAt, DateTimeOffset.UtcNow, results);
            var dateStr = startedAt.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
            var jsonFile = VerificationReportsDirectory / $"{dateStr}.json";
            var mdFile = VerificationReportsDirectory / $"{dateStr}.md";
            VerificationReportSupport.WriteJson(run, jsonFile);
            File.WriteAllText(mdFile, VerificationReportSupport.RenderMarkdown(jsonFile));

            Console.WriteLine();
            Console.WriteLine(VerificationReportSupport.RenderMarkdown(jsonFile));
            Console.WriteLine($"VerifyAll: wrote {jsonFile} and {mdFile}");

            var failedCount = results.Count(r => r.Status == "failed");
            if (failedCount > 0)
            {
                throw new Exception(
                    $"VerifyAll: {failedCount} of {results.Count} categories failed — see {mdFile} "
                    + "for the full table");
            }
        });

    CommandOutcome RunLogged(string category, string target, params string[] extraArgs) =>
        VerifyAllExec.Run(RootDirectory, category, VerifyAllLogDirectory, [target, .. extraArgs]);

    /// <summary>Runs one VerifyAll phase, swallowing any exception it did not itself handle — the
    /// reconciliation pass in the <see cref="VerifyAll"/> target body fills a <c>failed</c> row for
    /// whatever category(ies) that phase never got to add, so one broken phase costs only its own
    /// rows, never the rest of the run or the report this run writes.</summary>
    static void RunSafely(string phaseName, Action phase)
    {
        try
        {
            phase();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"VerifyAll: the {phaseName} phase threw and was skipped: {ex}");
        }
    }

    static CategoryResult NotImplementedRow(string category, string note) =>
        new(category, "none", "not-implemented", new Dictionary<string, object>(), 0.0, note);

    // ---------------------------------------------------------- format / lint / complexity / sast / secrets / sca / translation / types / clarity

    void VerifyAllStaticAndSecurityRows(Action<CategoryResult> addRow)
    {
        var formatOutcome = RunLogged("format", "FormatCheck");
        addRow(BuildFormatRow(formatOutcome));

        var analyzeOutcome = RunLogged("lint", "Analyze");
        var diagnostics = AnalyzeDiagnosticsSupport.ParseDiagnostics(analyzeOutcome.Output);
        addRow(BuildLintRow(analyzeOutcome, diagnostics));
        addRow(BuildComplexityRow(diagnostics));

        string[] securityRequiredArgs = SecurityRequired ? ["--security-required"] : [];

        var semgrepOutcome = RunLogged("sast", "Semgrep", securityRequiredArgs);
        addRow(BuildSastRow(semgrepOutcome, diagnostics));

        var secretsOutcome = RunLogged("secrets", "SecretsScan", securityRequiredArgs);
        addRow(BuildSecretsRow(secretsOutcome));

        var auditOutcome = RunLogged("sca-audit", "DependencyAudit");
        var osvOutcome = RunLogged("sca-osv", "OsvScan", securityRequiredArgs);
        var vulnOutcome = RunLogged("sca-vulnerable", "VulnerablePackages");
        addRow(BuildScaRow(auditOutcome, osvOutcome, vulnOutcome));

        var translationOutcome = RunLogged("translation", "TranslationCheck");
        addRow(BuildTranslationRow(translationOutcome));

        addRow(NotImplementedRow(
            "types",
            "the C# compiler's own nullable-reference-type checking and generic type inference run "
            + "on every build; no standalone type-checking tool (a mypy/tsc analog) is wired for "
            + ".NET — matches the java precedent (javac only)"));
        addRow(NotImplementedRow(
            "clarity",
            "no in-house clarity/naming-quality self-gate exists for this repo's own source (ts/"
            + "python/swift have one; java and .NET do not). NarrativeTrace.Clarity is a product "
            + "feature this library offers CONSUMERS, not a self-check on this repo — matches the "
            + "java precedent exactly"));
    }

    CategoryResult BuildFormatRow(CommandOutcome outcome)
    {
        var status = outcome.ExitCode == 0 ? "passed" : "failed";
        var note = status == "passed"
            ? null
            : "FormatCheck reported formatting violations; dotnet format's console output carries "
                + "no structured violation count to parse";
        return new CategoryResult(
            "format", "dotnet format 10.0.301 (whitespace + style)", status,
            new Dictionary<string, object>(), outcome.Seconds, VerifyAllExec.WithLogHint(note, outcome, status));
    }

    CategoryResult BuildLintRow(CommandOutcome outcome, IReadOnlyList<(string Severity, string RuleId)> diagnostics)
    {
        var findings = AnalyzeDiagnosticsSupport.CountLintFindings(diagnostics);
        var status = AnalyzeDiagnosticsSupport.HasLintErrors(diagnostics) ? "failed" : "passed";
        return new CategoryResult(
            "lint", "Roslyn analyzers + SonarAnalyzer.CSharp 10.20.0 (non-security, non-S138 rules)",
            status, new Dictionary<string, object> { ["findings"] = findings }, outcome.Seconds,
            VerifyAllExec.WithLogHint(null, outcome, status));
    }

    static CategoryResult BuildComplexityRow(IReadOnlyList<(string Severity, string RuleId)> diagnostics)
    {
        var findings = AnalyzeDiagnosticsSupport.CountComplexityFindings(diagnostics);
        var status = findings > 0 ? "failed" : "passed";
        return new CategoryResult(
            "complexity", "SonarAnalyzer S138 (.editorconfig, hard gate ≤20 lines/method)", status,
            new Dictionary<string, object> { ["findings"] = findings }, 0.0,
            "derived from the same Analyze run as the lint row above (0s: no separate invocation)");
    }

    CategoryResult BuildSastRow(CommandOutcome semgrepOutcome, IReadOnlyList<(string Severity, string RuleId)> analyzeDiagnostics)
    {
        var caFindings = AnalyzeDiagnosticsSupport.CountSecurityFindings(analyzeDiagnostics);
        var semgrepScannerStatus = ScannerGateSupport.Status(SecurityScanStatusDirectory, "semgrep");
        var semgrepSkipped = semgrepScannerStatus.StartsWith("skipped", StringComparison.Ordinal);
        var semgrepFindings = SecurityReportParsing.CountSemgrepFindings(SecurityDirectory / "semgrep-csharp.json");

        // Composite row (SCHEMA.md "Composite categories"): Semgrep is the tool that actually GATES
        // (`--error`, real invocation) — CA5xxx (AnalysisModeSecurity=All) only warns today (see
        // documentation/security-tooling.md's CA-security section), so it is folded in as a
        // metric/note, not a second vote on status, UNLESS Semgrep itself was skipped, in which
        // case CA5xxx's own findings are the only real signal left and decide the row.
        string status;
        string note;
        if (!semgrepSkipped)
        {
            status = semgrepOutcome.ExitCode == 0 ? "passed" : "failed";
            note = $"Semgrep p/csharp is the gate tool; CA5xxx findings={caFindings} (warn-only today, "
                + "not gating — see documentation/security-tooling.md)";
        }
        else
        {
            status = caFindings > 0 ? "failed" : "passed";
            note = $"Semgrep status: {semgrepScannerStatus}; falling back to CA5xxx findings={caFindings} "
                + "as the only tool that ran";
        }

        var metrics = new Dictionary<string, object> { ["findings"] = semgrepFindings ?? caFindings };
        if (semgrepFindings is not null)
            metrics["findings_ca5xxx"] = caFindings;

        return new CategoryResult(
            "sast", "Semgrep 1.176.0 (p/csharp) + Roslyn CA5xxx (AnalysisModeSecurity=All, warn-only)",
            status, metrics, semgrepOutcome.Seconds, VerifyAllExec.WithLogHint(note, semgrepOutcome, status));
    }

    CategoryResult BuildSecretsRow(CommandOutcome outcome)
    {
        var scannerStatus = ScannerGateSupport.Status(SecurityScanStatusDirectory, "gitleaks");
        var skipped = scannerStatus.StartsWith("skipped", StringComparison.Ordinal);
        var status = skipped ? "skipped" : outcome.ExitCode == 0 ? "passed" : "failed";
        var findings = SecurityReportParsing.CountGitleaksFindings(SecurityDirectory / "gitleaks-report.json");
        var metrics = findings is null
            ? new Dictionary<string, object>()
            : new Dictionary<string, object> { ["findings"] = findings };
        var note = skipped ? $"SecretsScan status: {scannerStatus}" : null;
        return new CategoryResult(
            "secrets", "gitleaks 8.30.1 (full git history)", status, metrics, outcome.Seconds,
            VerifyAllExec.WithLogHint(note, outcome, status));
    }

    CategoryResult BuildScaRow(CommandOutcome auditOutcome, CommandOutcome osvOutcome, CommandOutcome vulnOutcome)
    {
        var osvScannerStatus = ScannerGateSupport.Status(SecurityScanStatusDirectory, "osv-scanner");
        var osvSkipped = osvScannerStatus.StartsWith("skipped", StringComparison.Ordinal);
        var osvFindings = SecurityReportParsing.CountOsvFindings(SecurityDirectory / "osv-scanner-report.json");

        var failed = auditOutcome.ExitCode != 0 || (!osvSkipped && osvOutcome.ExitCode != 0) || vulnOutcome.ExitCode != 0;
        var status = failed ? "failed" : "passed";
        var metrics = osvFindings is null
            ? new Dictionary<string, object>()
            : new Dictionary<string, object> { ["findings"] = osvFindings };
        var note = "DependencyAudit (NuGet audit, NuGetAuditMode=all) exit=" + auditOutcome.ExitCode + "; "
            + (osvSkipped ? $"OsvScan status: {osvScannerStatus}" : $"OsvScan (osv.dev) exit={osvOutcome.ExitCode}")
            + $"; VulnerablePackages (dotnet list package --vulnerable) exit={vulnOutcome.ExitCode}";
        var seconds = auditOutcome.Seconds + osvOutcome.Seconds + vulnOutcome.Seconds;

        return new CategoryResult(
            "sca", "OSV-Scanner 2.5.1 (source scan) + NuGet audit (NuGetAuditMode=all) + dotnet list "
                + "package --vulnerable",
            status, metrics, seconds, VerifyAllExec.WithLogHint(note, status, auditOutcome, osvOutcome, vulnOutcome));
    }

    static CategoryResult BuildTranslationRow(CommandOutcome outcome)
    {
        var status = outcome.ExitCode == 0 ? "passed" : "failed";
        return new CategoryResult(
            "translation", "custom TranslationCheck (blob-hash headers + i18n manifest)", status,
            new Dictionary<string, object>(), outcome.Seconds, VerifyAllExec.WithLogHint(null, outcome, status));
    }

    // ------------------------------------------------------------------------------- unit-tests / property / fuzz-tier-a / architecture / conformance / stress-short

    /// <summary>
    /// One repo-wide <c>./build.sh Test</c> sweep, sliced six ways — the .NET analog of Java's
    /// single <c>./gradlew test</c> feeding <c>unit-tests</c>, <c>property</c>, <c>fuzz-tier-a</c>,
    /// half of <c>architecture</c> and <c>conformance</c>, and <c>stress-short</c> from one run.
    /// </summary>
    void VerifyAllTestSweepRows(Action<CategoryResult> addRow)
    {
        var testOutcome = RunLogged("unit-tests", "Test");
        var byProject = TrxSupport.ReadByProject(TestResultsDirectory, TestProjects.Select(p => p.Name));
        var allResults = VerifyAllTestSlices.AllResults(byProject);

        var unitTestsStatus = testOutcome.ExitCode == 0 ? "passed" : "failed";
        addRow(new CategoryResult(
            "unit-tests", "xUnit 2.9.2 (dotnet test, every *.Tests project in NarrativeTrace.sln)",
            unitTestsStatus, TrxSupport.Summarize(allResults), testOutcome.Seconds,
            VerifyAllExec.WithLogHint(
                "excludes Category=SpawnsBuild (BuildScript.Tests' own nested-build tests — see conformance)",
                testOutcome, unitTestsStatus)));

        var propertyClasses = PropertyTestScanner.FindPropertyClasses(RootDirectory / "tests");
        var propertyResults = VerifyAllTestSlices.Property(byProject, propertyClasses);
        addRow(DerivedTestRow(
            "property", "FsCheck 3.3.2 (FsCheck.Xunit)", propertyResults, testOutcome,
            "sliced from the unit-tests row's own ./build.sh Test run: every [Property]-attributed "
            + $"class repo-wide ({propertyClasses.Count} total), excluding NarrativeTrace.SecurityTests "
            + "(claimed whole by fuzz-tier-a below) — 0 additional invocations"));

        var fuzzTierAResults = VerifyAllTestSlices.FuzzTierA(byProject);
        addRow(DerivedTestRow(
            "fuzz-tier-a", "FsCheck 3.3.2 (hostile-corpus properties + corpus-replay tests, "
                + "NarrativeTrace.SecurityTests)",
            fuzzTierAResults, testOutcome,
            "sliced from the same test run: the whole NarrativeTrace.SecurityTests project, which "
            + "replays the shared hostile corpus every commit per this repo's own documented Tier A "
            + "(see documentation/security-testing.md) — 0 additional invocations"));

        var couplingOutcome = RunLogged("architecture", "CouplingReport");
        addRow(BuildArchitectureRow(VerifyAllTestSlices.ArchitectureSlice(byProject), couplingOutcome));

        addRow(DerivedTestRow(
            "conformance", "custom canonical-schema/trace-identity conformance tests "
                + "(NarrativeTrace.Core.Tests) + BuildScript.Tests (validates build behavior itself)",
            VerifyAllTestSlices.ConformanceSlice(byProject), testOutcome,
            "sliced from the unit-tests row's own run: the schema/identity conformance classes in "
            + "NarrativeTrace.Core.Tests, plus the whole of BuildScript.Tests — deliberately NOT the "
            + "separate BuildScriptTests NUKE target (its Category=SpawnsBuild tests shell out to a "
            + "nested ./build.sh, which cannot run as a VerifyAll subprocess: NUKE's own engine holds "
            + ".nuke/temp/build.log open for VerifyAll's whole run, so a nested invocation can never "
            + "acquire that same path — confirmed reproducing on an unmodified checkout) — "
            + "0 additional invocations"));

        addRow(DerivedTestRow(
            "stress-short", "xUnit (NarrativeTrace.StressTests, bounded NARRATIVETRACE_STRESS_ITERATIONS "
                + "default — see stress-long for the raised sweep)",
            VerifyAllTestSlices.StressShort(byProject), testOutcome,
            "sliced from the unit-tests row's own run — 0 additional invocations"));
    }

    static CategoryResult DerivedTestRow(
        string category, string tool, IReadOnlyList<TrxTestResult> results, CommandOutcome parentOutcome, string note)
    {
        var status = TrxSupport.AllGreen(results) ? "passed" : "failed";
        return new CategoryResult(
            category, tool, status, TrxSupport.Summarize(results), TrxSupport.TotalSeconds(results),
            VerifyAllExec.WithLogHint(note, parentOutcome, status));
    }

    static CategoryResult BuildArchitectureRow(IReadOnlyList<TrxTestResult> archSlice, CommandOutcome couplingOutcome)
    {
        var testsGreen = TrxSupport.AllGreen(archSlice);
        var couplingPassed = couplingOutcome.ExitCode == 0;
        var status = testsGreen && couplingPassed ? "passed" : "failed";
        return new CategoryResult(
            "architecture", "NetArchTest.Rules 1.3.2 (layered-dependency + no-cycle rules) + "
                + "CouplingReport (Ca/Ce/D ≤ 0.8)",
            status, TrxSupport.Summarize(archSlice), TrxSupport.TotalSeconds(archSlice) + couplingOutcome.Seconds,
            VerifyAllExec.WithLogHint(
                "NetArchTest tests are sliced from the unit-tests run; CouplingReport (namespace "
                + "coupling/distance) re-run separately, the .NET analog of JDepend",
                couplingOutcome, status));
    }

    // ----------------------------------------------------------------------------------------- coverage

    void VerifyAllCoverageRow(Action<CategoryResult> addRow)
    {
        var outcome = RunLogged("coverage", "Coverage");
        var status = outcome.ExitCode == 0 ? "passed" : "failed";
        var (coveredPct, linesCovered, linesMissed) = ReadCoverageSummary();
        var metrics = new Dictionary<string, object>
        {
            ["coverage_pct"] = coveredPct,
            ["lines_covered"] = linesCovered,
            ["lines_missed"] = linesMissed,
        };
        addRow(new CategoryResult(
            "coverage", "Coverlet 6.0.2 (cobertura, per-project thresholds — see CoverageAccounting.Thresholds)",
            status, metrics, outcome.Seconds, VerifyAllExec.WithLogHint(null, outcome, status)));
    }

    /// <summary>
    /// Aggregates coverage per DISTINCT production class, not per report — <c>Coverage</c>'s own
    /// <c>coverage-summary.txt</c> (a naive sum of every report's own lines-covered/lines-valid)
    /// looked wrong when this row first ran for real (35.97%, against every individual project's
    /// own gate passing at 90%+): coverlet instruments every assembly a test process happens to
    /// load, so a class incidentally pulled in by an unrelated test project's run is counted AGAIN
    /// in that report at whatever low coverage that unrelated run happened to exercise, inflating
    /// the denominator far more than the numerator. Reusing <see cref="ExtractClassCoverage"/>
    /// (the same per-class reader <c>CoverageReport</c> already trusts) and keeping only the
    /// highest-covered report per class name removes that double count: a class's real coverage is
    /// whatever its OWN owning test project's run achieved, not whatever an incidental bystander run
    /// happened to touch.
    /// </summary>
    (double CoveragePct, long LinesCovered, long LinesMissed) ReadCoverageSummary()
    {
        var reports = Directory.Exists(CoverageDirectory)
            ? Directory.EnumerateFiles(CoverageDirectory, "*.cobertura.xml", SearchOption.AllDirectories).ToList()
            : [];
        var rows = new List<(int Missed, int Covered, double Rate, string Name)>();
        foreach (var report in reports)
            ExtractClassCoverage(report, rows);

        var bestPerClass = rows
            .GroupBy(r => r.Name)
            .Select(g => g.OrderByDescending(r => r.Covered).First());

        long covered = 0;
        long missed = 0;
        foreach (var row in bestPerClass)
        {
            covered += row.Covered;
            missed += row.Missed;
        }
        var pct = covered + missed == 0 ? 0.0 : (double)covered / (covered + missed) * 100.0;
        return (pct, covered, missed);
    }

    // ----------------------------------------------------------------------------------------- mutation

    void VerifyAllMutationRow(Action<CategoryResult> addRow)
    {
        string[] excludeArgs = string.IsNullOrWhiteSpace(MutationExclude)
            ? []
            : ["--mutation-exclude", MutationExclude!];
        var outcome = RunLogged("mutation", "Mutation", excludeArgs);

        var configs = MutationAccounting.ConfigFiles(RootDirectory);
        var excludedModules = (MutationExclude ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var reportPaths = configs
            .Select(ExtractStrykerModuleName)
            .Where(module => !excludedModules.Contains(module))
            .ToDictionary(module => module, module => (string)(MutationDirectory / module / "reports" / "mutation-report.json"));

        var totals = StrykerReportSupport.ReadAll(reportPaths, out var missingModules);
        // A module producing no report at all is always a failure signal for this row, independent
        // of Mutation's own exit code — the exact silent-drop shape Java's own first run exposed.
        var status = outcome.ExitCode == 0 && missingModules.Count == 0 ? "passed" : "failed";

        var metrics = new Dictionary<string, object>
        {
            ["mutants_killed"] = totals.Killed,
            ["mutants_survived"] = totals.Survived,
            ["mutants_no_coverage"] = totals.NoCoverage,
            ["mutation_score"] = totals.Score,
        };

        var noteParts = new List<string>();
        if (excludedModules.Count > 0)
        {
            noteParts.Add(
                $"time-boxed for THIS run via --mutation-exclude={MutationExclude} (skips "
                + $"{string.Join(", ", excludedModules)}); the default ./build.sh VerifyAll invocation "
                + "passes nothing and runs Stryker's real sweep across every module");
        }
        if (missingModules.Count > 0)
            noteParts.Add($"no report produced for: {string.Join(", ", missingModules)}");
        var note = noteParts.Count == 0 ? null : string.Join("; ", noteParts);

        addRow(new CategoryResult(
            "mutation", $"Stryker.NET 4.16.0 ({reportPaths.Count} of {configs.Count} module(s); "
                + "per-module thresholds.break — see stryker-config*.json)",
            status, metrics, outcome.Seconds, VerifyAllExec.WithLogHint(note, outcome, status)));
    }

    // -------------------------------------------------------------------------------------- fuzz-tier-b

    private static readonly System.Text.RegularExpressions.Regex FuzzExecsPattern = new(
        @"afl-fuzz ran (\d+) executions", System.Text.RegularExpressions.RegexOptions.Compiled);
    private static readonly System.Text.RegularExpressions.Regex FuzzTargetPattern = new(
        @"\[fuzz:(\S+)\] afl-fuzz ran", System.Text.RegularExpressions.RegexOptions.Compiled);
    private static readonly System.Text.RegularExpressions.Regex FuzzSkipPattern = new(
        @"afl-fuzz not found on PATH", System.Text.RegularExpressions.RegexOptions.Compiled);

    void VerifyAllFuzzTierBRow(Action<CategoryResult> addRow)
    {
        var outcome = RunLogged(
            "fuzz-tier-b", "Fuzz", "--fuzz-seconds",
            FuzzSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture));
        var skipped = FuzzSkipPattern.IsMatch(outcome.Output);
        var executions = FuzzExecsPattern.Matches(outcome.Output)
            .Select(m => long.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture))
            .Sum();
        var targetsFuzzed = FuzzTargetPattern.Matches(outcome.Output).Select(m => m.Groups[1].Value).Distinct().Count();
        const int targetsTotal = 2; // renderer, json — see Fuzz target's RunFuzzTarget loop

        string status;
        Dictionary<string, object> metrics;
        string? note;
        if (skipped)
        {
            status = "skipped";
            metrics = new Dictionary<string, object>();
            note = "afl-fuzz not on PATH: SharpFuzz instrumentation ran, but the coverage-guided loop did not";
        }
        else
        {
            status = outcome.ExitCode == 0 ? "passed" : "failed";
            metrics = new Dictionary<string, object>
            {
                ["executions"] = executions,
                ["targets_fuzzed"] = targetsFuzzed,
                ["targets_total"] = targetsTotal,
            };
            note = $"executions parsed from ReportAndVerifyAflRun's own \"afl-fuzz ran N executions\" "
                + $"lines, summed across {targetsTotal} targets; FuzzSeconds={FuzzSeconds}s/target (default)";
        }

        addRow(new CategoryResult(
            "fuzz-tier-b", $"SharpFuzz 2.3.0 + AFL++ (afl-fuzz, coverage-guided, {targetsTotal} targets x "
                + $"{FuzzSeconds}s/target)",
            status, metrics, outcome.Seconds, VerifyAllExec.WithLogHint(note, outcome, status)));
    }

    // -------------------------------------------------------------------------------- benchmarks / allocation

    void VerifyAllBenchmarkRows(Action<CategoryResult> addRow)
    {
        var loadAverage = ReadOneMinuteLoadAverage();
        var skipForLoad = !VerifyAllSkipTiming && loadAverage is { } load && load > VerifyAllLoadThreshold;
        var loadDescription = loadAverage is { } l
            ? $"observed 1-minute load average: {l:F2} (threshold {VerifyAllLoadThreshold:F1})"
            : "1-minute load average unavailable on this platform (no /proc/loadavg)";

        if (VerifyAllSkipTiming || skipForLoad)
        {
            var reason = VerifyAllSkipTiming
                ? "--verify-all-skip-timing was passed"
                : "a timing job on a loaded host measures the host, not the code — see the release "
                    + "retrospective's phantom-benchmark-regression rule";
            var note = $"{reason}; {loadDescription}";
            addRow(new CategoryResult(
                "benchmarks", "BenchmarkDotNet 0.14.0 (not run this time)", "skipped",
                new Dictionary<string, object>(), 0.0, note));
            addRow(new CategoryResult(
                "allocation", "BenchmarkDotNet 0.14.0 MemoryDiagnoser (not run this time)", "skipped",
                new Dictionary<string, object>(), 0.0, note));
            return;
        }

        var outcome = RunLogged("benchmarks", "Benchmark");
        var current = BenchmarkGate.LoadResults(BenchmarkArtifactsDir);
        var (checkedCount, timeRegressions, allocRegressions) = File.Exists(BenchmarkBaselineFile)
            ? BenchmarkGate.CountRegressionsByKind(current, BenchmarkBaselineFile)
            : (0, 0, 0);
        var noBaseline = !File.Exists(BenchmarkBaselineFile);

        // Benchmark's own exit code fails on EITHER kind of regression — split back into the two
        // rows here so each reports only its own kind's count, per SCHEMA.md's per-category shape.
        var benchmarksStatus = timeRegressions > 0 ? "failed" : "passed";
        var allocationStatus = allocRegressions > 0 ? "failed" : "passed";
        var benchmarksNote = noBaseline ? "no benchmark baseline found — run ./build.sh BenchmarkBaseline" : loadDescription;
        var allocationNote = noBaseline
            ? "no benchmark baseline found — run ./build.sh BenchmarkBaseline"
            : "derived from the same BenchmarkDotNet MemoryDiagnoser run as the benchmarks row above "
                + "(0s: no additional invocation) — this ecosystem's MemoryDiagnoser captures "
                + "bytes-allocated-per-operation in the same run as mean time, unlike JMH's separate "
                + "GC-profiler pass for the java port";

        addRow(new CategoryResult(
            "benchmarks", "BenchmarkDotNet 0.14.0 vs benchmarks/benchmark-baseline.json (15% time threshold)",
            benchmarksStatus, new Dictionary<string, object> { ["benchmarks_run"] = checkedCount, ["regressions"] = timeRegressions },
            outcome.Seconds, VerifyAllExec.WithLogHint(benchmarksNote, outcome, benchmarksStatus)));
        addRow(new CategoryResult(
            "allocation", "BenchmarkDotNet 0.14.0 MemoryDiagnoser vs benchmarks/benchmark-baseline.json (0% alloc threshold)",
            allocationStatus, new Dictionary<string, object> { ["benchmarks_run"] = checkedCount, ["regressions"] = allocRegressions },
            0.0, allocationNote));
    }

    /// <summary>The container's own 1-minute load average from <c>/proc/loadavg</c> — <c>null</c> on
    /// a platform without one (e.g. running this build directly on macOS/Windows outside the Linux
    /// dev container), never a fabricated number.</summary>
    static double? ReadOneMinuteLoadAverage()
    {
        const string path = "/proc/loadavg";
        if (!File.Exists(path))
            return null;
        var firstField = File.ReadAllText(path).Split(' ', 2)[0];
        return double.TryParse(firstField, System.Globalization.CultureInfo.InvariantCulture, out var load) ? load : null;
    }

    // ------------------------------------------------------------------------------------- stress-long

    void VerifyAllStressLongRow(Action<CategoryResult> addRow)
    {
        var outcome = RunLogged(
            "stress-long", "Stress", "--stress-sweep-iterations",
            StressSweepIterations.ToString(System.Globalization.CultureInfo.InvariantCulture));
        var trxPath = TestResultsDirectory / "NarrativeTrace.StressTests.sweep.trx";
        // Guarded, not a bare TrxSupport.ReadResults call: if the nested Stress invocation failed
        // before dotnet test ever wrote a report (a build break, say), the file genuinely does not
        // exist — that must fall through to a failed row with an empty summary, never throw and
        // take the whole VerifyAll run down with it at the very last row before the report is written.
        var results = File.Exists(trxPath) ? TrxSupport.ReadResults(trxPath) : [];
        var status = outcome.ExitCode == 0 ? "passed" : "failed";
        addRow(new CategoryResult(
            "stress-long", $"xUnit (NarrativeTrace.StressTests, NARRATIVETRACE_STRESS_ITERATIONS="
                + $"{StressSweepIterations})",
            status, TrxSupport.Summarize(results), outcome.Seconds,
            VerifyAllExec.WithLogHint(
                "raised-iteration re-run of the same project stress-short already sliced; "
                + "--stress-sweep-iterations controls the count — unset here means the real default, "
                + "not a time-box override",
                outcome, status)));
    }

    /// <remarks>
    /// <see cref="RunExamples"/> and <see cref="DemoWiringCheck"/> are gate
    /// members, matching Java's <c>check</c> task (which runs its examples and
    /// <c>demoWiringCheck</c>). Both were measured before wiring them in
    /// (2026-09-01, this container): <c>DemoWiringCheck</c> is a source-text
    /// scan with no <see cref="Compile"/> dependency, well under a second;
    /// <c>RunExamples</c> runs all four example apps and finishes in ~2
    /// seconds once <see cref="Compile"/> has already run for <see cref="Test"/>
    /// — neither is the <see cref="Benchmark"/> class of slow/host-sensitive,
    /// so both are ordinary <c>DependsOn</c> members rather than exiled to the
    /// MR/schedule tier. <c>DemoWiringCheck</c> sits beside
    /// <see cref="TranslationCheck"/> (both are cheap, build-free consistency
    /// checks); <c>RunExamples</c> is pinned with <c>.After(Test)</c> so a
    /// failure there cannot preempt <see cref="Test"/> from having already run
    /// and reported — the same attribution concern <see cref="Benchmark"/>'s
    /// ordering comment describes, applied without needing "run dead last"
    /// since neither new member is flaky.
    ///
    /// <see cref="HeaderAbsenceCheck"/> is the same shape again: a cheap,
    /// build-free consistency check, so it sits with
    /// <see cref="TranslationCheck"/> and <see cref="DemoWiringCheck"/>
    /// rather than needing its own ordering constraint.
    ///
    /// <see cref="CoverageAccountingCheck"/> is the same shape once more —
    /// no <see cref="Compile"/> dependency, pure in-memory map lookups — so
    /// it joins the same cluster, ahead of the (slow) <see cref="Coverage"/>
    /// sweep that also runs it as its own first step; failing here fails
    /// fast, before Compile even needs to run for Coverage's sake.
    ///
    /// <see cref="MutationAccountingCheck"/> joins the same build-free cluster for the identical
    /// reason, on the mutation side: it is config/name accounting only (which project is a real
    /// Stryker target, a test suite by name, or exempted with a reason), never a Stryker run, so it
    /// can — and must — ride every commit even though <see cref="Mutation"/> itself (the real
    /// sweep) stays MR/schedule/web-tier, same cadence rule as <see cref="Fuzz"/>/<see cref="Benchmark"/>.
    ///
    /// <see cref="LegalCheck"/> joins the same build-free cluster: in default
    /// (non-strict) mode it only warns, so it cannot turn a green
    /// <see cref="Verify"/> red on its own — the sibling golden repo is
    /// normally absent in CI, and even present, drift is a warning, not a
    /// gate failure, until <c>LEGAL_CHECK_STRICT=1</c> is set.
    ///
    /// <see cref="SecretsScan"/> is offline and fast (git-log mode, no
    /// network) but not build-free — it needs <c>.git</c> intact, not the
    /// compiled output — so it is pinned <c>.After(Clean)</c> the same way
    /// <see cref="MetricsReport"/> is, rather than joining the build-free
    /// cluster above. Static analysis's CA-security rules
    /// (<c>AnalysisModeSecurity</c> in <c>Directory.Build.props</c>) need no
    /// separate target — they ride the existing <see cref="Analyze"/>
    /// dependency. <see cref="Semgrep"/>, <see cref="OsvScan"/> and
    /// <see cref="VulnerablePackages"/> are network-dependent, so — like
    /// <see cref="Mutation"/>, <see cref="Fuzz"/> and <see cref="Benchmark"/>
    /// — none of them are <see cref="Verify"/> members; they run on the
    /// MR/schedule/web CI tier instead (the private CI configuration).
    /// </remarks>
    Target Verify => _ => _
        .DependsOn(Clean)
        .DependsOn(FormatCheck)
        .DependsOn(Analyze)
        .DependsOn(TranslationCheck)
        .DependsOn(DemoWiringCheck)
        .DependsOn(HeaderAbsenceCheck)
        .DependsOn(CoverageAccountingCheck)
        .DependsOn(MutationAccountingCheck)
        .DependsOn(LegalCheck)
        .DependsOn(SecretsScan)
        .DependsOn(Test)
        .DependsOn(RunExamples)
        .DependsOn(Coverage)
        .DependsOn(DependencyAudit)
        .DependsOn(MetricsReport)
        .DependsOn(CouplingReport)
        .DependsOn(Benchmark);

    Target InstallGitHooks => _ => _
        .Executes(() =>
        {
            Git("config core.hooksPath .githooks");
            Console.WriteLine("Configured git hooks path to .githooks");
        });

    // ── Helpers ───────────────────────────────────────────────────────────────

    void PrepareBenchmarkArtifacts()
    {
        if (Directory.Exists(BenchmarkArtifactsDir))
            Directory.Delete(BenchmarkArtifactsDir, true);
        Directory.CreateDirectory(BenchmarkArtifactsDir);
    }

    void RunBenchmarks()
    {
        var project = RootDirectory / "benchmarks" / "NarrativeTrace.Benchmarks" / "NarrativeTrace.Benchmarks.csproj";
        // Medium job (10 warmup / 15 measured iterations): the short job's 3
        // samples cannot hold the 15% regression band on containerized CPU —
        // back-to-back identical runs differed by up to 40%.
        DotNet($"run --project \"{project}\" -c Release -- --filter * --exporters json --job medium --artifacts \"{BenchmarkArtifactsDir}\"");
    }

    static int CountNcss(IEnumerable<string> lines)
    {
        var count = 0;
        var inBlock = false;
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.Length == 0) continue;
            if (inBlock) { if (line.Contains("*/")) inBlock = false; continue; }
            if (line.StartsWith("/*")) { inBlock = !line.Contains("*/"); continue; }
            if (line.StartsWith("//")) continue;
            count++;
        }
        return count;
    }

    static int CountMethods(IEnumerable<string> lines)
    {
        var re = new Regex(
            @"\b(public|private|internal|protected)\s+(static\s+)?[\w<>,\[\]\?]+\s+\w+\s*\([^;]*\)\s*(\{|=>)",
            RegexOptions.Compiled);
        return lines.Count(line => re.IsMatch(line));
    }

    static List<(int Ncss, string Class, string Method, string File, int Line)> ExtractMethodMetrics(
        string[] lines, string relativePath)
    {
        var results = new List<(int, string, string, string, int)>();
        var methodRe = new Regex(
            @"\b(public|private|internal|protected)\s+(static\s+)?(override\s+)?(async\s+)?(virtual\s+)?[\w<>,\[\]\?]+\s+(\w+)\s*\(",
            RegexOptions.Compiled);
        var classRe = new Regex(@"\b(class|struct|record|interface)\s+(\w+)", RegexOptions.Compiled);
        var currentClass = "Unknown";

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            var cm = classRe.Match(line);
            if (cm.Success) currentClass = cm.Groups[2].Value;
            var mm = methodRe.Match(line);
            if (!mm.Success) continue;
            var ncss = CountMethodNcss(lines, i);
            if (ncss > 0)
                results.Add((ncss, currentClass, mm.Groups[6].Value, relativePath, i + 1));
        }

        return results;
    }

    static int CountMethodNcss(string[] lines, int start)
    {
        var depth = 0;
        var found = false;
        var ncss = 0;
        var inBlock = false;

        for (var i = start; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (inBlock) { if (line.Contains("*/")) inBlock = false; continue; }
            if (line.StartsWith("/*")) { inBlock = !line.Contains("*/"); continue; }
            if (line.StartsWith("//") || line.Length == 0) continue;

            if (line.Contains("=>") && !found && !line.Contains("{"))
                return CountExpressionBody(lines, i);

            foreach (var ch in line)
            {
                if (ch == '{') { depth++; found = true; }
                else if (ch == '}') { depth--; if (found && depth == 0) return ncss; }
            }

            if (found) ncss++;
        }

        return ncss;
    }

    static int CountExpressionBody(string[] lines, int start)
    {
        var ncss = 0;
        for (var i = start; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (line.Length == 0 || line.StartsWith("//")) continue;
            ncss++;
            if (line.EndsWith(";")) break;
        }
        return ncss;
    }

    void WriteFileMetrics(List<(int Ncss, int Methods, string File)> rows)
    {
        var ordered = rows.OrderByDescending(x => x.Ncss).ToList();
        var path = MetricsDirectory / "metrics-report.txt";
        using var w = new StreamWriter(path);
        w.WriteLine("SOURCE FILE METRICS");
        w.WriteLine(new string('=', 100));
        w.WriteLine($"{"NCSS",6}  {"Methods",7}  File");
        w.WriteLine(new string('-', 100));
        foreach (var r in ordered)
            w.WriteLine($"{r.Ncss,6}  {r.Methods,7}  {r.File}");
        w.WriteLine();
        w.WriteLine($"Total files: {ordered.Count}");
        w.WriteLine($"Total NCSS: {ordered.Sum(x => x.Ncss)}");
        w.WriteLine($"Total methods: {ordered.Sum(x => x.Methods)}");
        Console.WriteLine($"File metrics report written: {path}");
    }

    void WriteMethodMetrics(
        List<(int Ncss, string Class, string Method, string File, int Line)> rows)
    {
        var ordered = rows.OrderByDescending(x => x.Ncss).ToList();
        var path = MetricsDirectory / "method-metrics-report.txt";
        using var w = new StreamWriter(path);
        w.WriteLine("METHOD METRICS (sorted by NCSS descending)");
        w.WriteLine(new string('=', 100));
        w.WriteLine($"{"NCSS",6}  {"Method",-48}  File:Line");
        w.WriteLine(new string('-', 100));
        foreach (var r in ordered)
            w.WriteLine($"{r.Ncss,6}  {r.Class + "." + r.Method,-48}  {r.File}:{r.Line}");
        w.WriteLine();
        w.WriteLine($"Total methods: {ordered.Count}");
        w.WriteLine($"Max NCSS: {(ordered.Count > 0 ? ordered[0].Ncss : 0)}");
        Console.WriteLine($"Method metrics report written: {path}");
    }

    static string ExtractStrykerModuleName(string configPath)
    {
        var name = Path.GetFileNameWithoutExtension(configPath);
        const string prefix = "stryker-config.";
        return name == "stryker-config" ? "core" :
            name.StartsWith(prefix) ? name.Substring(prefix.Length) : name;
    }

    void SummarizeCoverage()
    {
        var reports = Directory.EnumerateFiles(
            CoverageDirectory, "*.cobertura.xml", SearchOption.AllDirectories).ToList();
        var covered = 0m;
        var valid = 0m;
        foreach (var report in reports)
        {
            var doc = XDocument.Load(report);
            if (doc.Root == null) continue;
            covered += decimal.TryParse(doc.Root.Attribute("lines-covered")?.Value, out var c) ? c : 0;
            valid += decimal.TryParse(doc.Root.Attribute("lines-valid")?.Value, out var v) ? v : 0;
        }
        var ratio = valid == 0 ? 0 : (covered / valid) * 100m;
        var path = CoverageDirectory / "coverage-summary.txt";
        using var w = new StreamWriter(path);
        w.WriteLine($"Reports: {reports.Count}");
        w.WriteLine($"Lines covered: {covered}");
        w.WriteLine($"Lines valid: {valid}");
        w.WriteLine($"Line coverage: {ratio:F2}%");
        Console.WriteLine($"Coverage summary written: {path}");
    }

    static void ExtractClassCoverage(
        string report, List<(int Missed, int Covered, double Rate, string Name)> rows)
    {
        var doc = XDocument.Load(report);
        foreach (var cls in doc.Descendants("class"))
        {
            var name = cls.Attribute("name")?.Value ?? "Unknown";
            var lineElements = cls.Descendants("line").ToList();
            if (lineElements.Count == 0) continue;
            var cov = lineElements.Count(l =>
                int.TryParse(l.Attribute("hits")?.Value, out var h) && h > 0);
            var missed = lineElements.Count - cov;
            var rate = (double)cov / lineElements.Count * 100.0;
            rows.Add((missed, cov, rate, name));
        }
    }

    // ── Coupling metrics ──────────────────────────────────────────────────────

    List<NsMetrics> CollectNamespaceData(List<string> sourceFiles)
    {
        var nsFiles = new Dictionary<string, List<string>>();
        var allNs = new HashSet<string>();

        foreach (var file in sourceFiles)
        {
            var ns = InferNamespace(file);
            if (ns == null) continue;
            allNs.Add(ns);
            if (!nsFiles.ContainsKey(ns)) nsFiles[ns] = new List<string>();
            nsFiles[ns].Add(file);
        }

        var abstractRe = new Regex(@"\b(interface|abstract\s+class)\s+\w+", RegexOptions.Compiled);
        var typeRe = new Regex(@"\b(class|struct|record|interface|enum)\s+\w+", RegexOptions.Compiled);
        var usingRe = new Regex(@"^\s*using\s+(NarrativeTrace\.\w+)", RegexOptions.Compiled);

        var results = new List<NsMetrics>();
        foreach (var ns in allNs.OrderBy(x => x))
        {
            var files = nsFiles.GetValueOrDefault(ns, new List<string>());
            var efferent = new HashSet<string>();
            var abstractCount = 0;
            var totalTypes = 0;

            foreach (var file in files)
            {
                foreach (var line in File.ReadAllLines(file))
                {
                    var um = usingRe.Match(line);
                    if (um.Success)
                    {
                        var dep = um.Groups[1].Value;
                        if (dep != ns && allNs.Contains(dep)) efferent.Add(dep);
                    }
                    if (abstractRe.IsMatch(line)) abstractCount++;
                    if (typeRe.IsMatch(line)) totalTypes++;
                }
            }

            results.Add(new NsMetrics(ns, efferent, abstractCount, totalTypes));
        }

        foreach (var m in results)
            m.Ca = results.Count(o => o.Name != m.Name && o.Efferent.Contains(m.Name));

        return results;
    }

    /// <summary>
    /// The two halves of the foundation-package split, exempt from the
    /// distance gate.
    /// </summary>
    /// <remarks>
    /// Both are concrete (low abstractness) and depended upon by nearly
    /// everything (high afferent coupling), which is exactly the "zone of
    /// pain" the distance metric names — and exactly the shape they are
    /// supposed to have. <c>Core</c> is a model package full of immutable
    /// records; <c>Runtime</c> holds the pipeline, the contexts and the
    /// exporters every integration module builds on, so its afferent coupling
    /// only ever grows. Splitting one Java module (<c>narrativetrace-core</c>)
    /// into two did not create two kinds of package, and the gate should not
    /// treat the second half differently from the first: the exemption was
    /// written for Core alone in the same commit that created both, when
    /// Runtime happened to sit just under the threshold. Every other package
    /// stays gated, which is where the metric earns its keep.
    /// </remarks>
    static readonly string[] FoundationPackages =
        ["NarrativeTrace.Core", "NarrativeTrace.Runtime"];

    void WriteCouplingReport(List<NsMetrics> data)
    {
        var path = MetricsDirectory / "coupling-report.txt";
        using var w = new StreamWriter(path);
        w.WriteLine("COUPLING METRICS");
        w.WriteLine(new string('=', 80));
        w.WriteLine($"{"Ca",4}  {"Ce",4}  {"A",5}  {"I",5}  {"D",5}  Namespace");
        w.WriteLine(new string('-', 80));

        var violations = new List<string>();
        foreach (var m in data.OrderBy(x => x.Name))
        {
            var ce = m.Efferent.Count;
            var ca = m.Ca;
            var a = m.TotalTypes == 0 ? 0.0 : (double)m.AbstractCount / m.TotalTypes;
            var instab = (ca + ce) == 0 ? 0.0 : (double)ce / (ca + ce);
            var d = Math.Abs(a + instab - 1.0);
            w.WriteLine($"{ca,4}  {ce,4}  {a,5:F2}  {instab,5:F2}  {d,5:F2}  {m.Name}");
            // Skip gate for: namespaces with ≤ 2 types (placeholders, tiny adapters),
            // and the foundation packages (see FoundationPackages).
            if (d > 0.8 && m.TotalTypes > 2 && !FoundationPackages.Contains(m.Name))
                violations.Add($"{m.Name} (D={d:F2})");
        }

        w.WriteLine();
        w.WriteLine($"Total namespaces: {data.Count}");
        Console.WriteLine($"Coupling report written: {path}");

        if (violations.Count > 0)
            throw new Exception(
                $"Coupling gate failed — Distance > 0.8: {string.Join(", ", violations)}");
    }

    static string? InferNamespace(string filePath)
    {
        var idx = filePath.IndexOf("/src/", StringComparison.Ordinal);
        if (idx < 0) return null;
        var after = filePath.Substring(idx + 5);
        var slash = after.IndexOf('/');
        return slash < 0 ? null : after.Substring(0, slash);
    }

    class NsMetrics
    {
        public string Name { get; }
        public HashSet<string> Efferent { get; }
        public int AbstractCount { get; }
        public int TotalTypes { get; }
        public int Ca { get; set; }

        public NsMetrics(string name, HashSet<string> efferent, int abstractCount, int totalTypes)
        {
            Name = name;
            Efferent = efferent;
            AbstractCount = abstractCount;
            TotalTypes = totalTypes;
        }
    }
}
