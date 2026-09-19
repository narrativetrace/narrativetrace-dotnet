// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Collections;
using System.Collections.Specialized;
using NarrativeTrace.Core;
using Xunit;

namespace NarrativeTrace.SecurityTests.Corpus;

/// <summary>
/// Corpus replay for the seven <c>graphs.json</c> rows added for the rendering rule copied into
/// the repository's agent guide ("rendering reads state, never runs
/// behaviour", its 2026-09-18 abstract-base refinement, and pair #5 I's two follow-up gaps):
/// <c>record-accessor-with-counter</c>, <c>platform-collection-side-effecting-iterator</c>,
/// <c>lookalike-collection-not-platform-defined</c>, <c>abstract-map-subclass-override</c>,
/// <c>abstract-collection-subclass-override</c>, <c>structured-path-user-collection-not-
/// enumerated</c> and <c>fieldless-abstract-subclass-tostring-door</c>. Each row's expected
/// outcome is "rendered without executing" — the renderer must never invoke the fixture's own
/// overridden member.
/// </summary>
/// <remarks>
/// <see cref="HostileCorpusTest.Every_declared_graph_shape_builds"/> already replays every row in
/// <c>check</c> for containment and well-formedness — that passes for all seven rows too (a
/// thrown override degrades to the typed failure marker, which leaks nothing). What that does not
/// check is whether the override ran at all, which is the property these rows exist for.
/// <para>
/// The first five are LIVE here: <c>the canonical implementation</c> (properties read from backing fields, never the
/// accessor) and <c>the canonical implementation</c> (collection-origin dispatch coverage) already landed the fix this
/// runtime's Java counterpart is still waiting on, so — unlike the Java twin, which keeps this
/// test class disabled — every fact below runs for real, per commit. The two 2026-09-18 rows are
/// LIVE for a distinct, narrower reason (see
/// <see cref="HostileMembers.AbstractCollectionSubclassOverride"/>'s remarks): this runtime's
/// origin check keys off the most-derived type's own declaring assembly and its ancestor-state
/// fallback recognizes only <see cref="List{T}"/>, so an abstract-platform-base subclass never
/// reaches the origin-trusts-platform branch that Java's fix is still landing — verified
/// empirically here, not assumed.
/// </para>
/// <para>
/// The two pair #5 I rows (§6.7 mirror wave, 2026-09-18) split, verified empirically rather than
/// assumed: <c>structured-path-user-collection-not-enumerated</c> is LIVE here — this runtime's
/// structured path was already built sharing the flat path's origin gate, so the two cannot
/// diverge by construction, unlike Java's still-origin-blind structured path.
/// <c>fieldless-abstract-subclass-tostring-door</c> stays PENDING here too, same as Java, for a
/// different reason: this runtime's fieldless-object fallback (<c>SafeToString</c>) calls the
/// fixture's own inherited <c>ToString()</c>, which enumerates — see
/// <see cref="HostileMembers.NarratingCollectionBase"/>'s remarks for why no platform base
/// reproduces this and a hand-rolled one was needed.
/// </para>
/// </remarks>
public sealed class RenderingReadsStateCorpusReplayTests
{
    [Fact]
    public void Record_accessor_with_counter_row_renders_without_invoking_the_accessor()
    {
        var secret = HostileGraphs.BuildSecret("sentinel");
        var fixture = new HostileMembers.CountingAccessorRecord(secret);

        ValueRenderer.Render(fixture);

        Assert.Equal(0, fixture.Reads);
    }

    [Fact]
    public void Platform_collection_side_effecting_iterator_row_renders_without_executing_the_override()
    {
        var secret = HostileGraphs.BuildSecret("sentinel");
        var fixture = new HostileMembers.SideEffectingIteratorList(secret);

        ValueRenderer.Render(fixture);

        Assert.Equal(0, fixture.GetEnumeratorCalls);
    }

    [Fact]
    public void Lookalike_collection_row_renders_without_executing_its_own_iterator()
    {
        var secret = HostileGraphs.BuildSecret("sentinel");
        var fixture = new HostileMembers.LookalikeCollection(secret);

        ValueRenderer.Render(fixture);

        Assert.Equal(0, fixture.GetEnumeratorCalls);
    }

