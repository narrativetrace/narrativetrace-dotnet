// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using FsCheck.Xunit;
using NarrativeTrace.Glossary;
using Xunit;

namespace NarrativeTrace.Glossary.Tests;

/// <summary>
/// Safety property: serialization is deterministic —
/// <c>write ∘ read ∘ write == write</c> for any structurally valid glossary.
/// </summary>
public class GlossarySerializationProperties
{

    [Property(Arbitrary = [typeof(GlossaryArbitraries)])]
    public void Write_read_write_produces_identical_bytes(Glossary glossary)
    {
        var firstWrite = GlossaryJsonWriter.Write(glossary);

        var reread = GlossaryJsonReader.Read(firstWrite);

        Assert.Equal(firstWrite, GlossaryJsonWriter.Write(reread));
        Assert.True(reread.Invariant());
    }

    [Property(Arbitrary = [typeof(GlossaryArbitraries)])]
    public void Term_insertion_order_does_not_affect_output(Glossary glossary)
    {
        var reversed = new Glossary(
            glossary.SchemaVersion,
            glossary.Contexts,
            glossary.Terms.Reverse().ToList(),
            glossary.Abbreviations);

        Assert.Equal(GlossaryJsonWriter.Write(glossary), GlossaryJsonWriter.Write(reversed));
    }

    [Property(Arbitrary = [typeof(GlossaryArbitraries)])]
    public void Markdown_rendering_never_crashes_and_ends_with_newline(Glossary glossary)
    {
        var markdown = GlossaryMarkdownRenderer.Render(glossary);

        Assert.EndsWith("\n", markdown, StringComparison.Ordinal);
    }
}
