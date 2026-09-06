// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Glossary;
using Xunit;

namespace NarrativeTrace.Glossary.Tests;

public class JsonParserTests
{
    [Fact]
    public void Parses_objects_arrays_strings_integers_booleans()
    {
        var value = JsonParser.Parse(
            """{"a": 1, "b": [true, false, "x"], "c": {"d": -2}}""");

        var root = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object>>(value);
        Assert.Equal(1L, root["a"]);
        var array = Assert.IsAssignableFrom<IReadOnlyList<object>>(root["b"]);
        Assert.Equal([true, false, "x"], array);
        var nested = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object>>(root["c"]);
        Assert.Equal(-2L, nested["d"]);
    }

    [Fact]
    public void Json_null_parses_to_sentinel_distinct_from_absent_key()
    {
        var root = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object>>(
            JsonParser.Parse("""{"a": null}"""));

        Assert.Same(JsonParser.Null, root["a"]);
        Assert.False(root.ContainsKey("b"));
    }

    [Fact]
    public void Parses_empty_object_and_empty_array()
    {
        Assert.Empty(Assert.IsAssignableFrom<IReadOnlyDictionary<string, object>>(
            JsonParser.Parse("{}")));
        Assert.Empty(Assert.IsAssignableFrom<IReadOnlyList<object>>(
            JsonParser.Parse("[]")));
    }

    [Theory]
    [InlineData("\"a\\\"b\"", "a\"b")]
    [InlineData("\"a\\\\b\"", "a\\b")]
    [InlineData("\"a\\/b\"", "a/b")]
    [InlineData("\"a\\nb\"", "a\nb")]
    [InlineData("\"a\\rb\"", "a\rb")]
    [InlineData("\"a\\tb\"", "a\tb")]
    [InlineData("\"a\\bb\"", "a\bb")]
    [InlineData("\"a\\fb\"", "a\fb")]
    [InlineData("\"a\\u00e9b\"", "aéb")]
    public void Parses_string_escapes(string json, string expected)
    {
        Assert.Equal(expected, JsonParser.Parse(json));
    }

    [Theory]
    [InlineData("1.5", "fractional")]
    [InlineData("1e3", "fractional")]
    [InlineData("1E3", "fractional")]
    [InlineData("""{"a": 1, "a": 2}""", "duplicate key 'a'")]
    [InlineData("{} {}", "trailing content")]
    [InlineData("\"abc", "unterminated string")]
    [InlineData("\"a\\", "unterminated escape")]
    [InlineData("\"a\\x\"", "invalid escape")]
    [InlineData("\"a\\u12", "unterminated unicode escape")]
    [InlineData("\"a\\uzzzz\"", "invalid unicode escape")]
    [InlineData("tru", "invalid literal")]
    [InlineData("{\"a\" 1}", "expected ':'")]
    [InlineData("[1 2]", "expected ','")]
    [InlineData("", "unexpected end of input")]
    [InlineData("-", "invalid number")]
    public void Rejects_malformed_json_with_position(string json, string expectedFragment)
    {
        var ex = Assert.Throws<ArgumentException>(() => JsonParser.Parse(json));

        Assert.Contains("invalid JSON at position", ex.Message, StringComparison.Ordinal);
        Assert.Contains(expectedFragment, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Rejects_raw_control_character_in_string()
    {
        var ex = Assert.Throws<ArgumentException>(
            () => JsonParser.Parse("\"a\u0001b\""));

        Assert.Contains("raw control character", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Rejects_null_input()
    {
        Assert.Throws<ArgumentNullException>(() => JsonParser.Parse(null!));
    }

    [Fact]
    public void Whitespace_around_tokens_is_ignored()
    {
        var root = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object>>(
            JsonParser.Parse(" \n\t{ \"a\" : [ 1 , 2 ] }\r\n"));

        Assert.Equal([1L, 2L], Assert.IsAssignableFrom<IReadOnlyList<object>>(root["a"]));
    }
}
