// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using NarrativeTrace.Runtime;
using Xunit;

namespace NarrativeTrace.Proxy.Tests;

/// <summary>
/// The proxy's CAPTURE path (<c>NarrativeInterceptor.BuildParameterInfo</c>,
/// feeding <c>CaptureParameters</c>) must apply <see cref="RedactionPolicy"/>'s
/// name-based deny-list to PARAMETER names, exactly the way reflective
/// introspection already applies it to field/property/record-component names —
/// not only when a caller has explicitly annotated the parameter with
/// <c>[NotTraced]</c>.
/// </summary>
/// <remarks>
/// None of the fixtures below carry <c>[NotTraced]</c> anywhere: every
/// assertion here proves the NAME axis alone, at the point of capture, not
/// the annotation axis and not a renderer's own redaction backstop.
/// </remarks>
public class ParameterNameRedactionTests
{
    [Fact]
    public void A_password_parameter_with_no_attribute_is_redacted_at_capture()
    {
        var ctx = new SyncNarrativeContext(new NarrativeTraceConfig());
        var proxy = NarrativeTraceProxy.Create<IAccountService>(
            new AccountService(), ctx);

        proxy.Authenticate("ada", "hunter2");

        var parameter = ctx.CaptureTrace().Roots[0].Signature.Parameters[1];
        Assert.True(parameter.Redacted);
        Assert.Equal(RedactionPolicy.Marker, parameter.RenderedValue);
        Assert.DoesNotContain(
            "hunter2", parameter.RenderedValue, StringComparison.Ordinal);
    }

    [Fact]
    public void A_paymentToken_parameter_with_no_attribute_is_redacted_at_capture()
    {
        // The real leak found in the wild: Pay(policyId, paymentToken) —
        // "token" is in the deny-list, but paymentToken never reached it.
        var ctx = new SyncNarrativeContext(new NarrativeTraceConfig());
        var proxy = NarrativeTraceProxy.Create<IAccountService>(
            new AccountService(), ctx);

        proxy.Pay("POL-123", "tok-super-secret-xyz");

        var parameter = ctx.CaptureTrace().Roots[0].Signature.Parameters[1];
        Assert.True(parameter.Redacted);
        Assert.Equal(RedactionPolicy.Marker, parameter.RenderedValue);
        Assert.DoesNotContain(
            "tok-super-secret-xyz",
            parameter.RenderedValue,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Ordinary_parameters_stay_visible()
    {
        // The over-redaction guard: must pass both before and after the fix.
        var ctx = new SyncNarrativeContext(new NarrativeTraceConfig());
        var proxy = NarrativeTraceProxy.Create<IAccountService>(
            new AccountService(), ctx);

        proxy.PlaceOrder("ORD-42", 7);

        var parameters = ctx.CaptureTrace().Roots[0].Signature.Parameters;
        Assert.False(parameters[0].Redacted);
        Assert.Equal("\"ORD-42\"", parameters[0].RenderedValue);
        Assert.False(parameters[1].Redacted);
        Assert.Equal("7", parameters[1].RenderedValue);
    }

    [Fact]
    public void Password_parameter_redaction_holds_across_every_renderer()
    {
        // "hunter2" is neither JWT- nor card-shaped, so only the NAME axis
        // can catch it here — proves the fix reaches every renderer, not
        // just the raw capture.
        var ctx = new SyncNarrativeContext(new NarrativeTraceConfig());
        var proxy = NarrativeTraceProxy.Create<IAccountService>(
            new AccountService(), ctx);

        proxy.Authenticate("ada", "hunter2");

        var tree = ctx.CaptureTrace();
        var markdown = MarkdownRenderer.Render(tree);
        var prose = ProseRenderer.Render(tree);
        var json = JsonExporter.Export(
            tree, new TraceMetadata("Password redaction", ScenarioResult.Success));

        Assert.DoesNotContain("hunter2", markdown, StringComparison.Ordinal);
        Assert.Contains(RedactionPolicy.Marker, markdown, StringComparison.Ordinal);

        Assert.DoesNotContain("hunter2", prose, StringComparison.Ordinal);
        Assert.Contains(RedactionPolicy.Marker, prose, StringComparison.Ordinal);

        Assert.DoesNotContain("hunter2", json, StringComparison.Ordinal);
        Assert.Contains(RedactionPolicy.Marker, json, StringComparison.Ordinal);
    }

    // Monotonicity: application code can construct a NARROWER
    // RedactionPolicy (RedactionPolicy.OfPatterns replaces the built-in
    // vocabulary wholesale rather than extending it — see its own remarks),
    // but there is no seam anywhere in the proxy pipeline (ProxyOptions,
    // NarrativeTraceProxy.Create/.Create<T>, NarrativeInterceptor) that
    // accepts a RedactionPolicy at all: BuildParameterInfo is hardcoded to
    // RedactionPolicy.Default, and Cache/GetMetadata/BuildMetadata/
    // BuildParameterInfo are all `private static`, so not even a subclass
    // could override the decision. Constructing the narrower policy here
    // is the point — its mere existence changes nothing, because nothing
    // can hand it to the interceptor.
    [Fact]
    public void A_narrower_custom_policy_cannot_reach_capture()
    {
        var narrower = RedactionPolicy.OfPatterns(["ssn"]);
        Assert.False(narrower.ShouldRedact("password")); // it really is narrower

        var ctx = new SyncNarrativeContext(new NarrativeTraceConfig());
        var proxy = NarrativeTraceProxy.Create<IAccountService>(
            new AccountService(), ctx);

        proxy.Authenticate("ada", "hunter2");

        var parameter = ctx.CaptureTrace().Roots[0].Signature.Parameters[1];
        Assert.True(parameter.Redacted);
        Assert.Equal(RedactionPolicy.Marker, parameter.RenderedValue);
    }

    public interface IAccountService
    {
        void Authenticate(string user, string password);

        void Pay(string policyId, string paymentToken);

        void PlaceOrder(string orderNumber, int quantity);
    }

    private sealed class AccountService : IAccountService
    {
        public void Authenticate(string user, string password)
        {
        }

        public void Pay(string policyId, string paymentToken)
        {
        }

        public void PlaceOrder(string orderNumber, int quantity)
        {
        }
    }
}
