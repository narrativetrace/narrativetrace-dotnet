// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace NarrativeTrace.Build;

/// <summary>One language's completeness result: hard failures (always) plus a warning when the language is in-progress.</summary>
internal sealed record CompletenessResult(IReadOnlyList<string> Failures, IReadOnlyList<string> Warnings);

/// <summary>
/// Backs the manifest-driven half of <c>TranslationCheck</c> — for every
/// document the manifest declares in scope, verifies each language either has
/// it or is allowed not to yet.
/// </summary>
/// <remarks>
/// A <c>complete</c> language missing a document fails the build: "complete"
/// is a public claim ("this language has full parity"), and a silent gap
/// behind that claim is exactly the failure mode this platform exists to
/// catch. An <c>in-progress</c> language missing documents only warns, with
/// the exact list, so the remaining work stays visible without blocking
/// unrelated commits.
///
/// A manifest entry that names a translation file which does not exist on
/// disk is a <em>hard</em> failure regardless of the language's status — that
/// is not an incomplete translation, it is the manifest lying about the tree,
/// and "in-progress" never excuses that.
/// </remarks>
internal static class TranslationCompletenessSupport
{
    /// <summary>Checks every language the manifest declares against every document it declares.</summary>
    public static CompletenessResult Check(string repoRoot, I18nManifest manifest)
    {
        var failures = new List<string>();
        var warnings = new List<string>();
        foreach (var language in manifest.Languages)
            CheckLanguage(repoRoot, language, manifest.Documents, failures, warnings);
        failures.Sort(StringComparer.Ordinal);
        warnings.Sort(StringComparer.Ordinal);
        return new CompletenessResult(failures, warnings);
    }

    private static void CheckLanguage(
        string repoRoot,
        I18nLanguage language,
        IReadOnlyList<I18nDocument> documents,
        List<string> failures,
        List<string> warnings)
    {
        var missing = new List<string>();
        foreach (var document in documents)
        {
            if (!document.Translations.TryGetValue(language.Code, out var translated))
            {
                missing.Add(document.Source);
                continue;
            }
            if (!ExistsUnderDocumentation(repoRoot, translated))
            {
                failures.Add(
                    $"{language.Code}: manifest declares 'documentation/{translated}' for {document.Source} "
                        + "but the file does not exist");
            }
        }
        if (missing.Count == 0)
            return;

        var message = $"{language.Code} ({language.Status.Label()}): missing translation of {string.Join(", ", missing)}";
        if (language.Status == I18nStatus.Complete)
            failures.Add(message);
        else
            warnings.Add(message);
    }

    private static bool ExistsUnderDocumentation(string repoRoot, string translationPath) =>
        File.Exists(Path.Combine(repoRoot, "documentation", translationPath));
}
