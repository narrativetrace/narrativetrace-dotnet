// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
#nullable enable
using System.Collections.Generic;
using System.Linq;

namespace NarrativeTrace.Build;

/// <summary>Everything the <c>TranslationCheck</c> target reports: hard failures (fail the build) and warnings (printed, never fail it).</summary>
internal sealed record TranslationPlatformResult(IReadOnlyList<string> Failures, IReadOnlyList<string> Warnings);

/// <summary>
/// The single orchestrator behind the <c>TranslationCheck</c> target — ties
/// together staleness (<see cref="TranslationCheckSupport"/>, unconditional),
/// and, when <c>documentation/i18n/manifest.json</c> is present, completeness
/// (<see cref="TranslationCompletenessSupport"/>), structure parity
/// (<see cref="TranslationStructureSupport"/>), index/menu integrity
/// (<see cref="TranslationIndexSupport"/>) and the review-field summary
/// (<see cref="TranslationReviewSupport"/>).
/// </summary>
/// <remarks>
/// Graceful degradation is deliberate: a repository (or a port that has not
/// yet adopted the manifest) with no <c>documentation/i18n/manifest.json</c>
/// still gets the original staleness-only check, plus exactly one warning
/// saying why nothing else ran — never a crash, never a silent skip.
/// </remarks>
internal static class TranslationPlatformSupport
{
    /// <summary>Runs every translation check the current tree supports; never throws — callers decide what to do with it.</summary>
    public static TranslationPlatformResult RunAll(string repoRoot)
    {
        var staleness = TranslationCheckSupport.Check(repoRoot);
        var manifest = I18nManifestSupport.LoadOrNull(repoRoot);
        if (manifest is null)
        {
            return new TranslationPlatformResult(
                staleness,
                [$"TranslationCheck: no manifest at {I18nManifestSupport.ManifestRelativePath} — ran the staleness-only check"]);
        }

        var completeness = TranslationCompletenessSupport.Check(repoRoot, manifest);
        var structure = TranslationStructureSupport.CheckAll(repoRoot);
        var index = TranslationIndexSupport.CheckAll(repoRoot, manifest);

        var failures = staleness.Concat(completeness.Failures).Concat(structure.Failures).Concat(index).ToList();
        var warnings = completeness.Warnings.Concat(structure.Warnings)
            .Append(TranslationReviewSupport.SummaryLine(repoRoot))
            .ToList();
        return new TranslationPlatformResult(failures, warnings);
    }
}
