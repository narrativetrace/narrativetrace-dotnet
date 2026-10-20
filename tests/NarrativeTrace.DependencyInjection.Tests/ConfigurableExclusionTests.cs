// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using Microsoft.Extensions.DependencyInjection;
using NarrativeTrace.DependencyInjection.Tests.Contracts;
using Xunit;

namespace NarrativeTrace.DependencyInjection.Tests;

/// <summary>List 2: the configurable exclude list, its shipped default, and how it combines with include.</summary>
public class ConfigurableExclusionTests
{
    private const string TestNamespace = "NarrativeTrace.DependencyInjection.Tests";

    private static readonly Func<string, string?> NoEnvironment = _ => null;

    private static Func<string, string?> Env(string? value) =>
        key => key == InstrumentationExclusions.EnvironmentKey ? value : null;

    private static (T Resolved, string Report) Resolve<T>(
        Action<IServiceCollection> register,
        Action<NarrativeTracingDiOptions> options,
        Func<string, string?>? env = null) where T : class
    {
        var services = new ServiceCollection();
        register(services);
        var diagnostics = new StringWriter();
        services.AddNarrativeTracing(options, env ?? NoEnvironment, diagnostics);
        using var provider = services.BuildServiceProvider();
        return (provider.GetRequiredService<T>(), diagnostics.ToString());
    }

    private static bool IsWrapped(object resolved, Type implementation) => resolved.GetType() != implementation;

    // --- shipped default -------------------------------------------------

    [Fact]
    public void Shipped_default_excludes_a_mocking_library_namespace_even_when_included()
    {
        var (resolved, _) = Resolve<Moq.Probe.IMocked>(
            s => s.AddSingleton<Moq.Probe.IMocked, Moq.Probe.Mocked>(), o => o.Namespaces("Moq"));

        Assert.False(IsWrapped(resolved, typeof(Moq.Probe.Mocked)));
    }

    [Fact]
    public void Shipped_default_excludes_a_test_runner_namespace_even_when_included()
    {
        var (resolved, _) = Resolve<Xunit.Probe.IRunnerHelper>(
            s => s.AddSingleton<Xunit.Probe.IRunnerHelper, Xunit.Probe.RunnerHelper>(), o => o.Namespaces("Xunit"));

        Assert.False(IsWrapped(resolved, typeof(Xunit.Probe.RunnerHelper)));
    }

    [Fact]
    public void Default_list_is_nonempty_unique_and_normalised()
    {
        var list = InstrumentationExclusions.DefaultExclude;

        Assert.NotEmpty(list);
        Assert.Equal(list.Count, list.Distinct(StringComparer.Ordinal).Count());
        Assert.All(list, p => Assert.Equal(p.Trim().TrimEnd('.'), p));
        Assert.Contains("Microsoft.Extensions", list);
        Assert.Contains("Moq", list);
    }

    // --- replace, not merge ------------------------------------------------

    [Fact]
    public void Explicit_option_replaces_the_default()
    {
        var (resolved, _) = Resolve<Moq.Probe.IMocked>(
            s => s.AddSingleton<Moq.Probe.IMocked, Moq.Probe.Mocked>(),
            o => o.Namespaces("Moq").ExcludeNamespaces("Unrelated"));

        Assert.True(IsWrapped(resolved, typeof(Moq.Probe.Mocked)));
    }

    [Fact]
    public void Environment_key_replaces_the_default()
    {
        var (resolved, _) = Resolve<Moq.Probe.IMocked>(
            s => s.AddSingleton<Moq.Probe.IMocked, Moq.Probe.Mocked>(), o => o.Namespaces("Moq"), Env("Unrelated"));

        Assert.True(IsWrapped(resolved, typeof(Moq.Probe.Mocked)));
    }

