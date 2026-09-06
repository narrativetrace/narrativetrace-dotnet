// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;

namespace NarrativeTrace.Glossary;

/// <summary>Harvests glossary candidates from captured trace trees.</summary>
/// <remarks>
/// Trace mode of the plan's harvest — v1 sources are method names (verb
/// phrase + object noun phrase), parameter names, class names (role suffix
/// stripped), and exception type names (<c>Exception</c>/<c>Error</c>
/// stripped). Observations are aggregated and deterministically ordered; the
/// harvester makes no merge decisions. Trace nodes carry only simple class
/// names, so the namespace used for context resolution comes from an injected
/// resolver function (the suite hook supplies a real one; tests supply a
/// map). Non-identifier names on synthetic nodes are skipped — harvesting is
/// best-effort by design.
/// </remarks>
public sealed class GlossaryHarvester
{
    private readonly ContextResolver contextResolver;
    private readonly Func<string, string?> namespaceOf;

    /// <param name="contextResolver">Resolver from namespace to bounded context; must not be null.</param>
    /// <param name="namespaceOf">
    /// Maps a simple class name to its namespace (empty or null when unknown,
    /// which resolves to <c>_unassigned</c>); must not be null.
    /// </param>
    /// <exception cref="ArgumentNullException">Any argument is null.</exception>
    public GlossaryHarvester(
        ContextResolver contextResolver, Func<string, string?> namespaceOf)
    {
        this.contextResolver = contextResolver
            ?? throw new ArgumentNullException(nameof(contextResolver));
        this.namespaceOf = namespaceOf
            ?? throw new ArgumentNullException(nameof(namespaceOf));
    }

    /// <summary>Harvests all candidate observations from the given trees.</summary>
    /// <param name="trees">Trace trees of one run; must not be null.</param>
    /// <returns>Aggregated observations sorted by <c>(context, phrase, kind, site)</c>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="trees"/> is null.</exception>
    public HarvestResult Harvest(IReadOnlyList<TraceTree> trees)
    {
        return Collect(trees, includeTemplates: false);
    }

    /// <summary>
    /// Harvests from trees built by scanning compiled assemblies, adding
    /// <see cref="TermKind.Template"/> candidates for <c>[Narrated]</c> /
    /// <c>[OnError]</c> text.
    /// </summary>
    /// <remarks>
    /// Templates are harvested <strong>only</strong> here. In a real trace,
    /// <see cref="MethodSignature.Narration"/> holds the template with
    /// parameter values already interpolated, so harvesting it would write
    /// runtime data into the committed glossary. A statically scanned
    /// signature carries the raw annotation text, which is what a per-locale
    /// template variant must key on.
    /// </remarks>
    /// <param name="trees">
    /// Synthetic trees whose narration fields hold raw annotation text; must
    /// not be null.
    /// </param>
    /// <returns>Aggregated observations sorted by <c>(context, phrase, kind, site)</c>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="trees"/> is null.</exception>
    public HarvestResult HarvestStatic(IReadOnlyList<TraceTree> trees)
    {
        return Collect(trees, includeTemplates: true);
    }

    private HarvestResult Collect(IReadOnlyList<TraceTree> trees, bool includeTemplates)
    {
        if (trees is null)
        {
            throw new ArgumentNullException(nameof(trees));
        }

        var occurrences = new Dictionary<HarvestCandidate, int>();
        foreach (var tree in trees)
        {
            // TraceNode.Children is a type, not a guarantee of acyclicity -
            // bound once per tree, here, so Walk's recursion below can never
            // overflow the stack or loop forever on a hand-built or replayed
            // cycle. Cheap on ordinary input: TreeWalk.Bound returns Roots
            // unchanged once it confirms there is nothing to bound.
            foreach (var root in TreeWalk.Bound(tree.Roots))
            {
                Walk(root, occurrences, includeTemplates);
            }
        }

        return new HarvestResult(SortedCandidates(occurrences));
    }

    private static List<HarvestCandidate> SortedCandidates(
        Dictionary<HarvestCandidate, int> occurrences)
    {
        return occurrences
            .Select(entry => WithOccurrences(entry.Key, entry.Value))
            .OrderBy(c => c.Context, StringComparer.Ordinal)
            .ThenBy(c => c.Phrase, StringComparer.Ordinal)
            .ThenBy(c => c.Kind)
            .ThenBy(c => c.Site, StringComparer.Ordinal)
            .ToList();
    }

    private void Walk(
        TraceNode node, Dictionary<HarvestCandidate, int> occurrences, bool includeTemplates)
    {
        var signature = node.Signature;
        var className = signature.ClassName;
        var context = contextResolver.Resolve(NamespaceOrEmpty(className));
        HarvestClass(context, className, occurrences);
        HarvestMethod(context, className, signature.MethodName, occurrences);
        HarvestParameters(node, context, occurrences);
        HarvestException(node, context, occurrences);
        if (includeTemplates)
        {
            HarvestTemplates(node, context, occurrences);
        }

        foreach (var child in node.Children)
        {
            Walk(child, occurrences, includeTemplates);
        }
    }

