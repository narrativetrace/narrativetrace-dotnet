// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.SkillsEvals;

/// <summary>
/// The one alias table for the Claude lane: <c>haiku</c>/<c>sonnet</c>/<c>opus</c> resolve to full model ids
/// where the runner arguments are parsed, so the agent command and the ledger row carry the same id.
/// An alias moves under you; a ledger row naming it would name the wrong model.
/// </summary>
public static class ModelIds
{
    private const string Auto = "auto";

    private static readonly IReadOnlyDictionary<string, string> Aliases = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["haiku"] = "claude-haiku-5-5",
        ["sonnet"] = "claude-sonnet-5-5",
        ["opus"] = "claude-opus-5-5",
    };

    /// <summary>
    /// Resolves <paramref name="model"/> to the id to run and record. Claude: an alias resolves, a full id or
    /// <c>auto</c> passes, anything else is refused naming the accepted ids. Other platforms: their own slug
    /// passes through, but a Claude alias is still resolved.
    /// </summary>
    public static string Resolve(Platform platform, string model)
    {
        if (Aliases.TryGetValue(model, out var id))
        {
            return id;
        }

        if (platform != Platform.Claude || IsRecordable(platform, model))
        {
            return model;
        }

        throw new ArgumentException(
            $"--model \"{model}\" is not a known model; accepted: {string.Join(", ", Aliases.Values)}, {Auto}");
    }

    /// <summary>True when a ledger row on <paramref name="platform"/> may carry <paramref name="model"/>: never an alias.</summary>
    public static bool IsRecordable(Platform platform, string model) =>
        !Aliases.ContainsKey(model)
        && (platform != Platform.Claude || Aliases.Values.Contains(model) || model == Auto);
}