    [Fact]
    public void Environment_key_present_but_empty_disables_the_default()
    {
        var (resolved, _) = Resolve<Moq.Probe.IMocked>(
            s => s.AddSingleton<Moq.Probe.IMocked, Moq.Probe.Mocked>(), o => o.Namespaces("Moq"), Env(""));

        Assert.True(IsWrapped(resolved, typeof(Moq.Probe.Mocked)));
    }

    [Fact]
    public void Environment_key_excludes_a_namespace_the_default_does_not_name()
    {
        var (resolved, _) = Resolve<IGreeter>(
            s => s.AddSingleton<IGreeter, Greeter>(), o => o.Namespaces(TestNamespace), Env(TestNamespace));

        Assert.False(IsWrapped(resolved, typeof(Greeter)));
    }

    [Fact]
    public void Option_wins_over_environment_when_both_are_given()
    {
        var (resolved, _) = Resolve<IGreeter>(
            s => s.AddSingleton<IGreeter, Greeter>(),
            o => o.Namespaces(TestNamespace).ExcludeNamespaces("Unrelated"), Env(TestNamespace));

        Assert.True(IsWrapped(resolved, typeof(Greeter)));
    }

    [Fact]
    public void Giving_the_option_twice_is_a_duplicate_and_throws()
    {
        var options = new NarrativeTracingDiOptions().ExcludeNamespaces("A");

        Assert.Throws<InvalidOperationException>(() => options.ExcludeNamespaces("B"));
    }

    // --- include and exclude together ------------------------------------

    [Fact]
    public void Exclude_wins_over_include_on_the_implementation_namespace()
    {
        var (resolved, _) = Resolve<IGreeter>(
            s => s.AddSingleton<IGreeter, Greeter>(),
            o => o.Namespaces(TestNamespace).ExcludeNamespaces(TestNamespace));

        Assert.False(IsWrapped(resolved, typeof(Greeter)));
    }

    [Fact]
    public void Exclude_matching_only_the_implementation_namespace_still_excludes()
    {
        var (resolved, _) = Resolve<IBridge>(
            s => s.AddSingleton<IBridge, Acme.Hidden.Bridge>(),
            o => o.Namespaces(TestNamespace, "Acme").ExcludeNamespaces("Acme.Hidden"));

        Assert.False(IsWrapped(resolved, typeof(Acme.Hidden.Bridge)));
    }

    [Fact]
    public void Included_and_not_excluded_service_is_still_traced()
    {
        var (resolved, _) = Resolve<IGreeter>(
            s => s.AddSingleton<IGreeter, Greeter>(), o => o.Namespaces(TestNamespace));

        Assert.True(IsWrapped(resolved, typeof(Greeter)));
    }

    // --- prefix boundaries -----------------------------------------------

    [Fact]
    public void Excluding_a_namespace_does_not_exclude_a_sibling_sharing_its_prefix()
    {
        var (excluded, _) = Resolve<Acme.Impl.IInner>(
            s => s.AddSingleton<Acme.Impl.IInner, Acme.Impl.Inner>(),
            o => o.Namespaces("Acme").ExcludeNamespaces("Acme.Impl"));
        var (sibling, _) = Resolve<Acme.Implx.ISibling>(
            s => s.AddSingleton<Acme.Implx.ISibling, Acme.Implx.Sibling>(),
            o => o.Namespaces("Acme").ExcludeNamespaces("Acme.Impl"));

        Assert.False(IsWrapped(excluded, typeof(Acme.Impl.Inner)));
        Assert.True(IsWrapped(sibling, typeof(Acme.Implx.Sibling)));
    }

    [Theory]
    [InlineData("Acme.Impl.*")]
    [InlineData("Acme.Impl.")]
    [InlineData("  Acme.Impl  ")]
    public void Trailing_dot_and_wildcard_spellings_mean_the_namespace_and_its_children(string spelling)
    {
        var (resolved, _) = Resolve<Acme.Impl.IInner>(
            s => s.AddSingleton<Acme.Impl.IInner, Acme.Impl.Inner>(),
            o => o.Namespaces("Acme").ExcludeNamespaces(spelling));

        Assert.False(IsWrapped(resolved, typeof(Acme.Impl.Inner)));
    }

