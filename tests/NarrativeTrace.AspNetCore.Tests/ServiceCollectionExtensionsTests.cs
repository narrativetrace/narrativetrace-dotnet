// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NarrativeTrace.AspNetCore;
using NarrativeTrace.Core;
using Xunit;

namespace NarrativeTrace.AspNetCore.Tests;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void Registers_middleware()
    {
        var services = new ServiceCollection();

        services.AddNarrativeTrace();

        var provider = services.BuildServiceProvider();
        var middleware = provider
            .GetService<NarrativeTraceMiddleware>();
        Assert.NotNull(middleware);
    }

    [Fact]
    public void Registers_config_with_options()
    {
        var services = new ServiceCollection();

        services.AddNarrativeTrace(opts =>
            opts.Level = TracingLevel.Errors);

        var provider = services.BuildServiceProvider();
        var config = provider
            .GetService<NarrativeTraceConfig>();
        Assert.NotNull(config);
        Assert.Equal(
            TracingLevel.Errors, config!.Level);
    }

    [Fact]
    public void Configured_service_identity_appears_on_exported_spans()
    {
        var services = new ServiceCollection();
        services.AddNarrativeTrace(o => o.ServiceIdentity =
            new ServiceIdentity("orders", "1.2.3", "prod"));

        var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var ctx = scope.ServiceProvider
            .GetRequiredService<INarrativeContext>();
        ctx.EnterMethod("Svc", "Run", []);
        ctx.ExitMethodWithReturn(null);

        var span = ctx.CaptureTrace().Roots[0].SpanContext!;
        Assert.Equal("orders", span.ServiceName);
        Assert.Equal("1.2.3", span.ServiceVersion);
        Assert.Equal("prod", span.Environment);
    }

    [Fact]
    public void Binds_level_and_exclusions_from_configuration()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["NarrativeTrace:Level"] = "Summary",
                ["NarrativeTrace:ExcludedPaths:0"] = "/health",
            })
            .Build();
        var services = new ServiceCollection();

        services.AddNarrativeTrace(configuration);

        var provider = services.BuildServiceProvider();
        Assert.Equal(
            TracingLevel.Summary,
            provider.GetRequiredService<NarrativeTraceConfig>().Level);
        Assert.Contains(
            "/health",
            provider.GetRequiredService<NarrativeTraceOptions>()
                .ExcludedPaths);
    }

    [Fact]
    public void Code_overrides_configured_level_but_exclusions_accumulate()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["NarrativeTrace:Level"] = "Summary",
                ["NarrativeTrace:ExcludedPaths:0"] = "/health",
            })
            .Build();
        var services = new ServiceCollection();

        services.AddNarrativeTrace(configuration, o =>
        {
            o.Level = TracingLevel.Errors;
            o.ExcludedPaths.Add("/metrics");
        });

        var provider = services.BuildServiceProvider();
        Assert.Equal(
            TracingLevel.Errors,
            provider.GetRequiredService<NarrativeTraceConfig>().Level);
        Assert.Equal(
            new[] { "/health", "/metrics" },
            provider.GetRequiredService<NarrativeTraceOptions>()
                .ExcludedPaths);
    }

    [Fact]
    public void Binds_service_identity_from_configuration()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["NarrativeTrace:ServiceName"] = "orders",
                ["NarrativeTrace:ServiceVersion"] = "1.2.3",
                ["NarrativeTrace:Environment"] = "prod",
            })
            .Build();
        var services = new ServiceCollection();

        services.AddNarrativeTrace(configuration);

        var identity = services.BuildServiceProvider()
            .GetRequiredService<NarrativeTraceOptions>().ServiceIdentity;
        Assert.Equal("orders", identity!.ServiceName);
        Assert.Equal("1.2.3", identity.ServiceVersion);
        Assert.Equal("prod", identity.Environment);
    }

    [Fact]
    public void A_partially_configured_identity_leaves_the_rest_null()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["NarrativeTrace:ServiceName"] = "orders",
            })
            .Build();
        var services = new ServiceCollection();

        services.AddNarrativeTrace(configuration);

        var identity = services.BuildServiceProvider()
            .GetRequiredService<NarrativeTraceOptions>().ServiceIdentity;
        Assert.Equal("orders", identity!.ServiceName);
        Assert.Null(identity.ServiceVersion);
        Assert.Null(identity.Environment);
    }

    [Fact]
    public void No_identity_keys_means_no_identity_rather_than_an_empty_one()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["NarrativeTrace:Level"] = "Summary",
            })
            .Build();
        var services = new ServiceCollection();

        services.AddNarrativeTrace(configuration);

        Assert.Null(services.BuildServiceProvider()
            .GetRequiredService<NarrativeTraceOptions>().ServiceIdentity);
    }

    [Fact]
    public void Configuration_identity_does_not_erase_one_set_in_code()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["NarrativeTrace:Level"] = "Summary",
            })
            .Build();
        var services = new ServiceCollection();

        services.AddNarrativeTrace(
            configuration,
            options => options.ServiceIdentity = new ServiceIdentity("coded"));

        Assert.Equal(
            "coded",
            services.BuildServiceProvider()
                .GetRequiredService<NarrativeTraceOptions>()
                .ServiceIdentity!.ServiceName);
    }

    [Fact]
    public void Explicit_configure_overrides_configuration_binding()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["NarrativeTrace:Level"] = "Summary",
            })
            .Build();
        var services = new ServiceCollection();

        services.AddNarrativeTrace(
            configuration, o => o.Level = TracingLevel.Off);

        var provider = services.BuildServiceProvider();
        Assert.Equal(
            TracingLevel.Off,
            provider.GetRequiredService<NarrativeTraceConfig>().Level);
    }
}
