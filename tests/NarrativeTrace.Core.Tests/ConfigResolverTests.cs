// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using Xunit;

namespace NarrativeTrace.Core.Tests;

public class ConfigResolverTests
{
    private static Func<string, string?> Env(
        params (string Key, string Value)[] entries)
    {
        var map = new Dictionary<string, string>();
        foreach (var (key, value) in entries)
        {
            map[key] = value;
        }

        return k => map.TryGetValue(k, out var v) ? v : null;
    }

    [Fact]
    public void Resolves_level_leniently_from_env()
    {
        var config = ConfigResolver.Resolve(
            Env(("NARRATIVETRACE_LEVEL", "narrative")),
            TracingLevel.Detail);

        Assert.Equal(TracingLevel.Narrative, config.Level);
    }

    [Fact]
    public void Uses_default_level_when_env_absent()
    {
        var config = ConfigResolver.Resolve(
            Env(), TracingLevel.Summary);

        Assert.Equal(TracingLevel.Summary, config.Level);
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("TRUE", true)]
    [InlineData("1", true)]
    [InlineData("false", false)]
    [InlineData("FALSE", false)]
    [InlineData("0", false)]
    [InlineData("yes", true)]
    [InlineData("on", true)]
    [InlineData("garbage", true)]
    public void Parses_output_flag(string value, bool expected)
    {
        var config = ConfigResolver.Resolve(
            Env(("NARRATIVETRACE_OUTPUT", value)), TracingLevel.Detail);

        Assert.Equal(expected, config.Output);
    }

    [Fact]
    public void Output_defaults_to_true_when_absent()
    {
        var config = ConfigResolver.Resolve(Env(), TracingLevel.Detail);

        Assert.True(config.Output);
    }

    [Fact]
    public void Output_is_disabled_only_by_an_explicit_false_or_zero()
    {
        Assert.False(ConfigResolver.Resolve(
            Env(("NARRATIVETRACE_OUTPUT", "false")), TracingLevel.Detail).Output);
        Assert.False(ConfigResolver.Resolve(
            Env(("NARRATIVETRACE_OUTPUT", "0")), TracingLevel.Detail).Output);
    }

    [Fact]
    public void Resolves_output_dir_and_trims_blank_to_null()
    {
        Assert.Equal("/traces", ConfigResolver.Resolve(
            Env(("NARRATIVETRACE_OUTPUT_DIR", "  /traces  ")),
            TracingLevel.Detail).OutputDir);
        Assert.Null(ConfigResolver.Resolve(
            Env(("NARRATIVETRACE_OUTPUT_DIR", "   ")),
            TracingLevel.Detail).OutputDir);
    }

    [Theory]
    [InlineData("json", OutputFormat.Json)]
    [InlineData("PROSE", OutputFormat.Prose)]
    [InlineData("bogus", OutputFormat.Markdown)]
    public void Resolves_format_leniently_defaulting_to_markdown(
        string value, OutputFormat expected)
    {
        var config = ConfigResolver.Resolve(
            Env(("NARRATIVETRACE_FORMAT", value)), TracingLevel.Detail);

        Assert.Equal(expected, config.Format);
    }

    [Fact]
    public void Approval_defaults_to_off()
    {
        var config = ConfigResolver.Resolve(Env(), TracingLevel.Detail);

        Assert.False(config.Approval);
        Assert.Null(config.ApprovedDir);
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("TRUE", true)]
    [InlineData("1", true)]
    [InlineData("false", false)]
    [InlineData("garbage", false)]
    public void Resolves_the_approval_flag(string value, bool expected)
    {
        var config = ConfigResolver.Resolve(
            Env((ConfigResolver.ApprovalKey, value)), TracingLevel.Detail);

        Assert.Equal(expected, config.Approval);
    }

    [Fact]
    public void Resolves_approved_dir_and_trims_blank_to_null()
    {
        var withValue = ConfigResolver.Resolve(
            Env((ConfigResolver.ApprovedDirKey, " narratives ")), TracingLevel.Detail);
        var blank = ConfigResolver.Resolve(
            Env((ConfigResolver.ApprovedDirKey, "   ")), TracingLevel.Detail);

        Assert.Equal("narratives", withValue.ApprovedDir);
        Assert.Null(blank.ApprovedDir);
    }

    [Fact]
    public void Resolves_the_service_identity_from_its_three_variables()
    {
        var config = ConfigResolver.Resolve(Env(
            ("NARRATIVETRACE_SERVICE_NAME", "order-service"),
            ("NARRATIVETRACE_SERVICE_VERSION", "2.3.1"),
            ("NARRATIVETRACE_ENVIRONMENT", "production")));

        Assert.Equal(new ServiceIdentity("order-service", "2.3.1", "production"), config.Identity);
    }

    [Fact]
    public void Resolves_no_identity_when_none_of_the_variables_is_set()
    {
        Assert.Null(ConfigResolver.Resolve(Env()).Identity);
    }

    [Fact]
    public void Resolves_no_identity_when_every_variable_is_blank()
    {
        var config = ConfigResolver.Resolve(Env(
            ("NARRATIVETRACE_SERVICE_NAME", "  "), ("NARRATIVETRACE_ENVIRONMENT", "")));

        Assert.Null(config.Identity);
    }

    [Fact]
    public void Resolves_an_identity_from_the_variables_that_are_set_and_trims_them()
    {
        var config = ConfigResolver.Resolve(Env(("NARRATIVETRACE_SERVICE_NAME", " order-service ")));

        Assert.Equal(new ServiceIdentity("order-service"), config.Identity);
    }
}
