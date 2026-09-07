// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using NarrativeTrace.Core.Annotation;
using NarrativeTrace.Runtime;
using Xunit;

namespace NarrativeTrace.Proxy.Tests;

/// <summary>
/// A bare scalar placeholder must obey both redaction axes, the way a path
/// placeholder and a whole-object placeholder already do.
/// </summary>
/// <remarks>
/// The same fix every runtime carries ("a scalar placeholder obeys both redaction axes").
/// The third production of the placeholder grammar
/// asked nothing at all: <c>{card.cvv}</c> routed through
/// <see cref="NarrationResolver"/>'s path-redaction check, <c>{card}</c>
/// routed through <c>ValueRenderer</c> (see
/// <c>RedactedObjectPlaceholderTests</c>), but a bare <c>{password}</c>
/// naming a scalar called the value's own <c>ToString()</c> and printed
/// it — the name axis never saw the placeholder key (it <em>is</em> the
/// parameter name), and the value axis never saw the bytes.
/// </remarks>
public class ScalarPlaceholderRedactionTests
{
    [Fact]
    public void A_bare_placeholder_naming_a_sensitive_parameter_is_redacted()
    {
        var ctx = new SyncNarrativeContext(new NarrativeTraceConfig());
        var proxy = NarrativeTraceProxy.Create<IAuthService>(new AuthService(), ctx);

        proxy.Authenticate("hunter2");

        var narration = ctx.CaptureTrace().Roots[0].Signature.Narration;
        Assert.Equal(RedactionPolicy.Marker, narration);
        Assert.DoesNotContain("hunter2", narration ?? "", StringComparison.Ordinal);
    }

    [Fact]
    public void A_bare_placeholder_naming_an_explicitly_not_traced_parameter_is_redacted()
    {
        var ctx = new SyncNarrativeContext(new NarrativeTraceConfig());
        var proxy = NarrativeTraceProxy.Create<IAuthService>(new AuthService(), ctx);

        proxy.Whisper("shh");

        var narration = ctx.CaptureTrace().Roots[0].Signature.Narration;
        Assert.Equal(RedactionPolicy.Marker, narration);
    }

    [Fact]
    public void A_bare_placeholder_whose_value_is_credential_shaped_is_redacted_under_an_innocent_name()
    {
        const string jwt = "eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiJhZGEifQ.c2lnbmF0dXJl";
        var ctx = new SyncNarrativeContext(new NarrativeTraceConfig());
        var proxy = NarrativeTraceProxy.Create<IAuthService>(new AuthService(), ctx);

        proxy.Issue(jwt);

        var narration = ctx.CaptureTrace().Roots[0].Signature.Narration;
        Assert.NotNull(narration);
        Assert.Contains(RedactionPolicy.Marker, narration, StringComparison.Ordinal);
        Assert.DoesNotContain(jwt, narration, StringComparison.Ordinal);
    }

    // An unresolvable placeholder (no matching parameter) must still stay
    // literal so the unresolved-placeholder warning keeps catching a typo —
    // the name axis is asked only once a value exists.
    [Fact]
    public void A_placeholder_naming_no_parameter_stays_literal()
    {
        var ctx = new SyncNarrativeContext(new NarrativeTraceConfig());
        var proxy = NarrativeTraceProxy.Create<IAuthService>(new AuthService(), ctx);

        proxy.Greet("Ada");

        var narration = ctx.CaptureTrace().Roots[0].Signature.Narration;
        Assert.Equal("{missing}", narration);
    }

    // The false-positive direction: an ordinary, innocuously-named scalar
    // placeholder still narrates plainly, unquoted.
    [Fact]
    public void An_ordinary_bare_placeholder_is_still_unquoted_and_plain()
    {
        var ctx = new SyncNarrativeContext(new NarrativeTraceConfig());
        var proxy = NarrativeTraceProxy.Create<IAuthService>(new AuthService(), ctx);

        proxy.Greet2("order-42");

        Assert.Equal(
            "hello order-42",
            ctx.CaptureTrace().Roots[0].Signature.Narration);
    }

    public interface IAuthService
    {
        [Narrated("{password}")]
        void Authenticate(string password);

        [Narrated("{secret}")]
        void Whisper([NotTraced] string secret);

        [Narrated("issued {token}")]
        void Issue(string token);

        [Narrated("{missing}")]
        void Greet(string name);

        [Narrated("hello {value}")]
        void Greet2(string value);
    }

    private sealed class AuthService : IAuthService
    {
        public void Authenticate(string password) { }

        public void Whisper(string secret) { }

        public void Issue(string token) { }

        public void Greet(string name) { }

        public void Greet2(string value) { }
    }
}
