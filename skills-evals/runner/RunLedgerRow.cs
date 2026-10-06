// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Globalization;
using System.Text.RegularExpressions;

namespace NarrativeTrace.SkillsEvals;

/// <summary>
/// One row the runner appends to <c>skills-evals/ledger/runs.jsonl</c> — one trial, any platform.
/// The wire shape mirrors the Python/Java reference exactly (<c>case</c>, not <c>caseName</c>), so
/// the promotion renderer — and any cross-port cross-check — reads one schema across the family.
/// </summary>
public sealed record RunLedgerRow(
    string Date,
    string Skill,
    string CaseName,
    Platform Platform,
    string Model,
    int Trial,
    bool Pass)
{
    /// <summary><c>pass</c> or <c>fail</c> — the ledger's own result token.</summary>
    public string Result => Pass ? "pass" : "fail";

    /// <summary>The single JSON object this row serializes to, one per line of the ledger.</summary>
    public string ToJsonLine() =>
        "{"
        + $"\"date\":{Quote(Date)},"
        + $"\"skill\":{Quote(Skill)},"
        + $"\"case\":{Quote(CaseName)},"
        + $"\"platform\":{Quote(Platform.WireName())},"
        + $"\"model\":{Quote(Model)},"
        + $"\"trial\":{Trial.ToString(CultureInfo.InvariantCulture)},"
        + $"\"result\":{Quote(Result)}"
        + "}";

    /// <summary>Parses one <see cref="ToJsonLine"/>-shaped object — this ledger's only reader.</summary>
    public static RunLedgerRow ParseJsonLine(string json)
    {
        var platform = Platforms.Parse(StringField(json, "platform"))
            ?? throw new ArgumentException($"unknown platform in: {json}");
        return new RunLedgerRow(
            StringField(json, "date"),
            StringField(json, "skill"),
            StringField(json, "case"),
            platform,
            StringField(json, "model"),
            IntField(json, "trial"),
            StringField(json, "result") == "pass");
    }

    /// <summary>Every non-blank line of a <c>runs.jsonl</c>-shaped file, one row per line.</summary>
    public static IReadOnlyList<RunLedgerRow> ParseJsonl(string content) =>
        content.Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.Length > 0)
            .Select(ParseJsonLine)
            .ToList();

    private static string Quote(string s) =>
        "\"" + s.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";

    private static string StringField(string json, string key)
    {
        var match = Regex.Match(json, "\"" + Regex.Escape(key) + "\":\"((?:\\\\.|[^\"\\\\])*)\"");
        if (!match.Success)
        {
            throw new ArgumentException($"no \"{key}\" field in: {json}");
        }

        return match.Groups[1].Value
            .Replace("\\\"", "\"", StringComparison.Ordinal)
            .Replace("\\\\", "\\", StringComparison.Ordinal);
    }

    private static int IntField(string json, string key)
    {
        var match = Regex.Match(json, "\"" + Regex.Escape(key) + "\":(-?\\d+)");
        if (!match.Success)
        {
            throw new ArgumentException($"no \"{key}\" field in: {json}");
        }

        return int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
    }
}