    /// <summary>
    /// The 2026-09-18 refinement's collection row: a subclass of the abstract, un-sealed-enumerator
    /// platform base <see cref="ReadOnlyCollectionBase"/> — see
    /// <see cref="HostileMembers.AbstractCollectionSubclassOverride"/>'s remarks for why this is the
    /// honest .NET twin of Java's <c>AbstractCollection</c>. LIVE: this runtime's origin check
    /// never reaches the branch that would call the override.
    /// </summary>
    [Fact]
    public void Abstract_collection_subclass_override_row_renders_without_executing_the_override()
    {
        var secret = HostileGraphs.BuildSecret("sentinel");
        var fixture = new HostileMembers.AbstractCollectionSubclassOverride(secret);

        ValueRenderer.Render(fixture);

        Assert.Equal(0, fixture.GetEnumeratorCalls);
    }

    /// <summary>
    /// The 2026-09-18 refinement's map row: a subclass of the abstract, un-sealed-enumerator
    /// platform base <see cref="NameObjectCollectionBase"/> — see
    /// <see cref="HostileMembers.AbstractMapSubclassOverride"/>'s remarks for why this is the
    /// honest .NET twin of Java's <c>AbstractMap</c>. LIVE for the same reason as the collection row.
    /// </summary>
    [Fact]
    public void Abstract_map_subclass_override_row_renders_without_executing_the_override()
    {
        var secret = HostileGraphs.BuildSecret("sentinel");
        var fixture = new HostileMembers.AbstractMapSubclassOverride(secret);

        ValueRenderer.Render(fixture);

        Assert.Equal(0, fixture.GetEnumeratorCalls);
    }

    /// <summary>
    /// The declarative path (<c>graphs.json</c> → <see cref="HostileGraphs.Build"/>) reaches the
    /// same five fixtures, not just a direct constructor call — the corpus wiring in
    /// <c>HostileGraphs.Hostile</c> is itself part of what these tests pin.
    /// </summary>
    [Theory]
    [InlineData("record-accessor-with-counter")]
    [InlineData("platform-collection-side-effecting-iterator")]
    [InlineData("lookalike-collection-not-platform-defined")]
    [InlineData("abstract-map-subclass-override")]
    [InlineData("abstract-collection-subclass-override")]
    [InlineData("structured-path-user-collection-not-enumerated")]
    [InlineData("fieldless-abstract-subclass-tostring-door")]
    [InlineData("fieldless-sidetable-tostring-door")]
    [InlineData("number-subclass-tostring-door")]
    public void The_declared_graph_case_builds_the_matching_fixture_type(string caseId)
    {
        var graphCase = HostileCorpus.Graphs().Single(c => c.Id == caseId);

        var built = HostileGraphs.Build(graphCase, "sentinel");

        Assert.True(
            built is HostileMembers.CountingAccessorRecord
                or HostileMembers.SideEffectingIteratorList
                or HostileMembers.LookalikeCollection
                or HostileMembers.AbstractMapSubclassOverride
                or HostileMembers.AbstractCollectionSubclassOverride
                or HostileMembers.FieldlessAbstractSubclassToStringDoor
                or HostileMembers.FieldlessSideTableToStringDoor
                or HostileMembers.NumberSubclassToStringDoor,
            $"{caseId} built an unexpected type: {built.GetType()}");
    }

