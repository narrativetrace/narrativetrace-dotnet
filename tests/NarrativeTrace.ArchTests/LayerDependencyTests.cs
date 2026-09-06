// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NetArchTest.Rules;
using NarrativeTrace.Core;
using NarrativeTrace.Proxy;
using NarrativeTrace.Clarity;
using NarrativeTrace.Diagrams;
using NarrativeTrace.Logging;
using NarrativeTrace.Observability;
using Xunit;

namespace NarrativeTrace.ArchTests;

/// <summary>
/// Enforces the layered dependency graph across all NarrativeTrace assemblies.
/// Core is the innermost layer with zero dependencies on higher modules.
/// </summary>
public class LayerDependencyTests
{
    // ── Core depends on nothing ───────────────────────────────────────────────

    [Theory]
    [InlineData("NarrativeTrace.Proxy")]
    [InlineData("NarrativeTrace.Clarity")]
    [InlineData("NarrativeTrace.Diagrams")]
    [InlineData("NarrativeTrace.Logging")]
    [InlineData("NarrativeTrace.Observability")]
    [InlineData("NarrativeTrace.AspNetCore")]
    [InlineData("NarrativeTrace.TestingXunit")]
    [InlineData("NarrativeTrace.TestingNUnit")]
    [InlineData("NarrativeTrace.Glossary")]
    public void Core_should_not_depend_on(string forbidden)
    {
        var result = Types.InAssembly(typeof(TraceNode).Assembly)
            .Should().NotHaveDependencyOn(forbidden)
            .GetResult();
        Assert.True(result.IsSuccessful, FailMessage(result));
    }

    // ── Proxy depends on Core only ────────────────────────────────────────────

    [Theory]
    [InlineData("NarrativeTrace.Logging")]
    [InlineData("NarrativeTrace.Observability")]
    [InlineData("NarrativeTrace.AspNetCore")]
    public void Proxy_should_not_depend_on(string forbidden)
    {
        var result = Types.InAssembly(typeof(NarrativeTraceProxy).Assembly)
            .Should().NotHaveDependencyOn(forbidden)
            .GetResult();
        Assert.True(result.IsSuccessful, FailMessage(result));
    }

    // ── Clarity depends on Core only ──────────────────────────────────────────

    [Theory]
    [InlineData("NarrativeTrace.Proxy")]
    [InlineData("NarrativeTrace.Logging")]
    [InlineData("NarrativeTrace.Observability")]
    [InlineData("NarrativeTrace.AspNetCore")]
    [InlineData("NarrativeTrace.Glossary")]
    public void Clarity_should_not_depend_on(string forbidden)
    {
        var result = Types.InAssembly(typeof(ClarityAnalyzer).Assembly)
            .Should().NotHaveDependencyOn(forbidden)
            .GetResult();
        Assert.True(result.IsSuccessful, FailMessage(result));
    }

    // ── Glossary depends on Core and Clarity only ─────────────────────────────

    [Theory]
    [InlineData("NarrativeTrace.Proxy")]
    [InlineData("NarrativeTrace.Diagrams")]
    [InlineData("NarrativeTrace.Logging")]
    [InlineData("NarrativeTrace.Observability")]
    [InlineData("NarrativeTrace.AspNetCore")]
    [InlineData("NarrativeTrace.TestingXunit")]
    [InlineData("NarrativeTrace.TestingNUnit")]
    public void Glossary_should_not_depend_on(string forbidden)
    {
        var result = Types.InAssembly(
                typeof(NarrativeTrace.Glossary.Glossary).Assembly)
            .Should().NotHaveDependencyOn(forbidden)
            .GetResult();
        Assert.True(result.IsSuccessful, FailMessage(result));
    }

    // ── Diagrams depends on Core only ─────────────────────────────────────────

    [Theory]
    [InlineData("NarrativeTrace.Proxy")]
    [InlineData("NarrativeTrace.Logging")]
    [InlineData("NarrativeTrace.Observability")]
    [InlineData("NarrativeTrace.AspNetCore")]
    public void Diagrams_should_not_depend_on(string forbidden)
    {
        var result = Types.InAssembly(typeof(MermaidSequenceRenderer).Assembly)
            .Should().NotHaveDependencyOn(forbidden)
            .GetResult();
        Assert.True(result.IsSuccessful, FailMessage(result));
    }

    // ── Logging depends on Core only ──────────────────────────────────────────

    [Theory]
    [InlineData("NarrativeTrace.Observability")]
    [InlineData("NarrativeTrace.AspNetCore")]
    public void Logging_should_not_depend_on(string forbidden)
    {
        var result = Types.InAssembly(typeof(LoggingNarrativeContext).Assembly)
            .Should().NotHaveDependencyOn(forbidden)
            .GetResult();
        Assert.True(result.IsSuccessful, FailMessage(result));
    }

    // ── Observability depends on Core only ────────────────────────────────────

    [Theory]
    [InlineData("NarrativeTrace.Logging")]
    [InlineData("NarrativeTrace.AspNetCore")]
    public void Observability_should_not_depend_on(string forbidden)
    {
        var result = Types.InAssembly(typeof(TraceActivityExporter).Assembly)
            .Should().NotHaveDependencyOn(forbidden)
            .GetResult();
        Assert.True(result.IsSuccessful, FailMessage(result));
    }

    private static string FailMessage(TestResult result)
    {
        if (result.FailingTypes == null)
            return "Unknown failing types";
        return "Forbidden dependency in: " +
               string.Join(", ", result.FailingTypes.Select(t => t.FullName));
    }
}
