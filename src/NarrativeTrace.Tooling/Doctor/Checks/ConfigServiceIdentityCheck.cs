// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Text.RegularExpressions;
using NarrativeTrace.Tooling.Frameworks;

namespace NarrativeTrace.Tooling.Doctor.Checks;

/// <summary>
/// <c>config.service-identity</c>: a project that wires tracing into its own (non-test) code and names no
/// service sends every span out as <c>unknown_service:dotnet</c> — on the platform and in OpenTelemetry the
/// service is then indistinguishable from every other that forgot. The name comes from
/// <c>NARRATIVETRACE_SERVICE_NAME</c>, a <c>ServiceIdentity</c> built in code, or the ASP.NET Core
/// <c>NarrativeTrace:ServiceName</c> setting. Test projects are not checked: the test fixtures read the
/// variable themselves and a test trace needs no service.
/// </summary>
public static class ConfigServiceIdentityCheck
{
    private const string Id = "config.service-identity";
    private const string Key = "NARRATIVETRACE_SERVICE_NAME";

    private static readonly Regex[] WiringSignals =
    [
        Signal(@"\bNarrativeTraceProxy\s*\.\s*Create\b"), Signal(@"\bAddNarrativeTracing\s*\("),
        Signal(@"\bAddNarrativeTrace\s*\("),
    ];

    private static readonly Regex IdentityInCode = Signal(@"\bnew\s+ServiceIdentity\s*\(");
    private static readonly Regex IdentityInSettings = Signal("\"ServiceName\"\\s*:");
    private static readonly Regex TestSegment = Signal(@"^(tests?|.*\.tests?|.*tests)$");

    /// <summary>Runs the check.</summary>
    public static DoctorFinding Run(DoctorSnapshot snapshot)
    {
        var sources = NonTestSources(snapshot);
        if (!sources.Any(text => WiringSignals.Any(signal => signal.IsMatch(text))))
        {
            return DoctorFinding.Pass(
                Id, "tracing is not wired into non-test code in this project — no service name to check",
                DoctorDocUrls.ConfigurationServiceIdentity);
        }

        return NamesTheService(snapshot, sources) ? Pass() : Fail();
    }

    private static bool NamesTheService(DoctorSnapshot snapshot, IReadOnlyList<string> sources)
    {
        return (snapshot.Env.TryGetValue(Key, out var name) && !string.IsNullOrWhiteSpace(name))
            || sources.Any(IdentityInCode.IsMatch)
            || snapshot.AppSettingsFiles.Values.Any(IdentityInSettings.IsMatch);
    }

    private static IReadOnlyList<string> NonTestSources(DoctorSnapshot snapshot)
    {
        return snapshot.SourceFiles
            .Where(file => !IsTestPath(file.Key))
            .Select(file => CSharpSource.WithoutComments(file.Value))
            .ToList();
    }

    /// <summary>A test path has a whole directory segment that is, or ends in, <c>Tests</c>/<c>Test</c>.</summary>
    private static bool IsTestPath(string path)
    {
        return path.Split('/', '\\')[..^1].Any(TestSegment.IsMatch);
    }

    private static Regex Signal(string pattern) =>
        new(pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static DoctorFinding Pass()
    {
        return DoctorFinding.Pass(
            Id, "a service name is set for the traced application",
            DoctorDocUrls.ConfigurationServiceIdentity);
    }

    private static DoctorFinding Fail()
    {
        return DoctorFinding.Fail(
            Id,
            "tracing is wired into non-test code but no service name is set — every span is attributed to " +
            "unknown_service:dotnet",
            $"Name the service: set {Key}=order-service in the environment, or build the identity in code — " +
            "new NarrativeTraceConfig(serviceIdentity: new ServiceIdentity(\"order-service\")), " +
            "options.ServiceIdentity = new ServiceIdentity(\"order-service\") for AddNarrativeTracing and " +
            "AddNarrativeTrace, or \"NarrativeTrace\": { \"ServiceName\": \"order-service\" } in appsettings.json.",
            DoctorDocUrls.ConfigurationServiceIdentity);
    }
}
