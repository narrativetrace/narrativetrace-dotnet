// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Cli;
using Xunit;

namespace NarrativeTrace.Cli.Tests.Init;

/// <summary>
/// The platform pointers through the real <c>init</c> / <c>uninstall</c> dispatch (Phase 8 D2), from
/// the CLI assembly into the tooling assembly: a project on Cursor and Replit gets both pointers, one
/// whose directory merely resembles a marker gets neither, and the usage text says so.
/// </summary>
public sealed class PlatformPointerVerbTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "nt-pointer-verb-" + Guid.NewGuid().ToString("N"));
    private readonly string _carrier;
    private readonly string _project;
    private readonly StringWriter _out = new();
    private readonly StringWriter _err = new();

    public PlatformPointerVerbTests()
    {
        Directory.CreateDirectory(_root);
        _carrier = TestCarriers.Write(Path.Combine(_root, "carrier"), "1.2.3");
        _project = Path.Combine(_root, "project");
        Directory.CreateDirectory(_project);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private int Init(params string[] extra) =>
        CliRunner.Run(["init", "--from", _carrier, "--dir", _project, .. extra], _out, _err);

    private int Uninstall() => CliRunner.Run(["uninstall", "--dir", _project], _out, _err);

    private string At(string relative) => Path.Combine(_project, relative.Replace('/', Path.DirectorySeparatorChar));

    [Fact]
    public void A_project_on_cursor_and_replit_gets_both_pointers_and_the_uninstall_takes_them_away()
    {
        Directory.CreateDirectory(At(".cursor"));
        File.WriteAllText(At(".replit"), "run = \"x\"\n");

        Assert.Equal(0, Init());

        Assert.Contains("alwaysApply: true", File.ReadAllText(At(".cursor/rules/narrativetrace.mdc")), StringComparison.Ordinal);
        Assert.Contains("narrativetrace:start NarrativeTrace.Skills/1.2.3", File.ReadAllText(At("replit.md")), StringComparison.Ordinal);

        Assert.Equal(0, Uninstall());
        Assert.False(File.Exists(At(".cursor/rules/narrativetrace.mdc")));
        Assert.False(File.Exists(At("replit.md")));
        Assert.True(Directory.Exists(At(".cursor")));
    }

    [Fact]
    public void The_dry_run_names_the_pointer_it_would_write_and_writes_nothing()
    {
        Directory.CreateDirectory(At(".cursor"));

        Assert.Equal(0, Init("--dry-run"));

        Assert.Contains(".cursor/rules/narrativetrace.mdc", _out.ToString(), StringComparison.Ordinal);
        Assert.False(File.Exists(At(".cursor/rules/narrativetrace.mdc")));
    }

    [Fact]
    public void A_directory_that_only_resembles_a_marker_gets_no_pointer()
    {
        Directory.CreateDirectory(At(".cursorx"));

        Assert.Equal(0, Init());

        Assert.False(Directory.Exists(At(".cursor")));
        Assert.False(File.Exists(At("replit.md")));
    }

    [Fact]
    public void A_replit_md_that_exists_without_our_section_is_refused_until_write_existing_says_otherwise()
    {
        File.WriteAllText(At("replit.md"), "# My Repl\n");

        Assert.Equal(1, Init());
        Assert.Equal("# My Repl\n", File.ReadAllText(At("replit.md")));
        Assert.Equal(0, Init("--write-existing"));
        Assert.Contains("narrativetrace:start", File.ReadAllText(At("replit.md")), StringComparison.Ordinal);
    }

    [Fact]
    public void The_usage_text_names_the_platform_pointers()
    {
        CliRunner.Run(["init", "--help"], _out, _err);

        Assert.Contains("replit.md", _out.ToString(), StringComparison.Ordinal);
        Assert.Contains(".cursor/rules/narrativetrace.mdc", _out.ToString(), StringComparison.Ordinal);
    }
}
