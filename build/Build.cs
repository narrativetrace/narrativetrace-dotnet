// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using NarrativeTrace.Build;
using Nuke.Common;
using Nuke.Common.Execution;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.Git;
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

    AbsolutePath ArtifactsDirectory => RootDirectory / "artifacts";
    AbsolutePath TestResultsDirectory => ArtifactsDirectory / "test-results";
    AbsolutePath CoverageDirectory => ArtifactsDirectory / "coverage";
    AbsolutePath MutationDirectory => ArtifactsDirectory / "mutation";
    AbsolutePath MetricsDirectory => ArtifactsDirectory / "metrics";
    AbsolutePath BenchmarkArtifactsDir => ArtifactsDirectory / "benchmarks";
    AbsolutePath BenchmarkBaselineFile => RootDirectory / "benchmarks" / "benchmark-baseline.json";
    AbsolutePath SecurityDirectory => ArtifactsDirectory / "security";
    AbsolutePath FuzzDirectory => ArtifactsDirectory / "fuzz";
    AbsolutePath FuzzProject => RootDirectory / "fuzz" / "NarrativeTrace.Fuzz" / "NarrativeTrace.Fuzz.csproj";
    AbsolutePath FuzzSeeds => RootDirectory / "fuzz" / "NarrativeTrace.Fuzz" / "seeds";

    [Parameter("Fuzz: seconds afl-fuzz spends on each target (renderer, json) — default 60")]
    readonly int FuzzSeconds = 60;

    [Parameter("Stress: repetitions per race in the long sweep — default 200000")]
    readonly int StressSweepIterations = 200_000;

    /// <summary>
    /// The projects the <c>Test</c> and <c>Coverage</c> sweeps run. Selection
    /// is by name — see <see cref="TestProjectSelection"/>, whose check keeps
    /// every project under <c>tests/</c> inside the gate.
    /// </summary>
    Project[] TestProjects => Solution.AllProjects
        .Where(x => TestProjectSelection.IsSelected(x.Name))
        .ToArray();

    sealed record CoverageGate(int Threshold, string? Include = null);

    /// <summary>
    /// Per-test-project line-coverage floors, enforced via coverlet. Where an
    /// Include filter is present the threshold applies only to that assembly —
    /// each test project's report also loads upstream modules at incidental
    /// low coverage. Floors are ratchets: measured 2026-08-20, rounded down;
    /// raise toward the Java 98 % norm as headroom allows
    /// (see ../narrative-trace-java/documentation/quality-tooling-parity.md).
    /// </summary>
    static readonly Dictionary<string, CoverageGate> CoverageThresholds = new()
    {
        // no Include: gates Core AND Runtime (both ≥ 98; Runtime has no own test project)
        ["NarrativeTrace.Core.Tests"] = new(98),
        ["NarrativeTrace.AspNetCore.Tests"] = new(98, "[NarrativeTrace.AspNetCore]*"),
        ["NarrativeTrace.Clarity.Tests"] = new(98, "[NarrativeTrace.Clarity]*"),
        // coverlet's filter parser chokes on the hyphen in dotnet-narrativetrace; prefix wildcard instead
        ["NarrativeTrace.Cli.Tests"] = new(93, "[dotnet*]*"),
        ["NarrativeTrace.DependencyInjection.Tests"] = new(98, "[NarrativeTrace.DependencyInjection]*"),
        ["NarrativeTrace.Diagrams.Tests"] = new(95, "[NarrativeTrace.Diagrams]*"),
        ["NarrativeTrace.Examples.ECommerce.Tests"] = new(87, "[NarrativeTrace.Examples.ECommerce]*"),
        ["NarrativeTrace.Glossary.Tests"] = new(98, "[NarrativeTrace.Glossary]*"),
        ["NarrativeTrace.Legacy.Tests"] = new(98, "[NarrativeTrace.Legacy]*"),
        ["NarrativeTrace.Logging.Tests"] = new(93, "[NarrativeTrace.Logging]*"),
        ["NarrativeTrace.Observability.Tests"] = new(96, "[NarrativeTrace.Observability]*"),
        ["NarrativeTrace.Proxy.Tests"] = new(94, "[NarrativeTrace.Proxy]*"),
        ["NarrativeTrace.Testing.NUnit.Tests"] = new(86, "[NarrativeTrace.Testing.NUnit]*"),
        ["NarrativeTrace.Testing.Xunit.Tests"] = new(88, "[NarrativeTrace.Testing.Xunit]*"),
    };

    /// <summary>
    /// Tests that re-invoke <c>./build.sh</c> (BuildScript.Tests). Excluded
    /// from the project-wide Test and Coverage sweeps, which would otherwise
    /// recurse into themselves; the BuildScriptTests target runs them.
    /// </summary>
    const string SpawnsBuildFilter = "Category!=SpawnsBuild";

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
            DotNet($"format whitespace \"{solution}\" --no-restore");
            DotNet($"format style \"{solution}\" --no-restore");
        });

    Target FormatCheck => _ => _
        .DependsOn(Restore)
        .Executes(() =>
        {
            var solution = WriteFormatSolution();
            DotNet($"format whitespace \"{solution}\" --no-restore --verify-no-changes");
            DotNet($"format style \"{solution}\" --no-restore --verify-no-changes");
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
            foreach (var project in TestProjects)
            {
                DotNetTest(s => s
                    .SetProjectFile(project.Path)
                    .SetConfiguration(Configuration)
                    .EnableNoBuild()
                    .SetFilter(SpawnsBuildFilter)
                    .SetResultsDirectory(TestResultsDirectory)
                    .SetLoggers($"trx;LogFileName={project.Name}.trx"));
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

    // ── Security tooling (shared cross-port convention) ─────────────────────────

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
    /// Degrades gracefully when the binary is absent — the same warn-and-pass
    /// shape <see cref="Fuzz"/> uses for afl-fuzz — because THIN-CI keeps
    /// tool installation out of the gate itself; CI provisions the binary
    /// before calling this target.
    /// </remarks>
    Target SecretsScan => _ => _
        .After(Clean)
        .Executes(() =>
        {
            const string tool = "gitleaks";
            if (!IsOnPath(tool))
            {
                Console.WriteLine($"{tool} not found on PATH: secrets scan skipped. Install gitleaks " +
                    "(https://github.com/gitleaks/gitleaks/releases) to run it locally; CI provisions it.");
                return;
            }

            Directory.CreateDirectory(SecurityDirectory);
            var reportPath = SecurityDirectory / "gitleaks-report.json";
            ProcessTasks.StartProcess(
                    tool,
                    $"detect --source \"{RootDirectory}\" --redact --no-banner --report-format json "
                        + $"--report-path \"{reportPath}\"")
                .AssertZeroExitCode();
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
    /// Degrades gracefully when the binary is absent, the same shape as
    /// <see cref="SecretsScan"/>.
    /// </remarks>
    Target Semgrep => _ => _
        .Executes(() =>
        {
            const string tool = "semgrep";
            if (!IsOnPath(tool))
            {
                Console.WriteLine($"{tool} not found on PATH: security ruleset scan skipped. Install " +
                    "semgrep (pip install semgrep) to run it locally; CI provisions it.");
                return;
            }

            Directory.CreateDirectory(SecurityDirectory);
            var reportPath = SecurityDirectory / "semgrep-csharp.json";
            ProcessTasks.StartProcess(
                    tool,
                    $"--config=p/csharp --metrics=off --error --json --output \"{reportPath}\" \"{RootDirectory}\"")
                .AssertZeroExitCode();
        });

    /// <summary>
    /// Scans every manifest (<c>*.csproj</c>, <c>Directory.Build.props</c>)
    /// for known-vulnerable dependencies against the OSV database.
    /// </summary>
    /// <remarks>
    /// Needs network (the osv.dev API), so — like <see cref="DependencyAudit"/>
    /// — it runs on schedule/web pipelines only, never per commit.
    /// <see cref="VulnerablePackages"/> runs beside it in the same tier as a
    /// second, NuGet-native source for the same question.
    /// </remarks>
    Target OsvScan => _ => _
        .Executes(() =>
        {
            const string tool = "osv-scanner";
            if (!IsOnPath(tool))
            {
                Console.WriteLine($"{tool} not found on PATH: OSV scan skipped. Install osv-scanner " +
                    "(https://github.com/google/osv-scanner/releases) to run it locally; CI provisions it.");
                return;
            }

            Directory.CreateDirectory(SecurityDirectory);
            var reportPath = SecurityDirectory / "osv-scanner-report.json";
            ProcessTasks.StartProcess(
                    tool,
                    $"scan source --recursive --format json --output-file \"{reportPath}\" \"{RootDirectory}\"")
                .AssertZeroExitCode();
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
            if (Directory.Exists(CoverageDirectory))
                Directory.Delete(CoverageDirectory, true);
            Directory.CreateDirectory(CoverageDirectory);
            Directory.CreateDirectory(TestResultsDirectory);

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

                if (CoverageThresholds.TryGetValue(project.Name, out var gate))
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

                DotNetTest(settings);
            }

            SummarizeCoverage();
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

    Target Mutation => _ => _
        .DependsOn(Compile)
        .Executes(() =>
        {
            if (Directory.Exists(MutationDirectory))
                Directory.Delete(MutationDirectory, true);
            Directory.CreateDirectory(MutationDirectory);

            DotNet("tool restore");

            var configs = Directory.EnumerateFiles(RootDirectory, "stryker-config*.json")
                .OrderBy(x => x)
                .ToList();

            foreach (var config in configs)
            {
                var moduleName = ExtractStrykerModuleName(config);
                var strykerOutput = (AbsolutePath)(RootDirectory / "StrykerOutput");
                if (Directory.Exists(strykerOutput))
                    Directory.Delete(strykerOutput, true);

                DotNet($"tool run dotnet-stryker -- --config-file \"{config}\"");

                if (Directory.Exists(strykerOutput))
                {
                    var dest = (AbsolutePath)(MutationDirectory / moduleName);
                    if (Directory.Exists(dest))
                        Directory.Delete(dest, true);
                    Directory.Move(strykerOutput, dest);
                }
            }
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
    /// <c>afl-fuzz</c> (a native binary this repository does not ship, is not on <c>PATH</c> in a
    /// bare container, and cannot be installed here without root/apt). Confirmed on the container
    /// this port was built in: <c>sharpfuzz</c> instrumentation succeeds (the published DLL grows
    /// from ~186 KB to ~312 KB — real IL added, verified by byte comparison); <c>afl-fuzz</c> is
    /// absent and `apt-cache search afl` returns nothing (no cached package to install even with
    /// root). So this target always performs the instrumentation half, proving the harness and
    /// tooling are wired correctly, and only attempts the actual fuzzing loop when it finds a driver
    /// on <c>PATH</c> — logging a clear, actionable message and returning (not failing) otherwise.
    /// A CI job that wants the real loop provisions AFL++ (or builds <c>libfuzzer-dotnet</c> against
    /// a clang/LLVM toolchain) before calling this target.
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
        ProcessTasks.StartProcess(
                driver,
                $"-V {FuzzSeconds} -i \"{inDir}\" -o \"{outDir}\" -- dotnet \"{harness}\" {target} @@")
            .AssertZeroExitCode();
    }

    // ── Stress (concurrency invariants shared across ports) ─────────────────────

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
