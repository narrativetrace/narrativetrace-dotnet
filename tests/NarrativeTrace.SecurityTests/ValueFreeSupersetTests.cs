// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using NarrativeTrace.SecurityTests.Corpus;
using NarrativeTrace.Tooling.Feedback;
using Xunit;

namespace NarrativeTrace.SecurityTests;

/// <summary>
/// The cross-module superset, asserted ONE way: every sensitive name and every value shape the
/// runtime's renderer redacts is ALSO refused by the zero-dependency value-free gate. This is the one
/// module that may see both — Core's <see cref="RedactionPolicy"/> and Tooling's
/// <see cref="ValueFreeCheck"/> — so the two implementations cannot drift without a build failure,
/// even though the gate never links the runtime.
/// </summary>
/// <remarks>
/// <para>
/// INTENT: the gate is a strict SUPERSET of the renderer, not its equal. A false positive in the gate
/// is a refusal that names its rule; a false negative is a public issue — the inverse of the
/// renderer's tradeoff, where blanking a user's data on a guess is the worse mistake. So the
/// implication runs renderer ⟹ gate and is asserted in that direction only.
/// </para>
/// </remarks>
public sealed class ValueFreeSupersetTests
{
    public static IEnumerable<object[]> RedactedNames() =>
        HostileCorpus.Redactions().Where(c => c.IsName && c.ExpectsRedaction).Select(c => new object[] { c });

    public static IEnumerable<object[]> RedactedValues() =>
        HostileCorpus.Redactions().Where(c => !c.IsName && !c.IsKind && c.ExpectsRedaction).Select(c => new object[] { c });

    [Theory]
    [MemberData(nameof(RedactedNames))]
    public void A_name_the_renderer_redacts_is_refused_by_the_gate(RedactionCase corpusCase)
    {
        var text = $"\"{corpusCase.Name}\": \"{corpusCase.Secret}\"";

        Assert.True(
            ValueFreeCheck.RulesRefusing(text).Count > 0,
            $"{corpusCase.Id}: the renderer redacts \"{corpusCase.Name}\" but the gate let it through");
    }

    [Theory]
    [MemberData(nameof(RedactedValues))]
    public void A_value_shape_the_renderer_redacts_is_refused_by_the_gate(RedactionCase corpusCase)
    {
        Assert.True(
            ValueFreeCheck.RulesRefusing(corpusCase.Secret).Count > 0,
            $"{corpusCase.Id}: the renderer redacts the value \"{corpusCase.Secret}\" but the gate let it through");
    }

    [Fact]
    public void The_gate_refuses_the_runtime_s_own_redaction_marker()
    {
        // The gate restates "[REDACTED]" rather than linking RedactionPolicy; this is the one-way
        // drift guard that the restated literal still equals the runtime's own marker.
        Assert.Contains(ValueFreeCheck.RulesRefusing(RedactionPolicy.Marker), rule => rule.Id == "vf.marker");
    }

    [Fact]
    public void The_gate_is_strictly_stricter_than_the_renderer()
    {
        // A CPF whose check digits are wrong: the renderer leaves it VISIBLE (no checksum passes), and
        // the gate still refuses it by shape. The one row that pins the superset as PROPER, so a later
        // "simplification" that makes the two equal fails here instead of quietly dropping a rule.
        const string checksumFailingCpf = "52998224726";

        Assert.Contains(checksumFailingCpf, ValueRenderer.Render(checksumFailingCpf), StringComparison.Ordinal);
        Assert.Contains(ValueFreeCheck.RulesRefusing(checksumFailingCpf), rule => rule.Id == "vf.value-shape");
    }
}
