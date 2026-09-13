// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Cli.Doctor;

/// <summary>
/// The outcome of running every registered check over one snapshot.
/// </summary>
/// <param name="Findings">One finding per registered check, in registration order.</param>
/// <param name="ExitCode">
/// <c>0</c> when every finding passed, <c>1</c> when at least one failed.
/// The CLI layer reserves <c>2</c> for "could not run at all" (bad
/// arguments, no project found) — a case this type never represents.
/// </param>
public sealed record DoctorReport(IReadOnlyList<DoctorFinding> Findings, int ExitCode);
