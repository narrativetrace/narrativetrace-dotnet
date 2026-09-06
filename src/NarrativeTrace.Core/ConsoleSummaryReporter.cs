// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Globalization;
using System.Text;

namespace NarrativeTrace.Core;

/// <summary>
/// Formats the console messages a test-framework integration prints at suite
/// boundaries: a header when recording starts and a footer summarizing scenario
/// count, clarity distribution, and where the reports were written.
/// </summary>
public static class ConsoleSummaryReporter
{
    private const double HighThreshold = 0.7;
    private const double ModerateThreshold = 0.4;

    /// <summary>
    /// A passing per-test line: <c>    ✓ name (Nms)</c>.
    /// </summary>
    public static string FormatTestResult(string testName, long durationMs)
    {
        return $"    ✓ {testName} ({Ms(durationMs)}ms)";
    }

    /// <summary>
    /// A passing per-test line carrying the clarity score:
    /// <c>    ✓ name (Nms, clarity: 0.NN)</c> (2-decimal, invariant culture).
    /// </summary>
    public static string FormatTestResult(
        string testName, long durationMs, double clarityScore)
    {
        return $"    ✓ {testName} ({Ms(durationMs)}ms, clarity: "
            + clarityScore.ToString("0.00", CultureInfo.InvariantCulture) + ")";
    }

    private static string Ms(long durationMs)
    {
        return durationMs.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// A failing per-test block: the <c>✗</c> line, the exception + location,
    /// and a pointer to the full trace file.
    /// </summary>
    public static string FormatTestFailure(
        string testName, long durationMs, string exceptionType,
        string location, string traceFilePath)
    {
        return $"    ✗ {testName} ({Ms(durationMs)}ms)\n"
            + $"      > {exceptionType} at {location}\n"
            + $"      > Full trace: {traceFilePath}";
    }

    /// <summary>
    /// Header printed once when suite recording begins.
    /// </summary>
    /// <remarks>
    /// Deliberately <c>static readonly</c>, not <c>const</c>: a const in a
    /// packaged library is inlined into consumer assemblies, so changing the
    /// text would not reach them without a recompile.
    /// </remarks>
    public static readonly string FormatSuiteHeader =
        "NarrativeTrace — Recording test narratives\n";

    /// <summary>
    /// Footer without a clarity breakdown: scenario count and reports path only.
    /// </summary>
    public static string FormatSuiteFooter(int scenarioCount, string outputPath)
    {
        var sb = new StringBuilder();
        sb.AppendLine("NarrativeTrace — Suite complete");
        sb.AppendLine(Line($"{scenarioCount} scenarios recorded"));
        sb.Append(Line($"Reports: {outputPath}"));
        return sb.ToString();
    }

    /// <summary>
    /// Footer summarizing the suite: scenario count, the high/moderate/low
    /// clarity split, and the reports path. An empty score list yields 0% for
    /// every bucket rather than dividing by zero.
    /// </summary>
    public static string FormatSuiteFooter(
        int scenarioCount, string outputPath, IReadOnlyList<double> clarityScores)
    {
        var (high, moderate, low) = Bucket(clarityScores);
        var total = clarityScores.Count;
        var sb = new StringBuilder();
        sb.AppendLine("NarrativeTrace — Suite complete");
        sb.AppendLine(Line($"{scenarioCount} scenarios recorded"));
        sb.AppendLine(Line(
            $"Clarity: {Percent(high, total)}% high | " +
            $"{Percent(moderate, total)}% moderate | {Percent(low, total)}% low"));
        sb.Append(Line($"Reports: {outputPath}"));
        return sb.ToString();
    }

    /// <summary>
    /// The suite footer with the loss line appended: what the run could not
    /// keep, named rather than left to a reader to notice.
    /// </summary>
    /// <param name="scenarioCount">Scenarios recorded.</param>
    /// <param name="outputPath">Where the reports were written.</param>
    /// <param name="clarityScores">One score per scenario; empty yields 0% buckets.</param>
    /// <param name="loss">What the run lost. A lossless run prints no loss line at all.</param>
    /// <returns>
    /// The same footer as the three-argument overload, plus a final
    /// <c>Incomplete: …</c> line when — and only when — something was lost.
    /// </returns>
    /// <remarks>
    /// The line is omitted rather than printed with zeroes on purpose: a
    /// clean run must stay quiet, or the signal that something went missing
    /// becomes background noise nobody reads.
    /// </remarks>
    public static string FormatSuiteFooter(
        int scenarioCount,
        string outputPath,
        IReadOnlyList<double> clarityScores,
        TraceLoss loss)
    {
        var footer = FormatSuiteFooter(
            scenarioCount, outputPath, clarityScores);
        return loss.Describe() is { } line
            ? footer + "\n" + Line(line)
            : footer;
    }

    private static (int High, int Moderate, int Low) Bucket(IReadOnlyList<double> scores)
    {
        int high = 0, moderate = 0, low = 0;
        for (var i = 0; i < scores.Count; i++)
        {
            if (scores[i] >= HighThreshold)
            {
                high++;
            }
            else if (scores[i] >= ModerateThreshold)
            {
                moderate++;
            }
            else
            {
                low++;
            }
        }

        return (high, moderate, low);
    }

    private static int Percent(int count, int total)
    {
        return total == 0
            ? 0
            : (int)Math.Round(100.0 * count / total, MidpointRounding.AwayFromZero);
    }

    private static string Line(string text)
    {
        return "  " + text;
    }
}
