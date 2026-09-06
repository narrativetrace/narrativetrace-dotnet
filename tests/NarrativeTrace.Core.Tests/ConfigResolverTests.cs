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
    [InlineData("0", false)]
    [InlineData("yes", false)]
    public void Parses_output_flag(string value, bool expected)
    {
        var config = ConfigResolver.Resolve(
            Env(("NARRATIVETRACE_OUTPUT", value)), TracingLevel.Detail);

        Assert.Equal(expected, config.Output);
    }

    [Fact]
    public void Output_defaults_to_false_when_absent()
    {
        var config = ConfigResolver.Resolve(Env(), TracingLevel.Detail);

        Assert.False(config.Output);
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
}
