// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Reflection;
using NetArchTest.Rules;
using NarrativeTrace.Core;
using NarrativeTrace.Proxy;
using NarrativeTrace.Clarity;
using NarrativeTrace.Diagrams;
using NarrativeTrace.Logging;
using NarrativeTrace.Observability;
using NarrativeTrace.AspNetCore;
using NarrativeTrace.TestingXunit;
using NarrativeTrace.TestingNUnit;
using Xunit;

namespace NarrativeTrace.ArchTests;

/// <summary>
/// Verifies no circular dependencies exist between NarrativeTrace assemblies.
/// For each ordered pair (A, B), if A depends on B then B must not depend on A.
/// </summary>
public class NoCyclicDependencyTests
{
    private static readonly (string Name, Assembly Assembly)[] Assemblies =
    [
        ("NarrativeTrace.Core", typeof(TraceNode).Assembly),
        ("NarrativeTrace.Proxy", typeof(NarrativeTraceProxy).Assembly),
        ("NarrativeTrace.Clarity", typeof(ClarityAnalyzer).Assembly),
        ("NarrativeTrace.Diagrams", typeof(MermaidSequenceRenderer).Assembly),
        ("NarrativeTrace.Logging", typeof(LoggingNarrativeContext).Assembly),
        ("NarrativeTrace.Observability", typeof(TraceActivityExporter).Assembly),
        ("NarrativeTrace.AspNetCore", typeof(NarrativeTraceMiddleware).Assembly),
        ("NarrativeTrace.TestingXunit", typeof(NarrativeFixture).Assembly),
        ("NarrativeTrace.TestingNUnit", typeof(NarrativeTestBase).Assembly),
        ("NarrativeTrace.Glossary", typeof(Glossary.Glossary).Assembly),
    ];

    [Fact]
    public void No_assembly_pair_has_circular_dependency()
    {
        var violations = new List<string>();

        for (var i = 0; i < Assemblies.Length; i++)
        {
            for (var j = i + 1; j < Assemblies.Length; j++)
            {
                var a = Assemblies[i];
                var b = Assemblies[j];

                var aDependsOnB = HasDependency(a.Assembly, b.Name);
                var bDependsOnA = HasDependency(b.Assembly, a.Name);

                if (aDependsOnB && bDependsOnA)
                    violations.Add($"{a.Name} <-> {b.Name}");
            }
        }

        Assert.True(
            violations.Count == 0,
            "Circular dependencies detected: " + string.Join("; ", violations));
    }

    private static bool HasDependency(Assembly assembly, string targetNamespace)
    {
        var result = Types.InAssembly(assembly)
            .Should().NotHaveDependencyOn(targetNamespace)
            .GetResult();
        return !result.IsSuccessful;
    }
}
