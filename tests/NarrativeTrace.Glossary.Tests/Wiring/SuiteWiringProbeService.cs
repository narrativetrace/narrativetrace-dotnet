// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Glossary.Tests.Wiring;

/// <summary>
/// A type whose namespace the production namespace index must find.
/// </summary>
/// <remarks>
/// Its name is deliberately unlike anything else in the test output
/// directory, so <see cref="ClassPackageIndex"/>'s ambiguity rule cannot mask
/// a wiring regression by resolving it to unknown for an unrelated reason.
/// </remarks>
public sealed class SuiteWiringProbeService
{
}
