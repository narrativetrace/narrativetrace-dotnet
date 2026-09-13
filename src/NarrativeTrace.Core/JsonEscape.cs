// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Globalization;
using System.Text;

namespace NarrativeTrace.Core;

/// <summary>
/// RFC 8259 JSON string escaping for Core's own hand-rolled JSON emitters
/// (<see cref="ScenarioManifest"/>). A separate copy from
/// <c>NarrativeTrace.Glossary.JsonEscape</c>: Core takes no dependency on any
/// other module, so the one small escaping routine is duplicated rather than
/// shared — mirrors the Java runtime's shared <c>JsonEscape</c> behavior.
/// </summary>
internal static class JsonEscape
{
    /// <summary>Escapes a string for embedding inside a JSON string literal.</summary>
    internal static string Escape(string s)
    {
        var sb = new StringBuilder(s.Length + 8);
        for (var i = 0; i < s.Length; i++)
        {
            AppendEscaped(sb, s[i]);
        }

        return sb.ToString();
    }

    private static void AppendEscaped(StringBuilder sb, char c)
    {
        switch (c)
        {
            case '\\': sb.Append("\\\\"); break;
            case '"': sb.Append("\\\""); break;
            case '\n': sb.Append("\\n"); break;
            case '\r': sb.Append("\\r"); break;
            case '\t': sb.Append("\\t"); break;
            case '\b': sb.Append("\\b"); break;
            case '\f': sb.Append("\\f"); break;
            default: AppendVerbatimOrUnicode(sb, c); break;
        }
    }

    private static void AppendVerbatimOrUnicode(StringBuilder sb, char c)
    {
        if (c < 0x20)
        {
            sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
        }
        else
        {
            sb.Append(c);
        }
    }
}
