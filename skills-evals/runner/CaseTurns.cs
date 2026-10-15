// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Globalization;
using System.Text.Json;

namespace NarrativeTrace.SkillsEvals;

/// <summary>
/// The scripted user replies that drive a Tier B case past its first turn — read from the case's
/// own <c>case.json</c>, beside the fixture and the registry it names.
///
/// <para>A case without them is one turn: the agent is handed <c>prompt.md</c> and whatever it does
/// with that is the whole trial. A case WITH them is a conversation the runner drives, and that is
/// the only way to measure something that must happen in a LATER turn than the one that asked — an
/// approval.</para>
///
/// <para>The declaration is keyed BY TURN NUMBER (<c>"turns": { "2": "yes, file it" }</c>) rather
/// than being a bare array, because the turn a reply is given at is the subject of these cases and
/// an array makes it implicit in its own ordering. Turn 1 is always the case's own
/// <c>prompt.md</c>.</para>
///
/// <para>Every way of being wrong here THROWS rather than degrading to a shorter conversation. A
/// case whose second turn silently did not happen passes its approval grader for the wrong reason
/// — nothing was filed because nothing was asked.</para>
/// </summary>
public static class CaseTurns
{
    private const string Field = "turns";

    /// <summary>
    /// The turns that open a NEW session rather than resuming the conversation:
    /// <c>"freshTurns": [2]</c>. Written for one question — what does a LATER session find? — which
    /// the install cases cannot ask: an install that goes green in the session that ran it says
    /// nothing about the next session, and on 2026-10-10 the next session listed no skill at all
    /// (item 166).
    /// </summary>
    private const string FreshField = "freshTurns";

    /// <summary>The replies <paramref name="caseDir"/> declares, in turn order; empty for a single-turn case.</summary>
    /// <exception cref="InvalidOperationException">the declaration cannot be driven as written.</exception>
    public static IReadOnlyList<string> ScriptedRepliesFor(string caseDir)
    {
        var manifest = Path.Combine(caseDir, "case.json");
        if (!File.Exists(manifest))
        {
            return [];
        }

        using var document = Parse(manifest);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException($"{manifest} is not a JSON object");
        }

        if (!document.RootElement.TryGetProperty(Field, out var turns))
        {
            return [];
        }

        return OrderedReplies(manifest, turns);
    }

    /// <summary>
    /// The turn numbers <paramref name="caseDir"/> declares as fresh sessions, ascending; empty when it
    /// declares none. Each must be a scripted turn — a fresh turn with nothing to say cannot be driven
    /// — and never turn 1, which is a fresh session by definition.
    /// </summary>
    /// <exception cref="InvalidOperationException">the declaration cannot be driven as written.</exception>
    public static IReadOnlyList<int> FreshTurnsFor(string caseDir)
    {
        var manifest = Path.Combine(caseDir, "case.json");
        if (!File.Exists(manifest))
        {
            return [];
        }

        using var document = Parse(manifest);
        if (document.RootElement.ValueKind != JsonValueKind.Object
            || !document.RootElement.TryGetProperty(FreshField, out var declared))
        {
            return [];
        }

        return OrderedFreshTurns(manifest, declared, ScriptedRepliesFor(caseDir).Count + 1);
    }

    private static List<int> OrderedFreshTurns(string manifest, JsonElement declared, int lastTurn)
    {
        if (declared.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException($"{manifest} declares \"{FreshField}\" as something other than an array of turn numbers");
        }

        var fresh = new SortedSet<int>();
        foreach (var entry in declared.EnumerateArray())
        {
            var turn = FreshTurnNumber(manifest, entry, lastTurn);
            if (!fresh.Add(turn))
            {
                throw new InvalidOperationException($"{manifest} declares fresh turn {turn} twice");
            }
        }

        return [.. fresh];
    }

    private static int FreshTurnNumber(string manifest, JsonElement entry, int lastTurn)
    {
        if (entry.ValueKind != JsonValueKind.Number || !entry.TryGetInt32(out var turn))
        {
            throw new InvalidOperationException($"{manifest} declares a \"{FreshField}\" entry that is not a turn number: {entry.GetRawText()}");
        }

        if (turn < 2)
        {
            throw new InvalidOperationException(
                $"{manifest} declares turn {turn} in \"{FreshField}\" — turn 1 is a fresh session already, so only a later turn can be listed");
        }

        return turn <= lastTurn
            ? turn
            : throw new InvalidOperationException(
                $"{manifest} declares turn {turn} in \"{FreshField}\" but scripts no reply for it — a fresh turn with nothing to say cannot be driven");
    }

    private static JsonDocument Parse(string manifest)
    {
        try
        {
            return JsonDocument.Parse(File.ReadAllText(manifest));
        }
        catch (JsonException e)
        {
            throw new InvalidOperationException($"{manifest} is not JSON: {e.Message}", e);
        }
    }

    private static List<string> OrderedReplies(string manifest, JsonElement turns)
    {
        if (turns.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException($"{manifest} declares \"{Field}\" as something other than an object of replies");
        }

        var byTurn = new SortedDictionary<int, string>();
        foreach (var entry in turns.EnumerateObject())
        {
            if (!byTurn.TryAdd(TurnNumber(manifest, entry.Name), Reply(manifest, entry.Value)))
            {
                throw new InvalidOperationException(
                    $"{manifest} declares turn {entry.Name} twice — one reply would silently replace the other");
            }
        }

        if (byTurn.Count == 0)
        {
            throw new InvalidOperationException($"{manifest} declares \"{Field}\" and lists no reply in it");
        }

        RequireContiguousFromTheSecondTurn(manifest, byTurn.Keys);
        return [.. byTurn.Values];
    }

    /// <summary>
    /// A key is a turn number in its one canonical spelling (no sign, no leading zero, so "2", "+2"
    /// and "02" cannot be three keys for one turn), and the first turn a reply can be given at is the
    /// second.
    /// </summary>
    private static int TurnNumber(string manifest, string key)
    {
        if (!int.TryParse(key, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var turn)
            || (turn > 0 && key != turn.ToString(CultureInfo.InvariantCulture)))
        {
            throw new InvalidOperationException($"{manifest} declares a turn keyed \"{key}\", which is not a turn number");
        }

        if (turn < 2)
        {
            throw new InvalidOperationException(
                $"{manifest} declares a reply at turn {turn} — turn 1 is the case's own prompt.md, so the first reply is turn 2");
        }

        return turn;
    }

    private static string Reply(string manifest, JsonElement value)
    {
        var reply = value.ValueKind == JsonValueKind.String
            ? value.GetString()!
            : throw new InvalidOperationException($"{manifest} declares a reply that is not a string: {value.GetRawText()}");
        if (string.IsNullOrWhiteSpace(reply))
        {
            throw new InvalidOperationException($"{manifest} declares a blank reply — a turn with nothing to say cannot be driven");
        }

        return reply;
    }

    /// <summary>
    /// Turns 2..n, with nothing missing. A gap is a turn with no input, and closing it quietly would
    /// hand a later turn's reply to an earlier turn — for an approval case, the difference between a
    /// question answered and a question never asked.
    /// </summary>
    private static void RequireContiguousFromTheSecondTurn(string manifest, IEnumerable<int> declared)
    {
        var expected = 2;
        foreach (var turn in declared)
        {
            if (turn != expected)
            {
                throw new InvalidOperationException(
                    $"{manifest} declares no reply for turn {expected}, so turn {turn} could never be reached");
            }

            expected++;
        }
    }
}
