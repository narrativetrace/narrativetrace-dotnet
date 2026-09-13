// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Linq;
using System.Reflection;
using NarrativeTrace.Diagrams;
using Xunit;

namespace NarrativeTrace.Diagrams.Tests;

/// <summary>
/// API-shape guarantee for <see cref="ISequenceGrammar"/>: every hook that carries trace-derived
/// text is typed <see cref="DiagramLabel"/>, never a raw <see cref="string"/> — proved here by
/// reflection over the interface's own members, since C# has no compile-time way to forbid a
/// parameter type from an interface declaration the way a TypeScript branded-type test or a
/// .NET analyzer rule could.
/// </summary>
public class SequenceGrammarShapeTest
{
    [Fact]
    public void No_grammar_hook_parameter_is_a_raw_string()
    {
        var offenders = typeof(ISequenceGrammar).GetMethods()
            .SelectMany(method => method.GetParameters())
            .Where(parameter => parameter.ParameterType == typeof(string))
            .ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Every_grammar_hook_with_a_parameter_takes_only_diagram_labels()
    {
        var methods = typeof(ISequenceGrammar).GetMethods()
            .Where(method => method.GetParameters().Length > 0);

        foreach (var method in methods)
        {
            Assert.All(
                method.GetParameters(),
                parameter => Assert.Equal(typeof(DiagramLabel), parameter.ParameterType));
        }
    }

    [Fact]
    public void Both_shipped_grammars_implement_the_label_only_interface()
    {
        Assert.IsAssignableFrom<ISequenceGrammar>(MermaidSequenceGrammar.Instance);
        Assert.IsAssignableFrom<ISequenceGrammar>(new PlantUmlSequenceGrammar(includeLifelines: false));
    }
}
