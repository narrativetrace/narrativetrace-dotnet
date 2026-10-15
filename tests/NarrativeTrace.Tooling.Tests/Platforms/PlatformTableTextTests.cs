// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Tooling.Platforms;
using Xunit;

namespace NarrativeTrace.Tooling.Tests.Platforms;

/// <summary>
/// The platform table's words are the public contract the docs page renders and a stranger follows, so
/// each row's sentences are pinned whole, not by a fragment.
/// </summary>
public sealed class PlatformTableTextTests
{
    private const string Init = "run `dotnet tool run dotnet-narrativetrace init` in the project";

    private static PlatformRow Row(string id) => PlatformTable.Row(id) ?? throw new InvalidOperationException(id);

    [Fact]
    public void Cursor_row_says_exactly_what_it_installs_and_finds()
    {
        var cursor = Row("cursor");

        Assert.Equal("Cursor", cursor.Name);
        Assert.Equal("Install the plugin from the public repository's `.cursor-plugin` manifest, or " + Init, cursor.InstallPath);
        Assert.Equal(
            "the skills in `.agents/skills` (Cursor also reads `.claude/skills`), and the one rule" +
            " `.cursor/rules/narrativetrace.mdc` that points at them",
            cursor.AgentFinds);
        Assert.Equal(
            "The NarrativeTrace skills live under `.agents/skills` — read them before changing traced code," +
            " and run the NarrativeTrace doctor to check the install.",
            Assert.IsType<Pointer.Rule>(cursor.Pointer).Text);
        Assert.Equal("2026-10-10", cursor.Verified);
    }

    [Fact]
    public void Replit_row_says_exactly_what_it_installs_and_finds()
    {
        var replit = Row("replit");

        Assert.Equal("Replit", replit.Name);
        Assert.Equal("replit.md", Assert.IsType<Pointer.ManagedSection>(replit.Pointer).File);
        Assert.Equal(
            "Import the public repository from GitHub (paste its URL, preview the skills, add them), or " + Init,
            replit.InstallPath);
        Assert.Equal(
            "the skills in `.agents/skills`, and a NarrativeTrace section in `replit.md`, the project memory" +
            " the Replit agent reads first",
            replit.AgentFinds);
        Assert.Equal(PlatformRow.Manual, replit.EvidenceLane);
    }

    [Fact]
    public void Antigravity_row_says_exactly_what_it_installs_and_finds()
    {
        var antigravity = Row("antigravity");

        Assert.Equal("Antigravity", antigravity.Name);
        Assert.Equal([".agents/rules", ".agents/workflows", ".agent"], antigravity.MarkerPaths);
        Assert.Equal(
            "Nothing to install beyond the project itself: " + Init + " and commit the skills (the `agy` CLI" +
            " and the IDE read them as workspace skills)",
            antigravity.InstallPath);
        Assert.Equal(
            "the skills in `.agents/skills`, which Antigravity reads as workspace skills, and the section in" +
            " `AGENTS.md`, which it loads as rules",
            antigravity.AgentFinds);
        Assert.Equal("antigravity", antigravity.EvidenceLane);
    }

    [Fact]
    public void Copilot_row_says_exactly_what_it_installs_and_finds()
    {
        var copilot = Row("github-copilot");

        Assert.Equal("GitHub Copilot", copilot.Name);
        Assert.Equal([".github/copilot-instructions.md"], copilot.MarkerPaths);
        Assert.Equal("Nothing to install beyond the repository itself: " + Init + " and commit the skills", copilot.InstallPath);
        Assert.Equal(
            "the skills in `.agents/skills` (Copilot also reads `.claude/skills` and `.github/skills`), and" +
            " the section in `AGENTS.md`",
            copilot.AgentFinds);
        Assert.Equal(PlatformRow.Manual, copilot.EvidenceLane);
    }

    [Fact]
    public void Claude_code_row_says_exactly_what_it_installs_and_finds()
    {
        var claude = Row("claude-code");

        Assert.Equal("Claude Code", claude.Name);
        Assert.Equal([".claude"], claude.MarkerPaths);
        Assert.Equal("Add the repository as a plugin marketplace and install its plugin, or " + Init, claude.InstallPath);
        Assert.Equal(
            "the skills in `.claude/skills` and `.agents/skills`, and the section in `AGENTS.md` that" +
            " `CLAUDE.md` imports",
            claude.AgentFinds);
        Assert.Equal("claude", claude.EvidenceLane);
    }

    [Fact]
    public void A_rule_pointer_with_blank_text_names_what_is_missing()
    {
        var error = Assert.Throws<ArgumentException>(() => new Pointer.Rule("a/b.mdc", "  "));

        Assert.Contains("a rule pointer carries text", error.Message, StringComparison.Ordinal);
        Assert.Equal("Text", error.ParamName);
    }

    [Theory]
    [InlineData("C:x")]
    [InlineData("a:b")]
    public void A_pointer_path_with_a_drive_letter_is_refused_and_named(string path)
    {
        var error = Assert.Throws<ArgumentException>(() => new Pointer.Rule(path, "text"));

        Assert.Contains("a pointer path is project-relative and stays inside it, got " + path, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_single_character_path_is_not_mistaken_for_a_drive_letter()
    {
        Assert.Equal("a", new Pointer.Rule("a", "text").Path);
    }

    [Fact]
    public void A_row_with_a_blank_install_path_names_the_row_and_the_column()
    {
        var error = Assert.Throws<ArgumentException>(() =>
            new PlatformRow("some-platform", "Name", [".some"], new Pointer.None(), " ", "finds", "manual", "2026-10-10"));

        Assert.Equal("row some-platform leaves its install path empty (Parameter 'install path')", error.Message);
    }

    [Fact]
    public void A_row_with_no_pointer_names_the_row()
    {
        var error = Assert.Throws<ArgumentException>(() =>
            new PlatformRow("some-platform", "Name", [".some"], null!, "install", "finds", "manual", "2026-10-10"));

        Assert.StartsWith("row some-platform leaves its pointer column empty", error.Message, StringComparison.Ordinal);
    }
}
