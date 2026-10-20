// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Skills.Catalogue;

/// <summary>
/// The commands and done-conditions of <c>narrativetrace-verify</c> and <c>narrativetrace-debug</c>,
/// in the closed <c>dotnet</c>/<c>git</c> vocabulary. Public: the Tier A2 replay keys its executors
/// off these exact constants rather than a second, hand-copied literal.
/// </summary>
/// <remarks>
/// Every command is what an ADOPTER runs in their own project. There is no file-finding command: the
/// vocabulary has none that is portable, and the steps name the folders the run writes to instead.
/// </remarks>
public static class VerifyCommands
{
    /// <summary>Runs the one test that drives the changed path; its artifacts are written by default.</summary>
    public const string RunThePath =
        "dotnet test --filter \"FullyQualifiedName~<the smallest test that drives the real path>\"";

    /// <summary>Runs the whole suite — what approval mode compares, every traced test at once.</summary>
    public const string RunTheSuite = "dotnet test";

    /// <summary>Promotes every reviewed <c>.received.nt</c> onto its <c>.approved.nt</c>.</summary>
    public const string Approve = "dotnet tool run dotnet-narrativetrace approve";

    /// <summary>Where a run writes its artifacts, under the test project — named once for every step.</summary>
    public const string OutputDir = "bin/<configuration>/<tfm>/TestResults/narrativetrace";

    /// <summary>
    /// Why a run left no files for its test — the cause every step's failure note names. An NUnit test
    /// deriving from <c>NarrativeTestBase</c> always writes; an xUnit test writes when its body ends.
    /// </summary>
    public const string NoTestWriteCause =
        "the xUnit test neither runs its body through NarrativeFixture.Run nor calls WriteArtifacts";

    /// <summary>Where the committed baselines and their review copies live, beside the test project.</summary>
    public const string ApprovedDir = "narratives/<TestClass>/<scenario>";
}
