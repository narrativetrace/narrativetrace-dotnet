// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace NarrativeTrace.Build;

/// <summary>
/// Every project the <c>Coverage</c> sweep runs must be accounted for — a
/// coverage GATE (<see cref="Thresholds"/>) or an explicit EXEMPTION carrying
/// a written reason (<see cref="Exemptions"/>). Both live here rather than on
/// <c>Build</c> itself so this class — like <see cref="TestProjectSelection"/>,
/// <see cref="HeaderAbsenceSupport"/> and the rest of the <c>*Support.cs</c>
/// family — is plain, Nuke-independent code the test suite can reference
/// directly (see <c>BuildScript.Tests/BuildScript.Tests.csproj</c>'s
/// <c>&lt;Compile Include&gt;</c> links) and check against the REAL data, not
/// a synthetic copy that could drift from what actually gates the build.
/// </summary>
/// <remarks>
/// Default-deny, the inverse of what a project absent from
/// <see cref="Thresholds"/> alone used to get: coverage was still collected
/// for it and nothing enforced or reported the gap. That is the exact shape
/// of hole <see cref="TestProjectSelection"/> closed for test SELECTION (a
/// name not ending in <c>Tests</c>) — this closes it for coverage ACCOUNTING.
/// Adding a test project must force a decision, gate it or write down why
/// not, instead of silently inheriting an ungated run.
/// </remarks>
internal static class CoverageAccounting
{
    /// <param name="Threshold">Line-coverage floor, whole percent.</param>
    /// <param name="Include">
    /// Coverlet assembly filter scoping the threshold to the product
    /// assembly this test project owns, or <see langword="null"/> when the
    /// project is the sole gate for more than one assembly (Core+Runtime).
    /// </param>
    public sealed record Gate(int Threshold, string? Include = null);

    /// <param name="Reason">
    /// Why this project is not coverage-gated — short and specific; this is
    /// what a reviewer reads two years from now, not the person who wrote it.
    /// </param>
    public sealed record Exemption(string Reason);

    /// <summary>
    /// Per-test-project line-coverage floors, enforced via coverlet. Where an
    /// Include filter is present the threshold applies only to that assembly —
    /// each test project's report also loads upstream modules at incidental
    /// low coverage. Floors are ratchets: measured 2026-08-20 (the five
    /// Examples/EngineTests rows: measured 2026-09-10), rounded down; raise
    /// toward the Java 98 % norm as headroom allows (see
    /// ../narrative-trace-java/documentation/quality-tooling-parity.md).
    /// </summary>
    public static readonly Dictionary<string, Gate> Thresholds = new()
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

        // The four Examples siblings ECommerce's own gate never got, plus the NUnit engine
        // adapter's own test project — all previously default-allowed (absent from this map
        // entirely), never a deliberate exemption. Measured 2026-09-10 (own-assembly Include,
        // confirmed identical to the unfiltered report's per-package rate), rounded down:
        ["NarrativeTrace.Examples.Clarity.Tests"] = new(98, "[NarrativeTrace.Examples.Clarity]*"),
        ["NarrativeTrace.Examples.Common.Tests"] = new(100, "[NarrativeTrace.Examples.Common]*"),
        ["NarrativeTrace.Examples.Library.Tests"] = new(91, "[NarrativeTrace.Examples.Library]*"),
        ["NarrativeTrace.Examples.Minecraft.Tests"] = new(98, "[NarrativeTrace.Examples.Minecraft]*"),
        // Exercises the shipped NUnit engine/runner adapter narrowly (the integration seam, not
        // the breadth NarrativeTrace.Testing.NUnit.Tests already covers at 86) — 71% is the
        // honest floor of that narrower slice, not an aspirational number; see the backlog entry
        // for the measured breakdown before reaching for "raise it."
        ["NarrativeTrace.Testing.NUnit.EngineTests"] = new(71, "[NarrativeTrace.Testing.NUnit]*"),
    };

    /// <summary>
    /// Test projects the <c>Coverage</c> sweep runs without a threshold, each
    /// with a specific, verified reason — never "not gotten to yet".
    /// </summary>
    public static readonly Dictionary<string, Exemption> Exemptions = new()
    {
        ["BuildScript.Tests"] = new(
            "measures build scripts, not shipped product code; carries no coverlet.msbuild "
            + "reference at all, so CollectCoverage=true collects nothing for it"),
        ["NarrativeTrace.ArchTests"] = new(
            "checks dependency DIRECTION (layering, no cycles) via NetArchTest's static "
            + "reflection over ten product assemblies' type metadata; never invokes a product "
            + "method, so a coverage number would be at or near zero regardless of quality — "
            + "also carries no coverlet.msbuild reference, matching that"),
        ["NarrativeTrace.SecurityTests"] = new(
            "fuzzes hostile input through every renderer across six product assemblies "
            + "incidentally, none as its primary exercise — each already has its own gated test "
            + "project. Measured without an Include filter (2026-09-10): Glossary 14.9%, "
            + "Proxy 54.1%, Runtime 29.9%, Core 64.9%, Diagrams 88.5%, Clarity 81.9%, blended "
            + "51.8% — an artifact of which assemblies happened to load that run, not a quality "
            + "signal for any one of them"),
        ["NarrativeTrace.StressTests"] = new(
            "races a handful of concurrency-sensitive paths in Core/Runtime many times over; "
            + "not a general exerciser of either — measured (2026-09-10): Core 0.9% line, "
            + "Runtime 21.0% line, both already gated at 98% by NarrativeTrace.Core.Tests"),
    };

    /// <summary>
    /// Every name in <paramref name="testProjectNames"/> present in neither
    /// <paramref name="gated"/> nor <paramref name="exempted"/>, sorted for a
    /// stable failure message.
    /// </summary>
    public static IReadOnlyList<string> Unaccounted(
        IEnumerable<string> testProjectNames,
        IReadOnlyCollection<string> gated,
        IReadOnlyCollection<string> exempted)
    {
        return testProjectNames
            .Where(name => !gated.Contains(name) && !exempted.Contains(name))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// Every name present in BOTH <paramref name="gated"/> and
    /// <paramref name="exempted"/> — a project cannot be a gate and an
    /// exemption at once; whichever was added second is very likely a
    /// copy-paste of the wrong project's row.
    /// </summary>
    public static IReadOnlyList<string> DoublyAccounted(
        IReadOnlyCollection<string> gated, IReadOnlyCollection<string> exempted)
    {
        return gated
            .Where(exempted.Contains)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();
    }
}
