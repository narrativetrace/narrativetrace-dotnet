// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.ContractProbe.Probes;

/// <summary>
/// The <c>entry-point</c> kind: does <paramref name="coordinate"/> resolve at all, at the version
/// under test, on the real registry — <c>.nupkg</c> and <c>.nuspec</c> both HEAD-checked, the same
/// classification <c>build/PublicationVerificationSupport.cs</c> uses (200 PRESENT, 404 LAGGING,
/// anything else MISSING). Deliberately reimplemented here rather than referencing that class: this
/// project consumes only registry artifacts and can never depend on the root build, which is not
/// published (see <c>build/PublicationVerificationSupport.cs</c>'s remarks for the shared origin).
/// </summary>
internal static class EntryPointProbe
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(20) };

    public static string Observe(string coordinate, string registryBase, string version)
    {
        var lower = coordinate.ToLowerInvariant();
        var loweredVersion = version.ToLowerInvariant();
        var root = $"{registryBase}/{lower}/{loweredVersion}/{lower}";
        var urls = new[] { $"{root}.nuspec", $"{root}.{loweredVersion}.nupkg" };

        var worst = "PRESENT";
        foreach (var url in urls)
        {
            var verdict = Classify(HeadStatus(url));
            if (verdict == "MISSING")
                return "MISSING";
            if (verdict == "LAGGING")
                worst = "LAGGING";
        }

        return worst;
    }

    private static int HeadStatus(string url)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Head, url);
            using var response = Http.Send(request);
            return (int)response.StatusCode;
        }
        catch
        {
            return 0;
        }
    }

    private static string Classify(int status) => status switch
    {
        200 => "PRESENT",
        404 => "LAGGING",
        _ => "MISSING",
    };
}