    /// <summary>
    /// §6.7 pair #6 T mirror, gap 1 —
    /// <c>structured-path-user-collection-not-enumerated</c>: reuses <see cref="HostileMembers.LookalikeCollection"/>
    /// (already pinned safe on the flat path above), replayed through <see cref="ValueRenderer.RenderStructured"/>
    /// instead. Java's structured path (<c>renderStructuredComplex</c>) is still origin-blind — it
    /// enumerates any <c>Collection</c> unconditionally, so this row stays <c>@Disabled</c> there. This
    /// runtime's structured path shares the SAME origin gate as the flat path
    /// (<see cref="ValueRenderer"/>'s <c>RenderStructuredEnumerableByOrigin</c> mirrors
    /// <c>RenderEnumerableByOrigin</c> exactly — <c>HasNarrativeElements(type) || PlatformTypes.IsPlatformDefined(type)</c>,
    /// else a <see cref="List{T}"/>-ancestor state read, else fall through to object introspection) — so
    /// the two paths cannot diverge here by construction. Verified empirically, not assumed: LIVE.
    /// </summary>
    [Fact]
    public void Lookalike_collection_row_is_never_enumerated_by_structured_rendering_either()
    {
        var secret = HostileGraphs.BuildSecret("sentinel");
        var fixture = new HostileMembers.LookalikeCollection(secret);

        ValueRenderer.RenderStructured(fixture);

        Assert.Equal(0, fixture.GetEnumeratorCalls);
    }

    /// <summary>
    /// §6.7 pair #6 T mirror, gap 2 —
    /// <c>fieldless-abstract-subclass-tostring-door</c>. PENDING here too, matching Java's own
    /// <c>@Disabled</c> twin: observed red before this attribute was added, not assumed.
    /// <see cref="HostileMembers.FieldlessAbstractSubclassToStringDoor"/> is fieldless, so
    /// <c>RenderShaped</c>/<c>RenderStructuredShaped</c> falls to <c>SafeToString</c> — the type's own
    /// <see cref="object.ToString"/>, inherited unchanged from <see cref="HostileMembers.NarratingCollectionBase"/>,
    /// which itself enumerates. <see cref="HostileMembers.AbstractCollectionSubclassOverride"/> is safe
    /// only because it carries a field (<c>_held</c>), which routes it to the object-member dump instead
    /// — this fixture has no field to earn that routing. The assertion below states the RULE (the
    /// override must never run), not the current behavior; it is skipped, not inverted, so a future fix
    /// turns it green rather than needing to be rewritten.
    /// </summary>
    [SkippableFact]
    public void Fieldless_abstract_subclass_tostring_door_row_renders_without_executing_the_override()
    {
        HostileMembers.FieldlessAbstractSubclassToStringDoor.ResetIteratorCalls();
        var fixture = new HostileMembers.FieldlessAbstractSubclassToStringDoor();

        ValueRenderer.Render(fixture);

        Assert.Equal(0, HostileMembers.FieldlessAbstractSubclassToStringDoor.IteratorCalls);
    }

    /// <summary>Same door, replayed on the structured path — both share <c>SafeToString</c>.</summary>
    [SkippableFact]
    public void Fieldless_abstract_subclass_tostring_door_row_renders_without_executing_the_override_structured()
    {
        HostileMembers.FieldlessAbstractSubclassToStringDoor.ResetIteratorCalls();
        var fixture = new HostileMembers.FieldlessAbstractSubclassToStringDoor();

        ValueRenderer.RenderStructured(fixture);

        Assert.Equal(0, HostileMembers.FieldlessAbstractSubclassToStringDoor.IteratorCalls);
    }

    /// <summary>
    /// §6.7 master mirror wave (2026-09-19): the <c>fieldless-sidetable-tostring-door</c> row. LIVE
    /// here, unlike its abstract-base sibling above: this fixture is not on
    /// <see cref="PlatformTypes.IsStatelessLeaf"/>'s explicit allowlist, so <c>RenderShaped</c>'s
    /// <c>Members.Length &gt; 0 || !IsStatelessLeaf(type)</c> guard is true even at zero members —
    /// the object-dump branch, never <c>SafeToString</c> — verified empirically here, not assumed.
    /// </summary>
    [Fact]
    public void Fieldless_sidetable_tostring_door_row_never_consults_the_side_table()
    {
        HostileMembers.FieldlessSideTableToStringDoor.ResetSideTableReads();
        var secret = HostileGraphs.BuildSecret("sentinel-side-table");
        var fixture = new HostileMembers.FieldlessSideTableToStringDoor(secret);

        var rendered = ValueRenderer.Render(fixture);

        Assert.Equal(0, HostileMembers.FieldlessSideTableToStringDoor.SideTableReads);
        Assert.Contains("FieldlessSideTableToStringDoor", rendered);
        Assert.DoesNotContain("sentinel-side-table", rendered, StringComparison.Ordinal);
        Assert.DoesNotContain("<error", rendered, StringComparison.Ordinal);
    }

