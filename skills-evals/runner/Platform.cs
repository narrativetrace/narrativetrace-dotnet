// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Globalization;

namespace NarrativeTrace.SkillsEvals;

/// <summary>
/// Tier B platform presets (the sporadic-lanes ruling, mirrored from the Python/Java reference).
/// Fills the runner's agent-command seam for each supported CLI so a trial can start with just
/// <c>--platform</c>, always overridable by an explicit <c>--agent-command</c>. Codex and Gemini
/// sit on cheaper plans and run under the sporadic policy (quota-guarded, promotion points only);
/// Claude runs on the regular cadence and is exempt.
/// </summary>
public enum Platform
{
    /// <summary>The Claude Code CLI (<c>claude -p</c>), the harness's regular, quota-exempt lane.</summary>
    Claude,

    /// <summary>The Codex CLI (<c>codex exec</c>), a sporadic, quota-guarded lane.</summary>
    Codex,

    /// <summary>The Gemini CLI (<c>gemini -p</c>), a sporadic, quota-guarded lane.</summary>
    Gemini,
}

/// <summary>Helpers over <see cref="Platform"/>: parsing, sporadic classification, and presets.</summary>
public static class Platforms
{
    /// <summary>
    /// What the Claude CLI is allowed to do in a trial, as a comma-separated <c>--allowed-tools</c>
    /// list — derived from the published init prompt, not convenience: step 1 reads llms.txt
    /// (<c>WebFetch</c>), steps 2-3 create and edit a project (<c>Read</c>, <c>Edit</c>,
    /// <c>Write</c>), step 5 runs it (<c>Bash</c>). Granting less would measure the sandbox, not
    /// the skill.
    /// </summary>
    public const string ClaudeTools = "Bash,Read,Edit,Write,WebFetch";

    /// <summary>Parses a platform name case-insensitively; null when it names no known lane.</summary>
    public static Platform? Parse(string? value)
    {
        if (value is null)
        {
            return null;
        }

        return Enum.TryParse<Platform>(value, ignoreCase: true, out var platform) ? platform : null;
    }

    /// <summary>The lowercase wire name (<c>claude</c>/<c>codex</c>/<c>gemini</c>) the ledger uses.</summary>
    public static string WireName(this Platform platform) =>
        platform.ToString().ToLowerInvariant();

    /// <summary>Codex and Gemini are sporadic (quota-guarded); Claude is exempt.</summary>
    public static bool IsSporadic(this Platform platform) =>
        platform is Platform.Codex or Platform.Gemini;

    /// <summary>
    /// <c>narrativetrace-doctor</c> is read-only by design; every other cataloged skill installs or
    /// edits files. Used to pick the least-privileged sandbox/approval mode a trial needs.
    /// </summary>
    public static bool IsReadOnlySkill(string canonicalSkillName) =>
        canonicalSkillName == "narrativetrace-doctor";

    /// <summary>
    /// The default <c>{prompt}</c>-templated agent command for this platform. Each CLI uses its own
    /// subscription login — the harness never passes an API key, so no preset threads one through.
    /// </summary>
    public static string PresetAgentCommand(this Platform platform, string model, string canonicalSkillName) =>
        platform switch
        {
            Platform.Claude => $"claude -p \"{{prompt}}\" --model {model} --allowed-tools \"{ClaudeTools}\"",
            Platform.Codex => CodexCommand(model, canonicalSkillName),
            Platform.Gemini => GeminiCommand(model, canonicalSkillName),
            _ => throw new ArgumentOutOfRangeException(nameof(platform), platform, "unknown platform"),
        };

    private static string CodexCommand(string model, string canonicalSkillName)
    {
        var sandbox = IsReadOnlySkill(canonicalSkillName) ? "read-only" : "workspace-write";
        return string.Create(
            CultureInfo.InvariantCulture,
            $"codex exec --sandbox {sandbox} --model {model} \"{{prompt}}\"");
    }

    private static string GeminiCommand(string model, string canonicalSkillName)
    {
        var approvalMode = IsReadOnlySkill(canonicalSkillName) ? "plan" : "auto_edit";
        return string.Create(
            CultureInfo.InvariantCulture,
            $"gemini -p \"{{prompt}}\" --model {model} --approval-mode {approvalMode}");
    }
}
