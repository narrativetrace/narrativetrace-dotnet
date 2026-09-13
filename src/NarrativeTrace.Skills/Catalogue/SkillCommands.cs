// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Skills.Catalogue;

/// <summary>
/// Shared <c>verify:</c> strings, so both skills describe the same doctor finding the same way
/// instead of drifting into two slightly different sentences. Public: <c>NarrativeTrace.Cli</c>'s
/// Tier A2 replay (<c>SkillReplayRegistry</c>) keys its mechanical checks off these exact constants
/// rather than a second, hand-copied literal of the prose.
/// </summary>
public static class SkillCommands
{
    /// <summary>Every finding's <c>passed: false</c> carries a non-empty <c>fix</c> — well-formedness, never an outcome.</summary>
    public const string DoctorReportWellFormed =
        "the JSON report's findings array has one entry per registered check, and every entry " +
        "with \"passed\": false carries a non-empty \"fix\"";

    /// <summary>Every <c>toolchain.*</c> finding reports <c>passed: true</c>.</summary>
    public const string ToolchainChecksHold =
        "every finding whose id starts with \"toolchain.\" reports \"passed\": true";

    /// <summary>The <c>trap.redaction-proof</c> finding reports <c>passed: true</c> — an outcome, not just presence.</summary>
    public const string RedactionProofFindingPasses =
        "re-running the doctor CLI, the finding with id \"trap.redaction-proof\" reports \"passed\": true";

    /// <summary>The <c>trap.approval-traces</c> finding reports <c>passed: true</c> — no stale <c>*.received.nt</c> files remain.</summary>
    public const string ApprovalTracesFindingPasses =
        "the finding with id \"trap.approval-traces\" reports \"passed\": true — no stale " +
        "*.received.nt files remain";

    /// <summary>The doctor CLI's own process exit code is 0 — no failing findings remain.</summary>
    public const string ExitCodeIsZero =
        "the doctor CLI's own process exit code is 0 — no failing findings remain";
}
