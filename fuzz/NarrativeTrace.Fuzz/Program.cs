// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using NarrativeTrace.Runtime;

namespace NarrativeTrace.Fuzz;

/// <summary>
/// Tier B coverage-guided fuzz harness — the top two targets from the parity document's fuzzing
/// list, driven by <c>SharpFuzz</c>. See <c>documentation/security-testing.md</c> and the
/// <c>Fuzz</c> NUKE target for how (and whether) this actually runs in a given environment.
/// </summary>
/// <remarks>
/// Selects the target from <c>args[0]</c> (<c>renderer</c> or <c>json</c>) so one harness binary
/// serves both, matching <c>FuzzBudget</c>'s per-target shape on the Java side. Everything past
/// that is <see cref="SharpFuzz.Fuzzer.OutOfProcess"/>'s own AFL persistent-mode wiring — the
/// harness itself never touches file descriptors or shared memory.
/// </remarks>
internal static class Program
{
    private static void Main(string[] args)
    {
        var run = Target(args.Length > 0 ? args[0] : "renderer");
        SharpFuzz.Fuzzer.OutOfProcess.Run(run);
    }

    private static Action<Stream> Target(string name) => name switch
    {
        "renderer" => RenderTarget,
        "json" => JsonTarget,
        _ => throw new ArgumentException($"unknown fuzz target: {name}"),
    };

    /// <summary>Target 2: the value renderer over an arbitrary byte-derived string.</summary>
    private static void RenderTarget(Stream stream)
    {
        var text = ReadText(stream);
        ValueRenderer.Render(text);
        ValueRenderer.RenderStructured(text);
    }

    /// <summary>Target 3: JSON emission over a one-node trace carrying an arbitrary captured value.</summary>
    private static void JsonTarget(Stream stream)
    {
        var text = ReadText(stream);
        var rendered = ValueRenderer.Render(text);
        var node = new TraceNode(
            new MethodSignature("FuzzTarget", "Probe", [new ParameterCapture("input", rendered, Redacted: false)]),
            new Returned(rendered),
            Children: [],
            DurationTicks: 0);
        var tree = new TraceTree([node]);
        JsonExporter.Export(tree, new TraceMetadata("fuzz", ScenarioResult.Success));
    }

    // AFL/libFuzzer hand over arbitrary bytes, not necessarily valid UTF-8 — decode leniently
    // (replacement character on a malformed sequence) rather than let a decoding exception mask
    // whatever the target itself does with the text.
    private static string ReadText(Stream stream)
    {
        using var reader = new StreamReader(stream, System.Text.Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
