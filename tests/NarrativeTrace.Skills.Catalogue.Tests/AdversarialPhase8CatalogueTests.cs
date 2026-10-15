// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Text.Json;
using NarrativeTrace.Skills.Render;
using NarrativeTrace.SkillsEvals;
using Xunit;

namespace NarrativeTrace.Skills.Tests;

/// <summary>
/// Adversarial Phase 8 cases for the Cursor lane's skip decision, its preset command and the Cursor
/// plugin manifest. Failing cases are suspected defects reported by name, not to be fixed by weakening
/// the assertion.
/// </summary>
public sealed class AdversarialPhase8CatalogueTests
{
    private const string Doctor = "narrativetrace-doctor";
    private const string Feedback = "narrativetrace-feedback";
    private static readonly DateOnly Today = new(2026, 10, 10);

    private static MarketplaceListing Listing(string description, IReadOnlyList<string> keywords) => new(
        Name: "narrativetrace-example",
        Owner: new MarketplaceOwner("Example Owner", "https://example.test"),
        Description: "An example marketplace.",
        PluginDescription: description,
        PluginSource: "./.claude",
        License: "Apache-2.0",
        Homepage: "https://example.test",
        Keywords: keywords);

    // --- the skip line ---------------------------------------------------------------------------------

    [Theory]
    [InlineData(Platform.Codex, "codex")]
    [InlineData(Platform.Antigravity, "agy")]
    [InlineData(Platform.Cursor, "agent")]
    public void A_zero_allowance_skip_names_only_its_own_lanes_CLI(Platform platform, string cli)
    {
        var ledger = new QuotaLedger([new QuotaAllowance(platform, "not installed", 0)], []);

        var reason = SporadicPolicy.SkipReason(platform, ledger);

        Assert.NotNull(reason);
        Assert.Contains($"`{cli}`", reason, StringComparison.Ordinal);
        Assert.Contains($"the {platform.WireName()} lane", reason, StringComparison.Ordinal);
    }

    [Fact]
    public void A_lane_listed_with_a_positive_allowance_before_a_zero_row_is_not_skipped_when_its_quota_allows_it()
    {
        // Suspected defect: SkipReason skips when ANY cursor row is zero, while CheckQuota reads only the
        // FIRST cursor row. On this ledger the quota check allows the lane, yet the lane is skipped, so
        // the two decisions about one ledger disagree.
        var ledger = new QuotaLedger(
            [new QuotaAllowance(Platform.Cursor, "pro", 4), new QuotaAllowance(Platform.Cursor, "not installed", 0)],
            []);

        var reason = SporadicPolicy.SkipReason(Platform.Cursor, ledger);
        var decision = QuotaMarkdown.CheckQuota(ledger, Platform.Cursor, Today);

        Assert.True(decision.Allowed);
        Assert.Null(reason);
    }

    [Fact]
    public void A_zero_row_listed_before_a_positive_one_skips_and_the_quota_check_refuses_so_they_agree()
    {
        var ledger = new QuotaLedger(
            [new QuotaAllowance(Platform.Cursor, "not installed", 0), new QuotaAllowance(Platform.Cursor, "pro", 4)],
            []);

        Assert.NotNull(SporadicPolicy.SkipReason(Platform.Cursor, ledger));
        Assert.False(QuotaMarkdown.CheckQuota(ledger, Platform.Cursor, Today).Allowed);
    }

    // --- parsing and the preset command ----------------------------------------------------------------

    [Theory]
    [InlineData("agent")]
    [InlineData("cursor ")]
    [InlineData(" cursor")]
    [InlineData("cursors")]
    [InlineData("cursor-cli")]
    public void The_CLI_executable_and_near_miss_spellings_are_not_platform_names(string value)
    {
        Assert.Null(Platforms.Parse(value));
    }

    [Fact]
    public void The_Cursor_preset_is_the_same_whatever_model_label_the_ledger_carries()
    {
        var first = Platform.Cursor.PresetAgentCommand("auto", Feedback);
        var second = Platform.Cursor.PresetAgentCommand("gpt-5", Feedback);

        Assert.Equal(first, second);
        Assert.DoesNotContain("--model", first, StringComparison.Ordinal);
    }

    // --- Cursor plugin manifest ------------------------------------------------------------------------

    [Fact]
    public void The_plugin_manifest_round_trips_hostile_text_through_a_real_JSON_parser()
    {
        var description = "Line one\r\nLine two\t\"quoted\" \\ back \u2028 separator \u0000 nul </script> <!-- narrativetrace:end -->";
        var rendered = CursorPluginJsonRenderer.RenderPlugin(Listing(description, ["one"]));

        using var document = JsonDocument.Parse(rendered);

        Assert.Equal(description, document.RootElement.GetProperty("description").GetString());
    }

    [Fact]
    public void An_empty_keyword_list_renders_as_an_empty_JSON_array_in_both_manifests()
    {
        var listing = Listing("An example plugin.", []);

        using var plugin = JsonDocument.Parse(CursorPluginJsonRenderer.RenderPlugin(listing));
        using var marketplace = JsonDocument.Parse(CursorPluginJsonRenderer.RenderMarketplace(listing));

        Assert.Equal(0, plugin.RootElement.GetProperty("keywords").GetArrayLength());
        Assert.Equal(0, marketplace.RootElement.GetProperty("plugins")[0].GetProperty("keywords").GetArrayLength());
    }

    [Fact]
    public void Both_manifests_end_with_exactly_one_newline_and_carry_no_carriage_return()
    {
        var listing = Listing("An example plugin.", ["one"]);

        foreach (var rendered in new[] { CursorPluginJsonRenderer.RenderPlugin(listing), CursorPluginJsonRenderer.RenderMarketplace(listing) })
        {
            Assert.EndsWith("}\n", rendered, StringComparison.Ordinal);
            Assert.False(rendered.EndsWith("\n\n", StringComparison.Ordinal));
            Assert.DoesNotContain('\r', rendered);
        }
    }
}
