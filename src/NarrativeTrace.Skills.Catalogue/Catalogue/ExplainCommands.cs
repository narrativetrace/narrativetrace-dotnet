// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Skills.Catalogue;

/// <summary>
/// The command of <c>narrativetrace-explain</c> beyond the shared vocabulary (<see cref="VerifyCommands"/>),
/// in the closed <c>dotnet</c>/<c>git</c> set. Public: the Tier A2 replay keys off this constant.
/// </summary>
public static class ExplainCommands
{
    /// <summary>Runs the one existing test that exercises the flow asked about; its artifacts are written by default.</summary>
    public const string RunTheScenario =
        "dotnet test --filter \"FullyQualifiedName~<the test that exercises the flow you were asked about>\"";
}