    /// <summary>Same door, replayed on the structured path — one decision, shared, so the two cannot drift.</summary>
    [Fact]
    public void Fieldless_sidetable_tostring_door_row_never_consults_the_side_table_structured()
    {
        HostileMembers.FieldlessSideTableToStringDoor.ResetSideTableReads();
        var secret = HostileGraphs.BuildSecret("sentinel-side-table-structured");
        var fixture = new HostileMembers.FieldlessSideTableToStringDoor(secret);

        var rendered = Dump(ValueRenderer.RenderStructured(fixture));

        Assert.Equal(0, HostileMembers.FieldlessSideTableToStringDoor.SideTableReads);
        Assert.Contains("FieldlessSideTableToStringDoor", rendered);
        Assert.DoesNotContain("sentinel-side-table-structured", rendered, StringComparison.Ordinal);
        Assert.DoesNotContain("<error", rendered, StringComparison.Ordinal);
    }

    /// <summary>
    /// §6.7 master mirror wave (2026-09-19): the <c>number-subclass-tostring-door</c> row. Always
    /// LIVE here — see <see cref="HostileMembers.NumberSubclassToStringDoor"/>'s remarks: .NET has
    /// no subclassable numeric base for a scalar fast path to trust in the first place, so this
    /// composite reaches the ordinary field walk the same way any other composite does.
    /// </summary>
    [Fact]
    public void Number_subclass_tostring_door_row_is_walked_rather_than_read_on_the_flat_path()
    {
        var fixture = new HostileMembers.NumberSubclassToStringDoor("sentinel-number-subclass");

        var rendered = ValueRenderer.Render(fixture);

        Assert.Contains("NumberSubclassToStringDoor", rendered);
        Assert.Contains("[REDACTED]", rendered);
        Assert.DoesNotContain("sentinel-number-subclass", rendered, StringComparison.Ordinal);
        Assert.DoesNotContain("<error", rendered, StringComparison.Ordinal);
    }

    /// <summary>The same row on the structured path: one decision, so the two channels cannot differ.</summary>
    [Fact]
    public void Number_subclass_tostring_door_row_is_walked_rather_than_read_on_the_structured_path()
    {
        var fixture = new HostileMembers.NumberSubclassToStringDoor("sentinel-number-subclass-structured");

        var rendered = Dump(ValueRenderer.RenderStructured(fixture));

        Assert.Contains("NumberSubclassToStringDoor", rendered);
        Assert.Contains("[REDACTED]", rendered);
        Assert.DoesNotContain("sentinel-number-subclass-structured", rendered, StringComparison.Ordinal);
        Assert.DoesNotContain("<error", rendered, StringComparison.Ordinal);
    }

    // RenderedValue.ObjectVal/ListVal carry an IReadOnlyDictionary/IReadOnlyList whose default
    // ToString() prints only the .NET type name, never a nested field's content — the same reason
    // RedactionVocabularyPropertyTests.Dump exists. Walks the whole tree instead.
    private static string Dump(RenderedValue value)
    {
        return value switch
        {
            RenderedValue.StringVal s => s.Value,
            RenderedValue.LongVal l => l.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
            RenderedValue.DoubleVal d => d.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
            RenderedValue.BooleanVal b => b.Value.ToString(),
            RenderedValue.InstantVal i => i.EpochMillis.ToString(System.Globalization.CultureInfo.InvariantCulture),
            RenderedValue.NullVal => "null",
            RenderedValue.ListVal list => string.Join(", ", list.Elements.Select(Dump)),
            RenderedValue.ObjectVal obj => obj.TypeName + "("
                + string.Join(", ", obj.Fields.Select(f => $"{f.Key}={Dump(f.Value)}")) + ")",
            _ => value.ToString() ?? "",
        };
    }
}
