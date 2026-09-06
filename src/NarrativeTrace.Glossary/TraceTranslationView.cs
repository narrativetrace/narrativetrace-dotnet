// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Text;
using NarrativeTrace.Core;

namespace NarrativeTrace.Glossary;

/// <summary>
/// Renders canonical trace entries as a translated, human-readable view for
/// one locale.
/// </summary>
/// <remarks>
/// <para>
/// The derived per-locale trace. The load-bearing rule is re-derivation, not
/// substitution: every line is rebuilt from structural fields plus verbatim
/// values, so no value token (<c>nt.parameters[].value</c>,
/// <c>nt.returnValue</c>, <c>exception.message</c>) is ever altered — the
/// pre-rendered <c>message</c> field is never parsed. Identifiers translate
/// via the glossary within the entry's bounded context (resolved from the
/// captured <c>nt.package</c> field, falling back to the injected namespace
/// resolver for pre-1.2 canonical files); translated identifiers keep the
/// original in parentheses so the canonical log stays greppable. Untranslated
/// phrases render as-is and are collected into a per-state gap registry — the
/// work queue that drives glossary completion.
/// </para>
/// <para>
/// Rendering is per-entry:
/// <see cref="RenderEntry(CanonicalEntry, TraceTranslationView.RenderState)"/>
/// renders exactly one canonical entry against a mutable
/// <see cref="RenderState"/> (created per trace and locale via
/// <see cref="NewRenderState"/>), so a live pipeline subscriber can translate
/// an event stream as it arrives. Depth is computed incrementally from the
/// parent-span chain — a parent's enter always precedes its child's — so an
/// entry whose enter was never seen (a late event after state eviction)
/// renders at depth 0: degraded, never wrong. The batch
/// <see cref="Render"/> is a loop over the same per-entry implementation plus
/// <see cref="RenderGapsFooter"/>.
/// </para>
/// </remarks>
public sealed class TraceTranslationView
{
    private readonly Glossary glossary;
    private readonly Func<string, string?> namespaceOf;
    private readonly GlossaryTranslator translator;
    private readonly ContextResolver resolver;

    /// <param name="glossary">Glossary providing per-context terms and translations; must not be null.</param>
    /// <param name="namespaceOf">
    /// Maps a simple class name to its namespace (null when unknown), the same
    /// function harvesting uses, so context resolution matches the committed
    /// glossary; must not be null.
    /// </param>
    /// <exception cref="ArgumentNullException">Either argument is null.</exception>
    public TraceTranslationView(Glossary glossary, Func<string, string?> namespaceOf)
    {
        this.glossary = glossary ?? throw new ArgumentNullException(nameof(glossary));
        this.namespaceOf = namespaceOf ?? throw new ArgumentNullException(nameof(namespaceOf));
        translator = new GlossaryTranslator(glossary);
        resolver = new ContextResolver(glossary);
    }

    /// <summary>
    /// Creates the mutable rendering state for one trace in one locale: span
    /// depths accumulated incrementally and the glossary-gap registry.
    /// </summary>
    /// <remarks>Not thread-safe — confine one state to one rendering sequence.</remarks>
    /// <param name="locale">Target locale tag (e.g. <c>es</c>); must not be blank.</param>
    /// <returns>Fresh state for <see cref="RenderEntry"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="locale"/> is null or blank.</exception>
    // S2325: an instance factory even though the state needs nothing from the
    // view — the pairing is the contract (a state belongs to the view that
    // created it), and it mirrors java's newRenderState.
#pragma warning disable S2325
    public RenderState NewRenderState(string locale)
#pragma warning restore S2325
    {
        if (string.IsNullOrWhiteSpace(locale))
        {
            throw new ArgumentException("locale must not be blank", nameof(locale));
        }

        return new RenderState(locale);
    }

