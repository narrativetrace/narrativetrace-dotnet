// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Globalization;
using System.Text;

namespace NarrativeTrace.Tooling.Feedback;

/// <summary>
/// The <c>--json</c> envelope for each of the verb's three paths, plus the refusal every one of them
/// can end in.
/// </summary>
/// <remarks>
/// <para>
/// INTENT: an agent reads this, a person reads the human text, and the exit code is in BOTH so
/// neither has to infer it. Every envelope names the verb it came from, because an agent that
/// pipelines <c>draft</c> into <c>url</c> needs to be able to tell which answer it is holding.
/// </para>
/// <para>
/// <b>@llmNote</b> Written by hand, like <c>DoctorRenderer</c>'s own shape is small and fixed. The
/// quoting is the only subtle part and it is in one method, so no envelope can escape differently from
/// another.
/// </para>
/// </remarks>
public static class FeedbackJson
{
    /// <summary>A drafted report: the facts, where the two files landed, and why no trace was attached.</summary>
    public static string Drafted(FeedbackReport report, string draftFile, string bodyFile, string traceNote)
    {
        var envelope = new (string Key, string Value)[]
        {
            ("verb", "draft"),
            ("status", "drafted"),
            ("runtime", report.Runtime),
            ("category", report.Category.Id),
            ("install", report.Install),
            ("step", report.Step),
            ("language", report.Language),
            ("agent", report.Agent.Describe()),
            ("draftFile", draftFile),
            ("bodyFile", bodyFile),
            ("traceNote", traceNote),
        };
        return Object(envelope, 0);
    }

    /// <summary>The gate refused: every field, rule and reason, and exit 2.</summary>
    public static string Refused(string verb, FeedbackDraft.Refused refused)
    {
        var builder = new StringBuilder("{\n  \"verb\": ").Append(Quote(verb));
        builder.Append(",\n  \"status\": \"refused\",\n  \"violations\": [\n");
        for (var i = 0; i < refused.Violations.Count; i++)
        {
            var violation = refused.Violations[i];
            builder.Append("    {\"field\": ").Append(Quote(violation.Field))
                .Append(", \"rule\": ").Append(Quote(violation.Rule.Id))
                .Append(", \"reason\": ").Append(Quote(violation.Rule.Reason))
                .Append('}')
                .Append(i == refused.Violations.Count - 1 ? "\n" : ",\n");
        }

        return builder.Append("  ],\n  \"exitCode\": 2\n}\n").ToString();
    }

    /// <summary>The pre-filled URL, and the file whose contents the user pastes once it is open.</summary>
    public static string Url(string url, string bodyFile) =>
        Object([("verb", "url"), ("status", "ready"), ("url", url), ("bodyFile", bodyFile)], 0);

    /// <summary>The <c>gh</c> line, printed and never run.</summary>
    public static string Gh(string command) =>
        Object([("verb", "gh"), ("status", "ready"), ("command", command)], 0);

    /// <summary>There is no <c>gh</c> line to offer, and why — exit 1: the verb ran, the path is not open.</summary>
    public static string GhUnavailable(string reason) =>
        Object([("verb", "gh"), ("status", "unavailable"), ("reason", reason)], 1);

    private static string Object(IReadOnlyList<(string Key, string Value)> envelope, int exitCode)
    {
        var builder = new StringBuilder("{\n");
        foreach (var (key, value) in envelope)
        {
            builder.Append("  ").Append(Quote(key)).Append(": ").Append(Quote(value)).Append(",\n");
        }

        return builder.Append("  \"exitCode\": ").Append(exitCode.ToString(CultureInfo.InvariantCulture))
            .Append("\n}\n").ToString();
    }

    /// <summary>An RFC 8259 quoted string: the two mandatory escapes and every control character escaped.</summary>
    private static string Quote(string text)
    {
        var builder = new StringBuilder(text.Length + 2).Append('"');
        foreach (var c in text)
        {
            switch (c)
            {
                case '"': builder.Append("\\\""); break;
                case '\\': builder.Append("\\\\"); break;
                case '\n': builder.Append("\\n"); break;
                case '\r': builder.Append("\\r"); break;
                case '\t': builder.Append("\\t"); break;
                default:
                    builder.Append(c < 0x20 ? $"\\u{(int)c:x4}" : c.ToString());
                    break;
            }
        }

        return builder.Append('"').ToString();
    }
}
