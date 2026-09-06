// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Clarity;
using Xunit;

namespace NarrativeTrace.Clarity.Tests;

public class IdentifierTokenizerTests
{
    [Theory]
    [InlineData("placeOrder", new[] { "place", "order" })]
    [InlineData("PlaceOrder", new[] { "place", "order" })]
    [InlineData("getOrderById", new[] { "get", "order", "by", "id" })]
    [InlineData("HTTPClient", new[] { "http", "client" })]
    [InlineData("parseJSON", new[] { "parse", "json" })]
    [InlineData("place_order", new[] { "place", "order" })]
    [InlineData("item2Count", new[] { "item", "2", "count" })]
    [InlineData("x", new[] { "x" })]
    [InlineData("", new string[0])]
    [InlineData("IO", new[] { "io" })]
    [InlineData("_place_order", new[] { "place", "order" })]
    [InlineData("__cache", new[] { "cache" })]
    [InlineData("_", new string[0])]
    [InlineData("  ", new string[0])]
    [InlineData("__  ", new string[0])]
    [InlineData(" Service", new[] { "service" })]
    public void Tokenizes_identifiers(
        string input, string[] expected)
    {
        var tokens = IdentifierTokenizer.Tokenize(input);
        Assert.Equal(expected, tokens);
    }
}
