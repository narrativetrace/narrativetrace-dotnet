// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Text.RegularExpressions;

namespace NarrativeTrace.Tooling.Frameworks;

/// <summary>
/// The framework table — Phase 6's D1: ONE committed registry of which frameworks NarrativeTrace
/// integrates with, how a project proves it uses one, which package it adds, how that package is
/// wired, and which doctor check watches the wiring.
/// </summary>
/// <remarks>
/// <para>INTENT: the table ships inside the tooling library the doctor runs from, so a project is
/// measured against the rows of the NarrativeTrace version it actually installed — never against the
/// repository or the live docs, which describe the newest release. The <c>add-narrative-tracing</c>
/// skill lists no framework at all; it runs the doctor and applies every framework fix the doctor
/// prints. The framework table in <c>llms-full.md</c> and the covered-frameworks line in
/// <c>llms.txt</c> render from these rows (<see cref="FrameworkTableDocs"/>).</para>
/// <para><b>@llmNote</b> Adding a framework is adding a row here and, when its wiring is source-level,
/// a compiled fixture plus its section in <c>wiring-snippets.md</c>: the doctor check, the docs and the
/// skill follow without new code. Row ids and check ids are a cross-port contract — append, never
/// rename. The ASP.NET Core row keeps <c>config.registration-pair</c>, the id that check shipped with
/// before the table existed.</para>
/// </remarks>
public static class FrameworkTable
{
    private const string Csharp = "csharp";
    private const string WiringRegion = "wiring";

    /// <summary>Every row, in table order — the order the doctor runs their checks and the docs list them.</summary>
    public static readonly IReadOnlyList<FrameworkRow> Rows =
        [AspNetCore(), DependencyInjection(), Xunit(), NUnit(), OpenTelemetry(), DefaultLogger(), MsBuildClarity()];

    /// <summary>The rows that earn a doctor wiring check of their own, in table order.</summary>
    public static IReadOnlyList<FrameworkRow> RowsWithWiringChecks() =>
        Rows.Where(row => row.Check is CheckBinding.WiringCheck).ToList();

    /// <summary>The ids of those checks — every framework wiring check the doctor runs — in order.</summary>
    public static IReadOnlyList<string> WiringCheckIds() =>
        RowsWithWiringChecks().Select(row => ((CheckBinding.WiringCheck)row.Check).Id).ToList();

    /// <summary>The row with this id, or <see langword="null"/> when the table has none.</summary>
    public static FrameworkRow? Row(string id) => Rows.FirstOrDefault(row => row.Id == id);

    /// <summary>
    /// Whether a manifest shows the row's framework: one of its marker patterns matches the
    /// manifest's start tags, and no row it defers to is detected as well.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The row defers to an id the table has no row for — a typo there would otherwise make the row
    /// silently never stand down.
    /// </exception>
    public static bool Detected(FrameworkRow row, string manifestText)
    {
        ArgumentNullException.ThrowIfNull(row);
        return Detected(row, ManifestElements.Read(manifestText));
    }

    private static bool Detected(FrameworkRow row, IReadOnlyList<ManifestElement> elements) =>
        row.Marker.Manifest.Any(pattern => elements.Any(element => pattern(element)))
        && !row.Marker.DeferTo.Select(id => Row(id)
                ?? throw new InvalidOperationException($"{row.Id} defers to {id}, which is not a row"))
            .Any(other => Detected(other, elements));

    private static FrameworkRow AspNetCore() => new(
        "aspnetcore",
        "ASP.NET Core",
        new FrameworkMarker(
            "the Microsoft.NET.Sdk.Web SDK or a Microsoft.AspNetCore.App framework reference",
            [ManifestPatterns.Sdk("Microsoft.NET.Sdk.Web"), ManifestPatterns.FrameworkReference("Microsoft.AspNetCore.App")],
            []),
        new IntegrationPackage("NarrativeTrace.AspNetCore"),
        new Wiring.Snippet(
            "AddNarrativeTrace(...) on the services, UseNarrativeTrace() on the app, and each service " +
            "wrapped against HttpContext.GetNarrativeContext() (namespace NarrativeTrace.AspNetCore)",
            "tests/NarrativeTrace.AspNetCore.Tests/Wiring/AspNetCoreWiring.cs",
            WiringRegion,
            Csharp,
            [Evidence(@"\bAddNarrativeTrace\s*\("),
                Evidence(@"\bUseNarrativeTrace\s*\(|\bUseMiddleware\s*<\s*(?:[\w.]+\.)?NarrativeTraceMiddleware\s*>")]),
        new CheckBinding.WiringCheck("config.registration-pair"),
        "init-prompt-aspnetcore-project",
        "documentation/guides/aspnetcore.md#1-register-and-wire-the-middleware");

