// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using Example.Orders;
using Microsoft.Extensions.DependencyInjection;
using NarrativeTrace.Core;
using Xunit;

namespace NarrativeTrace.DependencyInjection.Tests.Wiring;

/// <summary>
/// Proves the framework table's DI snippet (<see cref="DependencyInjectionWiring"/>): a service
/// resolved after it is the traced proxy, recording into the scope's context.
/// </summary>
public sealed class DependencyInjectionWiringTests
{
    [Fact]
    public void A_service_resolved_after_the_snippet_records_its_call_into_the_scope_context()
    {
        using var provider = DependencyInjectionWiring.Register(new ServiceCollection()).BuildServiceProvider();
        using var scope = provider.CreateScope();

        var result = scope.ServiceProvider.GetRequiredService<IOrderService>().PlaceOrder("cust-1");

        Assert.Equal("confirmed:cust-1", result);
        var call = Assert.Single(scope.ServiceProvider.GetRequiredService<INarrativeContext>().CaptureTrace().Roots);
        Assert.Equal("PlaceOrder", call.Signature.MethodName);
    }
}
