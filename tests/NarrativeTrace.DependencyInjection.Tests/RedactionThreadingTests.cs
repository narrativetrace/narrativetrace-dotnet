// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using Microsoft.Extensions.DependencyInjection;
using NarrativeTrace.Core;
using NarrativeTrace.DependencyInjection;
using Xunit;

namespace NarrativeTrace.DependencyInjection.Tests;

/// <summary>
/// <c>NarrativeTracingDiOptions.Redaction</c> reaches the
/// <c>ProxyOptions.Redaction</c> hook of every service <c>AddNarrativeTracing</c>
/// auto-wraps — the same "study cell" shape as
/// <c>NarrativeTrace.Proxy.Tests.ParameterNameRedactionTests</c>: a
/// plain-string PII property two levels down (<c>Policy.HolderName</c>) that
/// <see cref="RedactionPolicy.Default"/> does not recognize by name.
/// </summary>
public class RedactionThreadingTests
{
    private const string TestNamespace =
        "NarrativeTrace.DependencyInjection.Tests";

    public interface IPolicyService
    {
        void Renew(Policy policy);
    }

    public sealed record Policy(string PolicyId, string HolderName);

    private sealed class PolicyService : IPolicyService
    {
        public void Renew(Policy policy)
        {
        }
    }

    [Fact]
    public void A_custom_policy_configured_once_reaches_every_auto_wrapped_proxy()
    {
        var services = new ServiceCollection();
        services.AddScoped<IPolicyService, PolicyService>();
        services.AddNarrativeTracing(o => o
            .Namespaces(TestNamespace)
            .Redaction = RedactionPolicy.OfPatterns(["holdername"]));

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var sp = scope.ServiceProvider;
        sp.GetRequiredService<IPolicyService>()
            .Renew(new Policy("POL-123", "Ada Lovelace"));

        var parameter = sp.GetRequiredService<INarrativeContext>()
            .CaptureTrace().Roots[0].Signature.Parameters[0];
        Assert.False(parameter.Redacted); // the Policy argument's own name is ordinary
        Assert.DoesNotContain(
            "Ada Lovelace", parameter.RenderedValue, StringComparison.Ordinal);
        Assert.Contains(
            RedactionPolicy.Marker, parameter.RenderedValue, StringComparison.Ordinal);
    }

    // Control: leaving Redaction unset keeps today's behavior — the same
    // nested field is plainly visible under RedactionPolicy.Default, so the
    // previous test's redaction is proven to come from the option, not from
    // some other guard.
    [Fact]
    public void Leaving_Redaction_unset_keeps_the_default_policy()
    {
        var services = new ServiceCollection();
        services.AddScoped<IPolicyService, PolicyService>();
        services.AddNarrativeTracing(o => o.Namespaces(TestNamespace));

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var sp = scope.ServiceProvider;
        sp.GetRequiredService<IPolicyService>()
            .Renew(new Policy("POL-123", "Ada Lovelace"));

        var parameter = sp.GetRequiredService<INarrativeContext>()
            .CaptureTrace().Roots[0].Signature.Parameters[0];
        Assert.Contains(
            "Ada Lovelace", parameter.RenderedValue, StringComparison.Ordinal);
    }
}
