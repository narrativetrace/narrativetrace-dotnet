// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.SkillsEvals;
using Xunit;

namespace NarrativeTrace.SkillsEvals.Tests;

/// <summary>
/// The scripted user replies a case declares in its <c>case.json</c>, keyed BY TURN NUMBER. Every
/// malformed declaration throws rather than shortening the conversation: a case whose second turn
/// silently did not happen passes its approval grader for the worst reason — nothing was filed
/// because nothing was asked.
/// </summary>
public sealed class CaseTurnsTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("nt-caseturns-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private void Manifest(string json) => File.WriteAllText(Path.Combine(_dir, "case.json"), json);

    [Fact]
    public void A_case_with_no_manifest_at_all_is_one_turn()
    {
        Assert.Empty(CaseTurns.ScriptedRepliesFor(_dir));
    }

    [Fact]
    public void A_case_that_declares_no_turns_is_one_turn()
    {
        Manifest("{ \"fixture\": \"skills-evals/fixtures/existing-project\" }");

        Assert.Empty(CaseTurns.ScriptedRepliesFor(_dir));
    }

    [Fact]
    public void Reads_the_reply_a_declared_second_turn_is_given()
    {
        Manifest("{ \"fixture\": \"f\", \"turns\": { \"2\": \"yes, file it\" } }");

        Assert.Equal(["yes, file it"], CaseTurns.ScriptedRepliesFor(_dir));
    }

    [Fact]
    public void Reads_every_reply_in_turn_order_whatever_order_the_file_listed_them_in()
    {
        Manifest("{ \"turns\": { \"10\": \"ten\", \"3\": \"three\", \"2\": \"two\", \"4\": \"four\", \"5\": \"5\", \"6\": \"6\", \"7\": \"7\", \"8\": \"8\", \"9\": \"9\" } }");

        var replies = CaseTurns.ScriptedRepliesFor(_dir);

        Assert.Equal(["two", "three", "four", "5", "6", "7", "8", "9", "ten"], replies);
    }

    /// <summary>A top-level field spelled like a turn is not a turn — the reading is scoped to the object.</summary>
    [Fact]
    public void A_top_level_field_named_like_a_turn_is_not_read_as_one()
    {
        Manifest("{ \"2\": \"not a turn\", \"turns\": { \"2\": \"the turn\" } }");

        Assert.Equal(["the turn"], CaseTurns.ScriptedRepliesFor(_dir));
    }

    [Theory]
    [InlineData("1")]
    [InlineData("0")]
    [InlineData("-2")]
    public void Refuses_a_reply_declared_at_or_before_the_first_turn(string key)
    {
        Manifest("{ \"turns\": { \"" + key + "\": \"hello\" } }");

        var thrown = Assert.Throws<InvalidOperationException>(() => CaseTurns.ScriptedRepliesFor(_dir));
        Assert.Contains("turn 1 is the case's own prompt.md", thrown.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Refuses_a_gap_in_the_turn_numbers()
    {
        Manifest("{ \"turns\": { \"2\": \"yes\", \"4\": \"and then\" } }");

        var thrown = Assert.Throws<InvalidOperationException>(() => CaseTurns.ScriptedRepliesFor(_dir));
        Assert.Contains("no reply for turn 3", thrown.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("two")]
    [InlineData("2.0")]
    [InlineData("")]
    public void Refuses_a_turn_key_that_is_not_a_turn_number(string key)
    {
        Manifest("{ \"turns\": { \"" + key + "\": \"yes\" } }");

        var thrown = Assert.Throws<InvalidOperationException>(() => CaseTurns.ScriptedRepliesFor(_dir));
        Assert.Contains("not a turn number", thrown.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Refuses_a_declared_turns_object_that_lists_no_reply()
    {
        Manifest("{ \"turns\": { } }");

        var thrown = Assert.Throws<InvalidOperationException>(() => CaseTurns.ScriptedRepliesFor(_dir));
        Assert.Contains("lists no reply", thrown.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("\"\"")]
    [InlineData("\"   \"")]
    [InlineData("\"\\n\"")]
    public void Refuses_a_blank_reply_because_a_turn_with_nothing_to_say_cannot_be_driven(string reply)
    {
        Manifest("{ \"turns\": { \"2\": " + reply + " } }");

        var thrown = Assert.Throws<InvalidOperationException>(() => CaseTurns.ScriptedRepliesFor(_dir));
        Assert.Contains("blank reply", thrown.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("[\"yes\"]")]
    [InlineData("\"yes\"")]
    [InlineData("{ \"2\": 2 }")]
    public void Refuses_turns_that_are_not_an_object_of_strings(string turns)
    {
        Manifest("{ \"turns\": " + turns + " }");

        Assert.Throws<InvalidOperationException>(() => CaseTurns.ScriptedRepliesFor(_dir));
    }

    [Fact]
    public void Unescapes_what_json_had_to_escape_in_the_users_own_words()
    {
        Manifest("{ \"turns\": { \"2\": \"say \\\"no\\\"\\r\\nthen C:\\\\stop — sí\" } }");

        Assert.Equal(["say \"no\"\r\nthen C:\\stop — sí"], CaseTurns.ScriptedRepliesFor(_dir));
    }

    [Fact]
    public void A_manifest_that_is_not_json_names_its_own_file()
    {
        Manifest("{ \"turns\": { \"2\": ");

        var thrown = Assert.Throws<InvalidOperationException>(() => CaseTurns.ScriptedRepliesFor(_dir));
        Assert.Contains("case.json", thrown.Message, StringComparison.Ordinal);
    }
}