    /// <summary>Records raw template text verbatim — normalizing it would destroy its placeholders.</summary>
    private static void HarvestTemplates(
        TraceNode node, string context, Dictionary<HarvestCandidate, int> occurrences)
    {
        var signature = node.Signature;
        var site = $"{signature.ClassName}.{signature.MethodName}";
        ObserveTemplate(occurrences, context, signature.Narration, site);
        ObserveTemplate(occurrences, context, signature.ErrorContext, site);
    }

    private static void ObserveTemplate(
        Dictionary<HarvestCandidate, int> occurrences,
        string context, string? template, string site)
    {
        if (string.IsNullOrWhiteSpace(template))
        {
            return;
        }

        Observe(
            occurrences,
            context,
            new TermCandidate(template!, TermKind.Template),
            site,
            template!);
    }

    private string NamespaceOrEmpty(string className)
    {
        return namespaceOf(className) ?? "";
    }

    private static void HarvestClass(
        string context, string className, Dictionary<HarvestCandidate, int> occurrences)
    {
        if (!IsIdentifier(className))
        {
            return;
        }

        var candidate = TermNormalizer.ClassCandidate(className);
        if (candidate is not null)
        {
            Observe(occurrences, context, candidate, className, className);
        }
    }

    private static void HarvestMethod(
        string context, string className, string methodName,
        Dictionary<HarvestCandidate, int> occurrences)
    {
        if (!IsIdentifier(methodName))
        {
            return;
        }

        var site = $"{className}.{methodName}";
        foreach (var candidate in TermNormalizer.MethodCandidates(methodName))
        {
            Observe(occurrences, context, candidate, site, methodName);
        }
    }

    private static void HarvestParameters(
        TraceNode node, string context, Dictionary<HarvestCandidate, int> occurrences)
    {
        var signature = node.Signature;
        var site = $"{signature.ClassName}.{signature.MethodName}";
        var harvestable = signature.Parameters
            .Where(parameter => IsIdentifier(parameter.Name))
            .Select(parameter =>
                (parameter.Name, Candidate: TermNormalizer.ParameterCandidate(parameter.Name)))
            .Where(observed => observed.Candidate is not null);
        foreach (var (name, candidate) in harvestable)
        {
            Observe(occurrences, context, candidate!, site, name);
        }
    }

    private static void HarvestException(
        TraceNode node, string context, Dictionary<HarvestCandidate, int> occurrences)
    {
        var typeName = ExceptionTypeName(node);
        if (typeName is null || !IsIdentifier(typeName))
        {
            return;
        }

        var candidate = TermNormalizer.ExceptionCandidate(typeName);
        if (candidate is not null)
        {
            var site = $"{node.Signature.ClassName}.{node.Signature.MethodName}";
            Observe(occurrences, context, candidate, site, typeName);
        }
    }

    private static string? ExceptionTypeName(TraceNode node)
    {
        return node.Outcome is Threw threw ? threw.Error?.GetType().Name : null;
    }

    private static void Observe(
        Dictionary<HarvestCandidate, int> occurrences,
        string context, TermCandidate candidate, string site, string identifier)
    {
        var key = new HarvestCandidate(
            context, candidate.Phrase, candidate.Kind, site, identifier, 1);
        occurrences[key] = occurrences.TryGetValue(key, out var count) ? count + 1 : 1;
    }

    private static HarvestCandidate WithOccurrences(HarvestCandidate key, int occurrences)
    {
        return new HarvestCandidate(
            key.Context, key.Phrase, key.Kind, key.Site, key.Identifier, occurrences);
    }

    /// <summary>
    /// Best-effort filter: synthetic node names like <c>&lt;launcher&gt;</c> are not harvestable,
    /// and neither is a name built entirely from separators.
    /// </summary>
    /// <remarks>
    /// "Is a legal identifier" is not the same question as "has a word in it" — <c>__</c> and
    /// <c>$$</c> both answer yes to the first and no to the second, and bytecode/IL is full of
    /// names shaped like that. <see cref="TermNormalizer.MethodCandidates"/> throws on a name with
    /// no readable word (declared, not a crash — but this filter exists specifically so the harvest
    /// never reaches that guard for a name it was always going to skip anyway).
    /// </remarks>
    private static bool IsIdentifier(string name)
    {
        if (string.IsNullOrEmpty(name)
            || (!char.IsLetter(name[0]) && name[0] != '_'))
        {
            return false;
        }

        return name.All(c => char.IsLetterOrDigit(c) || c == '_')
            && name.Any(char.IsLetterOrDigit);
    }
}
