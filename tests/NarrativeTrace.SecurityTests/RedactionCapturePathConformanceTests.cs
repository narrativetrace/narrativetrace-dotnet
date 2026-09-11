// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using NarrativeTrace.Proxy;
using NarrativeTrace.Runtime;
using NarrativeTrace.SecurityTests.Corpus;
using NarrativeTrace.SecurityTests.Oracle;
using Xunit;

namespace NarrativeTrace.SecurityTests;

/// <summary>
/// The redaction oracle over <c>redaction.json</c>, replayed through the REAL capture path —
/// <see cref="NarrativeTraceProxy"/>/<c>NarrativeInterceptor</c>, the one an application actually
/// calls — rather than a hand-built payload fed straight to <see cref="ValueRenderer"/>.
/// </summary>
/// <remarks>
/// <para>
/// INTENT: <see cref="RedactionVocabularyPropertyTests"/> has driven this corpus for a long time
/// and never once caught the parameter-name capture defect this port's redaction wave just fixed,
/// because every one of its cases reaches <c>ValueRenderer</c> directly — one layer below where the
/// defect actually lived (<c>NarrativeInterceptor.BuildParameterInfo</c>, which used to consult only
/// <c>[NotTraced]</c>). A test aimed at the renderer cannot see a bug in the capture decision that
/// happens before any renderer runs. This class is the corpus aimed at the real seam instead — the
/// java flagship's mirror of the same finding: with the capture fix reverted, 28 of the 88 rows fail
/// here (every <c>name</c> case whose <c>expect</c> is <c>redacted</c>), and this file is what makes
/// that observable rather than assumed.
/// </para>
/// <para>
/// <b>@edgeCase</b> A parameter's name is normally fixed at compile time by the interface a test
/// writes — but the corpus's names ARE the test data (accented, NFD-decomposed and CJK spellings
/// included), so each name case's interface has to be synthesized at runtime; see
/// <see cref="DynamicCaptureHarness"/>. A name that cannot be emitted as a legal parameter
/// identifier fails LOUDLY, tagged <c>SKIPPED</c> with the row id in the message, rather than being
/// silently absent from the run — a green suite that quietly ran fewer than 88 cases is exactly the
/// failure mode this wave exists to close.
/// </para>
/// <para>
/// <b>@edgeCase</b> Assertions run on the captured <see cref="ParameterCapture"/> itself
/// (<see cref="ParameterCapture.Redacted"/>, <see cref="ParameterCapture.RenderedValue"/>), not only
/// on rendered text: the captured event is also what reaches the audit path, the buffered consumer
/// and any pipeline SPI listener, so a secret merely masked on the way out of a renderer has already
/// travelled further than that. The renderer/artifact sweep below is in addition to that, not
/// instead of it.
/// </para>
/// </remarks>
public class RedactionCapturePathConformanceTests
{
    // renderer:structural / renderer:structural-entries and the written .nt / .structural.json
    // artifacts never carry a value, redacted or not — StructuralTraceRenderer emits parameter
    // NAMES only (ADR-002: the AI-safe artifact is value-free by construction). Confirmed by
    // direct probe before this file was written, not assumed: every other emitter/artifact key
    // echoed a visible probe value; exactly these did not.
    private static readonly HashSet<string> ValueFreeEmitters =
        new(StringComparer.Ordinal) { "renderer:structural", "renderer:structural-entries" };

    public static IEnumerable<object[]> Corpus() =>
        HostileCorpus.Redactions().Select(c => new object[] { c });

