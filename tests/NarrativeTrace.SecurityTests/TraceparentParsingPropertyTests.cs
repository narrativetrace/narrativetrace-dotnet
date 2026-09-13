// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using NarrativeTrace.SecurityTests.Corpus;
using Xunit;

namespace NarrativeTrace.SecurityTests;

/// <summary>
/// Target 1 of the shared fuzzing list: the W3C <c>traceparent</c> header parser.
/// </summary>
/// <remarks>
/// <para>
/// INTENT: <see cref="Traceparent.Parse"/> is a request-path entry point — a stranger's header
/// reaches it verbatim — so it must be total (never throw) and must accept or reject exactly the
/// cases the shared corpus (<c>HostileCorpus/headers.json</c>, <c>traceparent</c> array) says it
/// should, the same corpus every NarrativeTrace runtime's Target 1 replays.
/// </para>
/// <para>
/// @edgeCase <c>tracestate</c> parsing (the sibling array in the same fixture) is still an
/// explicit non-goal here, same as before this type existed:
/// <see cref="SpanContext.TraceState"/> remains a plain settable string, never parsed by anything
/// in this codebase. <see cref="Tracestate_parser_still_does_not_exist_in_this_port"/> is the
/// canary for that half — it fails, and says so, the day a <c>Tracestate</c>-shaped type appears.
/// </para>
/// </remarks>
public class TraceparentParsingPropertyTests
{
    public static IEnumerable<object[]> Traceparents() =>
        HostileCorpus.Traceparents().Select(c => new object[] { c });

    [Theory]
    [MemberData(nameof(Traceparents))]
    public void Every_corpus_header_parses_without_throwing(HeaderCase hostile)
    {
        var exception = Record.Exception(() => Traceparent.Parse(hostile.Value));

        Assert.True(
            exception is null,
            $"{hostile.Id} ({hostile.Description}): Parse must never throw, threw {exception}");
    }

    [Theory]
    [MemberData(nameof(Traceparents))]
    public void Every_corpus_header_matches_its_declared_acceptance(HeaderCase hostile)
    {
        var parsed = Traceparent.Parse(hostile.Value);

        Assert.True(
            hostile.Accepted == (parsed is not null),
            $"{hostile.Id} ({hostile.Description}): expected accepted={hostile.Accepted}, got {parsed?.Format() ?? "null"}");
    }

    /// <summary>
    /// The totality property from <c>TraceparentPropertyTests</c>, over the real hostile corpus
    /// rather than random input: an accepted header re-emits to a value that parses back to the
    /// same trace and parent span.
    /// </summary>
    [Theory]
    [MemberData(nameof(Traceparents))]
    public void An_accepted_corpus_header_reformats_to_an_equivalent_value(HeaderCase hostile)
    {
        var parsed = Traceparent.Parse(hostile.Value);
        if (parsed is null)
        {
            return;
        }

        Assert.Equal(parsed, Traceparent.Parse(parsed.Format()));
    }

    [Fact]
    public void Tracestate_parser_still_does_not_exist_in_this_port()
    {
        var coreAssembly = typeof(SpanContext).Assembly;

        var candidate = coreAssembly.GetTypes()
            .FirstOrDefault(t => t.Name.Contains("Tracestate", StringComparison.OrdinalIgnoreCase));

        Assert.True(
            candidate is null,
            $"a tracestate-shaped type ({candidate?.FullName}) now exists — implement the " +
            "tracestate half of Target 1 for real instead of leaving this stub.");
    }
}
