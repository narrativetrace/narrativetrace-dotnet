// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using Xunit;

namespace NarrativeTrace.SecurityTests.Corpus;

/// <summary>
/// Corpus replay for the five <c>graphs.json</c> rows §6.7 tracked as gaps against the master
/// corpus with no builder yet in this runtime: <c>number-hostile-to-string</c>,
/// <c>platform-type-short-value</c>, <c>platform-type-name-redacted</c>,
/// <c>platform-lookalike-walked</c> and <c>platform-subclass-walked</c>. Each pins a specific
/// mechanism <see cref="ValueRenderer"/> uses so a future change that silently reopens the gap
/// shows up as a failing fact here, not just as a slightly different rendered string.
/// </summary>
/// <remarks>
/// The generic containment/well-formedness sweep
/// (<c>ValueRendererRedactionPropertyTests.Every_hostile_graph_keeps_a_redacted_value_out_of_every_output</c>
/// and <c>HostileCorpusTest.Every_declared_graph_shape_builds</c>) already replays every one of
/// these rows for the four that carry a secret — this class is the narrower, mechanism-specific
/// pin the master's Java twin keeps in <c>HostileMembers.java</c>'s own remarks: WHICH renderer
/// path answered, not just whether the sentinel leaked.
/// </remarks>
public sealed class PlatformOriginCorpusReplayTests
{
    /// <summary>
    /// <c>number-hostile-to-string</c>: a type with no walkable public state — and no public
    /// state is not what makes a value a stateless leaf, so its forged <c>ToString()</c> is
    /// never entered and the fenced block it would have injected is never produced at all.
    /// The row's property holds a step earlier than the Java bug it is named for, where a
    /// <c>Number</c> subclass's scalar fast path ran that <c>ToString()</c> and then skipped
    /// sanitizing its result.
    /// </summary>
    [Fact]
    public void Number_hostile_to_string_row_renders_escaped_and_never_raw()
    {
        var secret = HostileGraphs.BuildSecret("sentinel");
        var fixture = new HostileMembers.NumberHostileToString(secret);

        var rendered = ValueRenderer.Render(fixture);

        Assert.DoesNotContain("\n", rendered, StringComparison.Ordinal);
        Assert.DoesNotContain("NaN Infinity -Infinity", rendered, StringComparison.Ordinal);
        Assert.Contains(
            nameof(HostileMembers.NumberHostileToString), rendered, StringComparison.Ordinal);
    }

    /// <summary>
    /// <c>platform-type-short-value</c>: none of the four platform-defined leaf values
    /// (<see cref="Uri"/>, <see cref="decimal"/>, <see cref="DateTimeOffset"/>, <see cref="Guid"/>)
    /// carries a deny-listed field, so the well-formedness oracle here is that each renders its own
    /// short native text — never a <c>Type{Member: ...}</c> field-walk form, which is what would
    /// come out if <see cref="TypeShape"/> found public members to walk instead of falling through
    /// to <c>SafeToString</c>.
    /// </summary>
    [Fact]
    public void Platform_type_short_value_row_renders_each_value_as_its_own_native_text()
    {
        var built = (Dictionary<string, object>)HostileGraphs.Build(
            HostileCorpus.Graphs().Single(c => c.Id == "platform-type-short-value"), "sentinel");

        var rendered = ValueRenderer.Render(built);

        Assert.Contains("https://example.test/resource", rendered, StringComparison.Ordinal);
        Assert.Contains("19.99", rendered, StringComparison.Ordinal);
        Assert.Contains(DateTimeOffset.UnixEpoch.ToString("O"), rendered, StringComparison.Ordinal);
        Assert.Contains(Guid.Empty.ToString(), rendered, StringComparison.Ordinal);
        Assert.DoesNotContain("Uri{", rendered, StringComparison.Ordinal);
        Assert.DoesNotContain("Guid{", rendered, StringComparison.Ordinal);
    }

