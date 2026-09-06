// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using Xunit;

namespace NarrativeTrace.Core.Tests;

public class CamelCaseSplitterTests
{
    [Theory]
    [InlineData("placeOrder", "place order")]
    [InlineData("PlaceOrder", "place order")]
    [InlineData("getOrderById", "get order by id")]
    [InlineData("run", "run")]
    [InlineData("", "")]
    [InlineData("ABC", "abc")]
    [InlineData("HTTPServer", "httpserver")]
    [InlineData("getHTTPResponse", "get httpresponse")]
    [InlineData("parseURLPath", "parse urlpath")]
    public void Splits_identifiers_into_lowercase_phrases(
        string input, string expected)
    {
        Assert.Equal(expected, CamelCaseSplitter.ToPhrase(input));
    }
}
