// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Text.RegularExpressions;

namespace NarrativeTrace.SkillsEvals;

/// <summary>
/// Everything that drives a trial's agent — one command template per turn, one prompt per turn,
/// and the session id that makes several turns ONE conversation rather than several unrelated ones.
///
/// <para>A single-turn trial is the ordinary shape: one prompt, the platform's own preset, no
/// session. A multi-turn trial is the shape a case needs when what it measures must happen in a
/// LATER turn than the one that asked for it — an approval is only an approval if it arrives in a
/// turn of the user's own.</para>
///
/// <para>Every way of being wrong is REFUSED here rather than degraded, and that is the
/// load-bearing decision in this type. A multi-turn case that quietly ran as several independent
/// single turns would reach its approval question with an agent that had forgotten the draft, and
/// then pass its grader — "nothing was filed" — for a reason that has nothing to do with the gate.</para>
/// </summary>
public sealed partial class AgentTurns
{
    private readonly string? _firstTurnCommand;
    private readonly string? _resumedTurnCommand;
    private readonly string[] _prompts;

    /// <summary>
    /// <paramref name="firstTurnCommand"/> is turn 1's template, or <c>null</c> to drive no agent at
    /// all; <paramref name="resumedTurnCommand"/> is every later turn's, or <c>null</c> for one turn;
    /// <paramref name="prompts"/> holds one prompt per turn, <c>prompt.md</c> first.
    /// </summary>
    /// <exception cref="ArgumentException">no turn, a blank prompt, or several turns with nothing to resume them.</exception>
    public AgentTurns(string? firstTurnCommand, string? resumedTurnCommand, IReadOnlyList<string> prompts)
    {
        ArgumentNullException.ThrowIfNull(prompts);
        if (prompts.Count == 0)
        {
            throw new ArgumentException("a trial needs at least one turn's prompt", nameof(prompts));
        }

        if (prompts.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("a turn with a blank prompt cannot be driven", nameof(prompts));
        }

        RequireDrivableTemplates(firstTurnCommand, resumedTurnCommand, prompts.Count);
        if (prompts.Count > 1 && string.IsNullOrWhiteSpace(resumedTurnCommand))
        {
            throw new ArgumentException(
                $"a trial of {prompts.Count} turns needs a template that resumes its session", nameof(resumedTurnCommand));
        }

        _firstTurnCommand = firstTurnCommand;
        _resumedTurnCommand = resumedTurnCommand;
        _prompts = [.. prompts];
    }

    /// <summary>
    /// A blank template is an empty command that would only fail when the turn runs; no first
    /// template means "drive no agent", which a conversation of several turns cannot mean — its
    /// later turns would silently never run.
    /// </summary>
    private static void RequireDrivableTemplates(string? firstTurnCommand, string? resumedTurnCommand, int turns)
    {
        if ((firstTurnCommand is not null && string.IsNullOrWhiteSpace(firstTurnCommand))
            || (firstTurnCommand is null && (turns > 1 || resumedTurnCommand is not null)))
        {
            throw new ArgumentException("a turn's template is blank, or a conversation has no template to open it");
        }
    }

    /// <summary>
    /// What drives this case on this platform: the override or preset for one turn; the platform's
    /// session pair, with <paramref name="sessionId"/> substituted, for several.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// the platform cannot resume a session a multi-turn case needs, an override was given for a
    /// multi-turn case, or the session id is not one opaque token.
    /// </exception>
    public static AgentTurns Of(
        Platform platform, string model, string canonicalSkillName, string? overrideCommand,
        string firstPrompt, IReadOnlyList<string> scriptedReplies, string? sessionId)
    {
        RequireOneTokenModel(model);
        string[] prompts = [firstPrompt, .. scriptedReplies];
        if (prompts.Length == 1)
        {
            return new AgentTurns(overrideCommand ?? platform.PresetAgentCommand(model, canonicalSkillName), null, prompts);
        }

        RequireNoOverride(overrideCommand);
        var session = RequireAWellShapedSessionId(sessionId);
        return new AgentTurns(
            Named(platform.PresetFirstTurnCommand(model, canonicalSkillName), platform, session),
            Named(platform.PresetResumedTurnCommand(model, canonicalSkillName), platform, session),
            prompts);
    }

    /// <summary>A trial that drives no agent: the fixture is graded exactly as the case scaffolded it.</summary>
    public static AgentTurns Graded(string prompt) => new(null, null, [prompt]);

    /// <summary>Whether an agent runs at all, or the trial goes straight to its grader.</summary>
    public bool DrivesAnAgent => _firstTurnCommand is not null;

    /// <summary>How many turns this trial drives.</summary>
    public int TurnCount => _prompts.Length;

    /// <summary>What the agent is handed on <paramref name="turn"/>, 1-based.</summary>
    public string PromptForTurn(int turn) => _prompts[IndexOf(turn)];

    /// <summary>
    /// The template <paramref name="turn"/> runs, the session id already substituted — a caller
    /// hands this straight to <see cref="AgentArgv"/>, which only ever substitutes the prompt.
    /// </summary>
    public string CommandForTurn(int turn) =>
        (IndexOf(turn) == 0 ? _firstTurnCommand : _resumedTurnCommand)
        ?? throw new InvalidOperationException("a trial that drives no agent has no command to run");

    private int IndexOf(int turn)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(turn, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(turn, _prompts.Length);
        return turn - 1;
    }

    /// <summary>
    /// A multi-turn case uses its platform's preset pair, never an override: one override string
    /// cannot be two templates, and the one it would be is the FIRST turn's — so an accepted override
    /// would run every later turn as a fresh conversation.
    /// </summary>
    private static void RequireNoOverride(string? overrideCommand)
    {
        if (overrideCommand is not null)
        {
            throw new ArgumentException(
                "a multi-turn case runs on its platform's preset: one --agent-command cannot be both the"
                + " template that opens a session and the one that resumes it");
        }
    }

    /// <summary>
    /// The model id is spliced into a template that is tokenized afterwards, so one carrying
    /// whitespace or a quote would become extra argv — another flag, chosen by whoever typed it.
    /// </summary>
    private static void RequireOneTokenModel(string model)
    {
        if (string.IsNullOrWhiteSpace(model) || model.Any(c => char.IsWhiteSpace(c) || c is '"' or '\''))
        {
            throw new ArgumentException($"a model id is one token, not \"{model}\"", nameof(model));
        }
    }

    /// <summary>An id carrying whitespace or a quote would split into two argv elements and change the command.</summary>
    private static string RequireAWellShapedSessionId(string? sessionId) =>
        sessionId is not null && SessionId().IsMatch(sessionId)
            ? sessionId
            : throw new ArgumentException(
                $"a multi-turn trial's session id must be one opaque token (a uuid), not \"{sessionId}\"");

    private static string Named(string? template, Platform platform, string sessionId) =>
        template?.Replace(Platforms.SessionPlaceholder, sessionId, StringComparison.Ordinal)
        ?? throw new ArgumentException(
            $"{platform.WireName()} cannot drive a multi-turn case: its own multi-turn flags have never"
            + " been verified in the container the trials run in");

    [GeneratedRegex("^[0-9a-fA-F]{8}(-[0-9a-fA-F]{4}){3}-[0-9a-fA-F]{12}\\z")]
    private static partial Regex SessionId();
}