    private static FrameworkRow DependencyInjection() => new(
        "di",
        "Dependency injection",
        new FrameworkMarker(
            "a Microsoft.Extensions.DependencyInjection package reference outside a web app",
            [ManifestPatterns.Package("Microsoft.Extensions.DependencyInjection")],
            ["aspnetcore"]),
        new IntegrationPackage("NarrativeTrace.DependencyInjection"),
        new Wiring.Snippet(
            "AddNarrativeTracing(options => options.Namespaces(...)) after the service registrations " +
            "(namespace NarrativeTrace.DependencyInjection)",
            "tests/NarrativeTrace.DependencyInjection.Tests/Wiring/DependencyInjectionWiring.cs",
            WiringRegion,
            Csharp,
            [Evidence(@"\bAddNarrativeTracing\s*\("), Evidence(@"\.Namespaces\s*\(|\bBaseNamespaces\s*=")]),
        new CheckBinding.WiringCheck("config.di-namespaces"),
        FrameworkRow.NoTierBCase,
        "documentation/guides/installation.md#option-b--dependency-injection-auto-wrapping");

    private static FrameworkRow Xunit() => new(
        "xunit",
        "xUnit",
        new FrameworkMarker(
            "an xunit or xunit.v3 package reference",
            [ManifestPatterns.Package("xunit"), ManifestPatterns.Package("xunit.v3")],
            []),
        new IntegrationPackage("NarrativeTrace.Testing.Xunit"),
        new Wiring.Snippet(
            "a NarrativeFixture around the traced calls in a test (namespace NarrativeTrace.TestingXunit)",
            "tests/NarrativeTrace.Examples.SixtySeconds.Tests/RedactionQuickstartTests.cs",
            null,
            Csharp,
            [Evidence(@"\bNarrative(?:Suite)?Fixture\b")]),
        new CheckBinding.WiringCheck("config.xunit-fixture"),
        FrameworkRow.NoTierBCase,
        "documentation/guides/installation.md#option-d--xunit-auto-context--failure-narrative");

    private static FrameworkRow NUnit() => new(
        "nunit",
        "NUnit",
        new FrameworkMarker("an NUnit package reference", [ManifestPatterns.Package("NUnit")], []),
        new IntegrationPackage("NarrativeTrace.Testing.NUnit"),
        new Wiring.Snippet(
            "a test fixture deriving from NarrativeTestBase (namespace NarrativeTrace.TestingNUnit)",
            "tests/NarrativeTrace.Testing.NUnit.EngineTests/Wiring/NUnitWiring.cs",
            WiringRegion,
            Csharp,
            [Evidence(@":\s*(?:[\w.]+\.)?NarrativeTestBase\b")]),
        new CheckBinding.WiringCheck("config.nunit-base"),
        FrameworkRow.NoTierBCase,
        "documentation/guides/installation.md#option-e--nunit-auto-context--failure-narrative");

    private static FrameworkRow OpenTelemetry() => new(
        "opentelemetry",
        "OpenTelemetry",
        new FrameworkMarker(
            "an OpenTelemetry or OpenTelemetry.* package reference", [ManifestPatterns.PackageFamily("OpenTelemetry")], []),
        new IntegrationPackage("NarrativeTrace.Observability"),
        new Wiring.Snippet(
            "AddSource(\"NarrativeTrace\") on the tracer provider and the captured trace exported as spans " +
            "(namespace NarrativeTrace.Observability)",
            "tests/NarrativeTrace.Observability.Tests/Wiring/OpenTelemetryWiring.cs",
            WiringRegion,
            Csharp,
            // Bounded window: an unbounded [^)]* rescans to the end from every unclosed AddSource( — quadratic.
            [Evidence(@"\.AddSource\s*\([^)]{0,400}?""NarrativeTrace"""),
                Evidence(@"\b(?:TraceActivityExporter|OtelTraceEventListener)\b")]),
        new CheckBinding.WiringCheck("config.otel-source"),
        FrameworkRow.NoTierBCase,
        "documentation/guides/llms-full.md#opentelemetry-narrativetraceobservability");

    private static FrameworkRow DefaultLogger() => new(
        "default-logger",
        "Microsoft.Extensions.Logging",
        new FrameworkMarker("the default logger: tracing is wired and nothing reads the trace", [], []),
        new IntegrationPackage("NarrativeTrace.Logging"),
        new Wiring.Snippet(
            "TraceLogExporter.ExportToLogger(...) with the application's own ILoggerFactory (namespace NarrativeTrace.Logging)",
            "tests/NarrativeTrace.Logging.Tests/Wiring/LoggerWiring.cs",
            WiringRegion,
            Csharp,
            [Evidence(@"\bTraceLogExporter\b|\bAddNarrativeLogging\s*\(")]),
        new CheckBinding.ExistingCheck("trap.silent-sink"),
        FrameworkRow.NoTierBCase,
        "documentation/guides/installation.md#configure-trace-output");

    private static FrameworkRow MsBuildClarity() => new(
        "msbuild-clarity",
        "MSBuild clarity targets",
        new FrameworkMarker("the owner's opt-in to a build-time clarity gate", [], []),
        new IntegrationPackage("NarrativeTrace.MSBuild"),
        new Wiring.OwnerOptIn("the ClarityScan/ClarityCheck targets, set up by the add-narrativetrace-clarity skill"),
        new CheckBinding.NoCheck("owned by the add-narrativetrace-clarity skill"),
        FrameworkRow.NoTierBCase,
        "documentation/guides/clarity.md");

    private static Regex Evidence(string pattern) =>
        new(pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant);
}
