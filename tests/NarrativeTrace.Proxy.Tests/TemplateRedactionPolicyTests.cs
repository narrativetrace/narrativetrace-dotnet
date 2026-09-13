// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using NarrativeTrace.Core.Annotation;
using NarrativeTrace.Runtime;
using Xunit;

namespace NarrativeTrace.Proxy.Tests;

/// <summary>
/// Row 17c: <c>[Narrated]</c>/<c>[OnError]</c> template placeholders (resolved
/// through <see cref="NarrationResolver"/>) must honor the proxy's own
/// effective <see cref="ProxyOptions.Redaction"/> policy, the same way the
/// capture path already does — not silently fall back to
/// <see cref="RedactionPolicy.Default"/> regardless of what the proxy was
/// constructed with.
/// </summary>
public class TemplateRedactionPolicyTests
{
    public interface IAccountService
    {
        [Narrated("Renewing {policy.HolderName}")]
        void Renew(Policy policy);

        [OnError("Renewal failed for {policy.HolderName}")]
        void RenewThrowing(Policy policy);
    }

    public sealed record Policy(string PolicyId, string HolderName);

    private sealed class AccountService : IAccountService
    {
        public void Renew(Policy policy)
        {
        }

        public void RenewThrowing(Policy policy)
        {
            throw new InvalidOperationException("boom");
        }
    }

    // The reported shape: a custom policy widens the deny-list to catch a
    // plain-string PII property (Policy.HolderName) that RedactionPolicy.Default
    // does not recognize by name. Before this fix, NarrationResolver always
    // asked RedactionPolicy.Default, so the template printed the value in
    // full even though the same proxy's captured parameter would have hidden
    // it under this policy.
    [Fact]
    public void A_Narrated_template_honors_a_custom_policy_naming_a_nested_property()
    {
        var ctx = new SyncNarrativeContext(new NarrativeTraceConfig());
        var proxy = NarrativeTraceProxy.Create<IAccountService>(
            new AccountService(), ctx,
            new ProxyOptions(Redaction: RedactionPolicy.OfPatterns(["holdername"])));

        proxy.Renew(new Policy("POL-123", "Ada Lovelace"));

        var narration = ctx.CaptureTrace().Roots[0].Signature.Narration;
        Assert.NotNull(narration);
        Assert.DoesNotContain("Ada Lovelace", narration, StringComparison.Ordinal);
        Assert.Contains(RedactionPolicy.Marker, narration, StringComparison.Ordinal);
    }

    // Control: the default path is unchanged — the same template on a proxy
    // with no custom policy still shows the value, proving the previous
    // test's redaction comes from the policy, not from some other guard.
    [Fact]
    public void The_same_template_is_unchanged_under_the_default_policy()
    {
        var ctx = new SyncNarrativeContext(new NarrativeTraceConfig());
        var proxy = NarrativeTraceProxy.Create<IAccountService>(
            new AccountService(), ctx);

        proxy.Renew(new Policy("POL-123", "Ada Lovelace"));

        var narration = ctx.CaptureTrace().Roots[0].Signature.Narration;
        Assert.Equal("Renewing Ada Lovelace", narration);
    }

    // [OnError] resolves at exception time through the same NarrationResolver
    // call site — must honor the custom policy too.
    [Fact]
    public void An_OnError_template_honors_a_custom_policy_naming_a_nested_property()
    {
        var ctx = new SyncNarrativeContext(new NarrativeTraceConfig());
        var proxy = NarrativeTraceProxy.Create<IAccountService>(
            new AccountService(), ctx,
            new ProxyOptions(Redaction: RedactionPolicy.OfPatterns(["holdername"])));

        Assert.Throws<InvalidOperationException>(
            () => proxy.RenewThrowing(new Policy("POL-123", "Ada Lovelace")));

        var errorContext = ctx.CaptureTrace().Roots[0].Signature.ErrorContext;
        Assert.NotNull(errorContext);
        Assert.DoesNotContain("Ada Lovelace", errorContext, StringComparison.Ordinal);
        Assert.Contains(RedactionPolicy.Marker, errorContext, StringComparison.Ordinal);
    }

    // [NotTraced] still wins over a custom policy in a template, the same
    // guarantee the capture path carries (RedactionPolicy.Disabled cannot
    // un-redact an explicit annotation).
    public interface IAccountServiceWithNotTraced
    {
        [Narrated("Authenticating {credentials.Password}")]
        void Authenticate(Credentials credentials);
    }

    public sealed record Credentials(string User, [NotTraced] string Password);

    private sealed class AccountServiceWithNotTraced : IAccountServiceWithNotTraced
    {
        public void Authenticate(Credentials credentials)
        {
        }
    }

    [Fact]
    public void NotTraced_still_wins_in_a_template_under_RedactionPolicy_Disabled()
    {
        var ctx = new SyncNarrativeContext(new NarrativeTraceConfig());
        var proxy = NarrativeTraceProxy.Create<IAccountServiceWithNotTraced>(
            new AccountServiceWithNotTraced(), ctx,
            new ProxyOptions(Redaction: RedactionPolicy.Disabled));

        proxy.Authenticate(new Credentials("ada", "hunter2"));

        var narration = ctx.CaptureTrace().Roots[0].Signature.Narration;
        Assert.NotNull(narration);
        Assert.DoesNotContain("hunter2", narration, StringComparison.Ordinal);
        Assert.Contains(RedactionPolicy.Marker, narration, StringComparison.Ordinal);
    }
}
