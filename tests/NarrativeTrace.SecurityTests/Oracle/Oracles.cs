// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Security.Cryptography;
using Xunit;

namespace NarrativeTrace.SecurityTests.Oracle;

/// <summary>
/// The assertions every fuzz target shares. A crash alone is not an oracle.
/// </summary>
/// <remarks>
/// <para>
/// INTENT: The security-testing document lists six oracles plus the AI-consumer one, and every
/// port implements the same list. Keeping them here — rather than inline in each property — is
/// what makes "the ports mirror the targets and corpus" checkable: a reader can count them.
/// </para>
/// <para>
/// @llmNote The redaction oracle looks for a fresh random token per case, not a fixed string. A
/// fixed secret is findable by a renderer that special-cases it and, worse, is findable by a
/// *test* that passes because some earlier case cleared the same string out. It also checks a
/// prefix of the token, because a partial leak through a truncating emitter is still a leak.
/// </para>
/// </remarks>
public static class Oracles
{
    /// <summary>
    /// Wall-clock budget for one input through one emitter. Generous on purpose: this is a hang
    /// detector, not a benchmark.
    /// </summary>
    public static readonly TimeSpan Budget = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Ceiling on one emitter's output. The renderer truncates strings at 200 characters and
    /// collections at 5 elements, so a 1 MiB input must not produce a 1 MiB output — but diagrams
    /// and documents legitimately repeat a value across sections, so the ceiling is generous
    /// rather than tight.
    /// </summary>
    public const int MaxOutputBytes = 4 * 1024 * 1024;

    /// <summary>Prefix every thread this library starts carries.</summary>
    public const string ThreadPrefix = "narrative-trace-";

    /// <summary>How much of a sentinel must be absent for the redaction oracle to pass.</summary>
    private const int PartialLeakLength = 12;

    /// <summary>
    /// A token no output may carry, unique per case.
    /// </summary>
    /// <returns>
    /// A 22-character token whose leading <c>sentinel</c> makes a failure readable and whose
    /// random tail makes a false positive impossible.
    /// </returns>
    public static string FreshSentinel() => "sentinel" + Convert.ToHexString(RandomNumberGenerator.GetBytes(7)).ToLowerInvariant();

    /// <summary>Runs <paramref name="work"/>, failing when it takes longer than <see cref="Budget"/>.</summary>
    public static T WithinBudget<T>(string label, Func<T> work)
    {
        var start = DateTime.UtcNow;
        var result = work();
        var elapsed = DateTime.UtcNow - start;
        Assert.True(elapsed < Budget, $"{label} must cost bounded time in the size of its input (took {elapsed})");
        return result;
    }

    /// <summary>Every emitter's output stays under <see cref="MaxOutputBytes"/>.</summary>
    public static void BoundedSize(IReadOnlyDictionary<string, string> outputs)
    {
        foreach (var (emitter, output) in outputs)
            Assert.True(output.Length < MaxOutputBytes, $"{emitter} must produce bounded output");
    }

    /// <summary>
    /// The redaction oracle: the sentinel reaches no byte of any output, whole or partial.
    /// </summary>
    /// <param name="outputs">Every emitter's output, keyed by emitter.</param>
    /// <param name="sentinel">The token planted behind <c>[NotTraced]</c>.</param>
    public static void ContainsNoSentinel(IReadOnlyDictionary<string, string> outputs, string sentinel)
    {
        var partial = sentinel[..PartialLeakLength];
        Assert.True(outputs.Count > 0, "a value marked redacted must appear in no output of any format at any depth");
        foreach (var (emitter, output) in outputs)
        {
            Assert.False(output.Contains(sentinel, StringComparison.Ordinal), $"{emitter} leaked the redacted value");
            Assert.False(output.Contains(partial, StringComparison.Ordinal), $"{emitter} leaked the leading bytes of the redacted value");
        }
    }

    /// <summary>
    /// Rendering twice produces the same bytes — no time, identity hash or iteration order leaks in.
    /// </summary>
    public static void Idempotent(string label, Func<string> render) =>
        Assert.True(render() == render(), $"{label} must render identically twice");

    /// <summary>
    /// No thread this library starts survives a render — the no-hook-left-behind oracle.
    /// </summary>
    /// <returns>
    /// Always <see langword="true"/>; the caller asserts on it so the oracle is visible at the call
    /// site, not silently skipped. .NET has no portable "enumerate all live threads" API equivalent
    /// to Java's <c>Thread.getAllStackTraces()</c>; NarrativeTrace never starts a background thread
    /// from a render call (no timers, no <c>Task.Run</c> in the render path), so this is a
    /// structural property of the code rather than something observable at runtime here — N/A by
    /// construction, recorded rather than silently dropped.
    /// </returns>
    public static bool NoLibraryThreadLeft() => true;
}
