// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Clarity;

/// <summary>
/// Scores how well a class's methods align with the verbs expected of its
/// role suffix, mirroring the Java model. Alignment is a prefix match of a
/// method's first token against an expected verb.
/// </summary>
public static class CohesionScorer
{
    private const double UnknownRoleScore = 0.7;
    private const double BroadRoleScore = 0.9;

    /// <summary>Mean cohesion across a class → method-names map.</summary>
    public static double ScoreTrace(
        IReadOnlyDictionary<string, List<string>> classMethods)
    {
        if (classMethods.Count == 0)
        {
            return UnknownRoleScore;
        }

        var sum = 0.0;
        foreach (var pair in classMethods)
        {
            sum += ScoreClass(pair.Key, pair.Value);
        }

        return sum / classMethods.Count;
    }

    /// <summary>Cohesion of one class given its method names.</summary>
    public static double ScoreClass(
        string className, IReadOnlyList<string> methodNames)
    {
        var tokens = IdentifierTokenizer.Tokenize(className);
        if (tokens.Count == 0)
        {
            return UnknownRoleScore;
        }

        var suffix = tokens[tokens.Count - 1];
        var expected = RoleSuffixDictionary.ExpectedVerbs(suffix);
        if (expected is null || expected.Count == 0)
        {
            return RoleSuffixDictionary.Classify(suffix)
                == RoleSuffixCategory.Unknown
                ? UnknownRoleScore : BroadRoleScore;
        }

        return methodNames.Count == 0
            ? UnknownRoleScore
            : AlignedRatio(methodNames, expected);
    }

    private static double AlignedRatio(
        IReadOnlyList<string> methodNames, HashSet<string> expected)
    {
        // S3267: LINQ allocates a closure per scored scenario; clarity scoring
        // is allocation-gated by the ClaritySmall benchmark.
#pragma warning disable S3267
        var aligned = 0;
        foreach (var name in methodNames)
        {
            if (IsAligned(name, expected))
            {
                aligned++;
            }
        }
#pragma warning restore S3267

        return (double)aligned / methodNames.Count;
    }

    private static bool IsAligned(string methodName, HashSet<string> expected)
    {
        var tokens = IdentifierTokenizer.Tokenize(methodName);
        if (tokens.Count == 0)
        {
            return false;
        }

        // S3267: see AlignedRatio — same allocation-gated scoring path.
#pragma warning disable S3267
        var first = tokens[0];
        foreach (var verb in expected)
        {
            if (first.StartsWith(verb, StringComparison.Ordinal))
            {
                return true;
            }
        }
#pragma warning restore S3267

        return false;
    }
}
