// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Globalization;

namespace NarrativeTrace.SkillsEvals;

/// <summary>
/// Tier B platform presets (the sporadic-lanes ruling, mirrored from the Python/Java reference).
/// Fills the runner's agent-command seam for each supported CLI so a trial can start with just
/// <c>--platform</c>, always overridable by an explicit <c>--agent-command</c>. Codex, Antigravity and
/// Cursor sit on cheaper plans and run under the sporadic policy (quota-guarded, promotion points only);
/// Claude runs on the regular cadence and is exempt.
/// </summary>
public enum Platform
{
    /// <summary>The Claude Code CLI (<c>claude -p</c>), the harness's regular, quota-exempt lane.</summary>
    Claude,

    /// <summary>The Codex CLI (<c>codex exec</c>), a sporadic, quota-guarded lane.</summary>
    Codex,

    /// <summary>The Antigravity CLI (<c>agy -p</c>), a sporadic, quota-guarded lane.</summary>
    Antigravity,

    /// <summary>The Cursor CLI (<c>agent -p</c>), a sporadic, quota-guarded lane (Phase 8 D4).</summary>
    Cursor,
}

/// <summary>Helpers over <see cref="Platform"/>: parsing, sporadic classification, and presets.</summary>
public static class Platforms
{
    /// <summary>
    /// What the Claude CLI is allowed to do in a trial, as a comma-separated <c>--allowed-tools</c>
    /// list — derived from the published init prompt, not convenience: step 1 reads llms.txt
    /// (<c>WebFetch</c>), steps 2-3 create and edit a project (<c>Read</c>, <c>Edit</c>,
    /// <c>Write</c>), step 5 runs it (<c>Bash</c>), and step 4 follows the skill when it is
    /// available (<c>Skill</c>). Granting less would measure the sandbox, not the skill — a
    /// registry case whose agent cannot invoke a skill measures nothing the registry delivered.
    /// </summary>
    public const string ClaudeTools = "Bash,Read,Edit,Write,WebFetch,Skill";

    /// <summary>
    /// The machine-readable stream, because a grader has to read the agent's TOOL CALLS and not only
    /// its closing text: a URL a command printed and a URL typed into a reply are the same event to
    /// the gate that must not see either before the user's approval turn. <c>--verbose</c> is not
    /// decoration — without it this CLI exits 1 having written ZERO bytes, a silent failure
    /// indistinguishable from an agent that did nothing (verified in the trial container).
    /// </summary>
    public const string ClaudeTranscriptFormat = "--output-format stream-json --verbose";

    /// <summary>
    /// Cursor's headless CLI (<c>agent -p</c>, verified against cursor.com/docs/cli/headless
    /// 2026-10-10). <c>--force</c> applies edits and runs commands without a confirmation nobody is
    /// there to give, for every skill — the doctor included, which diagnoses by running a command.
    /// No <c>--model</c>: the runner's model argument is a ledger label, and one that is not a Cursor
    /// model name would fail the trial before the agent starts.
    /// </summary>
    private const string CursorCommand = "agent -p \"{prompt}\" --output-format text --force";

    /// <summary>
    /// Antigravity's headless CLI (<c>agy -p</c>, verified against <c>agy --help</c> of CLI 1.3.3 and
    /// a real print-mode run on 2026-10-10). <c>--dangerously-skip-permissions</c> is the only headless
    /// auto-approval the CLI has — no <c>--yolo</c>, no approval mode; <c>--mode plan</c> exists but a
    /// doctor trial RUNS a command, so plan mode would measure the sandbox — and it therefore rides
    /// every skill, exactly like Cursor's <c>--force</c>. <c>--model</c> is passed: the ledger's model
    /// is one of <c>agy models</c>' slugs (<c>gemini-3.8-flash-low</c> is the cheapest), so the Cursor
    /// lane's reason for omitting it does not apply.
    /// </summary>
    private const string AntigravityFlags = " --output-format text --dangerously-skip-permissions";

    /// <summary>What a multi-turn template carries where the trial's own session id goes.</summary>
    public const string SessionPlaceholder = "{session}";

