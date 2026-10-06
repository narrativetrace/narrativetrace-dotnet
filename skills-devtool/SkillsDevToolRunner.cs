// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.SkillsDevTool;

/// <summary>
/// Parses the dev tool's command line and dispatches to the matching verb. All logic lives here
/// (a thin <c>Program</c> just forwards <c>args</c>) so it is unit-testable without a process
/// boundary — the same shape the shipped CLI's <c>CliRunner</c> uses.
/// </summary>
public static class SkillsDevToolRunner
{
    /// <summary>Runs the dev tool, writing to the given streams; returns the process exit code.</summary>
    public static int Run(string[] args, TextWriter output, TextWriter error)
    {
        var dir = GetOption(args, "--dir") ?? Directory.GetCurrentDirectory();
        var verb = args.Length > 0 ? args[0] : null;
        return verb switch
        {
            "render" => SkillsRenderCommand.Run(dir, output, error),
            "lint" => SkillsLintCommand.Run(dir, output, error),
            "replay" => SkillsReplayCommand.Run(dir, output, error),
            _ => Usage(error),
        };
    }

    private static int Usage(TextWriter error)
    {
        error.WriteLine(
            "usage: dotnet run --project skills-devtool -- <verb> [--dir <path>]" +
            Environment.NewLine +
            "  render   regenerate every skill page and the AGENTS.md managed section" +
            Environment.NewLine +
            "  lint     Tier A catalogue lints + drift check" +
            Environment.NewLine +
            "  replay   Tier A2 mechanical replay against the sixty-seconds fixture");
        return 2;
    }

    private static string? GetOption(string[] args, string name)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == name)
            {
                return args[i + 1];
            }
        }

        return null;
    }
}
