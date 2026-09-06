// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Core;

/// <summary>
/// Overall result of a traced scenario, in the two spellings the product needs.
/// </summary>
/// <remarks>
/// <para>
/// One fact, two audiences. The canonical <c>chapter-tree.schema.json</c>
/// constrains <c>scenario.result</c> to <c>success</c>/<c>error</c>, while the
/// Markdown caption reads <c>**Result:** PASSED</c>. Carrying both spellings on
/// one enum keeps them from drifting and makes an out-of-contract value
/// unrepresentable rather than merely untested.
/// </para>
/// <para>
/// <b>Never write the member name into an artifact.</b>
/// <see cref="ScenarioResultExtensions.WireName"/> is the only spelling the
/// cross-port schema accepts;
/// <see cref="ScenarioResultExtensions.DisplayName"/> is for human-facing prose.
/// <c>ToString()</c> yields neither — it yields <c>"Success"</c>/<c>"Error"</c>,
/// which the schema rejects.
/// </para>
/// <para>
/// Ported from the Java reference's <c>ScenarioResult</c>, which is the
/// cross-port contract this derives from.
/// </para>
/// </remarks>
public enum ScenarioResult
{
    /// <summary>The scenario completed as intended. Wire <c>success</c>, display <c>PASSED</c>.</summary>
    Success,

    /// <summary>The scenario failed. Wire <c>error</c>, display <c>FAILED</c>.</summary>
    Error,
}

/// <summary>
/// The two spellings of a <see cref="ScenarioResult"/>, plus parsing that
/// rejects anything the canonical schema would refuse.
/// </summary>
public static class ScenarioResultExtensions
{
    /// <summary>The schema-legal spelling, written into JSON artifacts.</summary>
    /// <param name="result">The result to spell.</param>
    /// <returns><c>"success"</c> or <c>"error"</c> — lowercase, and the only spelling the schema accepts.</returns>
    public static string WireName(this ScenarioResult result)
    {
        return result == ScenarioResult.Error ? "error" : "success";
    }

    /// <summary>The human-facing spelling, rendered into Markdown and console output.</summary>
    /// <param name="result">The result to spell.</param>
    /// <returns><c>"PASSED"</c> or <c>"FAILED"</c>. Never write this into a JSON artifact.</returns>
    public static string DisplayName(this ScenarioResult result)
    {
        return result == ScenarioResult.Error ? "FAILED" : "PASSED";
    }

    /// <summary>Maps a test-outcome flag to the matching result.</summary>
    /// <param name="failed">Whether the test failed, as the test framework reported it.</param>
    /// <returns><see cref="ScenarioResult.Error"/> when <paramref name="failed"/>, else <see cref="ScenarioResult.Success"/>.</returns>
    /// <remarks>
    /// This is the framework's verdict, which is not the same as whether the
    /// traced code threw — a test can fail an assertion over a trace in which
    /// nothing threw.
    /// </remarks>
    public static ScenarioResult Of(bool failed)
    {
        return failed ? ScenarioResult.Error : ScenarioResult.Success;
    }

    /// <summary>Parses either spelling, case-insensitively.</summary>
    /// <param name="value">
    /// A wire spelling (<c>success</c>/<c>error</c>) or a display spelling
    /// (<c>PASSED</c>/<c>FAILED</c>). Surrounding whitespace is trimmed.
    /// </param>
    /// <returns>The matching result.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="value"/> is <see langword="null"/> or is neither a wire
    /// nor a display spelling. Throwing here is the guard that stops an
    /// out-of-contract value reaching an artifact — unlike the lenient
    /// <c>FromName</c> parsers elsewhere in this namespace, which degrade to a
    /// fallback because bad *configuration* must not crash capture.
    /// </exception>
    public static ScenarioResult From(string value)
    {
        if (value is null)
        {
            throw new ArgumentException(
                "scenario result must not be null", nameof(value));
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "success" or "passed" => ScenarioResult.Success,
            "error" or "failed" => ScenarioResult.Error,
            _ => throw new ArgumentException(
                $"unknown scenario result '{value}'; expected one of "
                + "success, error, PASSED, FAILED", nameof(value)),
        };
    }
}
