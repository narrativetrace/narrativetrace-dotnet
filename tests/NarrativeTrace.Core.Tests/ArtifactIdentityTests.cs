// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using Xunit;

namespace NarrativeTrace.Core.Tests;

public sealed class ArtifactIdentityTests
{
    [Fact]
    public void OfMethod_is_not_an_invocation()
    {
        var identity = ArtifactIdentity.OfMethod("OrderTests", "PlacesOrder");

        Assert.False(identity.IsInvocation);
        Assert.Equal(0, identity.InvocationIndex);
        Assert.Equal(string.Empty, identity.InvocationLabel);
    }

    [Fact]
    public void OfMethod_file_slug_is_unchanged_from_before_per_invocation_identity_existed()
    {
        var identity = ArtifactIdentity.OfMethod("OrderTests", "PlacesOrder");

        Assert.Equal("places_order", identity.FileSlug());
    }

    [Fact]
    public void OfInvocation_appends_the_zero_padded_index_and_the_label_slug()
    {
        var identity = ArtifactIdentity.OfInvocation(
            "EquipmentTests", "equipmentCanBeFound", 1, "find KAYAK");

        Assert.True(identity.IsInvocation);
        Assert.Equal("equipment_can_be_found-001-find_kayak", identity.FileSlug());
    }

    [Fact]
    public void OfInvocation_drops_a_label_that_slugs_to_nothing()
    {
        var identity = ArtifactIdentity.OfInvocation("EquipmentTests", "equipmentCanBeFound", 2, "---");

        Assert.Equal("equipment_can_be_found-002", identity.FileSlug());
    }

    [Fact]
    public void OfInvocation_rejects_a_zero_or_negative_index()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => ArtifactIdentity.OfInvocation("EquipmentTests", "equipmentCanBeFound", 0, "kayak"));
    }

    [Fact]
    public void Negative_invocation_index_is_rejected_by_the_constructor()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new ArtifactIdentity("EquipmentTests", "equipmentCanBeFound", -1, "kayak"));
    }

    [Fact]
    public void Null_test_class_or_method_name_is_rejected()
    {
        Assert.Throws<ArgumentNullException>(() => new ArtifactIdentity(null!, "m", 0, ""));
        Assert.Throws<ArgumentNullException>(() => new ArtifactIdentity("C", null!, 0, ""));
    }

    [Fact]
    public void Null_invocation_label_normalizes_to_empty()
    {
        var identity = new ArtifactIdentity("C", "m", 1, null);

        Assert.Equal(string.Empty, identity.InvocationLabel);
    }

    /// <summary>
    /// The header rule: an invocation's structural title is
    /// "&lt;humanized method&gt; #&lt;index&gt;", never the display name — a
    /// parameterized test's display-name template can interpolate arguments
    /// into it, which must never reach the value-free artifact.
    /// </summary>
    [Fact]
    public void Invocation_structural_scenario_is_the_humanized_method_plus_index_never_the_display_name()
    {
        var identity = ArtifactIdentity.OfInvocation(
            "EquipmentTests", "equipmentCanBeFound", 2, "find TENT");

        Assert.Equal("Equipment can be found #2", identity.StructuralScenario("find TENT"));
    }

    [Fact]
    public void Method_run_once_keeps_its_display_name_so_no_baseline_moves()
    {
        var identity = ArtifactIdentity.OfMethod("OrderTests", "PlacesOrder");

        Assert.Equal("places an order", identity.StructuralScenario("places an order"));
    }

    /// <summary>
    /// When the caller passed the method name as its own display name (no
    /// display name of its own — the JUnit-4-rule-equivalent case), the
    /// structural title falls back to the humanized bare method name.
    /// </summary>
    [Fact]
    public void Method_run_once_with_no_display_name_of_its_own_humanizes_the_bare_method_name()
    {
        var identity = ArtifactIdentity.OfMethod("EquipmentTests", "equipmentCanBeFound");

        Assert.Equal("Equipment can be found", identity.StructuralScenario("equipmentCanBeFound"));
        Assert.Equal("Equipment can be found", identity.StructuralScenario(null));
    }

    /// <summary>
    /// A runner-appended bracket label on a bare method name is the runner's,
    /// not the developer's — a .NET method name can never contain a bracket.
    /// </summary>
    [Fact]
    public void Runner_appended_bracket_label_is_stripped_from_the_structural_title()
    {
        var identity = ArtifactIdentity.OfMethod("EquipmentTests", "EquipmentCanBeFound[KAYAK]");

        Assert.Equal("Equipment can be found", identity.StructuralScenario("EquipmentCanBeFound[KAYAK]"));
    }

    [Fact]
    public void Two_invocations_of_one_method_always_produce_different_file_slugs()
    {
        var first = ArtifactIdentity.OfInvocation("EquipmentTests", "equipmentCanBeFound", 1, "find/TENT");
        var second = ArtifactIdentity.OfInvocation("EquipmentTests", "equipmentCanBeFound", 2, "find TENT");

        Assert.NotEqual(first.FileSlug(), second.FileSlug());
    }

    [Fact]
    public void Equality_is_structural_like_any_record()
    {
        var a = ArtifactIdentity.OfInvocation("C", "m", 1, "label");
        var b = ArtifactIdentity.OfInvocation("C", "m", 1, "label");

        Assert.Equal(a, b);
    }
}
