// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using Xunit;

namespace NarrativeTrace.Core.Tests;

public class TraceparentTests
{
    private const string TraceIdValue = "4bf92f3577b34da6a3ce929d0e0e4736";
    private const string SpanIdValue = "00f067aa0ba902b7";
    private const string Header = "00-" + TraceIdValue + "-" + SpanIdValue + "-01";

    /// <summary>
    /// Cross-runtime pin: the W3C specification's own worked example, the header every
    /// NarrativeTrace runtime's traceparent suite is built on. If this ever needs changing, the
    /// wire format diverged and the runtimes no longer interoperate.
    /// </summary>
    [Fact]
    public void Parses_the_w3c_specifications_worked_example()
    {
        var parsed = Traceparent.Parse(Header);

        Assert.Equal(
            new Traceparent(new TraceId(TraceIdValue), new SpanId(SpanIdValue), 1), parsed);
        Assert.Equal(Header, parsed!.Format());
    }

    [Fact]
    public void Parses_a_well_formed_header()
    {
        var parsed = Traceparent.Parse(Header);

        Assert.NotNull(parsed);
        Assert.Equal(TraceIdValue, parsed!.TraceId.Value);
        Assert.Equal(SpanIdValue, parsed.ParentSpanId.Value);
        Assert.Equal(1, parsed.TraceFlags);
    }

    [Fact]
    public void Treats_an_absent_header_as_no_trace_context()
    {
        Assert.Null(Traceparent.Parse(null));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-traceparent")]
    [InlineData("00-" + TraceIdValue + "-" + SpanIdValue)]
    [InlineData("0-" + TraceIdValue + "-" + SpanIdValue + "-01")]
    [InlineData("000-" + TraceIdValue + "-" + SpanIdValue + "-01")]
    [InlineData("0g-" + TraceIdValue + "-" + SpanIdValue + "-01")]
    [InlineData("00-bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01")]
    [InlineData("00-4BF92F3577B34DA6A3CE929D0E0E4736-00f067aa0ba902b7-01")]
    [InlineData("00-4bf92f3577b34da6a3ce929d0e0e4736-0f067aa0ba902b7-01")]
    [InlineData("00-4bf92f3577b34da6a3ce929d0e0e4736-00F067AA0BA902B7-01")]
    [InlineData("00-" + TraceIdValue + "-" + SpanIdValue + "-0")]
    [InlineData("00-" + TraceIdValue + "-" + SpanIdValue + "-0g")]
    [InlineData("00-" + TraceIdValue + "-" + SpanIdValue + "-011")]
    [InlineData(" 00-" + TraceIdValue + "-" + SpanIdValue + "-01")]
    [InlineData("00-" + TraceIdValue + "-" + SpanIdValue + "-01 ")]
    public void Rejects_a_malformed_header_without_throwing(string malformed)
    {
        Assert.Null(Traceparent.Parse(malformed));
    }

    [Fact]
    public void Rejects_the_forbidden_ff_version()
    {
        Assert.Null(Traceparent.Parse("ff-" + TraceIdValue + "-" + SpanIdValue + "-01"));
    }

    [Fact]
    public void Rejects_an_all_zero_trace_id()
    {
        Assert.Null(Traceparent.Parse("00-" + new string('0', 32) + "-" + SpanIdValue + "-01"));
    }

    [Fact]
    public void Rejects_an_all_zero_parent_span_id()
    {
        Assert.Null(Traceparent.Parse("00-" + TraceIdValue + "-" + new string('0', 16) + "-01"));
    }

    [Fact]
    public void Rejects_trailing_fields_on_version_zero()
    {
        Assert.Null(Traceparent.Parse(Header + "-extra"));
    }

    [Fact]
    public void Accepts_trailing_fields_on_a_future_version_and_reads_the_first_four()
    {
        var parsed = Traceparent.Parse(
            "01-" + TraceIdValue + "-" + SpanIdValue + "-01-what-comes-next");

        Assert.NotNull(parsed);
        Assert.Equal(TraceIdValue, parsed!.TraceId.Value);
        Assert.Equal(SpanIdValue, parsed.ParentSpanId.Value);
    }

    [Fact]
    public void Rejects_a_future_version_with_an_empty_trailing_field()
    {
        Assert.Null(Traceparent.Parse("01-" + TraceIdValue + "-" + SpanIdValue + "-01-"));
    }

    [Fact]
    public void Formats_as_a_version_zero_header()
    {
        var traceparent = new Traceparent(new TraceId(TraceIdValue), new SpanId(SpanIdValue), 1);

        Assert.Equal(Header, traceparent.Format());
        Assert.Equal(55, traceparent.Format().Length);
        Assert.Equal(Header, traceparent.ToString());
    }

    [Fact]
    public void Formats_a_future_version_back_as_version_zero()
    {
        var parsed = Traceparent.Parse("cc-" + TraceIdValue + "-" + SpanIdValue + "-01-vendor");

        Assert.StartsWith("00-", parsed!.Format(), StringComparison.Ordinal);
    }

    [Fact]
    public void Pads_a_single_digit_flags_byte()
    {
        var traceparent = new Traceparent(new TraceId(TraceIdValue), new SpanId(SpanIdValue), 0);

        Assert.EndsWith("-00", traceparent.Format(), StringComparison.Ordinal);
    }

    [Fact]
    public void Reads_the_sampled_bit_out_of_the_flags_byte()
    {
        Assert.True(Traceparent.Parse(Header)!.Sampled);
        Assert.False(Traceparent.Parse("00-" + TraceIdValue + "-" + SpanIdValue + "-00")!.Sampled);
        Assert.False(Traceparent.Parse("00-" + TraceIdValue + "-" + SpanIdValue + "-fe")!.Sampled);
        Assert.True(Traceparent.Parse("00-" + TraceIdValue + "-" + SpanIdValue + "-ff")!.Sampled);
    }

    [Fact]
    public void Rejects_empty_trace_id_at_construction()
    {
        var ex = Assert.Throws<ArgumentException>(
            () => new Traceparent(TraceId.Empty, new SpanId(SpanIdValue), 1));
        Assert.Contains("traceId", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Rejects_empty_parent_span_id_at_construction()
    {
        var ex = Assert.Throws<ArgumentException>(
            () => new Traceparent(new TraceId(TraceIdValue), SpanId.Empty, 1));
        Assert.Contains("parentSpanId", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(256)]
    public void Rejects_flags_outside_one_byte_at_construction(int flags)
    {
        var ex = Assert.Throws<ArgumentException>(
            () => new Traceparent(new TraceId(TraceIdValue), new SpanId(SpanIdValue), flags));
        Assert.Contains("traceFlags", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Header_name_is_the_canonical_lowercase_name()
    {
        Assert.Equal("traceparent", Traceparent.HeaderName);
    }
}
