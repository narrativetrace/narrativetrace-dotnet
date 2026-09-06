// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using FsCheck;
using FsCheck.Xunit;
using NarrativeTrace.Core;

namespace NarrativeTrace.Core.Tests;

public class SpanIdGeneratorPropertyTests
{
    [Property]
    public bool Arbitrary_strings_are_rejected_as_trace_ids(NonNull<string> input)
    {
        // Random strings should virtually never be valid 32-char lowercase hex
        var valid = SpanIdGenerator.IsValidTraceId(input.Get);
        return !valid || (input.Get.Length == 32
            && input.Get.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f'));
    }

    [Property]
    public bool Arbitrary_strings_are_rejected_as_span_ids(NonNull<string> input)
    {
        var valid = SpanIdGenerator.IsValidSpanId(input.Get);
        return !valid || (input.Get.Length == 16
            && input.Get.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f'));
    }

    [Property]
    public bool Valid_trace_id_has_correct_length_and_chars(NonNull<string> input)
    {
        if (!SpanIdGenerator.IsValidTraceId(input.Get))
            return true; // vacuously true for invalid inputs

        return input.Get.Length == 32
            && input.Get.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f')
            && input.Get.Any(c => c != '0');
    }

    [Property]
    public bool Valid_span_id_has_correct_length_and_chars(NonNull<string> input)
    {
        if (!SpanIdGenerator.IsValidSpanId(input.Get))
            return true;

        return input.Get.Length == 16
            && input.Get.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f')
            && input.Get.Any(c => c != '0');
    }

    [Property]
    public bool Generated_trace_id_passes_validation()
    {
        var traceId = SpanIdGenerator.GenerateTraceId();
        return SpanIdGenerator.IsValidTraceId(traceId.Value)
            && !traceId.IsEmpty;
    }

    [Property]
    public bool Generated_span_id_passes_validation()
    {
        var spanId = SpanIdGenerator.GenerateSpanId();
        return SpanIdGenerator.IsValidSpanId(spanId.Value)
            && !spanId.IsEmpty;
    }

    // The bug class these pin: an id struct whose members dereference a
    // reference-typed backing field, which `default(T)` leaves null. Every id
    // must survive the total-function contract however it was obtained —
    // constructed, Empty, or defaulted into an array slot.
    [Property]
    public bool Every_trace_id_however_obtained_is_total(NonNull<string> input)
    {
        var uninitializedSlot = new TraceId[1];
        var ids = new TraceId[]
        {
            default,
            TraceId.Empty,
            uninitializedSlot[0],
            SpanIdGenerator.IsValidTraceId(input.Get)
                ? new TraceId(input.Get)
                : SpanIdGenerator.GenerateTraceId(),
        };

        return ids.All(id =>
            id.Value is not null
            && id.ToString() is not null
            && id.HumanName is not null
            && id.IsEmpty == (id.Value.Length == 0)
            && (!id.IsEmpty || id == TraceId.Empty));
    }

    [Property]
    public bool Every_span_id_however_obtained_is_total(NonNull<string> input)
    {
        var uninitializedSlot = new SpanId[1];
        var ids = new SpanId[]
        {
            default,
            SpanId.Empty,
            uninitializedSlot[0],
            SpanIdGenerator.IsValidSpanId(input.Get)
                ? new SpanId(input.Get)
                : SpanIdGenerator.GenerateSpanId(),
        };

        return ids.All(id =>
            id.Value is not null
            && id.ToString() is not null
            && id.IsEmpty == (id.Value.Length == 0)
            && (!id.IsEmpty || id == SpanId.Empty));
    }
}
