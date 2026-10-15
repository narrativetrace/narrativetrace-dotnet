// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.SkillsEvals;
using Xunit;

namespace NarrativeTrace.SkillsEvals.Tests;

/// <summary>
/// A case may declare the registry that delivered its pages via <c>case.json</c>'s <c>registry</c>
/// field, out of the closed <see cref="RegistryPreStep"/> vocabulary. A case without one is an
/// ordinary case the harness scaffolds; an unknown value is an error at the parse site, never a
/// silently skipped pre-step.
/// </summary>
public sealed class CaseRegistryTests : IDisposable
{
    private readonly string _caseDir = Path.Combine(Path.GetTempPath(), "nt-case-" + Guid.NewGuid().ToString("N"));

    public CaseRegistryTests() => Directory.CreateDirectory(_caseDir);

    public void Dispose() => Directory.Delete(_caseDir, recursive: true);

    private void WriteCaseJson(string body) => File.WriteAllText(Path.Combine(_caseDir, "case.json"), body);

    [Fact]
    public void The_fresh_session_case_declares_the_installer_delivery_and_its_prompt_variant_does_not()
    {
        var cases = Path.Combine(NarrativeTrace.Skills.Tests.TestPaths.RepoRoot(), "skills-evals", "add-narrative-tracing");

        Assert.Equal(RegistryPreStep.NarrativetraceInit, CaseRegistry.RegistryFor(Path.Combine(cases, "fresh-session-sees-the-skills")));
        Assert.Null(CaseRegistry.RegistryFor(Path.Combine(cases, "fresh-session-after-the-prompt")));
    }

    [Theory]
    [InlineData("npx-skills", RegistryPreStep.NpxSkills)]
    [InlineData("claude-marketplace", RegistryPreStep.ClaudeMarketplace)]
    public void A_declared_registry_parses_to_its_delivery(string id, RegistryPreStep expected)
    {
        WriteCaseJson($"{{ \"fixture\": \"skills-evals/fixtures/empty-project\", \"registry\": \"{id}\" }}");

        Assert.Equal(expected, CaseRegistry.RegistryFor(_caseDir));
    }

    [Fact]
    public void A_case_with_no_registry_field_is_an_ordinary_case()
    {
        WriteCaseJson("{ \"fixture\": \"skills-evals/fixtures/empty-project\" }");

        Assert.Null(CaseRegistry.RegistryFor(_caseDir));
    }

    [Fact]
    public void A_case_with_no_manifest_at_all_is_an_ordinary_case()
    {
        Assert.Null(CaseRegistry.RegistryFor(_caseDir));
    }

    [Fact]
    public void An_unknown_registry_is_an_error_not_a_silent_skip()
    {
        WriteCaseJson("{ \"registry\": \"gemini-skills\" }");

        Assert.Throws<InvalidOperationException>(() => CaseRegistry.RegistryFor(_caseDir));
    }
}
