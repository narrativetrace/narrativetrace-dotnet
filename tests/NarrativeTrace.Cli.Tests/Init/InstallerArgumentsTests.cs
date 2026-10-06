// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Cli.Init;
using NarrativeTrace.Tooling.Init;
using Xunit;

namespace NarrativeTrace.Cli.Tests.Init;

/// <summary>
/// Flag parsing for <c>init</c>/<c>uninstall</c>, mirroring Java's <c>InstallerArgumentsTest</c>: every
/// switch and value flag, <c>--flag=value</c> and <c>--flag value</c>, first-error-wins, help-over-error,
/// and this port's additions (<c>--dir</c>, the hidden <c>--refresh</c>).
/// </summary>
public sealed class InstallerArgumentsTests
{
    private static InstallerArguments Parse(params string[] arguments) => InstallerArguments.Parse(arguments);

    [Fact]
    public void DefaultsWhenNothingIsGiven()
    {
        var parsed = Parse();

        Assert.Equal(InitOptions.Defaults(), parsed.Options);
        Assert.Null(parsed.From);
        Assert.Null(parsed.Dir);
        Assert.False(parsed.Json);
        Assert.False(parsed.Refresh);
        Assert.False(parsed.Help);
        Assert.Null(parsed.Error);
    }

    [Fact]
    public void ReadsEverySwitchFlag()
    {
        var parsed = Parse("--dry-run", "--write-existing", "--force", "--json", "--refresh");

        Assert.True(parsed.Options.DryRun);
        Assert.True(parsed.Options.WriteExisting);
        Assert.True(parsed.Options.Force);
        Assert.True(parsed.Json);
        Assert.True(parsed.Refresh);
        Assert.Null(parsed.Error);
    }

    [Fact]
    public void ReadsScopeAndVendorAndFromAndDir()
    {
        var parsed = Parse("--only", "skills", "--vendor", "claude", "--from", "/tmp/c", "--dir", "/tmp/p");

        Assert.Equal(Scope.Skills, parsed.Options.Scope);
        Assert.Equal(Vendor.On, parsed.Options.VendorClaude);
        Assert.Equal("/tmp/c", parsed.From);
        Assert.Equal("/tmp/p", parsed.Dir);
    }

    [Fact]
    public void ReadsOnlyAgentsMdAndVendorNone()
    {
        var parsed = Parse("--only", "agents-md", "--vendor", "none");

        Assert.Equal(Scope.AgentsMd, parsed.Options.Scope);
        Assert.Equal(Vendor.Off, parsed.Options.VendorClaude);
    }

    [Fact]
    public void AcceptsTheEqualsForm()
    {
        var parsed = Parse("--only=skills", "--from=/tmp/c", "--dir=/tmp/p");

        Assert.Equal(Scope.Skills, parsed.Options.Scope);
        Assert.Equal("/tmp/c", parsed.From);
        Assert.Equal("/tmp/p", parsed.Dir);
    }

    [Fact]
    public void ReadsTheHelpFlag()
    {
        Assert.True(Parse("--help").Help);
        Assert.True(Parse("-h").Help);
    }

    [Fact]
    public void AnUnknownOptionIsTheFirstError()
    {
        var parsed = Parse("--dry-run", "--nope", "--also-bad");

        Assert.Contains("--nope", parsed.Error!, StringComparison.Ordinal);
        Assert.DoesNotContain("--also-bad", parsed.Error!, StringComparison.Ordinal);
    }

    [Fact]
    public void AMissingValueIsAnError()
    {
        Assert.Contains("--from needs a value", Parse("--from").Error!, StringComparison.Ordinal);
    }

    [Fact]
    public void ABadScopeValueIsAnError()
    {
        Assert.Contains("--only takes skills or agents-md", Parse("--only", "both").Error!, StringComparison.Ordinal);
    }

    [Fact]
    public void ABadVendorValueIsAnError()
    {
        Assert.Contains("--vendor takes claude or none", Parse("--vendor", "codex").Error!, StringComparison.Ordinal);
    }

    [Fact]
    public void AnEmptyValueViaTheEqualsFormIsAnError()
    {
        Assert.Contains("--only needs a value", Parse("--only=").Error!, StringComparison.Ordinal);
        Assert.Contains("--from needs a value", Parse("--from=").Error!, StringComparison.Ordinal);
    }

    [Fact]
    public void AnUnknownOptionInTheEqualsFormIsAnError()
    {
        Assert.Contains("--nope", Parse("--nope=x").Error!, StringComparison.Ordinal);
    }

    [Fact]
    public void HelpIsReadableAlongsideAnError()
    {
        var parsed = Parse("--nope", "--help");

        Assert.True(parsed.Help);
        Assert.NotNull(parsed.Error);
    }
}
