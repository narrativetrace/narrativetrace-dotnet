// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Reflection;

namespace NarrativeTrace.DependencyInjection;

/// <summary>
/// List 2 of the instrumentation exclusions: library and framework namespaces that
/// <see cref="ServiceCollectionExtensions.AddNarrativeTracing"/> leaves untraced even when the include
/// list selects them, with a shipped default the host can replace.
/// </summary>
/// <remarks>
/// <b>@llmNote</b> The list is replaced, never merged: giving
/// <see cref="NarrativeTracingDiOptions.ExcludeNamespaces"/> or setting <see cref="EnvironmentKey"/>
/// (even to an empty value) makes that the whole list. To extend the default, copy
/// <see cref="DefaultExclude"/> into the value and add to it. An exclude always wins over an include.
/// The structural list (compiler-generated and runtime-emitted types) is separate, built in, and not
/// configurable.
/// </remarks>
public static class InstrumentationExclusions
{
    /// <summary>
    /// Environment variable holding the exclude list: namespace prefixes separated by <c>;</c>.
    /// Present means "this is the whole list" (empty disables the default); absent means the
    /// <see cref="DefaultExclude"/> applies. An explicit
    /// <see cref="NarrativeTracingDiOptions.ExcludeNamespaces"/> call takes precedence.
    /// </summary>
    public const string EnvironmentKey = "NARRATIVETRACE_EXCLUDE";

    private const string ResourceName = "NarrativeTrace.DependencyInjection.default-exclude.txt";

    /// <summary>
    /// The shipped default exclude list, as namespace prefixes with dot-boundary matching. Published
    /// verbatim in the dependency-injection guide.
    /// </summary>
    public static IReadOnlyList<string> DefaultExclude { get; } = ReadDefault();

    /// <summary>Splits an exclude value on <c>;</c> into normalised prefixes, dropping blanks.</summary>
    internal static IReadOnlyList<string> Parse(string? value)
    {
        return value is null ? [] : Normalise(value.Split(';'));
    }

    /// <summary>
    /// Trims each prefix, drops a trailing <c>.*</c> or <c>.</c> (both mean "this namespace and its
    /// children", which is all the matcher ever does) and discards blanks.
    /// </summary>
    internal static IReadOnlyList<string> Normalise(IEnumerable<string> prefixes)
    {
        var result = new List<string>();
        foreach (var raw in prefixes)
        {
            var prefix = raw.Trim();
            prefix = prefix.EndsWith(".*", StringComparison.Ordinal) ? prefix.Substring(0, prefix.Length - 2) : prefix;
            prefix = prefix.TrimEnd('.');
            if (prefix.Length > 0)
            {
                result.Add(prefix);
            }
        }

        return result;
    }

    private static IReadOnlyList<string> ReadDefault()
    {
        using var stream = typeof(InstrumentationExclusions).Assembly.GetManifestResourceStream(ResourceName)!;
        using var reader = new StreamReader(stream);
        var prefixes = new List<string>();
        for (var line = reader.ReadLine(); line is not null; line = reader.ReadLine())
        {
            if (line.Length > 0 && line[0] != '#')
            {
                prefixes.Add(line.Split('\t')[0]);
            }
        }

        return Normalise(prefixes);
    }
}