    /// <summary>
    /// Renders one canonical entry into the state's locale, updating the
    /// state's span depths and gap registry as a side effect.
    /// </summary>
    /// <param name="entry">The canonical entry to render; must not be null.</param>
    /// <param name="state">Per-trace state from <see cref="NewRenderState"/>; must not be null.</param>
    /// <returns>
    /// The rendered lines for this entry, empty when the entry renders nothing
    /// (e.g. a successful exit without a return value).
    /// </returns>
    /// <exception cref="ArgumentNullException">Either argument is null.</exception>
    public string RenderEntry(CanonicalEntry entry, RenderState state)
    {
        if (entry is null)
        {
            throw new ArgumentNullException(nameof(entry));
        }

        if (state is null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        var output = new StringBuilder();
        if (string.Equals(entry.NtEventType, "method_enter", StringComparison.Ordinal))
        {
            RenderEnter(output, entry, state, EnterDepth(entry, state));
        }
        else if (string.Equals(entry.NtEventType, "method_exit", StringComparison.Ordinal))
        {
            RenderExit(output, entry, state, DepthOf(entry, state));
        }

        return output.ToString();
    }

    /// <summary>
    /// Renders the glossary-gaps footer for everything accumulated in the
    /// state's gap registry.
    /// </summary>
    /// <param name="state">Per-trace state from <see cref="NewRenderState"/>; must not be null.</param>
    /// <returns>The footer, or an empty string when no phrase stayed untranslated.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="state"/> is null.</exception>
    // S2325: instance-scoped for the same reason as NewRenderState — footer
    // and entries are one rendering contract, reached through one object.
#pragma warning disable S2325
    public string RenderGapsFooter(RenderState state)
#pragma warning restore S2325
    {
        if (state is null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        if (state.Gaps.Count == 0)
        {
            return string.Empty;
        }

        var output = new StringBuilder("\n---\n").Append(state.Bundle.GapsHeading).Append(":\n");
        foreach (var gap in state.Gaps)
        {
            output.Append("- ").Append(gap).Append('\n');
        }

        return output.ToString();
    }

    /// <summary>
    /// Renders the entries of one trace file into the target locale — a loop
    /// over <see cref="RenderEntry"/> followed by
    /// <see cref="RenderGapsFooter"/>.
    /// </summary>
    /// <param name="entries">Canonical entries in file order; must not be null.</param>
    /// <param name="locale">Target locale tag (e.g. <c>es</c>); must not be blank.</param>
    /// <returns>
    /// The translated view, one line per rendered entry, plus a glossary-gaps
    /// footer whenever any phrase stayed untranslated.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="entries"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="locale"/> is null or blank.</exception>
    public string Render(IReadOnlyList<CanonicalEntry> entries, string locale)
    {
        if (entries is null)
        {
            throw new ArgumentNullException(nameof(entries));
        }

        var state = NewRenderState(locale);
        var output = new StringBuilder();
        foreach (var entry in entries)
        {
            output.Append(RenderEntry(entry, state));
        }

        return output.Append(RenderGapsFooter(state)).ToString();
    }

    /// <summary>
    /// Mutable per-trace, per-locale rendering state: the incremental
    /// span-depth map (fed by enter entries, read by exits) and the sorted
    /// glossary-gap registry the footer renders from.
    /// </summary>
    /// <remarks>
    /// Created via <see cref="NewRenderState"/>; not thread-safe.
    /// </remarks>
    public sealed class RenderState
    {
        internal RenderState(string locale)
        {
            Locale = locale;
            Bundle = ScaffoldingBundle.ForLocale(locale);
        }

        internal string Locale { get; }

        internal ScaffoldingBundle Bundle { get; }

        internal Dictionary<string, int> Depths { get; } =
            new(StringComparer.Ordinal);

        internal SortedSet<string> Gaps { get; } = new(StringComparer.Ordinal);
    }

    /// <summary>
    /// Records and returns this enter's depth from the parent-span chain. A
    /// parent's enter precedes its child's, so the parent depth is already in
    /// the state; an unknown parent (or a late event after eviction) lands at
    /// depth 0.
    /// </summary>
    private static int EnterDepth(CanonicalEntry entry, RenderState state)
    {
        if (entry.SpanId is not null)
        {
            var parentDepth = entry.ParentSpanId is not null
                && state.Depths.TryGetValue(entry.ParentSpanId, out var found)
                    ? found + 1
                    : 0;
            state.Depths[entry.SpanId] = parentDepth;
        }

        return DepthOf(entry, state);
    }

    private static int DepthOf(CanonicalEntry entry, RenderState state)
    {
        return entry.SpanId is not null && state.Depths.TryGetValue(entry.SpanId, out var depth)
            ? depth
            : 0;
    }

    private void RenderEnter(
        StringBuilder output, CanonicalEntry entry, RenderState state, int depth)
    {
        var context = ContextOf(entry);
        Indent(output, depth);
        output.Append(entry.CodeNamespace).Append('.');
        AppendFunction(output, entry.CodeFunction, context, state);
        AppendParameters(output, entry, context, state);
        output.Append('\n');
        AppendNarration(output, entry, context, state, depth);
    }

    /// <summary>
    /// Renders the translated narration line beneath the call when the raw
    /// template has a locale variant; a template without one becomes a
    /// glossary gap and renders nothing — the resolved English narration is
    /// never substituted into.
    /// </summary>
    private void AppendNarration(
        StringBuilder output, CanonicalEntry entry, string context, RenderState state, int depth)
    {
        var template = entry.NtNarrationTemplate;
        if (template is null)
        {
            return;
        }

        var variant = translator.TemplateVariant(template, context, state.Locale);
        if (variant is null)
        {
            state.Gaps.Add(template);
            return;
        }

        Indent(output, depth + 1);
        output.Append(FillPlaceholders(variant, entry)).Append('\n');
    }

    /// <summary>Fills <c>{name}</c> placeholders from the untouched parameter values.</summary>
    private static string FillPlaceholders(string template, CanonicalEntry entry)
    {
        var filled = template;
        foreach (var parameter in entry.NtParameters ?? [])
        {
            filled = filled.Replace("{" + parameter.Name + "}", parameter.RenderedValue);
        }

        return filled;
    }

    private void RenderExit(
        StringBuilder output, CanonicalEntry entry, RenderState state, int depth)
    {
        if (string.Equals(entry.NtOutcome, "failure", StringComparison.Ordinal))
        {
            Indent(output, depth + 1);
            AppendException(output, entry, state);
            output.Append('\n');
        }
        else if (string.Equals(entry.NtOutcome, "success", StringComparison.Ordinal)
            && entry.NtReturnValue is not null)
        {
            Indent(output, depth + 1);
            output.Append("-> ").Append(state.Bundle.Returns).Append(' ')
                .Append(entry.NtReturnValue).Append('\n');
        }
        else if (string.Equals(entry.NtOutcome, "incomplete", StringComparison.Ordinal))
        {
            Indent(output, depth + 1);
            output.Append(".. ").Append(state.Bundle.Incomplete).Append('\n');
        }
    }

    /// <summary>
    /// <c>!! &lt;translated phrase&gt; [OriginalType]: &lt;verbatim
    /// message&gt;</c>; an uncovered exception phrase falls back to the classic
    /// <c>!! OriginalType: message</c> line.
    /// </summary>
    private void AppendException(StringBuilder output, CanonicalEntry entry, RenderState state)
    {
        output.Append("!! ").Append(ExceptionPhrase(entry, state));
        if (entry.ExceptionMessage is not null)
        {
            output.Append(": ").Append(entry.ExceptionMessage);
        }
    }

    /// <summary>
    /// The glossed exception phrase with the original type beside it, or the
    /// bare type when the glossary does not cover it. A type whose name
    /// carries nothing but the suffix (<c>Exception</c>) yields no candidate
    /// at all and renders as itself.
    /// </summary>
    private string ExceptionPhrase(CanonicalEntry entry, RenderState state)
    {
        if (entry.ExceptionType is null)
        {
            return string.Empty;
        }

        var candidate = TermNormalizer.ExceptionCandidate(entry.ExceptionType);
        if (candidate is null)
        {
            return entry.ExceptionType;
        }

        var result = TranslateOrGap(candidate.Phrase, ContextOf(entry), state);
        return result.Complete
            ? $"{result.Text} [{entry.ExceptionType}]"
            : entry.ExceptionType;
    }

    private static void Indent(StringBuilder output, int depth)
    {
        output.Append(' ', depth * 2);
    }

    /// <summary>
    /// The function name, glossed when the glossary covers it.
    /// </summary>
    /// <remarks>
    /// <c>code.function</c> is a schema-required string with no shape
    /// constraint, so a valid canonical entry may carry one that normalizes to
    /// nothing (blank, or all punctuation). A renderer on the live pipeline
    /// path must be total over its own input format, so such a name renders
    /// verbatim — the same answer as an uncovered name — rather than throwing
    /// out of a subscriber and into the fan-out.
    /// </remarks>
    private void AppendFunction(
        StringBuilder output, string? function, string context, RenderState state)
    {
        if (string.IsNullOrEmpty(function))
        {
            return;
        }

        var phrase = TermNormalizer.PhraseOrNull(function!);
        if (phrase is null)
        {
            output.Append(function);
            return;
        }

        var result = TranslateOrGap(phrase, context, state);
        if (result.Complete)
        {
            output.Append(result.Text).Append(" (").Append(function).Append(") ");
        }
        else
        {
            output.Append(function);
        }
    }

    private void AppendParameters(
        StringBuilder output, CanonicalEntry entry, string context, RenderState state)
    {
        output.Append('(');
        var parameters = entry.NtParameters ?? [];
        for (var i = 0; i < parameters.Count; i++)
        {
            if (i > 0)
            {
                output.Append(", ");
            }

            output.Append(ParameterName(parameters[i].Name, context, state))
                .Append(": ").Append(parameters[i].RenderedValue);
        }

        output.Append(')');
    }

    private string ParameterName(string name, string context, RenderState state)
    {
        var candidate = TermNormalizer.ParameterCandidate(name);
        if (candidate is null)
        {
            return name;
        }

        var result = TranslateOrGap(candidate.Phrase, context, state);
        return result.Complete ? result.Text : name;
    }

    /// <summary>
    /// Resolves the bounded context for an entry.
    /// </summary>
    /// <remarks>
    /// The captured <c>nt.package</c> field (schema 1.2) is the primary
    /// source — identity captured at the site is authoritative; the injected
    /// namespace resolver is the fallback for pre-1.2 canonical files. When
    /// the namespace is unknown or unmatched and the glossary declares exactly
    /// one context, that context applies — a single-context glossary is
    /// unambiguous. Multi-context glossaries stay strict and resolve to
    /// <see cref="ContextResolver.Unassigned"/>.
    /// </remarks>
    private string ContextOf(CanonicalEntry entry)
    {
        var namespaceName = entry.NtPackage ?? NamespaceOrEmpty(entry.CodeNamespace);
        var resolved = resolver.Resolve(namespaceName);
        // No is-unassigned guard: the resolver only returns declared context
        // names or Unassigned, so when exactly one context is declared it
        // either matched (identity) or is the fallback.
        var declared = glossary.Contexts.Keys
            .Where(name => !string.Equals(
                name, ContextResolver.Unassigned, StringComparison.Ordinal))
            .ToList();
        return declared.Count == 1 ? declared[0] : resolved;
    }

    private PhraseTranslation TranslateOrGap(string phrase, string context, RenderState state)
    {
        var result = translator.Translate(phrase, context, state.Locale);
        if (!result.Complete)
        {
            state.Gaps.Add(phrase);
        }

        return result;
    }

    private string NamespaceOrEmpty(string? className)
    {
        return className is null ? string.Empty : namespaceOf(className) ?? string.Empty;
    }
}
