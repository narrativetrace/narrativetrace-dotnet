// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Cli;
using Xunit;

namespace NarrativeTrace.Cli.Tests;

/// <summary>
/// <c>approve</c>: an adopter's way to promote every reviewed <c>*.received.nt</c> onto its
/// <c>*.approved.nt</c> — the pin's last act, behind the user's yes. Build output is never a
/// baseline, so <c>bin</c>, <c>obj</c> and <c>.git</c> are not searched.
/// </summary>
public sealed class ApproveCommandTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "nt-approve-" + Guid.NewGuid().ToString("N"));

    public ApproveCommandTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private string Write(string relative, string content)
    {
        var path = Path.Combine(_root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    private (int Exit, string Out, string Err) Run(params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var exit = CliRunner.Run(["approve", .. args], output, error);
        return (exit, output.ToString(), error.ToString());
    }

    [Fact]
    public void Promotes_every_received_trace_onto_its_approved_trace_and_names_each()
    {
        Write("App.Tests/narratives/OrderTests/places_order.received.nt", "scenario: new\n");
        Write("App.Tests/narratives/OrderTests/places_order.approved.nt", "scenario: old\n");
        Write("Other.Tests/narratives/PayTests/pays.received.nt", "scenario: pay\n");

        var (exit, output, _) = Run("--dir", _root);

        Assert.Equal(0, exit);
        Assert.Equal("scenario: new\n", File.ReadAllText(Path.Combine(_root, "App.Tests/narratives/OrderTests/places_order.approved.nt")));
        Assert.Equal("scenario: pay\n", File.ReadAllText(Path.Combine(_root, "Other.Tests/narratives/PayTests/pays.approved.nt")));
        Assert.Empty(Directory.GetFiles(_root, "*.received.nt", SearchOption.AllDirectories));
        Assert.Contains("places_order.approved.nt", output, StringComparison.Ordinal);
        Assert.Contains("pays.approved.nt", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Leaves_build_output_and_git_internals_alone()
    {
        var inBin = Write("App.Tests/bin/Debug/net10.0/narratives/T/a.received.nt", "x");
        var inObj = Write("App.Tests/obj/T/b.received.nt", "x");
        var inGit = Write(".git/T/c.received.nt", "x");

        var (exit, output, _) = Run("--dir", _root);

        Assert.Equal(0, exit);
        Assert.True(File.Exists(inBin) && File.Exists(inObj) && File.Exists(inGit));
        Assert.Contains("nothing to approve", output, StringComparison.Ordinal);
    }

    [Fact]
    public void A_file_that_merely_contains_the_suffix_is_not_a_received_trace()
    {
        var nearMiss = Write("narratives/T/a.received.nt.bak", "x");
        var other = Write("narratives/T/a.received.ntx", "x");

        Run("--dir", _root);

        Assert.True(File.Exists(nearMiss) && File.Exists(other));
        Assert.False(File.Exists(Path.Combine(_root, "narratives/T/a.approved.nt")));
    }

    [Fact]
    public void A_directory_that_does_not_exist_is_a_usage_error()
    {
        var (exit, _, error) = Run("--dir", Path.Combine(_root, "missing"));

        Assert.Equal(2, exit);
        Assert.Contains("no such directory", error, StringComparison.Ordinal);
    }

    [Fact]
    public void Is_listed_in_the_usage_text()
    {
        var output = new StringWriter();
        CliRunner.Run(["--help"], output, new StringWriter());

        Assert.Contains("approve ", output.ToString(), StringComparison.Ordinal);
    }
}
