// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using Microsoft.Extensions.DependencyInjection;
using NarrativeTrace.Core;
using NarrativeTrace.DependencyInjection;
using Xunit;

namespace NarrativeTrace.DependencyInjection.Tests;

/// <summary>The auto-wrap stamps the identity the environment names, and a code-set identity beats it.</summary>
public class ServiceIdentityEnvironmentTests
{
    private static NarrativeTraceConfig ConfigWith(
        Func<string, string?> env, Action<NarrativeTracingDiOptions>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddNarrativeTracing(o => configure?.Invoke(o), env, TextWriter.Null);
        using var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<NarrativeTraceConfig>();
    }

    [Fact]
    public void The_registered_config_carries_the_identity_from_the_environment()
    {
        var config = ConfigWith(key => key == ConfigResolver.ServiceNameKey ? "order-service" : null);

        Assert.Equal("order-service", config.ServiceIdentity?.ServiceName);
    }

    [Fact]
    public void The_registered_config_carries_no_identity_when_the_environment_names_none()
    {
        Assert.Null(ConfigWith(_ => null).ServiceIdentity);
    }

    [Fact]
    public void An_identity_set_in_code_beats_the_environment()
    {
        var config = ConfigWith(
            key => key == ConfigResolver.ServiceNameKey ? "from-env" : null,
            o => o.ServiceIdentity = new ServiceIdentity("from-code"));

        Assert.Equal("from-code", config.ServiceIdentity?.ServiceName);
    }
}
