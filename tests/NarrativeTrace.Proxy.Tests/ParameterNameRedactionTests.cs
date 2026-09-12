// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using NarrativeTrace.Core.Annotation;
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

    // Monotonicity: merely constructing a RedactionPolicy changes nothing
    // by itself — it has to be handed to something. Proxies created with no
    // ProxyOptions (or ProxyOptions with no Redaction) keep using
    // RedactionPolicy.Default regardless of what other policy objects exist
    // in the same process.
    [Fact]
    public void A_custom_policy_never_given_to_ProxyOptions_does_not_reach_capture()
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

    // The seam: ProxyOptions.Redaction reaches BuildParameterInfo's name
    // decision (via NameRedacted) end to end. RedactionPolicy.Disabled
    // REPLACES the default decision rather than widening it (same contract
    // as RedactionPolicy.OfPatterns), so a parameter the default policy
    // would have redacted is visible once a proxy explicitly opts out.
    [Fact]
    public void A_custom_policy_given_to_ProxyOptions_replaces_the_default_at_capture()
    {
        var ctx = new SyncNarrativeContext(new NarrativeTraceConfig());
        var proxy = NarrativeTraceProxy.Create<IAccountService>(
            new AccountService(), ctx,
            new ProxyOptions(Redaction: RedactionPolicy.Disabled));

        proxy.Authenticate("ada", "hunter2");

        var parameter = ctx.CaptureTrace().Roots[0].Signature.Parameters[1];
        Assert.False(parameter.Redacted);
        Assert.Equal("\"hunter2\"", parameter.RenderedValue);
    }

    // [NotTraced] always wins, even under RedactionPolicy.Disabled — the
    // same guarantee the annotation carries everywhere else in this runtime.
    [Fact]
    public void NotTraced_still_wins_under_a_custom_policy()
    {
        var ctx = new SyncNarrativeContext(new NarrativeTraceConfig());
        var proxy = NarrativeTraceProxy.Create<IAccountService>(
            new AccountService(), ctx,
            new ProxyOptions(Redaction: RedactionPolicy.Disabled));

        proxy.PayWithAccount("POL-123", "tok-super-secret-xyz", "acct-1");

        var parameter = ctx.CaptureTrace().Roots[0].Signature.Parameters[2];
        Assert.True(parameter.Redacted);
        Assert.Equal(RedactionPolicy.Marker, parameter.RenderedValue);
    }

    // The reported leak: a plain-string PII property two levels down
    // (Policy.HolderName) is not caught by RedactionPolicy.Default, and
    // before ProxyOptions.Redaction existed, application code had no way to
    // widen the deny-list for a name like this anywhere the proxy path
    // rendered — only a hand-written ValueRenderer.Render call reached
    // RedactionPolicy.OfPatterns at all. This proves the custom policy
    // reaches a NESTED object's reflective walk too, not just top-level
    // parameter names.
    [Fact]
    public void A_custom_policy_redacts_a_nested_property_the_default_policy_misses()
    {
        var ctx = new SyncNarrativeContext(new NarrativeTraceConfig());
        var proxy = NarrativeTraceProxy.Create<IAccountService>(
            new AccountService(), ctx,
            new ProxyOptions(Redaction: RedactionPolicy.OfPatterns(["holdername"])));

        proxy.Renew(new Policy("POL-123", "Ada Lovelace"));

        var parameter = ctx.CaptureTrace().Roots[0].Signature.Parameters[0];
        Assert.False(parameter.Redacted); // the Policy argument itself has an ordinary name
        Assert.DoesNotContain("Ada Lovelace", parameter.RenderedValue, StringComparison.Ordinal);
        Assert.Contains(RedactionPolicy.Marker, parameter.RenderedValue, StringComparison.Ordinal);
    }

    // Without the custom policy, the same nested field is plainly visible —
    // proves the previous test's redaction comes from the policy, not from
    // some other guard (value-shape masking, [NotTraced], …).
    [Fact]
    public void The_same_nested_property_is_visible_under_the_default_policy()
    {
        var ctx = new SyncNarrativeContext(new NarrativeTraceConfig());
        var proxy = NarrativeTraceProxy.Create<IAccountService>(
            new AccountService(), ctx);

        proxy.Renew(new Policy("POL-123", "Ada Lovelace"));

        var parameter = ctx.CaptureTrace().Roots[0].Signature.Parameters[0];
        Assert.Contains("Ada Lovelace", parameter.RenderedValue, StringComparison.Ordinal);
    }

    public interface IAccountService
    {
        void Authenticate(string user, string password);

        void Pay(string policyId, string paymentToken);

        void PayWithAccount(
            string policyId, string paymentToken,
            [NotTraced] string accountId);

        void PlaceOrder(string orderNumber, int quantity);

        void Renew(Policy policy);
    }

    private sealed class AccountService : IAccountService
    {
        public void Authenticate(string user, string password)
        {
        }

        public void Pay(string policyId, string paymentToken)
        {
        }

        public void PayWithAccount(
            string policyId, string paymentToken, string accountId)
        {
        }

        public void PlaceOrder(string orderNumber, int quantity)
        {
        }

        public void Renew(Policy policy)
        {
        }
    }

    // The reported shape: an ordinary top-level parameter name
    // ("policy") wrapping a plain-string PII property the built-in
    // deny-list does not recognize.
    public sealed record Policy(string PolicyId, string HolderName);
}