    [Theory]
    [MemberData(nameof(Corpus))]
    public void Every_corpus_row_goes_the_way_it_declares_through_the_real_proxy(
        RedactionCase corpusCase)
    {
        var parameterName = corpusCase.IsName ? corpusCase.Name! : "data";
        var argument = corpusCase.IsName ? corpusCase.Canary! : corpusCase.Value!;

        DynamicCaptureCase built;
        try
        {
            built = DynamicCaptureHarness.Build(corpusCase.Id, parameterName);
        }
        catch (Exception ex)
        {
            Assert.Fail(
                $"SKIPPED {corpusCase.Id} ({corpusCase.Description}): the name '{parameterName}' "
                + $"could not be emitted as a legal parameter identifier — {ex.GetType().Name}: {ex.Message}");
            return;
        }

        var (parameter, tree) = Capture(built, argument);
        var outputs = Oracles.WithinBudget(
            "capture-path outputs for " + corpusCase.Id, () => Emitters.EveryOutput(tree));

        if (corpusCase.ExpectsRedaction)
        {
            AssertRedactedAtCapture(corpusCase, parameter);
            AssertHiddenEverywhere(corpusCase, outputs);
        }
        else
        {
            AssertVisibleAtCapture(corpusCase, parameter);
            AssertVisibleWhereValuesCanAppear(corpusCase, outputs);
        }

        Oracles.BoundedSize(outputs);
    }

    [Fact]
    public void The_corpus_still_covers_both_directions_and_both_axes()
    {
        // Pinned independently of RedactionVocabularyPropertyTests's own copy of this assertion —
        // this file must keep running the same corpus, not a narrowed view of it, if that file's
        // copy ever drifts or is deleted.
        var rows = HostileCorpus.Redactions();

        Assert.Equal(88, rows.Count);
        Assert.Equal(50, rows.Count(r => r.IsName));
        Assert.Equal(38, rows.Count(r => !r.IsName));
        Assert.Equal(28, rows.Count(r => r.IsName && r.ExpectsRedaction));
    }

    private static (ParameterCapture Parameter, TraceTree Tree) Capture(
        DynamicCaptureCase built, string argument)
    {
        var ctx = new SyncNarrativeContext(new NarrativeTraceConfig());
        var proxy = NarrativeTraceProxy.Create(built.InterfaceType, built.Instance, ctx);

        built.Method.Invoke(proxy, [argument]);

        var tree = ctx.CaptureTrace();
        return (tree.Roots[0].Signature.Parameters[0], tree);
    }

    private static void AssertRedactedAtCapture(RedactionCase corpusCase, ParameterCapture parameter)
    {
        Assert.True(
            parameter.Redacted,
            $"{corpusCase.Id} ({corpusCase.Description}): must be flagged Redacted at capture");
        Assert.DoesNotContain(
            corpusCase.Secret, parameter.RenderedValue, StringComparison.Ordinal);
        Assert.Equal(RedactionPolicy.Marker, parameter.RenderedValue);
    }

    private static void AssertVisibleAtCapture(RedactionCase corpusCase, ParameterCapture parameter)
    {
        Assert.False(
            parameter.Redacted,
            $"{corpusCase.Id} ({corpusCase.Description}): must NOT be flagged Redacted at capture "
            + "— a false positive here is a default teams switch off entirely");
        Assert.Contains(
            corpusCase.Secret, parameter.RenderedValue, StringComparison.Ordinal);
    }

    private static void AssertHiddenEverywhere(
        RedactionCase corpusCase, IReadOnlyDictionary<string, string> outputs)
    {
        Assert.True(outputs.Count > 0, "the emitter sweep must not be empty");
        foreach (var (emitter, output) in outputs)
        {
            Assert.False(
                output.Contains(corpusCase.Secret, StringComparison.Ordinal),
                $"{corpusCase.Id} ({corpusCase.Description}) must not reach {emitter}");
        }
    }

    private static void AssertVisibleWhereValuesCanAppear(
        RedactionCase corpusCase, IReadOnlyDictionary<string, string> outputs)
    {
        foreach (var (emitter, output) in outputs)
        {
            if (IsValueFree(emitter))
            {
                continue;
            }

            Assert.Contains(
                corpusCase.Secret,
                output,
                StringComparison.Ordinal);
        }
    }

    private static bool IsValueFree(string emitterKey) =>
        ValueFreeEmitters.Contains(emitterKey)
        || emitterKey.EndsWith(".nt", StringComparison.Ordinal)
        || emitterKey.EndsWith(".structural.json", StringComparison.Ordinal);
}
