// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Text.RegularExpressions;
using NarrativeTrace.SkillsEvals;
using Xunit;

namespace NarrativeTrace.SkillsEvals.Tests;

/// <summary>
/// A case document's <c>**Fixture**</c> line says what the trial starts from; the runner starts from what
/// <see cref="CaseFixture.FixtureFor"/> resolves (a <c>case.json</c>, else the default). B-93: the two must
/// be the same fixture, or the case grades a premise nobody wrote down.
/// </summary>
public sealed partial class CaseDocumentFixtureTests
{
    private static readonly string Root = Skills.Tests.TestPaths.RepoRoot();

    private static readonly string EvalsRoot = Path.Combine(Root, "skills-evals");

    private static IEnumerable<(string CaseDir, string Named)> DocumentedFixtures() =>
        Directory.EnumerateFiles(EvalsRoot, "prompt.md", SearchOption.AllDirectories)
            .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}fixtures{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Select(p => (CaseDir: Path.GetDirectoryName(p)!, Text: File.ReadAllText(p)))
            .Where(c => c.Text.StartsWith("# Case:", StringComparison.Ordinal))
            .Select(c => (c.CaseDir, Named: NamedFixture(c.Text)))
            .Where(c => c.Named is not null)
            .Select(c => (c.CaseDir, c.Named!));

    private static string? NamedFixture(string document)
    {
        var line = document.Split('\n').FirstOrDefault(l => l.StartsWith("**Fixture", StringComparison.Ordinal));
        var path = line is null ? null : BacktickedPath().Match(line);
        return path is { Success: true } ? RepoRelative(path.Groups[1].Value) : null;
    }

    private static string RepoRelative(string named) =>
        named.StartsWith("fixtures/", StringComparison.Ordinal) ? "skills-evals/" + named : named;

    [GeneratedRegex("`([^`\\s]+/[^`\\s]+)`")]
    private static partial Regex BacktickedPath();

    [Fact]
    public void Every_case_document_names_the_fixture_the_runner_resolves_for_it()
    {
        var cases = DocumentedFixtures().ToList();

        Assert.NotEmpty(cases);
        foreach (var (caseDir, named) in cases)
        {
            Assert.True(
                named == CaseFixture.FixtureFor(caseDir),
                $"{Path.GetRelativePath(Root, caseDir)}: its document names {named} but the runner resolves {CaseFixture.FixtureFor(caseDir)}");
        }
    }

    [Fact]
    public void Every_named_fixture_exists()
    {
        foreach (var (caseDir, named) in DocumentedFixtures())
        {
            Assert.True(Directory.Exists(Path.Combine(Root, named)), $"{Path.GetRelativePath(Root, caseDir)}: {named} is not a directory");
        }
    }

    private const string EmptyProject = "skills-evals/fixtures/empty-project";

    private static readonly string[] PresupposedContent =
        ["this project has", "the existing", "behind an interface", "already has", "existing service"];

    private static string TaskPrompt(string caseDir)
    {
        var lines = File.ReadAllText(Path.Combine(caseDir, "prompt.md")).Split('\n')
            .SkipWhile(l => !l.StartsWith("**Task prompt**", StringComparison.Ordinal)).Skip(1)
            .SkipWhile(l => !l.StartsWith('>')).TakeWhile(l => l.StartsWith('>'));
        return string.Join(' ', lines.Select(l => l.TrimStart('>', ' ').TrimEnd('\r')));
    }

    private static IEnumerable<(string CaseDir, string Prompt)> EmptyProjectPrompts() =>
        DocumentedFixtures().Where(c => c.Named == EmptyProject).Select(c => (c.CaseDir, TaskPrompt(c.CaseDir)));

    [Fact]
    public void A_prompt_on_the_empty_project_claims_no_content_the_fixture_lacks()
    {
        var prompts = EmptyProjectPrompts().ToList();

        Assert.NotEmpty(prompts);
        foreach (var (caseDir, prompt) in prompts)
        {
            var claimed = PresupposedContent.Where(p => prompt.Contains(p, StringComparison.OrdinalIgnoreCase)).ToList();
            Assert.True(
                claimed.Count == 0,
                $"{Path.GetRelativePath(Root, caseDir)}: the prompt claims \"{string.Join("\", \"", claimed)}\" but {EmptyProject} is a README only");
        }
    }

    [Fact]
    public void A_prompt_on_the_empty_project_says_the_project_is_new()
    {
        foreach (var (caseDir, prompt) in EmptyProjectPrompts())
        {
            Assert.True(
                prompt.Contains("brand new", StringComparison.OrdinalIgnoreCase),
                $"{Path.GetRelativePath(Root, caseDir)}: the prompt never says the project is new — \"{prompt}\"");
        }
    }
}
