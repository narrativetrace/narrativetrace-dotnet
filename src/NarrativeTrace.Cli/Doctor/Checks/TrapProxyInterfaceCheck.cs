// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Text.RegularExpressions;

namespace NarrativeTrace.Cli.Doctor.Checks;

/// <summary>
/// <c>trap.proxy-interface</c>: <c>NarrativeTraceProxy.Create&lt;T&gt;</c> requires
/// <c>T</c> to be an interface — the underlying <c>DispatchProxy</c> throws
/// <see cref="ArgumentException"/> at creation time if it is a concrete class.
/// This check catches the mistake earlier, from source, by finding a call
/// whose type argument is declared as a <c>class</c> in the same project.
/// </summary>
public static class TrapProxyInterfaceCheck
{
    private const string Id = "trap.proxy-interface";

    private static readonly Regex CreateCall =
        new(@"NarrativeTraceProxy\.Create<(\w+)>\(", RegexOptions.Compiled);

    /// <summary>Runs the check.</summary>
    public static DoctorFinding Run(DoctorSnapshot snapshot)
    {
        var typeArguments = snapshot.SourceFiles.Values
            .SelectMany(content => CreateCall.Matches(content).Select(m => m.Groups[1].Value))
            .Distinct()
            .ToList();
        if (typeArguments.Count == 0)
        {
            return DoctorFinding.Pass(
                Id, "no NarrativeTraceProxy.Create<T>(...) calls found — nothing to check",
                DoctorDocUrls.InstallationProxyOption);
        }

        var offender = typeArguments.FirstOrDefault(DeclaredAsClass(snapshot));
        return offender is null
            ? DoctorFinding.Pass(
                Id, "every traced type resolved in this project is declared as an interface",
                DoctorDocUrls.InstallationProxyOption)
            : Fail(offender);
    }

    private static Func<string, bool> DeclaredAsClass(DoctorSnapshot snapshot)
    {
        return typeName => snapshot.SourceFiles.Values.Any(content =>
            Regex.IsMatch(content, $@"\bclass\s+{Regex.Escape(typeName)}\b"));
    }

    private static DoctorFinding Fail(string typeName)
    {
        return DoctorFinding.Fail(
            Id,
            $"NarrativeTraceProxy.Create<{typeName}>(...) targets '{typeName}', which is declared " +
            "as a class in this project",
            $"NarrativeTraceProxy.Create<T> requires an interface — extract an interface for " +
            $"'{typeName}' and proxy that instead.",
            DoctorDocUrls.InstallationProxyOption);
    }
}
