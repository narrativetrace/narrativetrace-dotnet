// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Reflection;
using NetArchTest.Rules;
using Xunit;

namespace NarrativeTrace.ArchTests;

/// <summary>
/// Guards the Phase 3 Q3 refactor: the <c>skills render|lint|replay</c> verbs left the shipped
/// <c>dotnet-narrativetrace</c> tool for the standalone <c>skills-devtool/</c> so the CLI's
/// <c>PackAsTool</c> NuGet package no longer carries the <c>NarrativeTrace.Skills.Catalogue</c>
/// assembly (ladder §6.3: the catalogue is not distributed). Renamed from
/// <c>NarrativeTrace.Skills</c> in Phase 3 milestone 1, which freed that coordinate for the
/// published, resources-only carrier package the CLI legitimately embeds pages from (as raw
/// <c>EmbeddedResource</c>s, never an assembly reference) — this test's purpose, "no catalogue
/// assembly ships in the tool package", is unchanged by that.
///
/// <para>A <c>PackAsTool</c> package bundles the running assembly plus its whole reference closure.
/// So "the built .nupkg contains no <c>NarrativeTrace.Skills.Catalogue</c> assembly" reduces to "the
/// CLI's reference closure contains no <c>NarrativeTrace.Skills.Catalogue</c>" — asserted here
/// directly off the built assemblies, which is faster and just as faithful as unzipping a freshly
/// packed nupkg, and fails the moment anyone reintroduces the dependency.</para>
/// </summary>
public class CliPackagingTests
{
    private const string CatalogueAssembly = "NarrativeTrace.Skills.Catalogue";

    private static Assembly CliAssembly => typeof(NarrativeTrace.Cli.CliRunner).Assembly;

    [Fact]
    public void The_shipped_cli_does_not_depend_on_the_catalogue_assembly()
    {
        var result = Types.InAssembly(CliAssembly)
            .Should().NotHaveDependencyOn(CatalogueAssembly)
            .GetResult();

        Assert.True(
            result.IsSuccessful,
            "The shipped CLI must not depend on " + CatalogueAssembly + "; forbidden dependency in: "
                + string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void The_cli_reference_closure_carries_no_catalogue_assembly()
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Queue<Assembly>();
        pending.Enqueue(CliAssembly);

        while (pending.Count > 0)
        {
            foreach (var reference in pending.Dequeue().GetReferencedAssemblies())
            {
                var name = reference.Name ?? "";
                if (!name.StartsWith("NarrativeTrace.", StringComparison.Ordinal) || !seen.Add(name))
                {
                    continue;
                }

                Assert.NotEqual(CatalogueAssembly, name);
                pending.Enqueue(Assembly.Load(reference));
            }
        }
    }
}