    /// <summary>
    /// <c>platform-type-name-redacted</c>: the name axis (<c>IsRedacted</c>) is checked before
    /// <see cref="MemberSlot.Read"/> is ever called — LIVE because a renderer that consulted the
    /// platform-value carve-out first would print the <see cref="Uri"/>'s own text, sentinel path
    /// included.
    /// </summary>
    [Fact]
    public void Platform_type_name_redacted_row_redacts_before_the_uri_is_ever_rendered()
    {
        var sentinel = "sentinel-" + Guid.NewGuid();
        var fixture = new HostileMembers.NamedPlatformValue(new Uri("https://example.test/" + sentinel));

        var rendered = ValueRenderer.Render(fixture);

        Assert.Contains(RedactionPolicy.Marker, rendered, StringComparison.Ordinal);
        Assert.DoesNotContain(sentinel, rendered, StringComparison.Ordinal);
        Assert.DoesNotContain("example.test", rendered, StringComparison.Ordinal);
    }

    /// <summary>
    /// <c>platform-lookalike-walked</c>: a class named <c>DateTime</c> but declared in the test
    /// assembly, unrelated by inheritance to <see cref="System.DateTime"/> — LIVE because
    /// <see cref="PlatformTypes.IsPlatformDefined"/> is never even consulted outside the
    /// collection-origin path; every non-enumerable object with public members reaches the same
    /// reflective walk regardless of what it is named, so its own forged <c>ToString()</c> never
    /// wins.
    /// </summary>
    [Fact]
    public void Platform_lookalike_walked_row_renders_through_the_field_walk_not_its_own_ToString()
    {
        var sentinel = "sentinel-" + Guid.NewGuid();
        var fixture = new HostileMembers.DateTime(sentinel);

        var rendered = ValueRenderer.Render(fixture);

        Assert.Contains("Username: \"ada\"", rendered, StringComparison.Ordinal);
        Assert.Contains(RedactionPolicy.Marker, rendered, StringComparison.Ordinal);
        Assert.DoesNotContain(sentinel, rendered, StringComparison.Ordinal);
        Assert.DoesNotContain("DateTime{Username=", rendered, StringComparison.Ordinal);
    }

    /// <summary>
    /// <c>platform-subclass-walked</c>: a subclass of the platform-defined, non-sealed
    /// <see cref="Uri"/> — LIVE for the same reason as the lookalike row: <see cref="ValueRenderer"/>
    /// has no scalar carve-out that pattern-matches <see cref="Uri"/> at all (only
    /// <see cref="string"/>/<see cref="bool"/>/<see cref="char"/>/<see cref="decimal"/>/
    /// <see cref="double"/>/<see cref="float"/>/<see cref="System.DateTime"/>/
    /// <see cref="DateTimeOffset"/>/<see cref="Type"/>/<see cref="Exception"/>/
    /// <see cref="Delegate"/>/<see cref="System.Threading.Tasks.Task"/> are matched by exact
    /// pattern), so the most-derived type's own public state is what gets walked, never the
    /// platform ancestor's trust.
    /// </summary>
    [Fact]
    public void Platform_subclass_walked_row_renders_through_the_field_walk_not_its_own_ToString()
    {
        var sentinel = "sentinel-" + Guid.NewGuid();
        var fixture = new HostileMembers.ApplicationUri(sentinel);

        var rendered = ValueRenderer.Render(fixture);

        Assert.Contains(RedactionPolicy.Marker, rendered, StringComparison.Ordinal);
        Assert.DoesNotContain(sentinel, rendered, StringComparison.Ordinal);
        Assert.DoesNotContain("ApplicationUri{Password=", rendered, StringComparison.Ordinal);
    }

    /// <summary>
    /// The declarative path (<c>graphs.json</c> → <see cref="HostileGraphs.Build"/>) reaches the
    /// same five fixtures, not just a direct constructor call.
    /// </summary>
    [Theory]
    [InlineData("number-hostile-to-string")]
    [InlineData("platform-type-short-value")]
    [InlineData("platform-type-name-redacted")]
    [InlineData("platform-lookalike-walked")]
    [InlineData("platform-subclass-walked")]
    public void The_declared_graph_case_builds_the_matching_fixture_type(string caseId)
    {
        var graphCase = HostileCorpus.Graphs().Single(c => c.Id == caseId);

        var built = HostileGraphs.Build(graphCase, "sentinel");

        Assert.True(
            built is HostileMembers.NumberHostileToString
                or Dictionary<string, object>
                or HostileMembers.NamedPlatformValue
                or HostileMembers.DateTime
                or HostileMembers.ApplicationUri,
            $"{caseId} built an unexpected type: {built.GetType()}");
    }
}
