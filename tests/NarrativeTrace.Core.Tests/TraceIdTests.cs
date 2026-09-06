// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using Xunit;

namespace NarrativeTrace.Core.Tests;

public class TraceIdTests
{
    [Fact]
    public void Constructor_accepts_valid_32_lowercase_hex()
    {
        var traceId = new TraceId("4bf92f3577b34da6a3ce929d0e0e4736");

        Assert.Equal("4bf92f3577b34da6a3ce929d0e0e4736", traceId.Value);
    }

    [Theory]
    [InlineData("INVALID")]
    [InlineData("4BF92F3577B34DA6A3CE929D0E0E4736")]
    [InlineData("00000000000000000000000000000000")]
    public void Constructor_throws_for_invalid_hex(string value)
    {
        var ex = Assert.Throws<ArgumentException>(() => new TraceId(value));
        Assert.Contains(value, ex.Message);
    }

    [Fact]
    public void Empty_has_empty_value_and_IsEmpty_is_true()
    {
        Assert.Equal("", TraceId.Empty.Value);
        Assert.True(TraceId.Empty.IsEmpty);
    }

    [Fact]
    public void IsEmpty_is_false_for_valid_trace_id()
    {
        var traceId = new TraceId("4bf92f3577b34da6a3ce929d0e0e4736");

        Assert.False(traceId.IsEmpty);
    }

    [Fact]
    public void ToString_returns_raw_hex_value()
    {
        var traceId = new TraceId("4bf92f3577b34da6a3ce929d0e0e4736");

        Assert.Equal("4bf92f3577b34da6a3ce929d0e0e4736", traceId.ToString());
    }

    [Fact]
    public void Two_trace_ids_with_same_hex_are_equal()
    {
        var a = new TraceId("4bf92f3577b34da6a3ce929d0e0e4736");
        var b = new TraceId("4bf92f3577b34da6a3ce929d0e0e4736");

        Assert.Equal(a, b);
        Assert.True(a == b);
    }

    [Fact]
    public void HumanName_delegates_to_TraceNamer()
    {
        var traceId = new TraceId("4bf92f3577b34da6a3ce929d0e0e4736");

        Assert.Equal(
            TraceNamer.Name("4bf92f3577b34da6a3ce929d0e0e4736"),
            traceId.HumanName);
    }

    [Fact]
    public void HumanName_is_empty_for_empty_trace_id()
    {
        Assert.Equal("", TraceId.Empty.HumanName);
    }

    [Fact]
    public void Default_instance_is_empty()
    {
        var uninitialized = default(TraceId);

        Assert.True(uninitialized.IsEmpty);
    }

    [Fact]
    public void Default_instance_equals_Empty()
    {
        Assert.Equal(TraceId.Empty, default(TraceId));
        Assert.True(default(TraceId) == TraceId.Empty);
        Assert.Equal(TraceId.Empty.GetHashCode(), default(TraceId).GetHashCode());
    }

    [Fact]
    public void Default_instance_renders_as_empty_string()
    {
        var uninitialized = default(TraceId);

        Assert.Equal("", uninitialized.Value);
        Assert.Equal("", uninitialized.ToString());
        Assert.Equal("", uninitialized.HumanName);
    }

    [Fact]
    public void Trace_ids_grown_into_a_collection_by_capacity_are_empty()
    {
        var ids = new TraceId[3];

        Assert.All(ids, id => Assert.True(id.IsEmpty));
        Assert.All(ids, id => Assert.Equal(TraceId.Empty, id));
    }
}
