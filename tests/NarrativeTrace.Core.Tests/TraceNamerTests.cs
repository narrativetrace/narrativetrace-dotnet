// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using Xunit;

namespace NarrativeTrace.Core.Tests;

public class TraceNamerTests
{
    [Fact]
    public void All_zeros_hex_produces_first_word_from_each_array()
    {
        var name = TraceNamer.Name("00000000000000000000000000000000");
        var words = name.Split(' ');

        Assert.Equal(3, words.Length);
        Assert.Equal("red", words[0]);
        Assert.Equal("fox", words[1]);
        Assert.Equal("runs", words[2]);
    }

    [Fact]
    public void All_fs_hex_produces_last_word_from_each_array()
    {
        var name = TraceNamer.Name("ffffffffffffffffffffffffffffffff");
        var words = name.Split(' ');

        // bits = 0xFFFFFFF, adj=255, noun=511, verb=255
        Assert.Equal("nutty", words[0]);
        Assert.Equal("rig", words[1]);
        Assert.Equal("fans", words[2]);
    }

    [Fact]
    public void Determinism_same_input_same_output()
    {
        var hex = "a3f7c1b290de4f8801234567deadbeef";

        Assert.Equal(TraceNamer.Name(hex), TraceNamer.Name(hex));
    }

    [Fact]
    public void Output_is_three_space_separated_lowercase_words()
    {
        var hex = "5a8b3c2d1e0f9876543210abcdef0123";

        Assert.Matches("^[a-z]+ [a-z]+ [a-z]+$", TraceNamer.Name(hex));
    }

    [Fact]
    public void Each_word_is_between_3_and_6_characters()
    {
        var hex = "deadbeef01234567890abcdef1234567";

        foreach (var word in TraceNamer.Name(hex).Split(' '))
        {
            Assert.InRange(word.Length, 3, 6);
        }
    }

    [Fact]
    public void Different_trace_ids_produce_different_names()
    {
        var names = new HashSet<string>();
        for (var i = 0; i < 1000; i++)
        {
            var traceId = SpanIdGenerator.GenerateTraceId();
            names.Add(TraceNamer.Name(traceId.Value));
        }

        Assert.True(names.Count > 990, $"only {names.Count} distinct");
    }

    [Fact]
    public void Only_first_7_hex_chars_matter()
    {
        var hex1 = "abcdef0000000000000000000000aaaa";
        var hex2 = "abcdef0000000000000000000000bbbb";

        Assert.Equal(TraceNamer.Name(hex1), TraceNamer.Name(hex2));
    }

    [Fact]
    public void Different_first_7_hex_chars_produce_different_names()
    {
        var hex1 = "1234567000000000000000000000aaaa";
        var hex2 = "7654321000000000000000000000aaaa";

        Assert.NotEqual(TraceNamer.Name(hex1), TraceNamer.Name(hex2));
    }

    [Fact]
    public void Validate_rejects_wrong_array_length()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => TraceNamer.Validate("TEST", ["abc"], 2));
        Assert.Contains("has 1 words, expected 2", ex.Message);
    }

    [Fact]
    public void Validate_rejects_word_shorter_than_3_chars()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => TraceNamer.Validate("TEST", ["ab", "def"], 2));
        Assert.Contains("length 2 outside 3-6 range", ex.Message);
    }

    [Fact]
    public void Validate_rejects_word_longer_than_6_chars()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => TraceNamer.Validate("TEST", ["toolong7"], 1));
        Assert.Contains("length 8 outside 3-6 range", ex.Message);
    }
}
