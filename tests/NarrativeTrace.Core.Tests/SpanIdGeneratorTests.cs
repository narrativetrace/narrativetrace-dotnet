// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using Xunit;

namespace NarrativeTrace.Core.Tests;

public class SpanIdGeneratorTests
{
    [Fact]
    public void IsValidTraceId_accepts_32_lowercase_hex_chars()
    {
        Assert.True(SpanIdGenerator.IsValidTraceId("4bf92f3577b34da6a3ce929d0e0e4736"));
    }

    [Theory]
    [InlineData("4BF92F3577B34DA6A3CE929D0E0E4736")]
    [InlineData("4bf92f3577b34da6a3ce929d0e0e473G")]
    public void IsValidTraceId_rejects_non_lowercase_hex(string value)
    {
        Assert.False(SpanIdGenerator.IsValidTraceId(value));
    }

    [Theory]
    [InlineData("4bf92f3577b34da6")]
    [InlineData("4bf92f3577b34da6a3ce929d0e0e47360")]
    [InlineData("")]
    public void IsValidTraceId_rejects_wrong_length(string value)
    {
        Assert.False(SpanIdGenerator.IsValidTraceId(value));
    }

    [Fact]
    public void IsValidTraceId_rejects_all_zeros()
    {
        Assert.False(SpanIdGenerator.IsValidTraceId("00000000000000000000000000000000"));
    }

    [Fact]
    public void IsValidTraceId_rejects_null()
    {
        Assert.False(SpanIdGenerator.IsValidTraceId(null!));
    }

    [Fact]
    public void IsValidSpanId_accepts_16_lowercase_hex_chars()
    {
        Assert.True(SpanIdGenerator.IsValidSpanId("00f067aa0ba902b7"));
    }

    [Theory]
    [InlineData("00F067AA0BA902B7")]
    [InlineData("00f067aa0ba902bz")]
    public void IsValidSpanId_rejects_non_lowercase_hex(string value)
    {
        Assert.False(SpanIdGenerator.IsValidSpanId(value));
    }

    [Theory]
    [InlineData("00f067aa")]
    [InlineData("00f067aa0ba902b70")]
    [InlineData("")]
    public void IsValidSpanId_rejects_wrong_length(string value)
    {
        Assert.False(SpanIdGenerator.IsValidSpanId(value));
    }

    [Fact]
    public void IsValidSpanId_rejects_all_zeros()
    {
        Assert.False(SpanIdGenerator.IsValidSpanId("0000000000000000"));
    }

    [Fact]
    public void IsValidSpanId_rejects_null()
    {
        Assert.False(SpanIdGenerator.IsValidSpanId(null!));
    }

    [Fact]
    public void GenerateTraceId_returns_valid_trace_id()
    {
        var traceId = SpanIdGenerator.GenerateTraceId();

        Assert.False(traceId.IsEmpty);
        Assert.Equal(32, traceId.Value.Length);
        Assert.True(SpanIdGenerator.IsValidTraceId(traceId.Value));
    }

    [Fact]
    public void GenerateSpanId_returns_valid_span_id()
    {
        var spanId = SpanIdGenerator.GenerateSpanId();

        Assert.False(spanId.IsEmpty);
        Assert.Equal(16, spanId.Value.Length);
        Assert.True(SpanIdGenerator.IsValidSpanId(spanId.Value));
    }

    [Fact]
    public void Generated_trace_ids_are_unique()
    {
        var ids = Enumerable.Range(0, 1000)
            .Select(_ => SpanIdGenerator.GenerateTraceId().Value)
            .ToHashSet();

        Assert.Equal(1000, ids.Count);
    }

    [Fact]
    public void Generated_span_ids_are_unique()
    {
        var ids = Enumerable.Range(0, 1000)
            .Select(_ => SpanIdGenerator.GenerateSpanId().Value)
            .ToHashSet();

        Assert.Equal(1000, ids.Count);
    }
}
