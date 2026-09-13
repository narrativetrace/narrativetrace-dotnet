// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Text.RegularExpressions;
using FsCheck.Xunit;
using NarrativeTrace.Core;

namespace NarrativeTrace.Core.Tests;

public class TraceparentPropertyTests
{
    [Property]
    public bool Format_then_parse_is_identity(int flagsSeed)
    {
        var flags = Mod(flagsSeed, 256);
        var original = new Traceparent(
            SpanIdGenerator.GenerateTraceId(), SpanIdGenerator.GenerateSpanId(), flags);

        return Traceparent.Parse(original.Format()) == original;
    }

    /// <summary>Totality: a header from a stranger either yields nothing or yields a re-emittable value.</summary>
    [Property]
    public bool Parse_is_total_over_arbitrary_input(string anything)
    {
        var parsed = Traceparent.Parse(anything);
        return parsed is null || Traceparent.Parse(parsed.Format()) == parsed;
    }

    /// <summary>One corrupted character anywhere must never be silently accepted as a different trace.</summary>
    [Property]
    public bool A_corrupted_header_never_parses_to_the_original(int positionSeed, int replacementSeed)
    {
        var original = new Traceparent(
            SpanIdGenerator.GenerateTraceId(), SpanIdGenerator.GenerateSpanId(), 1);
        var header = original.Format();
        var position = Mod(positionSeed, header.Length);
        var replacement = (char)('g' + Mod(replacementSeed, 'z' - 'g' + 1));
        var corrupted = header[..position] + replacement + header[(position + 1)..];

        return Traceparent.Parse(corrupted) != original;
    }

    [Property]
    public bool Every_generated_id_pair_formats_to_the_canonical_length(int flagsSeed)
    {
        var flags = Mod(flagsSeed, 256);
        var traceparent = new Traceparent(
            SpanIdGenerator.GenerateTraceId(), SpanIdGenerator.GenerateSpanId(), flags);

        var formatted = traceparent.Format();
        return formatted.Length == 55
            && Regex.IsMatch(formatted, "^00-[0-9a-f]{32}-[0-9a-f]{16}-[0-9a-f]{2}$", RegexOptions.None,
                TimeSpan.FromSeconds(1));
    }

    private static int Mod(int value, int modulus) => ((value % modulus) + modulus) % modulus;
}
