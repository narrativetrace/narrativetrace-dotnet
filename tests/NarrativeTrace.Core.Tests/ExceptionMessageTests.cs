// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using Xunit;

namespace NarrativeTrace.Core.Tests;

/// <summary>
/// The .NET mirror of java's <c>ExceptionMessageTest</c>. The redaction claim an exception
/// message used to be outside of: <see cref="SecretValueShapes"/> exists
/// because a bearer token arrives with no name at all, and an exception message is exactly such a
/// place — the one channel <c>TraceOutcome.Threw</c> carried past every renderer as a live
/// <see cref="Exception"/> rather than text the policy had already seen.
/// </summary>
public class ExceptionMessageTests
{
    private const string Jwt = "eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiJhZGEifQ.c2lnbmF0dXJl";

    [Fact]
    public void A_credential_shaped_message_is_hidden_like_a_parameter_value()
    {
        var asParameter = ValueRenderer.Render(Jwt);
        var asMessage = ExceptionMessage.Of(new ArgumentException(Jwt));

        Assert.Equal(RedactionPolicy.Marker, asParameter);
        Assert.Equal(RedactionPolicy.Marker, asMessage);
        Assert.DoesNotContain("eyJhbGciOiJIUzI1NiJ9", asMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void A_luhn_valid_card_number_in_a_message_is_hidden_too()
    {
        Assert.Equal(
            RedactionPolicy.Marker,
            ExceptionMessage.Of(new InvalidOperationException("4111111111111111")));
    }

    // The narrowness is the point: a message is what a reader acts on, so
    // only the shapes the value axis recognises are hidden. A message that
    // merely mentions a password is diagnostics.
    [Fact]
    public void An_ordinary_diagnostic_message_is_left_alone()
    {
        Assert.Equal(
            "password rejected for user ada",
            ExceptionMessage.Of(new InvalidOperationException("password rejected for user ada")));
    }

    [Fact]
    public void A_control_character_in_a_message_is_rendered_inert()
    {
        var newline = char.ConvertFromUtf32(0x0a);
        var esc = char.ConvertFromUtf32(0x1b);
        var message = "denied" + newline + "GET /admin 200" + esc + "[31m";

        var of = ExceptionMessage.Of(new InvalidOperationException(message));

        Assert.Equal("denied\\nGET /admin 200\\u001b[31m", of);
        Assert.DoesNotContain(newline, of, StringComparison.Ordinal);
    }

    // DIVERGENT-BY-PLATFORM: Exception.Message is never actually null for an
    // ordinary .NET exception -- unlike Throwable.getMessage(), the base
    // implementation synthesizes a default string ("Exception of type ...
    // was thrown.") when none was supplied. The null branch this pins is
    // reachable only through a hostile override, which is exactly the
    // shape ExceptionMessage.Of degrades safely for -- see
    // A_message_that_cannot_be_read_degrades_instead_of_escaping.
    [Fact]
    public void An_absent_message_stays_absent_rather_than_becoming_text()
    {
        Assert.Null(ExceptionMessage.Of(new NullMessageException()));
        Assert.Equal("null", ExceptionMessage.Text(new NullMessageException()));
    }

    [Fact]
    public void A_null_exception_answers_null_instead_of_failing_the_emitter()
    {
        Assert.Null(ExceptionMessage.Of(null));
    }

    // Exception.Message is virtual, and it runs at emission time on
    // whichever thread is rendering. A getter that throws used to be able
    // to break a renderer once per emitter; an observability failure may
    // never become an application failure.
    [Fact]
    public void A_message_that_cannot_be_read_degrades_instead_of_escaping()
    {
        var hostile = new ThrowingMessageException();

        var exception = Record.Exception(() => ExceptionMessage.Of(hostile));

        Assert.Null(exception);
        Assert.Equal("<error>", ExceptionMessage.Of(hostile));
    }

    // DIVERGENT-BY-PLATFORM: java's sibling test recurses getMessage() into
    // itself to pin that a StackOverflowError degrades to the marker rather
    // than propagating. .NET's StackOverflowException cannot be caught at
    // all -- the CLR tears the process down unconditionally, by design --
    // so there is no guard that could make an infinitely-recursing Message
    // getter degrade gracefully, and a test that actually recursed would
    // crash the test host rather than assert anything. The one guard .NET
    // *can* offer -- a getter that throws an ordinary exception degrades to
    // "<error>" instead of escaping into the renderer -- is pinned above.

    private sealed class NullMessageException : Exception
    {
        public override string Message => null!;
    }

    private sealed class ThrowingMessageException : Exception
    {
        public override string Message =>
            throw new InvalidOperationException("message builder failed");
    }
}
