// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Cli.Doctor;

/// <summary>
/// A single doctor check: a pure function from a snapshot to one finding.
/// Never touches disk, the network, or the process environment directly —
/// everything it needs already lives on <paramref name="snapshot"/>.
/// </summary>
/// <param name="snapshot">The environment to diagnose.</param>
/// <returns>The check's outcome.</returns>
public delegate DoctorFinding DoctorCheck(DoctorSnapshot snapshot);