    [Fact]
    public void Environment_value_is_split_on_semicolons_ignoring_blanks()
    {
        Assert.Equal(["A", "B.C", "D"], InstrumentationExclusions.Parse(" A ;; B.C.* ;D. ; "));
        Assert.Empty(InstrumentationExclusions.Parse(""));
        Assert.Empty(InstrumentationExclusions.Parse(null));
    }

    [Fact]
    public void Matching_prefix_reports_the_longest_configured_prefix_that_matches()
    {
        var matcher = new NamespaceMatcher(["Acme", "Acme.Impl"]);

        Assert.Equal("Acme.Impl", matcher.MatchingPrefix("Acme.Impl.Deep"));
        Assert.Equal("Acme", matcher.MatchingPrefix("Acme.Implx"));
        Assert.Null(matcher.MatchingPrefix("Acmex"));
        Assert.Null(matcher.MatchingPrefix(null));
    }

    // --- reporting ---------------------------------------------------------

    [Fact]
    public void One_report_per_matched_prefix_not_per_target()
    {
        var (_, report) = Resolve<Moq.Probe.IMocked>(
            s =>
            {
                s.AddSingleton<Moq.Probe.IMocked, Moq.Probe.Mocked>();
                s.AddSingleton<Moq.Probe.IMockedToo, Moq.Probe.MockedToo>();
            },
            o => o.Namespaces("Moq"));

        Assert.Single(Lines(report));
        Assert.Contains("Moq", report);
    }

    [Fact]
    public void Each_distinct_matched_prefix_is_reported_once()
    {
        var (_, report) = Resolve<Moq.Probe.IMocked>(
            s =>
            {
                s.AddSingleton<Moq.Probe.IMocked, Moq.Probe.Mocked>();
                s.AddSingleton<Xunit.Probe.IRunnerHelper, Xunit.Probe.RunnerHelper>();
            },
            o => o.Namespaces("Moq", "Xunit"));

        var lines = Lines(report);
        Assert.Equal(2, lines.Length);
        Assert.Single(lines, l => l.Contains("Moq"));
        Assert.Single(lines, l => l.Contains("Xunit"));
    }

    [Fact]
    public void A_target_the_include_list_never_selected_is_not_reported()
    {
        var (_, report) = Resolve<IGreeter>(
            s => s.AddSingleton<IGreeter, Greeter>().AddSingleton<Moq.Probe.IMocked, Moq.Probe.Mocked>(),
            o => o.Namespaces(TestNamespace));

        Assert.Empty(Lines(report));
    }

    // --- cannot reach (B-125 principle) ------------------------------------

    [Fact]
    public void A_wrap_that_throws_returns_the_original_and_reports_once()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IUnproxyable, Unproxyable>();
        var diagnostics = new StringWriter();
        services.AddNarrativeTracing(o => o.Namespaces(TestNamespace), NoEnvironment, diagnostics);
        using var provider = services.BuildServiceProvider();

        using var first = provider.CreateScope();
        using var second = provider.CreateScope();
        var one = first.ServiceProvider.GetRequiredService<IUnproxyable>();
        var two = second.ServiceProvider.GetRequiredService<IUnproxyable>();

        Assert.IsType<Unproxyable>(one);
        Assert.IsType<Unproxyable>(two);
        Assert.Single(Lines(diagnostics.ToString()));
        Assert.Contains(nameof(IUnproxyable), diagnostics.ToString());
    }

    private static string[] Lines(string text) =>
        text.Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries);
}

public interface IUnproxyable
{
    [NarrativeTrace.Core.Annotation.NotTraced]
    string Ping();
}

public sealed class Unproxyable : IUnproxyable
{
    public string Ping() => "u";
}
