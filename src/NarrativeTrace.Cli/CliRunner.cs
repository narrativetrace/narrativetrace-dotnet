// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Globalization;
using NarrativeTrace.Cli.Init;
using NarrativeTrace.Tooling.Doctor;

namespace NarrativeTrace.Cli;

/// <summary>
/// Parses command-line arguments and dispatches to the matching verb. All logic
/// lives here (a thin <c>Program</c> just forwards <c>args</c>) so the CLI is
/// unit-testable without a process boundary.
/// </summary>
public static class CliRunner
{
    /// <summary>Runs the CLI, writing to the given streams; returns the process exit code.</summary>
    public static int Run(string[] args, TextWriter output, TextWriter error)
    {
        if (args.Length == 0)
        {
            return Usage(error);
        }

        if (args[0] is "--help" or "-h")
        {
            output.Write(UsageText);
            return 0;
        }

        return Dispatch(args, output, error);
    }

    private static int Dispatch(string[] args, TextWriter output, TextWriter error) => args[0] switch
    {
        "clarity-scan" => RunClarityScan(args, output, error),
        "clarity-aggregate" => RunClarityAggregate(args, output, error),
        "clarity-check" => RunClarityCheck(args, output, error),
        "glossary" => RunGlossary(args, output, error),
        "doctor" => RunDoctor(args, output, error),
        "approve" => RunApprove(args, output, error),
        "feedback" => Feedback.FeedbackCommand.Run(Rest(args), output, error),
        "init" => InitCommand.Run(true, Rest(args), output, error),
        "uninstall" => InitCommand.Run(false, Rest(args), output, error),
        _ => UnknownVerb(args[0], error),
    };

    /// <summary>The arguments after the verb, the slice each command parses.</summary>
    private static string[] Rest(string[] args) => args[1..];

    /// <summary>
    /// <c>approve</c> writes into the project, so a <c>--dir</c> with no value is refused rather than
    /// read as "the current directory".
    /// </summary>
    private static int RunApprove(string[] args, TextWriter output, TextWriter error)
    {
        var dir = GetOption(args, "--dir");
        if (dir is null && HasFlag(args, "--dir"))
        {
            error.WriteLine("error: approve: --dir needs a directory");
            return 2;
        }

        return ApproveCommand.Run(dir ?? Directory.GetCurrentDirectory(), output, error);
    }

    private static int RunGlossary(string[] args, TextWriter output, TextWriter error)
    {
        var file = GetOption(args, "--file") ?? "glossary.json";
        return GlossaryCommand.Run(file, output, error);
    }

    private static int RunDoctor(string[] args, TextWriter output, TextWriter error)
    {
        var json = HasFlag(args, "--json");
        var dir = GetOption(args, "--dir") ?? Directory.GetCurrentDirectory();
        return HasFlag(args, "--report")
            ? DoctorReportFile.Run(
                dir, Environment.GetEnvironmentVariable, () => CarrierLocator.Open(null), json, output, error)
            : DoctorCommand.Run(
                dir, Environment.GetEnvironmentVariable, () => CarrierLocator.Open(null), json, output, error);
    }

    private static int RunClarityCheck(string[] args, TextWriter output, TextWriter error)
    {
        var results = GetOption(args, "--results");
        if (results is null)
        {
            error.WriteLine("error: clarity-check requires --results <path>");
            return 2;
        }

        var minScore = ParseDouble(GetOption(args, "--min-score"), 0.0);
        var maxHighIssues = ParseInt(GetOption(args, "--max-high-issues"), int.MaxValue);
        var warnOnly = HasFlag(args, "--warn-only");
        return ClarityCheckCommand.Run(
            results, minScore, maxHighIssues, warnOnly, output, error);
    }

    private static bool HasFlag(string[] args, string name)
    {
        return Array.IndexOf(args, name) >= 0;
    }

    private static double ParseDouble(string? value, double fallback)
    {
        return double.TryParse(
            value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result)
            ? result
            : fallback;
    }

    private static int ParseInt(string? value, int fallback)
    {
        return int.TryParse(
            value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result)
            ? result
            : fallback;
    }

    private static int RunClarityAggregate(string[] args, TextWriter output, TextWriter error)
    {
        var inputDir = GetOption(args, "--input-dir");
        if (inputDir is null)
        {
            error.WriteLine("error: clarity-aggregate requires --input-dir <path>");
            return 2;
        }

        var outputDir = GetOption(args, "--output-dir") ?? inputDir;
        return ClarityAggregateCommand.Run(inputDir, outputDir, output, error);
    }

    private static int RunClarityScan(string[] args, TextWriter output, TextWriter error)
    {
        var assembly = GetOption(args, "--assembly");
        if (assembly is null)
        {
            error.WriteLine("error: clarity-scan requires --assembly <path>");
            return 2;
        }

        var outputDir = GetOption(args, "--output-dir") ?? ".";
        var format = GetOption(args, "--format") ?? "both";
        return ClarityScanCommand.Run(assembly, outputDir, format, output, error);
    }

    private static string? GetOption(string[] args, string name)
    {
        for (var i = 1; i < args.Length - 1; i++)
        {
            if (args[i] == name)
            {
                return args[i + 1];
            }
        }

        return null;
    }

    private static int UnknownVerb(string verb, TextWriter error)
    {
        error.WriteLine($"error: unknown command '{verb}'");
        return Usage(error);
    }

    private static int Usage(TextWriter error)
    {
        error.WriteLine(UsageText);
        return 2;
    }

    private static readonly string UsageText =
        "usage: dotnet-narrativetrace <command>" + Environment.NewLine +
        "  clarity-scan      --assembly <path> [--output-dir <dir>] " +
        "[--format both|md|json]" + Environment.NewLine +
        "  clarity-aggregate --input-dir <dir> [--output-dir <dir>]" + Environment.NewLine +
        "  clarity-check     --results <path> [--min-score <x>] " +
        "[--max-high-issues <n>] [--warn-only]" + Environment.NewLine +
        "  glossary          [--file <glossary.json>]" + Environment.NewLine +
        "  doctor            [--json] [--report] [--dir <path>]" + Environment.NewLine +
        "  approve           [--dir <path>]  promote every reviewed *.received.nt" + Environment.NewLine +
        "  feedback          <draft|url|gh> --category <c> --step <s> --did <s> --happened <s>" +
        Environment.NewLine +
        "                    --expected <s> [--language <tag>] [--trace <suffix>] [--json]" +
        Environment.NewLine +
        "  init              [--dry-run] [--write-existing] [--force] [--only skills|agents-md]" +
        Environment.NewLine +
        "                    [--vendor claude|none] [--from <carrier>] [--dir <path>] [--json]" +
        Environment.NewLine +
        "  uninstall         [--dry-run] [--only skills|agents-md] [--dir <path>] [--json]";
}
