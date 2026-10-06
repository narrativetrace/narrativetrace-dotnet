// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Reflection;
using Xunit;

namespace NarrativeTrace.ArchTests;

/// <summary>
/// Guards the tooling library's independence (Phase 3 D3, as changed 2026-09-25):
/// <c>NarrativeTrace.Tooling</c> holds the doctor checks and the installer with ZERO dependencies, and
/// — the load-bearing rule for the installer — it must never reference the catalogue project
/// <c>NarrativeTrace.Skills.Catalogue</c>. The installer reads rendered SKILL.md bytes from a carrier; it must
/// never render them itself, or the bytes it installs could diverge from the bytes the drift gate
/// pins. Forbidding the reference makes that structurally impossible.
/// </summary>
/// <remarks>
/// These assert against the assembly's real <see cref="Assembly.GetReferencedAssemblies"/> metadata
/// rather than NetArchTest's name-prefix <c>NotHaveDependencyOn</c>: the library carries the coordinate
/// string literal <c>"NarrativeTrace.Skills/&lt;version&gt;"</c> (D6), and NetArchTest's dependency
/// search matches that constant, reporting a dependency that does not exist. The referenced-assembly
/// closure has no such blind spot.
/// </remarks>
public class ToolingLibraryArchTests
{
    private const string CatalogueAssembly = "NarrativeTrace.Skills.Catalogue";

    private static Assembly ToolingAssembly => typeof(NarrativeTrace.Tooling.Doctor.DoctorCommand).Assembly;

    private static IReadOnlyList<string> ReferencedNarrativeTraceAssemblies() =>
        ToolingAssembly.GetReferencedAssemblies()
            .Select(reference => reference.Name ?? "")
            .Where(name => name.StartsWith("NarrativeTrace.", StringComparison.Ordinal))
            .ToList();

    [Fact]
    public void The_tooling_library_does_not_reference_the_catalogue_assembly()
    {
        Assert.DoesNotContain(CatalogueAssembly, ReferencedNarrativeTraceAssemblies());
    }

    [Fact]
    public void The_tooling_library_references_no_narrativetrace_assembly_at_all()
    {
        // Zero-dependency by design: the doctor + installer run on the BCL alone, so the catalogue
        // cannot sneak in transitively either.
        var offenders = ReferencedNarrativeTraceAssemblies();

        Assert.True(
            offenders.Count == 0,
            "The tooling library must reference no NarrativeTrace assembly; found: "
                + string.Join(", ", offenders));
    }
}
