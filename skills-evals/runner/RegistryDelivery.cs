// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.SkillsEvals;

/// <summary>
/// How the case a runner is driving got its skill pages — by a registry, or by the harness itself.
/// One value answers all three questions that follow from it: whether the harness still copies the
/// rendered pages in, what runs before the agent starts, and which configuration directory every
/// command of the trial uses.
///
/// <para>Both fields or neither: a step without a work directory would run a vendor tool against
/// the ambient configuration, and a work directory without a step would isolate a trial that
/// installs nothing. One instance per case, built once by the runner.</para>
/// </summary>
public sealed class RegistryDelivery
{
    private static readonly RegistryDelivery NoDelivery = new(null, null);

    private readonly RegistryPreStep? _preStep;
    private readonly string? _workDir;

    private RegistryDelivery(RegistryPreStep? preStep, string? workDir)
    {
        if (preStep.HasValue != (workDir is not null))
        {
            throw new ArgumentException(
                "a RegistryDelivery carries a pre-step and the work directory it runs in, or neither");
        }

        _preStep = preStep;
        _workDir = workDir;
    }

    /// <summary>The ordinary case: the harness copies the rendered pages in and runs nothing before it.</summary>
    public static RegistryDelivery None() => NoDelivery;

    /// <summary>A case whose pages <paramref name="preStep"/> delivers, with <paramref name="workDir"/> to itself.</summary>
    public static RegistryDelivery Through(RegistryPreStep preStep, string workDir)
    {
        ArgumentNullException.ThrowIfNull(workDir);
        return new RegistryDelivery(preStep, workDir);
    }

    /// <summary>
    /// Whether a registry puts this case's pages in place. When it does, the harness copies NONE of
    /// its own rendered pages in: what such a case measures is the state the registry left behind,
    /// and a page the harness wrote over it would answer the case's own question for it.
    /// </summary>
    public bool DeliversTheSkills() => _preStep.HasValue;

    /// <summary>The tree the registry tool reads, outside the graded project.</summary>
    public string StagedSnapshot()
    {
        if (!DeliversTheSkills())
        {
            throw new InvalidOperationException("a delivery that delivers nothing stages nothing");
        }

        return Path.Combine(_workDir!, "staged");
    }

    /// <summary>Everything that runs before the agent starts, in order; empty for the ordinary case.</summary>
    public IReadOnlyList<IReadOnlyList<string>> Commands(string repoRoot) =>
        DeliversTheSkills() ? _preStep!.Value.Commands(repoRoot, StagedSnapshot()) : [];

    /// <summary>The environment every command of this trial runs with; empty for the ordinary case.</summary>
    public IReadOnlyDictionary<string, string> Environment() =>
        DeliversTheSkills()
            ? IsolatedAgentConfig.Env(_workDir!)
            : new Dictionary<string, string>(StringComparer.Ordinal);
}
