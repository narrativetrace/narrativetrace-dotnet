// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using Microsoft.Extensions.Logging;
using NarrativeTrace.Core;
using NarrativeTrace.Examples.Common;
using Xunit;

namespace NarrativeTrace.Examples.Common.Tests;

/// <summary>
/// The live <c>→ ← !!</c> lines must keep the shapes the demo launcher's
/// colorizer keys on (the Java <c>Slf4jTraceEventListener</c> shapes).
/// </summary>
public sealed class NarrationStreamListenerTests
{
    private readonly StringWriter _output = new();
    private readonly NarrationStreamListener _listener;

    public NarrationStreamListenerTests()
    {
        using var factory = new ConsoleLoggerFactory(_output, LogFormat.Classic);
        _listener = new NarrationStreamListener(factory.CreateLogger("narrativetrace"));
    }

    private static SpanContext Span()
    {
        return SpanContext.Create(TraceId.Empty, SpanId.Empty, null, null);
    }

    private static EnterEvent Enter(params ParameterCapture[] parameters)
    {
        return new EnterEvent(Span(), 0, new MethodSignature("OrderService", "PlaceOrder", parameters));
    }

    /// <summary>The level and message of the single line written, prefix stripped.</summary>
    private static ExitEvent Exit(TraceOutcome outcome, string? errorContext = null)
    {
        return new ExitEvent(Span(), 0, outcome, errorContext);
    }

    private string Line()
    {
        var line = _output.ToString().TrimEnd();
        var level = line.Split(' ')[2];
        return level + " " + line[(line.IndexOf(" - ", StringComparison.Ordinal) + 3)..];
    }

    [Fact]
    public void Entry_logs_the_call_with_named_parameters_at_trace_level()
    {
        _listener.OnEvent(Enter(
            new ParameterCapture("customerId", "\"cust-1\"", false),
            new ParameterCapture("quantity", "2", false)));

        Assert.Equal("TRACE → OrderService.PlaceOrder(customerId: \"cust-1\", quantity: 2)", Line());
    }

    [Fact]
    public void Redacted_parameter_prints_the_redaction_marker_not_its_value()
    {
        _listener.OnEvent(Enter(new ParameterCapture("cardToken", "\"tok_secret\"", true)));

        Assert.Equal("TRACE → OrderService.PlaceOrder(cardToken: [REDACTED])", Line());
    }

    [Fact]
    public void Return_logs_the_rendered_value()
    {
        _listener.OnEvent(Exit(new Returned("\"ORD-00001\"")));

        Assert.Equal("TRACE ← returned: \"ORD-00001\"", Line());
    }

    [Fact]
    public void Void_completion_logs_completed_without_inventing_a_value()
    {
        _listener.OnEvent(Exit(new Returned(null)));

        Assert.Equal("TRACE ← completed", Line());
    }

    [Fact]
    public void Exception_logs_type_and_message_at_warning_level()
    {
        _listener.OnEvent(Exit(new Threw(new InvalidOperationException("Insufficient stock"))));

        Assert.Equal("WARN !! InvalidOperationException: Insufficient stock", Line());
    }

    [Fact]
    public void Exception_with_an_error_narrative_appends_it_in_brackets()
    {
        _listener.OnEvent(Exit(
            new Threw(new InvalidOperationException("Insufficient stock")),
            "Insufficient stock for usb-hub, requested 9999"));

        Assert.Equal(
            "WARN !! InvalidOperationException: Insufficient stock [Insufficient stock for usb-hub, requested 9999]",
            Line());
    }

    [Fact]
    public void Exception_message_control_characters_cannot_forge_a_second_log_line()
    {
        _listener.OnEvent(Exit(new Threw(new InvalidOperationException("boom\n→ Forged.Call()"))));

        Assert.Single(_output.ToString().TrimEnd().Split(Environment.NewLine));
    }

    [Fact]
    public void Exception_without_an_error_object_still_logs_a_line()
    {
        _listener.OnEvent(Exit(new Threw(null)));

        Assert.Equal("WARN !! Unknown:", Line());
    }

    [Fact]
    public void Fork_join_lifecycle_events_log_their_group_ids()
    {
        _listener.OnEvent(new ForkCreatedEvent("g-1", 0));
        _listener.OnEvent(new MergeEvent("g-1", 2, 0, 0));
        _listener.OnEvent(new FireAndForgetEvent("g-2", 0));

        var lines = _output.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.EndsWith("- ⑂ fork group created [groupId: g-1]", lines[0], StringComparison.Ordinal);
        Assert.EndsWith("- ⑃ fork joined [groupId: g-1, members: 2]", lines[1], StringComparison.Ordinal);
        Assert.EndsWith("- ⤳ fire-and-forget launched [groupId: g-2]", lines[2], StringComparison.Ordinal);
    }

    [Fact]
    public void Events_with_no_live_line_are_ignored_and_null_is_rejected()
    {
        _listener.OnEvent(Exit(new Incomplete()));
        Assert.Equal("", _output.ToString());

        Assert.Throws<ArgumentNullException>(() => _listener.OnEvent(null!));
    }
}
