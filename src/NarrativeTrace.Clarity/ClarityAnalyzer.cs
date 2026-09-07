// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;

namespace NarrativeTrace.Clarity;

/// <summary>
/// Scores the identifiers in a trace for readability and reports what to fix.
/// </summary>
/// <remarks>
/// The entry point to the clarity engine. It reads a finished
/// <see cref="TraceTree"/> — which is why clarity is measured over names that
/// actually executed, rather than over every name in the assembly. Pure and
/// stateless; the same tree always scores the same. The weighting that combines
/// the dimensions is a cross-language contract shared with the Java runtime.
/// </remarks>
public static class ClarityAnalyzer
{
    /// <summary>Scores a trace, treating every member name as a method name.</summary>
    /// <param name="tree">The trace whose identifiers should be scored. An empty tree scores without issues.</param>
    /// <returns>The scores and ranked issues.</returns>
    /// <remarks>
    /// Convenience overload with no property names supplied. Prefer
    /// <see cref="Analyze(TraceTree, ISet{string})"/> when you know which
    /// members are properties: judged as methods, a correctly-named noun
    /// property is marked down for lacking a verb.
    /// </remarks>
    public static ClarityResult Analyze(TraceTree tree)
    {
        return Analyze(tree, EmptyPropertyNames);
    }

    /// <summary>
    /// Analyzes one trace tree, scoring the named members on the noun rubric.
    /// </summary>
    /// <remarks>
    /// A property's name is a noun, so judging it against the verb+noun method
    /// standard mis-calibrates it — the author is marked down for a name they
    /// wrote correctly. Callers that know which names are properties (see
    /// <see cref="ClarityScanner"/>) pass them here; those names are scored
    /// like parameter names, so <c>amount</c> scores well while <c>data</c>
    /// still scores poorly. The scores stay in the existing method dimension:
    /// the weight vector is a cross-language contract.
    /// </remarks>
    /// <param name="tree">Trace tree whose identifiers should be scored.</param>
    /// <param name="propertyNames">
    /// Member names to score as nouns instead of verb+noun.
    /// </param>
    /// <param name="vocabulary">
    /// The project's committed glossary vocabulary (ADR-012), which extends
    /// every built-in dictionary without overriding any of them; null scores
    /// with the built-in dictionaries alone — what a project with no glossary
    /// gets.
    /// </param>
    public static ClarityResult Analyze(
        TraceTree tree,
        ISet<string> propertyNames,
        DomainVocabulary? vocabulary = null)
    {
        var ctx = new AnalysisContext();
        // TraceNode.Children is a type, not a guarantee of acyclicity - bound
        // once, here, so the recursive walk below can never overflow the
        // stack or loop forever on a hand-built or replayed cycle. Cheap on
        // ordinary input: TreeWalk.Bound returns Roots unchanged once it
        // confirms there is nothing to bound.
        CollectNames(TreeWalk.Bound(tree.Roots), ctx, 1);
        var scores = ComputeScores(ctx, propertyNames, vocabulary);
        return BuildResult(
            scores,
            ClarityIssueFinder.Find(ctx.Nodes, propertyNames, vocabulary));
    }

    // netstandard2.0 has no IReadOnlySet<T>, so the contract is ISet<string>.
    private static readonly HashSet<string> EmptyPropertyNames =
        new(StringComparer.Ordinal);

    private static Scores ComputeScores(
        AnalysisContext ctx,
        ISet<string> propertyNames,
        DomainVocabulary? vocabulary)
    {
        return new Scores(
            AverageScore(
                ctx.Methods,
                name => ScoreMemberName(name, propertyNames, vocabulary)),
            AverageScore(
                ctx.Classes.Distinct().ToList(),
                name => ClassNameScorer.Score(name, vocabulary)),
            ctx.Parameters.Count > 0
                ? AverageScore(
                    ctx.Parameters,
                    name => ParameterNameScorer.Score(name, vocabulary))
                : 1.0,
            StructuralScorer.Score(
                ctx.MaxParams, ctx.MaxDepth),
            CohesionScorer.ScoreTrace(ctx.ClassMethods));
    }

    private static double ScoreMemberName(
        string name, ISet<string> propertyNames, DomainVocabulary? vocabulary)
    {
        return propertyNames.Contains(name)
            ? ParameterNameScorer.Score(name, vocabulary)
            : MethodNameScorer.Score(name, vocabulary);
    }

    private static ClarityResult BuildResult(
        Scores s, IReadOnlyList<ClarityIssue> issues)
    {
        var overall = (s.Method * 0.30)
            + (s.Class * 0.20)
            + (s.Parameter * 0.25)
            + (s.Structural * 0.15)
            + (s.Cohesion * 0.10);

        return new ClarityResult(
            Math.Round(overall, 3),
            Math.Round(s.Method, 3),
            Math.Round(s.Class, 3),
            Math.Round(s.Parameter, 3),
            Math.Round(s.Structural, 3),
            Math.Round(s.Cohesion, 3),
            issues);
    }

    private static void CollectNames(
        IReadOnlyList<TraceNode> nodes,
        AnalysisContext ctx, int depth)
    {
        for (var i = 0; i < nodes.Count; i++)
        {
            CollectNode(nodes[i], ctx, depth);
        }
    }

    private static void CollectNode(
        TraceNode node, AnalysisContext ctx, int depth)
    {
        ctx.Methods.Add(node.Signature.MethodName);
        ctx.Classes.Add(node.Signature.ClassName);
        ctx.Nodes.Add(node);
        ctx.AddClassMethod(
            node.Signature.ClassName, node.Signature.MethodName);
        CollectParams(node, ctx.Parameters);
        ctx.UpdateMax(
            depth, node.Signature.Parameters.Count);
        CollectNames(node.Children, ctx, depth + 1);
    }

    private static void CollectParams(
        TraceNode node, List<string> parameters)
    {
        for (var i = 0; i < node.Signature.Parameters.Count; i++)
        {
            parameters.Add(
                node.Signature.Parameters[i].Name);
        }
    }

    private static double AverageScore(
        List<string> names, Func<string, double> scorer)
    {
        if (names.Count == 0)
        {
            return 0.0;
        }

        var sum = 0.0;
        for (var i = 0; i < names.Count; i++)
        {
            sum += scorer(names[i]);
        }

        return sum / names.Count;
    }

    private sealed class AnalysisContext
    {
        public List<string> Methods { get; } = [];
        public List<string> Classes { get; } = [];
        public List<string> Parameters { get; } = [];

        /// <summary>Every visited node, flattened, for issue detection.</summary>
        public List<TraceNode> Nodes { get; } = [];

        public Dictionary<string, List<string>> ClassMethods { get; } =
            new(StringComparer.Ordinal);

        public int MaxDepth { get; private set; }
        public int MaxParams { get; private set; }

        public void AddClassMethod(string className, string methodName)
        {
            if (!ClassMethods.TryGetValue(className, out var methods))
            {
                methods = [];
                ClassMethods[className] = methods;
            }

            methods.Add(methodName);
        }

        public void UpdateMax(int depth, int paramCount)
        {
            if (depth > MaxDepth)
            {
                MaxDepth = depth;
            }

            if (paramCount > MaxParams)
            {
                MaxParams = paramCount;
            }
        }
    }

    private sealed record Scores(
        double Method, double Class, double Parameter,
        double Structural, double Cohesion);
}
