// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Globalization;

namespace NarrativeTrace.SkillsEvals;

/// <summary>
/// Where a trial's evidence is kept: under the ignored <c>artifacts/</c>, one directory per skill,
/// case, trial AND run, so a three-trial run leaves three records and a re-run never overwrites the
/// one before it. A passing trial's directory ends in <c>-pass</c>: its transcript is what a "this
/// passed for the right reason" claim — or a demonstration recording — is read from (Java's runner
/// keeps it too, Phase 7 cross-port item 8).
/// </summary>
public static class EvidenceDirectory
{
    /// <summary>The directory one trial's evidence is copied to.</summary>
    public static string For(string repoRoot, string skill, string caseName, int trial, bool pass, DateTimeOffset at) =>
        Path.Combine(
            repoRoot, "artifacts", "skills-evals", skill, caseName,
            at.UtcDateTime.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture) + "-trial-" +
                trial.ToString(CultureInfo.InvariantCulture) + (pass ? "-pass" : ""));
}