    /// <summary>Parses a platform name case-insensitively; null when it names no known lane.</summary>
    public static Platform? Parse(string? value)
    {
        if (value is null)
        {
            return null;
        }

        // By NAME only: Enum.TryParse also accepts numeric text, which would make "--platform 1" Codex.
        return Enum.GetValues<Platform>()
            .Where(platform => string.Equals(platform.ToString(), value, StringComparison.OrdinalIgnoreCase))
            .Select(platform => (Platform?)platform)
            .FirstOrDefault();
    }

    /// <summary>The lowercase wire name (<c>claude</c>/<c>codex</c>/<c>antigravity</c>/<c>cursor</c>) the ledger uses.</summary>
    public static string WireName(this Platform platform) =>
        platform.ToString().ToLowerInvariant();

    /// <summary>Codex, Antigravity and Cursor are sporadic (quota-guarded); Claude is exempt.</summary>
    public static bool IsSporadic(this Platform platform) =>
        platform is Platform.Codex or Platform.Antigravity or Platform.Cursor;

    /// <summary>
    /// The executable a trial on this platform starts — what the skip line names when the lane cannot
    /// run. Cursor's headless CLI is <c>agent</c>, not <c>cursor</c>; Antigravity's is <c>agy</c>.
    /// </summary>
    public static string CliTool(this Platform platform) => platform switch
    {
        Platform.Cursor => "agent",
        Platform.Antigravity => "agy",
        _ => platform.WireName(),
    };

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
            Platform.Claude =>
                $"claude -p \"{{prompt}}\" --model {model} --allowed-tools \"{ClaudeTools}\" {ClaudeTranscriptFormat}",
            Platform.Codex => CodexCommand(model, canonicalSkillName),
            Platform.Antigravity => $"agy -p \"{{prompt}}\" --model {model}{AntigravityFlags}",
            Platform.Cursor => CursorCommand,
            _ => throw new ArgumentOutOfRangeException(nameof(platform), platform, "unknown platform"),
        };

    /// <summary>
    /// How this platform's trial opens a conversation the runner will drive further, or <c>null</c>
    /// when its own multi-turn flags have never been verified in the container the trials run in.
    /// </summary>
    public static string? PresetFirstTurnCommand(this Platform platform, string model, string canonicalSkillName) =>
        MultiTurn(platform, model, canonicalSkillName, "--session-id " + SessionPlaceholder);

    /// <summary>
    /// How this platform's trial continues that same conversation, or <c>null</c> for the same
    /// reason. The id is RESUMED rather than forked, so every later turn names the id turn 1 was given.
    /// </summary>
    public static string? PresetResumedTurnCommand(this Platform platform, string model, string canonicalSkillName) =>
        MultiTurn(platform, model, canonicalSkillName, "--resume " + SessionPlaceholder);

    /// <summary>
    /// The single-turn preset plus one session flag and nothing else. <c>null</c> for every platform
    /// but Claude, and for BOTH halves: a platform that could open a conversation but not resume it
    /// would run its "second turn" as a FRESH session with no memory of the draft, where "nothing
    /// was filed" is true for a reason that has nothing to do with the gate under test. Antigravity is
    /// the near miss: <c>agy --conversation &lt;id&gt;</c> resumes one, but no flag lets the runner choose
    /// the id on turn 1 — it would have to be read back out of the first turn's output, which has not
    /// been verified in the container.
    /// </summary>
    private static string? MultiTurn(Platform platform, string model, string canonicalSkillName, string sessionFlag) =>
        platform switch
        {
            Platform.Claude => platform.PresetAgentCommand(model, canonicalSkillName) + " " + sessionFlag,
            Platform.Codex or Platform.Antigravity or Platform.Cursor => null,
            _ => throw new ArgumentOutOfRangeException(nameof(platform), platform, "unknown platform"),
        };

    private static string CodexCommand(string model, string canonicalSkillName)
    {
        var sandbox = IsReadOnlySkill(canonicalSkillName) ? "read-only" : "workspace-write";
        return string.Create(
            CultureInfo.InvariantCulture,
            $"codex exec --sandbox {sandbox} --model {model} \"{{prompt}}\"");
    }
}
