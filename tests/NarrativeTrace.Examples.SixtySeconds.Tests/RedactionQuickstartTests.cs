// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using NarrativeTrace.Proxy;
using NarrativeTrace.TestingXunit;
using Xunit;

namespace NarrativeTrace.Examples.SixtySeconds.Tests;

public sealed class RedactionQuickstartTests
{
    [Fact]
    public void Redacts_a_deny_listed_parameter()
    {
        using var narrative = new NarrativeFixture();

        narrative.Run("pay with a deny-listed parameter", context =>
        {
            var account = NarrativeTraceProxy.Create<IAccountService>(new AccountService(), context);
            account.Pay("POL-123", "tok-super-secret-xyz");
        });

        var rendered = IndentedTextRenderer.Render(narrative.CaptureTrace());
        Assert.Contains("paymentToken: [REDACTED]", rendered);
        Assert.DoesNotContain("tok-super-secret-xyz", rendered);
    }
}

public interface IAccountService
{
    string Pay(string policyId, string paymentToken);
}

public sealed class AccountService : IAccountService
{
    public string Pay(string policyId, string paymentToken) => $"paid:{policyId}";
}
