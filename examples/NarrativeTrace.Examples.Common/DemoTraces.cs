// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Globalization;
using System.Text.RegularExpressions;
using NarrativeTrace.Core;
using NarrativeTrace.Glossary;
using NarrativeTrace.Runtime;

namespace NarrativeTrace.Examples.Common;

/// <summary>
/// Demo-launcher hook: writes each scenario's captured trace as a translated
/// markdown file.
/// </summary>
/// <remarks>
/// <para>
/// The demo's translated mode (<c>./demo.sh --lang es</c>) renders the run
/// through the example's committed glossary in the same process — the same
/// <see cref="TraceTranslationView"/> rendering the live
/// <see cref="TranslationSubscriber"/> uses, whose parity a property in the
/// glossary module pins. A live subscriber only knows trace ids; this hook
/// owns the scenario-named files, because only the demo knows which scenario
/// a trace belongs to.
/// </para>
/// <para>
/// <see cref="DemoRun.TraceTree"/> calls it for every scenario that renders a
/// tree; the call is a no-op unless the launcher set
/// <see cref="DirectoryKey"/> and <see cref="LocaleKey"/>, so ordinary runs
/// write nothing. The glossary resolves through
/// <see cref="GlossaryLoader"/> (<c>NARRATIVETRACE_GLOSSARY_PATH</c> in the
/// demo). A best-effort sink: any problem is reported to standard error and
/// swallowed — trace persistence must never break a demo run.
/// </para>
/// </remarks>
public static class DemoTraces
{
    /// <summary>Environment key naming the directory that receives translated scenario files.</summary>
    public const string DirectoryKey = "NARRATIVETRACE_DEMO_TRANSLATION_DIR";

    /// <summary>Environment key naming the locale the demo run is rendered into.</summary>
    public const string LocaleKey = "NARRATIVETRACE_DEMO_LOCALE";

    private static readonly Regex NonSlugCharacters = new(
        "[^a-z0-9]+", RegexOptions.CultureInvariant);

    private static int _sequence;

    /// <summary>
    /// Writes <c>&lt;nn&gt;_&lt;slug(scenario)&gt;.md</c> into the configured
    /// directory, or does nothing when the demo launcher did not request
    /// translated capture or the trace is empty.
    /// </summary>
    /// <remarks>
    /// The <c>nn</c> run-sequence prefix keeps alphabetical file order equal
    /// to capture order, so the launcher walks translated scenarios in the
    /// order they ran. Line 1 is the exact scenario title
    /// (<c>=== … ===</c>): the launcher uses it as the section header, and a
    /// slug round-trip would lose the casing and punctuation it carries.
    /// </remarks>
    /// <param name="scenario">The scenario's exact printed title.</param>
    /// <param name="trace">The scenario's captured trace.</param>
    public static void Capture(string scenario, TraceTree trace)
    {
        Capture(scenario, trace, Environment.GetEnvironmentVariable, Console.Error);
    }

    /// <summary>Test seam: injected environment and diagnostics sink.</summary>
    /// <param name="scenario">The scenario's exact printed title.</param>
    /// <param name="trace">The scenario's captured trace.</param>
    /// <param name="readEnv">Environment reader.</param>
    /// <param name="diagnostics">Receives any best-effort failure report.</param>
    internal static void Capture(
        string scenario, TraceTree trace, Func<string, string?> readEnv, TextWriter diagnostics)
    {
        var directory = readEnv(DirectoryKey);
        var locale = readEnv(LocaleKey);
        if (string.IsNullOrWhiteSpace(directory)
            || string.IsNullOrWhiteSpace(locale)
            || trace is null
            || trace.Roots.Count == 0)
        {
            return;
        }

        try
        {
            WriteTranslated(scenario, trace, directory!, locale!, readEnv, diagnostics);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException
            or ArgumentException or InvalidOperationException or NotSupportedException)
        {
            diagnostics.WriteLine($"DemoTraces: could not write translated trace: {e.Message}");
        }
    }

    /// <summary>Test seam: the run-sequence counter, so file order is assertable.</summary>
    internal static void ResetSequence()
    {
        _sequence = 0;
    }

    private static void WriteTranslated(
        string scenario,
        TraceTree trace,
        string directory,
        string locale,
        Func<string, string?> readEnv,
        TextWriter diagnostics)
    {
        var glossary = GlossaryLoader.Load(readEnv, AppContext.BaseDirectory);
        if (glossary is null)
        {
            diagnostics.WriteLine("DemoTraces: no glossary found — translated capture skipped");
            return;
        }

        var view = new TraceTranslationView(glossary, _ => null);
        var text = view.Render(TraceTreeCanonicalMapper.FromTree(trace), locale);
        var name = string.Create(
            CultureInfo.InvariantCulture,
            $"{Interlocked.Increment(ref _sequence):D2}_{Slug(scenario)}.md");
        Directory.CreateDirectory(directory);
        File.WriteAllText(
            Path.Combine(directory, name), $"=== {scenario} ===\n\n{text}");
    }

    /// <summary>
    /// Lowercased, punctuation-free file stem: <c>"Scenario 1: Order!"</c> →
    /// <c>scenario_1_order</c>.
    /// </summary>
    private static string Slug(string scenario)
    {
        return NonSlugCharacters
            .Replace(scenario.ToLowerInvariant(), "_")
            .Trim('_');
    }
}
