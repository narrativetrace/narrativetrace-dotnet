// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Text;

namespace NarrativeTrace.Core;

/// <summary>
/// Content-addressed deduplication of captured values for one rendering pass.
/// </summary>
/// <remarks>
/// A value whose rendered form repeats across a trace carries information only
/// where it differs; byte-identical repetition is noise that hides the render
/// that changed. This index collects the rendered parameter and return values
/// of a <see cref="TraceTree"/>, decides which of them earn a reference (long
/// enough and emitted more than once), and hands renderers a display form:
/// first emission defines <c>‹label›=full</c>, later emissions are just
/// <c>‹label›</c>. Equality is byte equality of the rendered string.
/// <para>
/// A value that differs is not simply re-rendered in full, though. When two
/// rendered forms belong to the same <em>entity</em> — same structured type
/// name, same value in the identity field that names the label — the later one
/// renders as <c>‹label›′{Amount: 100→92}</c>, a diff against the reference
/// this document already defines. That keeps the artifact self-contained (the
/// baseline is on the page) and makes the one field that moved the thing the
/// reader sees, instead of a second near-identical blob. Anything the diff
/// cannot express — a structural change, an identity-less value — falls back
/// to the full render, unchanged.
/// </para>
/// <para>
/// One instance serves exactly one rendering pass: label definition order
/// follows emission order, so the instance is stateful and must not be shared
/// across renders.
/// </para>
/// </remarks>
internal sealed class ValueReferenceIndex
{
    private const int MinRefLength = 40;
    private const int MaxLabelLength = 24;

    private static readonly string[] IdentityFields =
    [
        "name", "id", "description", "title", "key", "label", "code",
    ];

    private readonly HashSet<string> _referenced;
    private readonly List<string> _referencedByLengthDesc;
    private readonly Dictionary<string, RenderedValue> _structuredByContent;
    private readonly Dictionary<string, string> _deltaKeyByContent;

    private readonly Dictionary<string, string> _groupAnchors =
        new(StringComparer.Ordinal);

    private readonly Dictionary<string, string> _definedLabels = [];
    private readonly Dictionary<string, int> _baseLabelUses = [];
    private int _genericCounter;

    private ValueReferenceIndex(
        HashSet<string> referenced,
        Dictionary<string, RenderedValue> structuredByContent,
        Dictionary<string, string> deltaKeyByContent)
    {
        _referenced = referenced;
        _deltaKeyByContent = deltaKeyByContent;
        _referencedByLengthDesc = referenced
            .OrderByDescending(s => s.Length)
            .ThenBy(s => s, StringComparer.Ordinal)
            .ToList();
        _structuredByContent = structuredByContent;
    }

    /// <summary>
    /// Collects candidate values from the tree; values emitted at least twice
    /// earn a reference.
    /// </summary>
    public static ValueReferenceIndex Build(TraceTree tree)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        var structured =
            new Dictionary<string, RenderedValue>(StringComparer.Ordinal);
        // TraceNode.Children is a type, not a guarantee of acyclicity - bound
        // once, here, so the recursive walk below can never overflow the
        // stack or loop forever on a hand-built or replayed cycle. Cheap on
        // ordinary input: TreeWalk.Bound returns Roots unchanged once it
        // confirms there is nothing to bound.
        var roots = TreeWalk.Bound(tree.Roots);
        for (var i = 0; i < roots.Count; i++)
        {
            CountNode(roots[i], counts, structured);
        }

        var referenced = new HashSet<string>(StringComparer.Ordinal);
        foreach (var pair in counts)
        {
            if (pair.Value + ContainmentCount(pair.Key, counts) >= 2)
            {
                referenced.Add(pair.Key);
            }
        }

