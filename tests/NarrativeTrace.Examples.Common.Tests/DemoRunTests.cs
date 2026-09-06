// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using NarrativeTrace.Examples.Common;
using NarrativeTrace.Proxy;
using Xunit;

namespace NarrativeTrace.Examples.Common.Tests;

/// <summary>
/// The shared scenario driver: one logger for the example's own prose, one
/// live event stream, and Java's section markers — so every example prints
/// the same shapes and the launcher can walk any of them.
/// </summary>
public sealed class DemoRunTests : IDisposable
{
    private readonly StringWriter _output = new();
    private readonly DemoRun _run;

    public DemoRunTests()
    {
        _run = new DemoRun(new ConsoleLoggerFactory(_output, LogFormat.Bare), "demo");
    }

    public void Dispose()
    {
        _run.Dispose();
        _output.Dispose();
    }

    public interface IGreeter
    {
        string Greet(string name);
    }

    public sealed class Greeter : IGreeter
    {
        public string Greet(string name)
        {
            return "hello " + name;
        }
    }

    private IGreeter TracedGreeter()
    {
        return NarrativeTraceProxy.Create<IGreeter>(new Greeter(), _run.Context);
    }

    private string[] Lines()
    {
        return _output.ToString().Split(Environment.NewLine);
    }

    [Fact]
    public void Scenario_header_is_the_title_between_triple_equals_followed_by_a_blank_line()
    {
        _run.BeginScenario("Scenario 1: Successful Order");

        Assert.Equal(["=== Scenario 1: Successful Order ===", "", ""], Lines());
    }

    [Fact]
    public void Calls_through_the_run_context_stream_live_entry_and_return_lines()
    {
        _run.BeginScenario("S");

        TracedGreeter().Greet("Ada");

        Assert.Equal("→ IGreeter.Greet(name: \"Ada\")", Lines()[2]);
        Assert.Equal("← returned: \"hello Ada\"", Lines()[3]);
    }

    [Fact]
    public void Trace_tree_section_prints_the_marker_then_the_indented_rendering()
    {
        TracedGreeter().Greet("Ada");
        _output.GetStringBuilder().Clear();

        _run.TraceTree(_run.Context.CaptureTrace());

        Assert.Equal(["", "--- Trace tree ---", "", ""], Lines()[..4]);
        Assert.StartsWith("└── IGreeter.Greet(name: \"Ada\") → \"hello Ada\"", Lines()[4], StringComparison.Ordinal);
    }

    [Fact]
    public void Classic_lines_carry_a_sequential_trace_id_per_scenario_like_a_request_bound_mdc()
    {
        using var classic = new StringWriter();
        using var run = new DemoRun(new ConsoleLoggerFactory(classic, LogFormat.Classic), "demo");

        run.BeginScenario("first");
        run.BeginScenario("second");
        NarrativeTraceProxy.Create<IGreeter>(new Greeter(), run.Context).Greet("Ada");

        var lines = classic.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Contains("[trace-001] [demo] - === first ===", lines[0], StringComparison.Ordinal);
        Assert.Contains("[trace-002] [demo] - === second ===", lines[1], StringComparison.Ordinal);
        Assert.Contains("[trace-002] [narrativetrace] - → IGreeter", lines[2], StringComparison.Ordinal);
    }

    [Fact]
    public void Beginning_a_scenario_resets_the_context_so_each_trace_tells_one_story()
    {
        _run.BeginScenario("first");
        TracedGreeter().Greet("Ada");
        _run.BeginScenario("second");
        TracedGreeter().Greet("Grace");

        var root = Assert.Single(_run.Context.CaptureTrace().Roots);
        Assert.Equal("\"Grace\"", root.Signature.Parameters[0].RenderedValue);
    }

    [Fact]
    public void Prose_mermaid_and_plantuml_sections_each_open_with_their_marker()
    {
        TracedGreeter().Greet("Ada");
        var trace = _run.Context.CaptureTrace();
        _output.GetStringBuilder().Clear();

        _run.Prose(trace);
        _run.Mermaid(trace);
        _run.PlantUml(trace);

        var text = _output.ToString();
        Assert.Contains("\n--- Prose ---\n\n\nThe ", text.Replace("\r", "", StringComparison.Ordinal), StringComparison.Ordinal);
        Assert.Contains("\n--- Mermaid ---\n\n\nsequenceDiagram", text.Replace("\r", "", StringComparison.Ordinal), StringComparison.Ordinal);
        Assert.Contains("\n--- PlantUML ---\n\n\n@startuml", text.Replace("\r", "", StringComparison.Ordinal), StringComparison.Ordinal);
    }

    [Fact]
    public void Info_prints_free_text_verbatim_even_when_it_contains_braces()
    {
        _run.Info("  ^ Notice: {productId} was reserved but never released.");

        Assert.Equal("  ^ Notice: {productId} was reserved but never released.", Lines()[0]);
    }

    [Fact]
    public void Create_names_an_unnamed_calling_thread_main_for_the_classic_thread_column()
    {
        string? name = null;
        var thread = new Thread(() =>
        {
            using var run = DemoRun.Create("demo", new DemoOptions(Classic: true));
            name = Thread.CurrentThread.Name;
        });

        thread.Start();
        thread.Join();

        Assert.Equal("main", name);
    }
}
