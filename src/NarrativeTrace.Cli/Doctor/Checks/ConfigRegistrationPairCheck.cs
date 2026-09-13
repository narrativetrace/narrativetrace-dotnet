// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Cli.Doctor.Checks;

/// <summary>
/// <c>config.registration-pair</c>: the ASP.NET Core integration is a pair —
/// <c>AddNarrativeTrace(...)</c> registers the services, <c>UseNarrativeTrace()</c>
/// inserts the middleware. Calling only the first compiles cleanly and does
/// nothing at request time.
/// </summary>
public static class ConfigRegistrationPairCheck
{
    private const string Id = "config.registration-pair";

    /// <summary>Runs the check.</summary>
    public static DoctorFinding Run(DoctorSnapshot snapshot)
    {
        var addCalls = snapshot.SourceFiles.Where(f => f.Value.Contains("AddNarrativeTrace(")).ToList();
        if (addCalls.Count == 0)
        {
            return DoctorFinding.Pass(
                Id, "AddNarrativeTrace(...) is not used — nothing to check",
                DoctorDocUrls.AspNetCoreRegistration);
        }

        var useCalled = snapshot.SourceFiles.Any(f =>
            f.Value.Contains("UseNarrativeTrace(") || f.Value.Contains("UseMiddleware<NarrativeTraceMiddleware>"));
        return useCalled
            ? DoctorFinding.Pass(
                Id, "AddNarrativeTrace(...) and UseNarrativeTrace() are both present",
                DoctorDocUrls.AspNetCoreRegistration)
            : DoctorFinding.Fail(
                Id,
                $"{addCalls[0].Key} calls AddNarrativeTrace(...) but no UseNarrativeTrace() " +
                "or UseMiddleware<NarrativeTraceMiddleware>() was found",
                "Call app.UseNarrativeTrace() on the IApplicationBuilder — without it, requests " +
                "are never wrapped and nothing is traced.",
                DoctorDocUrls.AspNetCoreRegistration);
    }
}
