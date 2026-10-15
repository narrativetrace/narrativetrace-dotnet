// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Tooling.Doctor;
using NarrativeTrace.Tooling.Doctor.Checks;
using NarrativeTrace.Tooling.Init;
using NarrativeTrace.Tooling.Platforms;
using NarrativeTrace.Tooling.Tests.Doctor;
using Xunit;
using Action = NarrativeTrace.Tooling.Init.Action;
using Status = NarrativeTrace.Tooling.Init.PointerReading.PointerStatus;

namespace NarrativeTrace.Tooling.Tests.Init;

/// <summary>
/// Adversarial Phase 8 cases: byte-order marks and CRLF through a rule refresh, user edits to a rule's
/// frontmatter, fenced copies of the managed markers in the pointer files, marker near-misses, pointer
/// path validation, row immutability and the doctor's reading of malformed stamps. Failing cases are
/// suspected defects reported by name; they are not to be fixed by weakening the assertion.
/// </summary>
public sealed class AdversarialPhase8Tests : IDisposable
{
    private const string ReplitMd = "replit.md";
    private const string CursorRulePath = ".cursor/rules/narrativetrace.mdc";
    private const string StaleCoordinate = "NarrativeTrace.Skills/0.0.1";
    private const string ReplitSection = "<!-- narrativetrace:start NarrativeTrace.Skills/1.2.3 -->\nbody\n<!-- narrativetrace:end -->\n";

    private readonly string _root = Path.Combine(Path.GetTempPath(), "nt-adv-phase8-" + Guid.NewGuid().ToString("N"));
    private readonly Carrier _carrier;

