// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using FsCheck.Xunit;
using NarrativeTrace.Glossary;
using Xunit;

namespace NarrativeTrace.Glossary.Tests;

/// <summary>
/// Safety properties: merging is additive-only (never deletes or rewrites an
/// existing entry) and idempotent (re-merging the same harvest changes
/// nothing, byte-for-byte).
/// </summary>
public class GlossaryMergerProperties
{
    private static readonly GlossaryMerger Merger =
        new(() => new DateTime(2026, 8, 11, 0, 0, 0, DateTimeKind.Utc));

    [Property(Arbitrary = [typeof(GlossaryArbitraries)])]
    public void Merge_never_removes_or_mutates_existing_entries(
        Glossary existing, HarvestResult harvest)
    {
        var merged = Merger.Merge(existing, harvest).Glossary;

        Assert.All(existing.Terms, term => Assert.Contains(term, merged.Terms));
        Assert.All(
            existing.Contexts,
            pair => Assert.Same(pair.Value, merged.Contexts[pair.Key]));
        Assert.Equal(existing.SchemaVersion, merged.SchemaVersion);
    }

    [Property(Arbitrary = [typeof(GlossaryArbitraries)])]
    public void Merging_the_same_harvest_twice_is_byte_identical(
        Glossary existing, HarvestResult harvest)
    {
        var once = Merger.Merge(existing, harvest);

        var twice = Merger.Merge(once.Glossary, harvest);

        Assert.Empty(twice.NewTerms);
        Assert.Equal(GlossaryJsonWriter.Write(once.Glossary), GlossaryJsonWriter.Write(twice.Glossary));
    }
}
