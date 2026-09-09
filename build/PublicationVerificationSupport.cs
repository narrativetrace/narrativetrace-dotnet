// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace NarrativeTrace.Build;

/// <summary>
/// Pure logic behind the <c>VerifyPublication</c> target — the post-publish consumer-truth check
/// that proves a NuGet.org release actually landed and an adopter's documented day one still
/// works. Kept free of network and filesystem access so
/// <c>PublicationVerificationSupportTests</c> can exercise it without a release or a live
/// registry, mirroring how the java runtime's <c>scripts/verify-publication.sh</c> separates its
/// pure functions from the process/network calls that use them.
/// </summary>
internal static class PublicationVerificationSupport
{
    /// <summary>NuGet.org's flat-container API — the same resource CDN <c>dotnet restore</c> itself resolves against.</summary>
    public const string NuGetOrgFlatContainerBase = "https://api.nuget.org/v3-flatcontainer";

    /// <summary>
    /// One solution project's packability, as the build itself evaluates it (<c>dotnet msbuild
    /// -getProperty:IsPackable,PackageId</c>) — never a hand-kept name list, so a newly added
    /// project can never be silently missed either way.
    /// </summary>
    public sealed record ProjectPackability(string RelativeProjectPath, string PackageId, bool IsPackable);

    /// <summary>
    /// The packable/non-packable split of a solution's projects, plus any structural anomaly
    /// worth a human's attention before a single network call is made.
    /// </summary>
    public sealed record PublicationManifest(
        IReadOnlyList<ProjectPackability> ExpectedOnRegistry,
        IReadOnlyList<ProjectPackability> ExpectedOffRegistry,
        IReadOnlyList<string> Warnings);

    /// <summary>
    /// Splits every solution project into what this build will actually publish
    /// (<see cref="PublicationManifest.ExpectedOnRegistry"/>) and what it will not
    /// (<see cref="PublicationManifest.ExpectedOffRegistry"/>), and flags any packable project
    /// living outside <paramref name="shippedProjectPrefix"/>.
    /// </summary>
    /// <remarks>
    /// This is the exact shape of the defect that let a benchmark harness ride along into a real
    /// release: nothing about being under <c>benchmarks/</c> rather than <c>src/</c> stopped a
    /// project from being packable, and nothing said so out loud before its package shipped. A
    /// hand-kept "these 15 packages ship" list would have been silently wrong the same way; this
    /// derivation instead asks the build what it would actually pack and calls out anything
    /// that does not live where a shipped package's project is expected to live.
    /// </remarks>
    public static PublicationManifest BuildManifest(
        IReadOnlyList<ProjectPackability> projects, string shippedProjectPrefix = "src/")
    {
        var onRegistry = projects
            .Where(p => p.IsPackable)
            .OrderBy(p => p.PackageId, StringComparer.Ordinal)
            .ToList();
        var offRegistry = projects
            .Where(p => !p.IsPackable)
            .OrderBy(p => p.PackageId, StringComparer.Ordinal)
            .ToList();
        var warnings = onRegistry
            .Where(p => !Normalize(p.RelativeProjectPath).StartsWith(shippedProjectPrefix, StringComparison.Ordinal))
            .Select(p => $"{p.PackageId} is packable but its project is not under '{shippedProjectPrefix}' "
                + $"({Normalize(p.RelativeProjectPath)}). A project meant only for this repository's own use "
                + "(benchmark harness, example, fuzz driver, test) should carry <IsPackable>false</IsPackable> "
                + "— otherwise it publishes next release the same way it did once already.")
            .ToList();
        return new PublicationManifest(onRegistry, offRegistry, warnings);
    }

    private static string Normalize(string path) => path.Replace('\\', '/');

    // ------------------------------------------------------------------------------------------
    // Registry URLs + presence classification — mirrors java's maven_*_url / classify_http_status.
    // ------------------------------------------------------------------------------------------

    /// <summary>The flat-container URL for a package's <c>.nupkg</c> blob at an exact version.</summary>
    public static string NupkgUrl(string baseUrl, string packageId, string version) =>
        $"{baseUrl}/{Lower(packageId)}/{Lower(version)}/{Lower(packageId)}.{Lower(version)}.nupkg";

    /// <summary>The flat-container URL for a package's <c>.nuspec</c> manifest at an exact version.</summary>
    public static string NuspecUrl(string baseUrl, string packageId, string version) =>
        $"{baseUrl}/{Lower(packageId)}/{Lower(version)}/{Lower(packageId)}.nuspec";

    private static string Lower(string value) => value.ToLowerInvariant();

    /// <summary>A package version's registry verdict.</summary>
    public enum Presence
    {
        /// <summary>Every checked resource returned 200.</summary>
        Present,

        /// <summary>A checked resource 404'd — not synced yet, an ordinary and expected state right after a publish.</summary>
        Lagging,

        /// <summary>A checked resource returned something other than 200/404 (5xx, or no response at all) — not explained by ordinary sync lag.</summary>
        Missing,
    }

    /// <summary>HTTP status -&gt; verdict. 200 is present; 404 is "not synced yet"; anything else is missing outright.</summary>
    public static Presence ClassifyHttpStatus(int status) => status switch
    {
        200 => Presence.Present,
        404 => Presence.Lagging,
        _ => Presence.Missing,
    };

    /// <summary>
    /// The worst of several per-resource verdicts for one package — every required resource must
    /// read <see cref="Presence.Present"/> for the package itself to. <see cref="Presence.Missing"/>
    /// always wins; otherwise <see cref="Presence.Lagging"/> wins over <see cref="Presence.Present"/>.
    /// </summary>
    public static Presence Worst(IEnumerable<Presence> verdicts)
    {
        var worst = Presence.Present;
        foreach (var verdict in verdicts)
        {
            if (verdict == Presence.Missing)
                return Presence.Missing;
            if (verdict == Presence.Lagging)
                worst = Presence.Lagging;
        }
        return worst;
    }

    /// <summary>Exponential backoff between polling rounds, capped at <paramref name="capSeconds"/> — mirrors the shell script's doubling wait.</summary>
    public static int NextBackoffSeconds(int currentSeconds, int capSeconds) =>
        Math.Min(currentSeconds * 2, capSeconds);
}
