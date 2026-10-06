// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Tooling.Init;

/// <summary>Which half of the install a run covers.</summary>
public enum Scope
{
    /// <summary>The skill directories only.</summary>
    Skills,

    /// <summary>The managed section and the import line only.</summary>
    AgentsMd,

    /// <summary>Both — the default.</summary>
    Both,
}

/// <summary>Whether the vendor flavour is installed beside the open-standard one.</summary>
public enum Vendor
{
    /// <summary>Install it where the project looks like that vendor's.</summary>
    Auto,

    /// <summary>Install it, detected or not.</summary>
    On,

    /// <summary>Never install it.</summary>
    Off,
}

/// <summary>What each <see cref="Scope"/> covers.</summary>
public static class ScopeExtensions
{
    /// <summary>Whether skill directories are part of this scope.</summary>
    public static bool IncludesSkills(this Scope scope) => scope != Scope.AgentsMd;

    /// <summary>Whether the managed section is part of this scope.</summary>
    public static bool IncludesAgentsMd(this Scope scope) => scope != Scope.Skills;
}

/// <summary>
/// What a person asked the installer to do.
/// </summary>
/// <remarks>
/// <para>INTENT: every flag that changes a plan, in one value the planner takes as input — so a plan
/// is reproducible from a snapshot, a carrier and this, and from nothing else.</para>
/// <para>The defaults write nothing they were not asked to: an existing file is left alone without
/// <see cref="WriteExisting"/>, and a skill directory somebody else owns is left alone without
/// <see cref="Force"/>.</para>
/// </remarks>
public sealed record InitOptions
{
    /// <param name="dryRun">Compute and show the plan, write nothing, and exit 0 even on a refusal.</param>
    /// <param name="writeExisting">Permission to touch a context file that exists and carries no markers.</param>
    /// <param name="force">Permission to overwrite a skill directory that is not ours.</param>
    /// <param name="scope">Which half of the install to plan.</param>
    /// <param name="vendorClaude">Whether the vendor flavour is installed as well.</param>
    public InitOptions(bool dryRun, bool writeExisting, bool force, Scope scope, Vendor vendorClaude)
    {
        if (!Enum.IsDefined(scope))
        {
            throw new ArgumentException("an install needs a scope");
        }

        if (!Enum.IsDefined(vendorClaude))
        {
            throw new ArgumentException("an install needs a vendor rule");
        }

        DryRun = dryRun;
        WriteExisting = writeExisting;
        Force = force;
        Scope = scope;
        VendorClaude = vendorClaude;
    }

    /// <summary>Compute and show the plan, write nothing, and exit 0 even on a refusal.</summary>
    public bool DryRun { get; init; }

    /// <summary>Permission to touch a context file that exists and carries no markers.</summary>
    public bool WriteExisting { get; init; }

    /// <summary>Permission to overwrite a skill directory that is not ours.</summary>
    public bool Force { get; init; }

    /// <summary>Which half of the install to plan.</summary>
    public Scope Scope { get; init; }

    /// <summary>Whether the vendor flavour is installed as well.</summary>
    public Vendor VendorClaude { get; init; }

    /// <summary>Write both halves, touch nothing that exists, detect the vendor.</summary>
    public static InitOptions Defaults() => new(false, false, false, Scope.Both, Vendor.Auto);

    /// <summary>This value with <see cref="DryRun"/> set to <paramref name="value"/>.</summary>
    public InitOptions WithDryRun(bool value) =>
        new(value, WriteExisting, Force, Scope, VendorClaude);

    /// <summary>This value with <see cref="WriteExisting"/> set to <paramref name="value"/>.</summary>
    public InitOptions WithWriteExisting(bool value) =>
        new(DryRun, value, Force, Scope, VendorClaude);

    /// <summary>This value with <see cref="Force"/> set to <paramref name="value"/>.</summary>
    public InitOptions WithForce(bool value) =>
        new(DryRun, WriteExisting, value, Scope, VendorClaude);

    /// <summary>This value with <see cref="Scope"/> set to <paramref name="value"/>.</summary>
    public InitOptions WithScope(Scope value) =>
        new(DryRun, WriteExisting, Force, value, VendorClaude);

    /// <summary>This value with <see cref="VendorClaude"/> set to <paramref name="value"/>.</summary>
    public InitOptions WithVendorClaude(Vendor value) =>
        new(DryRun, WriteExisting, Force, Scope, value);
}
