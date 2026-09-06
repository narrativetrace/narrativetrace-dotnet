// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Core;

/// <summary>
/// Reads the operator-supplied deny-list additions from the process
/// environment.
/// </summary>
/// <remarks>
/// <see cref="RedactionPolicy.OfPatterns"/> <em>replaces</em> the built-in
/// vocabulary, which is the wrong tool for "our DTOs also carry a
/// <c>betalingskort</c> field": taking it means giving up every default the
/// library ships, and a team that does so silently loses the next release's
/// new words. This is the additive half — whatever the application asked
/// for, plus whatever the operator running it asked for.
/// <para>
/// The java edition reads a JVM system property first, then an environment
/// variable (<c>narrativetrace.redaction.additionalPatterns</c> /
/// <c>NARRATIVETRACE_REDACTION_ADDITIONALPATTERNS</c>), deliberately not
/// routed through its config resolver so the person deploying the artifact
/// can widen redaction without rebuilding it. .NET has no system-property
/// analogue, so this reads the same environment variable only — the
/// .NET-native override channel every other <see cref="ConfigResolver"/>
/// switch already uses.
/// </para>
/// <para>
/// Additions are substring patterns, matched exactly as the built-in
/// substring vocabulary is. A short addition therefore carries the
/// <c>pan</c> trap with it: adding <c>id</c> would blank every identifier
/// in the trace. The code cannot tell an intentional short pattern from a
/// mistaken one.
/// </para>
/// </remarks>
internal static class AdditionalRedactionPatterns
{
    /// <summary>
    /// Environment variable appending comma-separated field-name patterns to
    /// the built-in deny-list.
    /// </summary>
    internal const string EnvironmentVariable = "NARRATIVETRACE_REDACTION_ADDITIONALPATTERNS";

    /// <summary>
    /// The additions configured for this process, read at the moment a
    /// policy is constructed.
    /// </summary>
    /// <returns>The parsed patterns, empty when the variable is not set.</returns>
    internal static IReadOnlyCollection<string> Configured()
    {
        return Configured(Environment.GetEnvironmentVariable);
    }

    /// <summary>
    /// The same lookup against a supplied source, so a test can exercise it
    /// without setting real process environment variables.
    /// </summary>
    /// <param name="environment">Where to read <see cref="EnvironmentVariable"/>.</param>
    internal static IReadOnlyCollection<string> Configured(Func<string, string?> environment)
    {
        return Parse(environment(EnvironmentVariable));
    }

    /// <summary>
    /// Splits one configured value into patterns: comma-separated, each
    /// trimmed, blanks dropped.
    /// </summary>
    /// <param name="raw"><see langword="null"/> and blank yield no patterns.</param>
    internal static IReadOnlyCollection<string> Parse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return [];
        }

        return new HashSet<string>(
            raw.Split(',')
                .Select(p => p.Trim())
                .Where(p => p.Length > 0));
    }
}
