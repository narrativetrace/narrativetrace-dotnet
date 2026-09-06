// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using Xunit;

namespace NarrativeTrace.Core.Tests;

public class SpanIdTests
{
    [Fact]
    public void Constructor_accepts_valid_16_lowercase_hex()
    {
        var spanId = new SpanId("00f067aa0ba902b7");

        Assert.Equal("00f067aa0ba902b7", spanId.Value);
    }

    [Theory]
    [InlineData("INVALID")]
    [InlineData("00F067AA0BA902B7")]
    [InlineData("0000000000000000")]
    public void Constructor_throws_for_invalid_hex(string value)
    {
        var ex = Assert.Throws<ArgumentException>(() => new SpanId(value));
        Assert.Contains(value, ex.Message);
    }

    [Fact]
    public void Empty_has_empty_value_and_IsEmpty_is_true()
    {
        Assert.Equal("", SpanId.Empty.Value);
        Assert.True(SpanId.Empty.IsEmpty);
    }

    [Fact]
    public void IsEmpty_is_false_for_valid_span_id()
    {
        var spanId = new SpanId("00f067aa0ba902b7");

        Assert.False(spanId.IsEmpty);
    }

    [Fact]
    public void ToString_returns_raw_hex_value()
    {
        var spanId = new SpanId("00f067aa0ba902b7");

        Assert.Equal("00f067aa0ba902b7", spanId.ToString());
    }

    [Fact]
    public void Two_span_ids_with_same_hex_are_equal()
    {
        var a = new SpanId("00f067aa0ba902b7");
        var b = new SpanId("00f067aa0ba902b7");

        Assert.Equal(a, b);
        Assert.True(a == b);
    }

    [Fact]
    public void Default_instance_is_empty()
    {
        var uninitialized = default(SpanId);

        Assert.True(uninitialized.IsEmpty);
    }

    [Fact]
    public void Default_instance_equals_Empty()
    {
        Assert.Equal(SpanId.Empty, default(SpanId));
        Assert.True(default(SpanId) == SpanId.Empty);
        Assert.Equal(SpanId.Empty.GetHashCode(), default(SpanId).GetHashCode());
    }

    [Fact]
    public void Default_instance_renders_as_empty_string()
    {
        var uninitialized = default(SpanId);

        Assert.Equal("", uninitialized.Value);
        Assert.Equal("", uninitialized.ToString());
    }

    [Fact]
    public void Span_ids_grown_into_a_collection_by_capacity_are_empty()
    {
        var ids = new SpanId[3];

        Assert.All(ids, id => Assert.True(id.IsEmpty));
        Assert.All(ids, id => Assert.Equal(SpanId.Empty, id));
    }
}
