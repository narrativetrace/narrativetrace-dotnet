// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;

namespace NarrativeTrace.Clarity;

/// <summary>
/// Turns the identifiers of a trace into ranked, de-duplicated
/// <see cref="ClarityIssue"/>s.
/// </summary>
/// <remarks>
/// Four categories, mirroring the Java edition: <c>method-name</c>,
/// <c>class-name</c> and <c>param-name</c> fire when the corresponding scorer
/// puts an identifier below 0.50, with the severity derived from that score;
/// <c>collocation</c> fires when a method's verb is not one the
/// <see cref="CollocationDictionary"/> associates with its object noun, and is
/// always Low because it is a suggestion, not a defect. Repeats of the same
/// <c>(category, element)</c> collapse into one issue counting occurrences,
/// and the result is ordered by impact so the report leads with what costs
/// most.
/// </remarks>
public static class ClarityIssueFinder
{
    /// <summary>Score at or above which an identifier raises no issue.</summary>
    private const double IssueThreshold = 0.50;

    private const string MethodSuggestion =
        "Use a domain-specific verb+noun (e.g., calculateTotal, reserveInventory)";

    private const string ClassSuggestion =
        "Use a domain-specific name or a recognized pattern suffix "
        + "(e.g., OrderService, PaymentGateway)";

    private const string ParameterSuggestion =
        "Use a domain-specific name (e.g., customerId, orderAmount)";

    private const string PropertySuggestion =
        "Name the member after its domain concept "
        + "(e.g., customerId, orderAmount)";

    private static readonly HashSet<string> NoPropertyNames =
        new(StringComparer.Ordinal);

    /// <summary>Finds every issue in the given nodes, de-duplicated and ranked.</summary>
    /// <param name="nodes">Flattened trace nodes; must not be null.</param>
    /// <returns>Issues ordered by descending impact score.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="nodes"/> is null.</exception>
    public static IReadOnlyList<ClarityIssue> Find(IReadOnlyList<TraceNode> nodes)
    {
        return Find(nodes, NoPropertyNames);
    }

    /// <summary>
    /// Finds every issue, treating the named members as properties: they are
    /// scored on the noun rubric, reported under <c>property-name</c>, and
    /// exempt from the collocation rubric — a bare noun has no verb+noun
    /// pairing to check.
    /// </summary>
    /// <param name="nodes">Flattened trace nodes; must not be null.</param>
    /// <param name="propertyNames">
    /// Member names to judge as nouns; must not be null.
    /// </param>
    /// <returns>Issues ordered by descending impact score.</returns>
    /// <exception cref="ArgumentNullException">Either argument is null.</exception>
    /// <param name="vocabulary">
    /// The project's committed glossary vocabulary, so issues are raised in the
    /// same language the scores are computed in; null uses the built-in
    /// dictionaries alone.
    /// </param>
    public static IReadOnlyList<ClarityIssue> Find(
        IReadOnlyList<TraceNode> nodes,
        ISet<string> propertyNames,
        DomainVocabulary? vocabulary = null)
    {
        if (nodes is null)
        {
            throw new ArgumentNullException(nameof(nodes));
        }

        if (propertyNames is null)
        {
            throw new ArgumentNullException(nameof(propertyNames));
        }

        var raw = new List<ClarityIssue>();
        raw.AddRange(MethodNameIssues(nodes, propertyNames, vocabulary));
        raw.AddRange(PropertyNameIssues(nodes, propertyNames, vocabulary));
        raw.AddRange(ClassNameIssues(nodes, vocabulary));
        raw.AddRange(ParameterNameIssues(nodes, vocabulary));
        raw.AddRange(CollocationIssues(nodes, propertyNames));
        return DeduplicateAndRank(raw);
    }

    /// <summary>
    /// Collapses repeats of one <c>(category, element)</c> into a single issue
    /// whose occurrence count drives its rank.
    /// </summary>
    private static List<ClarityIssue> DeduplicateAndRank(List<ClarityIssue> issues)
    {
        return issues
            .GroupBy(issue => $"{issue.Category}|{issue.Element}", StringComparer.Ordinal)
            .Select(group => group.First().WithOccurrences(group.Count()))
            .OrderByDescending(issue => issue.ImpactScore)
            .ThenBy(issue => issue.Category, StringComparer.Ordinal)
            .ThenBy(issue => issue.Element, StringComparer.Ordinal)
            .ToList();
    }

