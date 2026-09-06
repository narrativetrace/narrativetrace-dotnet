// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Clarity;
using Xunit;

namespace NarrativeTrace.Clarity.Tests;

public sealed class ElementNoteComposerTests
{
    [Fact]
    public void Method_note_names_a_generic_verb_and_a_vague_noun()
    {
        Assert.Equal(
            "Generic verb 'process' + vague noun 'data'",
            ElementNoteComposer.MethodNote("ProcessData"));
    }

    [Fact]
    public void Method_note_stays_plain_for_a_domain_verb_and_a_concrete_noun()
    {
        Assert.Equal(
            "Verb 'reserve' + noun 'inventory'",
            ElementNoteComposer.MethodNote("ReserveInventory"));
    }

    [Fact]
    public void A_single_token_name_is_read_as_both_the_verb_and_the_noun()
    {
        // First and last token are the same one. Java's first cycle behaves
        // identically; naming an object the method has none of is the point.
        Assert.Equal(
            "Generic verb 'process' + noun 'process'",
            ElementNoteComposer.MethodNote("Process"));
    }

    [Fact]
    public void A_null_method_name_is_rejected()
    {
        Assert.Throws<ArgumentNullException>(() => ElementNoteComposer.MethodNote(null!));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("_")]
    public void A_name_with_no_tokens_is_rejected(string methodName)
    {
        // "_" is the trap: non-empty, but the tokenizer treats underscores as
        // delimiters, so it yields no tokens and an unguarded first/last read
        // would throw IndexOutOfRange instead of naming the bad argument.
        Assert.Throws<ArgumentException>(() => ElementNoteComposer.MethodNote(methodName));
    }
}
