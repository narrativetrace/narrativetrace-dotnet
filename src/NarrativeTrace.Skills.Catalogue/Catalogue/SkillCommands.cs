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

    /// <summary>
    /// The framework hand-off (Phase 6 D2): which frameworks this project uses, and how each is wired,
    /// is the installed doctor's answer — the step names no framework. <c>config.registration-pair</c>
    /// is named because it kept the id it shipped with before the framework table existed.
    /// </summary>
    public const string FrameworkFixesApplied =
        "every config.<framework>-* finding in the report, and config.registration-pair, passes: apply " +
        "each failing one's fix in order, as printed, then run the doctor again; a framework the doctor " +
        "reports as having no integration shipped is left alone";

    /// <summary>The doctor CLI's own process exit code is 0 — no failing findings remain.</summary>
    public const string ExitCodeIsZero =
        "the doctor CLI's own process exit code is 0 — no failing findings remain";

    /// <summary>
    /// The preview's definition of done, in prose (D11): a diff to read, and a tree nobody has
    /// touched. Prose rather than a command because "nothing was written" is the half a shell exit
    /// code cannot express, and it is the half that matters.
    /// </summary>
    public const string InitPreviewWroteNothing =
        "the diff names .agents/skills/ and AGENTS.md, and neither exists yet — nothing is " +
        "written until the same command runs again without --dry-run";

    /// <summary>
    /// The install's hand-off to the next session (Phase 7 D7), closing the preview step: once the skills
    /// are installed, the session after this one reads what its changes did before reporting them.
    /// </summary>
    public const string InitPreviewThenNextSessionVerifies =
        InitPreviewWroteNothing + "; the next session verifies with narrativetrace-verify — once a change's " +
        "tests are green, it reads the trace before it reports";
}
