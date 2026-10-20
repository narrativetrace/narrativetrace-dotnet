// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using NarrativeTrace.TestingXunit;
using Xunit;

namespace NarrativeTrace.Testing.Xunit.Tests;

/// <summary>The fixture stamps the service identity the environment names onto what it captures.</summary>
public class NarrativeFixtureServiceIdentityTests
{
    private static string? ServiceOfFirstSpan(NarrativeFixture fixture)
    {
        fixture.Context.EnterMethod("InventoryService", "Reserve", []);
        fixture.Context.ExitMethodWithReturn("true");
        return fixture.CaptureTrace().Roots[0].SpanContext?.ServiceName;
    }

    [Fact]
    public void Captured_spans_carry_the_service_name_from_the_environment()
    {
        using var fixture = new NarrativeFixture(
            key => key == ConfigResolver.ServiceNameKey ? "order-service" : null);

        Assert.Equal("order-service", ServiceOfFirstSpan(fixture));
    }

    [Fact]
    public void Captured_spans_carry_no_service_name_when_the_environment_names_none()
    {
        using var fixture = new NarrativeFixture(_ => null);

        Assert.Null(ServiceOfFirstSpan(fixture));
    }
}