        return new ValueReferenceIndex(
            referenced, structured, DeltaKeys(structured));
    }

    /// <summary>
    /// Maps each candidate value to its identity key when that key covers more
    /// than one rendered form — the same entity, captured again with something
    /// changed — <em>and</em> at least one of those forms can actually be
    /// expressed as a delta of another.
    /// </summary>
    /// <remarks>
    /// The second condition is what keeps the fallback silent: a pair whose
    /// difference is structural renders exactly as it did before this feature
    /// existed, with no reference label stamped on a definition nothing ever
    /// refers back to.
    /// </remarks>
    private static Dictionary<string, string> DeltaKeys(
        Dictionary<string, RenderedValue> structured)
    {
        var keyByContent =
            new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in FormsByIdentity(structured)
            .Where(p => AnyPairDiffs(p.Value, structured)))
        {
            foreach (var rendered in pair.Value)
            {
                keyByContent[rendered] = pair.Key;
            }
        }

        return keyByContent;
    }

    /// <summary>
    /// Groups every candidate value's rendered form under its identity key,
    /// skipping values that carry no identity field.
    /// </summary>
    private static Dictionary<string, List<string>> FormsByIdentity(
        Dictionary<string, RenderedValue> structured)
    {
        var formsByKey =
            new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var pair in structured)
        {
            if (IdentityKey(pair.Value) is not { } key)
            {
                continue;
            }

            if (!formsByKey.TryGetValue(key, out var forms))
            {
                forms = [];
                formsByKey[key] = forms;
            }

            forms.Add(pair.Key);
        }

        return formsByKey;
    }

    /// <summary>
    /// Whether any two of one identity's rendered forms differ by scalar fields
    /// alone. Self-pairs cost nothing to include: a value has no delta against
    /// itself.
    /// </summary>
    private static bool AnyPairDiffs(
        List<string> forms, Dictionary<string, RenderedValue> structured)
    {
        var values = forms.Select(f => structured[f]).ToList();
        return values.Any(
            one => values.Any(
                other => ValueDelta.Between(one, other) is not null));
    }

    /// <summary>
    /// Emissions of <paramref name="value"/> nested inside other captured
    /// values, weighted by their own emission counts.
    /// </summary>
    private static int ContainmentCount(
        string value, Dictionary<string, int> counts)
    {
        var total = 0;
        foreach (var pair in counts.Where(
            p => !string.Equals(p.Key, value, StringComparison.Ordinal)))
        {
            total += OccurrencesIn(pair.Key, value) * pair.Value;
        }

        return total;
    }

    private static int OccurrencesIn(string container, string value)
    {
        var occurrences = 0;
        var idx = container.IndexOf(value, StringComparison.Ordinal);
        while (idx >= 0)
        {
            occurrences++;
            idx = container.IndexOf(
                value, idx + value.Length, StringComparison.Ordinal);
        }

        return occurrences;
    }

    private static void CountNode(
        TraceNode node,
        Dictionary<string, int> counts,
        Dictionary<string, RenderedValue> structured)
    {
        var parameters = node.Signature.Parameters;
        for (var i = 0; i < parameters.Count; i++)
        {
            if (!parameters[i].Redacted)
            {
                CountValue(
                    parameters[i].RenderedValue,
                    parameters[i].StructuredValue, counts, structured);
            }
        }

        if (node.Outcome is Returned returned)
        {
            CountValue(
                returned.RenderedValue, returned.StructuredValue,
                counts, structured);
        }

        for (var i = 0; i < node.Children.Count; i++)
        {
            CountNode(node.Children[i], counts, structured);
        }
    }

    private static void CountValue(
        string? rendered,
        RenderedValue? structuredValue,
        Dictionary<string, int> counts,
        Dictionary<string, RenderedValue> structured)
    {
        if (rendered is null || rendered.Length < MinRefLength)
        {
            return;
        }

        counts[rendered] = counts.TryGetValue(rendered, out var count)
            ? count + 1
            : 1;
        if (structuredValue is not null
            && !structured.ContainsKey(rendered))
        {
            structured[rendered] = structuredValue;
        }
    }

    /// <summary>
    /// Returns the emission form of a rendered value: the full text for
    /// unreferenced values, <c>‹label›=full</c> the first time a referenced
    /// value is emitted, <c>‹label›</c> afterwards, and
    /// <c>‹label›′{Field: before→after}</c> for a later emission of the same
    /// entity that changed.
    /// </summary>
    public string? Display(string? rendered)
    {
        if (rendered is null)
        {
            return null;
        }

        if (_referenced.Contains(rendered))
        {
            return ReferenceDisplay(rendered);
        }

        return DeltaDisplay(rendered) ?? ReplaceContained(rendered);
    }

    /// <summary>
    /// Defines or reuses the label of a byte-repeated value. A repeated value
    /// that is itself a changed re-capture of an already-defined reference is
    /// defined AS the delta (<c>‹label·2›=‹label›′{…}</c>) rather than as a
    /// second full blob — it repeats, so it earns its own label, but the change
    /// is still what the reader sees.
    /// </summary>
    private string ReferenceDisplay(string value)
    {
        if (_definedLabels.TryGetValue(value, out var label))
        {
            return label;
        }

        var created = NewLabel(value);
        _definedLabels[value] = created;
        var asDelta = DeltaAgainstAnchor(value);
        RememberAnchor(value);
        return created + "=" + (asDelta ?? ReplaceContained(value));
    }

    /// <summary>
    /// Emission form for a value whose identity was seen in more than one
    /// rendered form: the first such form emitted becomes the in-document
    /// reference (defined in full), and every later, differing form renders as
    /// a diff against it. Returns <see langword="null"/> when the value is not
    /// part of such a pair or the difference cannot be expressed as a scalar
    /// field diff, leaving the caller on the full-render path.
    /// </summary>
    private string? DeltaDisplay(string rendered)
    {
        if (!_deltaKeyByContent.TryGetValue(rendered, out var key))
        {
            return null;
        }

        return _groupAnchors.ContainsKey(key)
            ? DeltaAgainstAnchor(rendered)
            : DefineAnchor(key, rendered);
    }

    private string DefineAnchor(string key, string rendered)
    {
        var label = NewLabel(rendered);
        _definedLabels[rendered] = label;
        _groupAnchors[key] = rendered;
        return label + "=" + ReplaceContained(rendered);
    }

    /// <summary>
    /// Records a newly-labelled value as its identity's reference, if it
    /// belongs to a delta pair.
    /// </summary>
    private void RememberAnchor(string rendered)
    {
        if (_deltaKeyByContent.TryGetValue(rendered, out var key)
            && !_groupAnchors.ContainsKey(key))
        {
            _groupAnchors[key] = rendered;
        }
    }

    /// <summary>
    /// The <c>‹anchor›′{Field: before→after}</c> form of a value whose identity
    /// already has a reference defined in this document, or
    /// <see langword="null"/> when this value belongs to no delta pair, its
    /// identity has no reference yet (this emission is about to become one), or
    /// the difference is not a scalar field diff.
    /// </summary>
    private string? DeltaAgainstAnchor(string rendered)
    {
        if (!_deltaKeyByContent.TryGetValue(rendered, out var key)
            || !_groupAnchors.TryGetValue(key, out var anchor))
        {
            return null;
        }

        var delta = ValueDelta.Between(
            _structuredByContent[anchor], _structuredByContent[rendered]);
        return delta is null ? null : _definedLabels[anchor] + "′" + delta;
    }

    /// <summary>
    /// Replaces every occurrence of a referenced value inside
    /// <paramref name="rendered"/> with its reference, defining it inline
    /// (<c>‹label›=full</c>) on first emission. Longest values are replaced
    /// first so a value nested inside another referenced value resolves inside
    /// that definition.
    /// </summary>
    private string ReplaceContained(string rendered)
    {
        var result = rendered;
        for (var i = 0; i < _referencedByLengthDesc.Count; i++)
        {
            var value = _referencedByLengthDesc[i];
            if (!string.Equals(value, rendered, StringComparison.Ordinal))
            {
                result = ReplaceOccurrences(result, value);
            }
        }

        return result;
    }

    private string ReplaceOccurrences(string text, string value)
    {
        var idx = text.IndexOf(value, StringComparison.Ordinal);
        if (idx < 0)
        {
            return text;
        }

        var sb = new StringBuilder();
        var from = 0;
        while (idx >= 0)
        {
            sb.Append(text, from, idx - from);
            sb.Append(ReferenceDisplay(value));
            from = idx + value.Length;
            idx = text.IndexOf(value, from, StringComparison.Ordinal);
        }

        sb.Append(text, from, text.Length - from);
        return sb.ToString();
    }

    private string NewLabel(string rendered)
    {
        _structuredByContent.TryGetValue(rendered, out var structured);
        var baseLabel = DeriveBaseLabel(structured);
        if (baseLabel is null)
        {
            return $"‹v{++_genericCounter}›";
        }

        var uses = _baseLabelUses.TryGetValue(baseLabel, out var used)
            ? used + 1
            : 1;
        _baseLabelUses[baseLabel] = uses;
        return uses == 1
            ? $"‹{baseLabel}›"
            : $"‹{baseLabel}·{uses}›";
    }

    private static string? DeriveBaseLabel(RenderedValue? structured)
    {
        if (structured is not RenderedValue.ObjectVal obj)
        {
            return null;
        }

        return IdentityFieldValue(obj) ?? obj.TypeName;
    }

    /// <summary>
    /// Picks the first identity-signaling field with a usable plain-string
    /// value. Redacted markers are skipped so a label can never resurrect a
    /// value that redaction removed.
    /// </summary>
    private static string? IdentityFieldValue(RenderedValue.ObjectVal obj)
    {
        return IdentityFieldOf(obj) is { } field
            ? CapLabel(ControlEscape.Sanitize(field.Value))
            : null;
    }

    /// <summary>
    /// The first identity-signaling field with a usable plain-string value,
    /// as its canonical (lower-case, cross-port) name and its text.
    /// </summary>
    private static (string Name, string Value)? IdentityFieldOf(
        RenderedValue.ObjectVal obj)
    {
        for (var i = 0; i < IdentityFields.Length; i++)
        {
            if (FindField(obj, IdentityFields[i])
                    is RenderedValue.StringVal s
                && IsUsableLabelText(s.Value))
            {
                return (IdentityFields[i], s.Value);
            }
        }

        return null;
    }

    /// <summary>
    /// Stable key for "the same entity": the structured type name plus the
    /// uncapped value of the identity field that named it. Two captures sharing
    /// this key are the same thing at two moments, which is what makes a delta
    /// between them meaningful. <see langword="null"/> when the value carries no
    /// identity field — a type name alone would group unrelated instances of the
    /// same class, and a diff between two different expenses is noise, not
    /// signal.
    /// </summary>
    private static string? IdentityKey(RenderedValue? structured)
    {
        if (structured is RenderedValue.ObjectVal obj
            && IdentityFieldOf(obj) is { } field)
        {
            return obj.TypeName + ' ' + field.Name + ' ' + field.Value;
        }

        return null;
    }

    /// <summary>
    /// Matches an identity field name case-insensitively: .NET field and
    /// property names are conventionally PascalCase, while the identity list
    /// (shared with the other language ports) is lower-case.
    /// </summary>
    private static RenderedValue? FindField(
        RenderedValue.ObjectVal obj, string name)
    {
        return obj.Fields
            .Where(p => string.Equals(
                p.Key, name, StringComparison.OrdinalIgnoreCase))
            .Select(p => p.Value)
            .FirstOrDefault();
    }

    private static bool IsUsableLabelText(string? text)
    {
        return !string.IsNullOrWhiteSpace(text)
            && !string.Equals(
                text, RedactionPolicy.Marker, StringComparison.Ordinal);
    }

    private static string CapLabel(string text)
    {
        return text.Length > MaxLabelLength
            ? text[..MaxLabelLength] + "…"
            : text;
    }
}
