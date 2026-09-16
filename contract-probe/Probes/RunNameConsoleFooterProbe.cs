// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Reflection;
using System.Text.RegularExpressions;
using NarrativeTrace.Core;

namespace NarrativeTrace.ContractProbe.Probes;

/// <summary>
/// <c>probed-default</c>, since 0.1.5: the console suite footer names the enclosing test-suite run
/// (configuration-guide.md §7) — a <c>run: &lt;phrase&gt;</c> line on its own.
/// </summary>
/// <remarks>
/// <c>NarrativeTrace.Core.RunIdentity</c> does not exist in the 0.1.3 package this project compiles
/// against by default — contract-probe compiles every probe class together against whichever
/// single published version is under test (documentation/contract-gate.md), so a probe for a type
/// that only exists in a newer release must never name that type at compile time. Found entirely
/// through reflection, mirroring <see cref="ReflectablePerInvocationIdentityProbe"/>'s own note;
/// this entry's own <c>since</c> keeps it unreached before the type actually ships.
/// <see cref="ConsoleSummaryReporter"/> itself predates 0.1.5 and is referenced directly — only the
/// <c>RunIdentity</c>-typed overload of <c>FormatSuiteFooter</c> is new, and is dispatched by
/// reflection.
/// </remarks>
internal static class RunNameConsoleFooterProbe
{
    private static readonly Regex RunLine = new(@"(?m)^\s*run: [a-z]+ [a-z]+ [a-z]+$");

    public static string Observe()
    {
        var runIdentityType = Type.GetType("NarrativeTrace.Core.RunIdentity, NarrativeTrace.Core")
            ?? throw new InvalidOperationException(
                "NarrativeTrace.Core.RunIdentity not found in the installed package — "
                    + "this probe should never run before its contract entry's since version");
        var generate = runIdentityType.GetMethod("Generate", BindingFlags.Public | BindingFlags.Static)!;
        var run = generate.Invoke(null, null)!;

        var formatSuiteFooter = typeof(ConsoleSummaryReporter)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .First(m => m.Name == "FormatSuiteFooter"
                && m.GetParameters() is [_, _, var third] && third.ParameterType == runIdentityType);
        var footer = (string)formatSuiteFooter.Invoke(null, [1, "out", run])!;

        return RunLine.IsMatch(footer) ? "true" : "false";
    }
}
