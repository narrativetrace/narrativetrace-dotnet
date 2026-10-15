// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Tooling.Platforms;
using Xunit;

namespace NarrativeTrace.Tooling.Tests.Platforms;

/// <summary>
/// The platform table's rows (Phase 8 D1): ids are a cross-port contract, detection reads exact
/// paths, and a row says what <c>init</c> writes for its platform.
/// </summary>
public sealed class PlatformTableTests
{
    private static PlatformRow Row(string id) => PlatformTable.Row(id) ?? throw new InvalidOperationException(id);

    [Fact]
    public void The_rows_are_the_five_platforms_in_their_published_order()
    {
        Assert.Equal(
            ["cursor", "replit", "antigravity", "github-copilot", "claude-code"],
            PlatformTable.Rows.Select(row => row.Id));
    }

    [Fact]
    public void Cursor_is_detected_by_its_directory_and_pointed_at_by_one_rule()
    {
        var cursor = Row("cursor");

        Assert.Equal([".cursor"], cursor.MarkerPaths);
        var rule = Assert.IsType<Pointer.Rule>(cursor.Pointer);
        Assert.Equal(".cursor/rules/narrativetrace.mdc", rule.Path);
        Assert.Contains(".agents/skills", rule.Text, StringComparison.Ordinal);
        Assert.Contains("doctor", rule.Text, StringComparison.Ordinal);
        Assert.Equal("cursor", cursor.EvidenceLane);
    }

    [Fact]
    public void Replit_is_detected_by_either_of_its_files_and_carries_the_managed_section_in_its_memory_file()
    {
        var replit = Row("replit");

        Assert.Equal(["replit.md", ".replit"], replit.MarkerPaths);
        Assert.Equal(new Pointer.ManagedSection("replit.md"), replit.Pointer);
        Assert.Contains("GitHub", replit.InstallPath, StringComparison.Ordinal);
        Assert.Equal(PlatformRow.Manual, replit.EvidenceLane);
    }

    [Fact]
    public void Antigravity_and_copilot_need_no_pointer_because_they_read_the_open_standard_directly()
    {
        // Antigravity's markers are its OWN layout, never .agents/skills (init writes that one); no
        // registry install path: `agy plugin install` reads only a plugin's top-level skills/ directory.
        Assert.Equal("Antigravity", Row("antigravity").Name);
        Assert.Equal(new Pointer.None(), Row("antigravity").Pointer);
        Assert.Equal([".agents/rules", ".agents/workflows", ".agent"], Row("antigravity").MarkerPaths);
        Assert.Contains("dotnet-narrativetrace init", Row("antigravity").InstallPath, StringComparison.Ordinal);
        Assert.DoesNotContain("plugin install", Row("antigravity").InstallPath, StringComparison.Ordinal);
        Assert.Contains(".agents/skills", Row("antigravity").AgentFinds, StringComparison.Ordinal);
        Assert.Contains("AGENTS.md", Row("antigravity").AgentFinds, StringComparison.Ordinal);
        Assert.Equal("antigravity", Row("antigravity").EvidenceLane);
        Assert.Equal(new Pointer.None(), Row("github-copilot").Pointer);
        Assert.Equal([".github/copilot-instructions.md"], Row("github-copilot").MarkerPaths);
    }

    [Fact]
    public void Claude_code_is_the_native_platform_with_the_claude_lane()
    {
        var claude = Row("claude-code");

        Assert.Equal([".claude"], claude.MarkerPaths);
        Assert.Equal(new Pointer.None(), claude.Pointer);
        Assert.Equal("claude", claude.EvidenceLane);
    }

    [Fact]
    public void Every_row_carries_the_date_its_facts_were_verified()
    {
        Assert.All(PlatformTable.Rows, row => Assert.Equal("2026-10-10", row.Verified));
    }

    [Fact]
    public void An_unknown_or_near_miss_id_is_no_row()
    {
        Assert.Null(PlatformTable.Row("cursorx"));
        Assert.Null(PlatformTable.Row("Cursor"));
    }
}
