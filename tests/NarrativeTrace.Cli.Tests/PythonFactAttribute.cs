// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Diagnostics;
using Xunit;

namespace NarrativeTrace.Cli.Tests;

/// <summary>
/// A <see cref="FactAttribute"/> for tests that execute a skills-eval grader, which is a Python script
/// (<c>python3 -I -B</c>). The graders ship publicly, but a bare .NET SDK image carries no Python, so on
/// such a machine the grader's own "command not found" would be asserted against instead of its verdict.
/// The test skips itself with a reason — a real "Skipped" result — rather than failing for a missing tool.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class PythonFactAttribute : FactAttribute
{
    private static readonly Lazy<bool> PythonAvailable = new(ProbePython);

    /// <summary>Skips the test when <c>python3</c> cannot be started.</summary>
    public PythonFactAttribute()
    {
        if (!PythonAvailable.Value)
        {
            Skip = "python3 is not on PATH; the skills-eval graders are Python scripts.";
        }
    }

    private static bool ProbePython()
    {
        try
        {
            var start = new ProcessStartInfo("python3", "--version")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            using var process = Process.Start(start)!;
            process.StandardOutput.ReadToEnd();
            process.StandardError.ReadToEnd();
            process.WaitForExit();
            return process.ExitCode == 0;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }
}