    private static IEnumerable<ClarityIssue> MethodNameIssues(
        IReadOnlyList<TraceNode> nodes,
        ISet<string> propertyNames,
        DomainVocabulary? vocabulary)
    {
        return nodes
            .Where(node => !string.IsNullOrWhiteSpace(node.Signature.MethodName))
            .Where(node => !propertyNames.Contains(node.Signature.MethodName))
            .Select(node => (
                node,
                score: MethodNameScorer.Score(
                    node.Signature.MethodName, vocabulary)))
            .Where(scored => scored.score < IssueThreshold)
            .Select(scored => new ClarityIssue(
                "method-name",
                $"{scored.node.Signature.ClassName}.{scored.node.Signature.MethodName}",
                MethodSuggestion,
                ClaritySeverityExtensions.FromScore(scored.score)));
    }

    /// <summary>Properties are judged as nouns, like parameter names.</summary>
    private static IEnumerable<ClarityIssue> PropertyNameIssues(
        IReadOnlyList<TraceNode> nodes,
        ISet<string> propertyNames,
        DomainVocabulary? vocabulary)
    {
        return nodes
            .Where(node => propertyNames.Contains(node.Signature.MethodName))
            .Select(node => (
                node,
                score: ParameterNameScorer.Score(
                    node.Signature.MethodName, vocabulary)))
            .Where(scored => scored.score < IssueThreshold)
            .Select(scored => new ClarityIssue(
                "property-name",
                $"{scored.node.Signature.ClassName}.{scored.node.Signature.MethodName}",
                PropertySuggestion,
                ClaritySeverityExtensions.FromScore(scored.score)));
    }

    /// <summary>One issue per distinct class, however many nodes it owns.</summary>
    private static IEnumerable<ClarityIssue> ClassNameIssues(
        IReadOnlyList<TraceNode> nodes, DomainVocabulary? vocabulary)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in nodes)
        {
            var className = node.Signature.ClassName;
            if (string.IsNullOrWhiteSpace(className) || !seen.Add(className))
            {
                continue;
            }

            var score = ClassNameScorer.Score(className, vocabulary);
            if (score < IssueThreshold)
            {
                yield return new ClarityIssue(
                    "class-name", className, ClassSuggestion,
                    ClaritySeverityExtensions.FromScore(score));
            }
        }
    }

    private static IEnumerable<ClarityIssue> ParameterNameIssues(
        IReadOnlyList<TraceNode> nodes, DomainVocabulary? vocabulary)
    {
        return nodes
            .SelectMany(node => node.Signature.Parameters)
            .Where(parameter => !string.IsNullOrWhiteSpace(parameter.Name))
            .Select(parameter => (
                parameter,
                score: ParameterNameScorer.Score(parameter.Name, vocabulary)))
            .Where(scored => scored.score < IssueThreshold)
            .Select(scored => new ClarityIssue(
                "param-name", scored.parameter.Name, ParameterSuggestion,
                ClaritySeverityExtensions.FromScore(scored.score)));
    }

    private static IEnumerable<ClarityIssue> CollocationIssues(
        IReadOnlyList<TraceNode> nodes, ISet<string> propertyNames)
    {
        return nodes
            .Where(node => !propertyNames.Contains(node.Signature.MethodName))
            .Select(node => (
                node,
                suggestion: CollocationSuggestion(node.Signature.MethodName)))
            .Where(advised => advised.suggestion is not null)
            .Select(advised => new ClarityIssue(
                "collocation",
                $"{advised.node.Signature.ClassName}.{advised.node.Signature.MethodName}",
                advised.suggestion!,
                ClaritySeverity.Low));
    }

    /// <summary>
    /// Returns advice when the method's leading verb is not idiomatic for its
    /// trailing noun, or null when it is (or when nothing is known).
    /// </summary>
    private static string? CollocationSuggestion(string methodName)
    {
        var tokens = IdentifierTokenizer.Tokenize(methodName);
        if (tokens.Count < 2)
        {
            return null;
        }

        var verb = tokens[0];
        var noun = tokens[tokens.Count - 1];
        var preferred = CollocationDictionary.PreferredVerbs(noun);
        if (preferred.Count == 0 || preferred.Contains(verb))
        {
            return null;
        }

        var capitalized = char.ToUpperInvariant(noun[0]) + noun.Substring(1);
        return "Consider: " + string.Join(
            ", ",
            preferred.OrderBy(v => v, StringComparer.Ordinal)
                .Select(v => v + capitalized));
    }
}
