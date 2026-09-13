// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Xml.Linq;

namespace NarrativeTrace.ContractProbe.Probes;

/// <summary>
/// <c>config-shape</c>: the published <c>.nuspec</c> for <c>NarrativeTrace.Proxy</c> declares a
/// dependency on <c>NarrativeTrace.Runtime</c>, and <c>NarrativeTrace.Runtime</c>'s own
/// <c>.nuspec</c> declares one on <c>NarrativeTrace.Core</c> — the transitive chain
/// sixty-seconds.md's "one <c>dotnet add package</c> resolves the whole chain" claim depends on.
/// Reads the real published manifests rather than trusting a local restore, which would prove
/// nothing about what nuget.org itself serves.
/// </summary>
internal static class OnePackageInstallProbe
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(20) };

    public static string Observe(string registryBase, string version)
    {
        var proxyDependsOnRuntime = DependsOn(registryBase, "NarrativeTrace.Proxy", version, "NarrativeTrace.Runtime");
        var runtimeDependsOnCore = DependsOn(registryBase, "NarrativeTrace.Runtime", version, "NarrativeTrace.Core");
        return proxyDependsOnRuntime && runtimeDependsOnCore ? "true" : "false";
    }

    private static bool DependsOn(string registryBase, string packageId, string version, string expectedDependencyId)
    {
        var lower = packageId.ToLowerInvariant();
        var url = $"{registryBase}/{lower}/{version.ToLowerInvariant()}/{lower}.nuspec";
        var xml = GetStringOrNull(url);
        if (xml is null)
            return false;

        var dependencyIds = XDocument.Parse(xml).Descendants()
            .Where(e => e.Name.LocalName == "dependency")
            .Select(e => e.Attribute("id")?.Value)
            .Where(id => id is not null);
        return dependencyIds.Contains(expectedDependencyId, StringComparer.OrdinalIgnoreCase);
    }

    private static string? GetStringOrNull(string url)
    {
        try
        {
            var response = Http.Send(new HttpRequestMessage(HttpMethod.Get, url));
            return response.IsSuccessStatusCode ? response.Content.ReadAsStringAsync().GetAwaiter().GetResult() : null;
        }
        catch
        {
            return null;
        }
    }
}
