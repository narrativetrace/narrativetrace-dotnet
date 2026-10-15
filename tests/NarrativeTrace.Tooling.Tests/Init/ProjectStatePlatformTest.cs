// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Tooling.Init;
using Xunit;

namespace NarrativeTrace.Tooling.Tests.Init;

/// <summary>
/// What a snapshot knows about the agent platforms a project is on (Phase 8 D2): detection by exact
/// marker path, an existing pointer file byte for byte, and a pointer whose path runs through a link.
/// </summary>
public sealed class ProjectStatePlatformTest : IDisposable
{
    private const string RulePath = ".cursor/rules/narrativetrace.mdc";

    private readonly string _dir = Path.Combine(Path.GetTempPath(), "nt-platforms-" + Guid.NewGuid().ToString("N"));

    public ProjectStatePlatformTest() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private string At(string relative) => Path.Combine(_dir, relative.Replace('/', Path.DirectorySeparatorChar));

    private void Write(string relative, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(At(relative))!);
        File.WriteAllText(At(relative), content);
    }

    [Fact]
    public void An_untouched_project_is_on_no_platform_and_has_no_pointer_files()
    {
        var state = ProjectState.NewBuilder().Build();

        Assert.Empty(state.DetectedPlatforms);
        Assert.Null(state.PointerFile(RulePath));
        Assert.False(state.PointerBehindLink("replit.md"));
    }

    [Fact]
    public void A_snapshot_keeps_the_detected_platforms_and_pointer_files_it_was_given()
    {
        var state = ProjectState.NewBuilder()
            .DetectedPlatform("cursor")
            .DetectedPlatform("replit")
            .PointerFile("replit.md", "# Replit\n")
            .PointerBehindLink(RulePath)
            .Build();

        Assert.Equal(["cursor", "replit"], state.DetectedPlatforms.Order());
        Assert.Equal("# Replit\n", state.PointerFile("replit.md"));
        Assert.True(state.PointerBehindLink(RulePath));
    }

    [Fact]
    public void The_reader_detects_cursor_by_its_directory_and_nothing_named_like_it()
    {
        Directory.CreateDirectory(At(".cursorx"));
        Directory.CreateDirectory(At("cursor"));
        Assert.Empty(ProjectStateReader.Read(_dir).DetectedPlatforms);

        Directory.CreateDirectory(At(".cursor"));

        Assert.Equal(["cursor"], ProjectStateReader.Read(_dir).DetectedPlatforms);
    }

    [Fact]
    public void The_reader_detects_replit_by_either_of_its_files()
    {
        Write(".replit", "run = \"x\"\n");
        Assert.Equal(["replit"], ProjectStateReader.Read(_dir).DetectedPlatforms);

        File.Delete(At(".replit"));
        Write("replit.md", "# Replit\n");
        Assert.Equal(["replit"], ProjectStateReader.Read(_dir).DetectedPlatforms);
    }

    [Fact]
    public void The_reader_detects_every_platform_the_project_carries_at_once()
    {
        Directory.CreateDirectory(At(".cursor"));
        Directory.CreateDirectory(At(".agents/rules"));
        Write("replit.md", "# Replit\n");

        Assert.Equal(["antigravity", "cursor", "replit"], ProjectStateReader.Read(_dir).DetectedPlatforms.Order());
    }

    /// <summary>
    /// <c>.agents/skills</c> is what <c>init</c> itself writes, so it must prove nothing: a project that
    /// ran <c>init</c> on Copilot must not come back "on Antigravity" the next time.
    /// </summary>
    [Fact]
    public void The_skills_directory_init_writes_is_not_an_antigravity_marker()
    {
        Directory.CreateDirectory(At(".agents/skills/narrativetrace-doctor"));

        Assert.Empty(ProjectStateReader.Read(_dir).DetectedPlatforms);
    }

    [Fact]
    public void Antigravity_is_detected_by_its_legacy_agent_directory_too()
    {
        Directory.CreateDirectory(At(".agent/workflows"));

        Assert.Equal(["antigravity"], ProjectStateReader.Read(_dir).DetectedPlatforms);
    }

    [Fact]
    public void The_reader_reads_an_existing_pointer_file_byte_for_byte_including_a_byte_order_mark()
    {
        Write("replit.md", "﻿# Replit\r\nnotes");
        Write(RulePath, "---\n---\nrule");

        var state = ProjectStateReader.Read(_dir);

        Assert.Equal("﻿# Replit\r\nnotes", state.PointerFile("replit.md"));
        Assert.Equal("---\n---\nrule", state.PointerFile(RulePath));
        Assert.False(state.PointerBehindLink("replit.md"));
    }

    [Fact]
    public void A_pointer_behind_a_symbolic_link_is_reported_and_never_read()
    {
        Directory.CreateDirectory(At("elsewhere"));
        Write("elsewhere/narrativetrace.mdc", "somebody else's file");
        Directory.CreateDirectory(At(".cursor"));
        Directory.CreateSymbolicLink(At(".cursor/rules"), At("elsewhere"));
        Write("real.md", "real");
        File.CreateSymbolicLink(At("replit.md"), At("real.md"));

        var state = ProjectStateReader.Read(_dir);

        Assert.True(state.PointerBehindLink(RulePath));
        Assert.Null(state.PointerFile(RulePath));
        Assert.True(state.PointerBehindLink("replit.md"));
        Assert.Null(state.PointerFile("replit.md"));
    }

    [Fact]
    public void A_marker_that_is_a_symbolic_link_still_counts_as_the_platform_being_present()
    {
        Directory.CreateDirectory(At("real-cursor"));
        Directory.CreateSymbolicLink(At(".cursor"), At("real-cursor"));

        var state = ProjectStateReader.Read(_dir);

        Assert.Equal(["cursor"], state.DetectedPlatforms);
        Assert.True(state.PointerBehindLink(RulePath));
    }
}