    public AdversarialPhase8Tests()
    {
        Directory.CreateDirectory(_root);
        _carrier = Carriers.Open(Path.Combine(_root, "carrier"), "1.2.3");
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private static string TableRuleText => ((Pointer.Rule)PlatformTable.Row("cursor")!.Pointer).Text;

    private static ProjectState.Builder Project() => ProjectState.NewBuilder();

    private InitPlan Plan(ProjectState state) => InitPlanner.Plan(state, _carrier, InitOptions.Defaults());

    private InitPlan PlanWithPermission(ProjectState state) =>
        InitPlanner.Plan(state, _carrier, InitOptions.Defaults().WithWriteExisting(true));

    private static Action? On(InitPlan plan, string path) => plan.Actions.FirstOrDefault(action => action.Path == path);

    private static string After(InitPlan plan, string path) => ((FileEdit)On(plan, path)!).After;

    private static string Crlf(string text) => text.Replace("\n", "\r\n", StringComparison.Ordinal);

    private static string SectionOf(string rule) => rule[CursorRule.Frontmatter.Length..];

    // --- rule refresh through a byte-order mark and CRLF -------------------------------------------

    [Fact]
    public void A_stale_CRLF_rule_saved_with_a_byte_order_mark_is_refreshed_in_place_keeping_both()
    {
        var stale = "\uFEFF" + Crlf(CursorRule.Render(TableRuleText, StaleCoordinate));
        var state = Project().DetectedPlatform("cursor").PointerFile(CursorRulePath, stale).Build();

        var after = After(Plan(state), CursorRulePath);

        Assert.Equal("\uFEFF" + Crlf(CursorRule.Render(TableRuleText, _carrier.Coordinate)), after);
        Assert.DoesNotMatch("(?<!\r)\n", after);
    }

    [Fact]
    public void A_CRLF_rule_refreshed_through_a_byte_order_mark_is_current_on_the_next_plan()
    {
        var refreshed = "\uFEFF" + Crlf(CursorRule.Render(TableRuleText, _carrier.Coordinate));
        var state = Project().DetectedPlatform("cursor").PointerFile(CursorRulePath, refreshed).Build();

        Assert.Null(On(Plan(state), CursorRulePath));
    }

    [Fact]
    public void A_refresh_keeps_a_rule_frontmatter_the_user_edited_and_replaces_only_the_section()
    {
        var userFrontmatter = "---\ndescription: my own words\nalwaysApply: false\n---\n";
        var stale = userFrontmatter + SectionOf(CursorRule.Render(TableRuleText, StaleCoordinate)) + "\nMy notes.\n";
        var state = Project().DetectedPlatform("cursor").PointerFile(CursorRulePath, stale).Build();

        var after = After(Plan(state), CursorRulePath);

        Assert.Equal(
            userFrontmatter + SectionOf(CursorRule.Render(TableRuleText, _carrier.Coordinate)) + "\nMy notes.\n",
            after);
    }

    // --- uninstall keeps a rule whose frontmatter the user changed ----------------------------------

    [Fact]
    public void Uninstall_keeps_a_rule_whose_frontmatter_the_user_edited_instead_of_deleting_it()
    {
        var userFrontmatter = "---\ndescription: my own words\nalwaysApply: false\n---\n";
        var text = userFrontmatter + SectionOf(CursorRule.Render(TableRuleText, _carrier.Coordinate));
        var state = Project().PointerFile(CursorRulePath, text).Build();

        var plan = UninstallPlanner.Plan(state, InitOptions.Defaults());

        Assert.IsNotType<DeleteFile>(On(plan, CursorRulePath));
        Assert.Equal(userFrontmatter, After(plan, CursorRulePath));
    }

    // --- fenced copies of the markers are text, not a section ----------------------------------------

    [Fact]
    public void A_section_shown_only_inside_a_fence_in_replit_md_is_appended_to_and_the_example_survives()
    {
        var example = "# Notes\n\n```md\n" + ReplitSection + "```\n";
        var state = Project().DetectedPlatform("replit").PointerFile(ReplitMd, example).Build();

        var edit = Assert.IsType<AppendBlock>(On(PlanWithPermission(state), ReplitMd));

        Assert.StartsWith(example, edit.After, StringComparison.Ordinal);
        Assert.True(MarkedBlock.ScanText(edit.After).HasExactlyOneRegion);
    }

    [Fact]
    public void A_fenced_stale_section_next_to_a_real_current_one_does_not_hide_the_real_coordinate()
    {
        var text = "```md\n<!-- narrativetrace:start NarrativeTrace.Skills/0.0.1 -->\nexample\n<!-- narrativetrace:end -->\n```\n\n"
            + ReplitSection;
        var state = Project().DetectedPlatform("replit").PointerFile(ReplitMd, text).Build();

        var reading = Assert.Single(PointerReading.ReadAll(state));

        Assert.Equal(Status.Section, reading.Status);
        Assert.Equal("NarrativeTrace.Skills/1.2.3", reading.Coordinate);
    }

    [Fact]
    public void Uninstall_leaves_a_fenced_copy_of_the_markers_below_a_real_cursor_section_as_the_users_text()
    {
        var fencedCopy = "```md\n<!-- narrativetrace:start NarrativeTrace.Skills/1.2.3 -->\nexample\n<!-- narrativetrace:end -->\n```\n";
        var state = Project().PointerFile(CursorRulePath, CursorRule.Render(TableRuleText, _carrier.Coordinate) + "\n" + fencedCopy).Build();

        var plan = UninstallPlanner.Plan(state, InitOptions.Defaults());

        var edit = Assert.IsType<ReplaceBlock>(On(plan, CursorRulePath));
        Assert.EndsWith(fencedCopy, edit.After, StringComparison.Ordinal);
        Assert.Empty(MarkedBlock.ScanText(edit.After).Regions);
    }

    [Fact]
    public void Uninstall_of_replit_md_keeps_a_fenced_copy_of_the_section_below_the_real_one()
    {
        var fencedCopy = "```\n" + ReplitSection + "```\n";
        var state = Project().PointerFile(ReplitMd, ReplitSection + "\n" + fencedCopy).Build();

        var edit = Assert.IsType<ReplaceBlock>(On(UninstallPlanner.Plan(state, InitOptions.Defaults()), ReplitMd));

        Assert.EndsWith(fencedCopy, edit.After, StringComparison.Ordinal);
        Assert.Empty(MarkedBlock.ScanText(edit.After).Regions);
    }

    // --- marker boundaries ----------------------------------------------------------------------------

    [Fact]
    public void A_near_miss_opening_marker_with_an_extra_word_does_not_open_a_section()
    {
        // Suspected defect: "narrativetrace:starts" shares the prefix "<!-- narrativetrace:start" but is
        // not the marker, so a user's line would open a region that an end marker then closes.
        var text = "<!-- narrativetrace:starts -->\nthe user's own note\n<!-- narrativetrace:end -->\n";

        Assert.Empty(MarkedBlock.ScanText(text).Regions);
    }

    // --- pointer paths --------------------------------------------------------------------------------

    [Theory]
    [InlineData("replit.md/")]
    [InlineData("C:/notes.md")]
    [InlineData("\\\\host\\share\\replit.md")]
    public void A_pointer_path_that_is_not_a_plain_project_relative_path_is_refused(string path)
    {
        // Suspected defect: Pointer.Relative rejects only a leading "/" and "..", so a trailing separator,
        // a drive-letter path or a UNC-style path is accepted as "inside the project".
        Assert.Throws<ArgumentException>(() => new Pointer.ManagedSection(path));
    }

    // --- row immutability -----------------------------------------------------------------------------

    [Fact]
    public void Mutating_the_marker_list_passed_to_a_row_does_not_change_the_row()
    {
        var markers = new List<string> { ".foo" };
        var row = new PlatformRow("foo-bar", "Foo", markers, new Pointer.None(), "install", "finds", "manual", "2026-10-10");

        markers.Add(".bar");
        markers[0] = ".changed";

        Assert.Equal([".foo"], row.MarkerPaths);
    }
}
