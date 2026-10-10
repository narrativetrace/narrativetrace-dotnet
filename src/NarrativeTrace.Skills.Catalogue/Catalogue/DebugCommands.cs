// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Skills.Catalogue;

/// <summary>
/// The commands of <c>narrativetrace-debug</c> beyond the shared pin (<see cref="VerifyCommands"/>), in
/// the closed <c>dotnet</c>/<c>git</c> vocabulary. Public: the Tier A2 replay keys off these constants.
/// </summary>
public static class DebugCommands
{
    /// <summary>Runs the test that reproduces the symptom; its artifacts are written by default.</summary>
    public const string RunTheReproduction =
        "dotnet test --filter \"FullyQualifiedName~<the test that reproduces the symptom>\"";
}
