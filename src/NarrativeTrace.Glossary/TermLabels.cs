// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Glossary;

/// <summary>
/// The <c>glossary.json</c> labels of <see cref="TermKind"/> and
/// <see cref="TermStatus"/> values.
/// </summary>
/// <remarks>
/// The file format is shared across NarrativeTrace ports, so labels are fixed
/// strings (kebab-case kinds, lowercase statuses), never derived from .NET
/// enum names.
/// </remarks>
public static class TermLabels
{
    /// <summary>Returns the kebab-case label used in <c>glossary.json</c> (e.g. <c>noun-phrase</c>).</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not a defined <see cref="TermKind"/>.</exception>
    public static string JsonName(this TermKind kind)
    {
        return kind switch
        {
            TermKind.Word => "word",
            TermKind.NounPhrase => "noun-phrase",
            TermKind.VerbPhrase => "verb-phrase",
            TermKind.Template => "template",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };
    }

    /// <summary>Returns the lowercase label used in <c>glossary.json</c> (e.g. <c>harvested</c>).</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not a defined <see cref="TermStatus"/>.</exception>
    public static string JsonName(this TermStatus status)
    {
        return status switch
        {
            TermStatus.Harvested => "harvested",
            TermStatus.Curated => "curated",
            TermStatus.Stale => "stale",
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
        };
    }
}
