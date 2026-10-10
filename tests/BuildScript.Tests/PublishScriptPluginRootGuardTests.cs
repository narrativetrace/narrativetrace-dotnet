// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Diagnostics;

using Xunit;

namespace Build.Tests;

/// <summary>
/// The plugin-root guard in <c>scripts/publish-public.sh</c>: when the staged snapshot carries
/// <c>.claude-plugin/marketplace.json</c>, the marketplace's one plugin is rooted at
/// <c>./.claude</c>, so every path that survives under <c>.claude/</c> other than <c>skills/</c>
/// would ship as plugin content — a <c>commands/</c>, <c>agents/</c> or <c>hooks/</c> directory,
/// or a private file that slipped the strip. The guard fails the other way too: a plugin root
/// the strip removed whole would publish a marketplace entry pointing at a directory that does
/// not exist. This port strips <c>.claude</c> FILE BY FILE (not whole-directory-then-restore, see
/// <c>.publishignore</c>'s own remarks), so nothing else currently enforces this invariant — a
/// new vendor directory under <c>.claude/</c> would ship as plugin content by default, not by
/// accident, which is exactly why this guard matters most in this port.
/// </summary>
/// <remarks>
/// Exercised against the real, full <c>scripts/publish-public.sh --dry-run</c> — same harness as
/// <see cref="PublishScriptLicenseTests"/> and <see cref="PublishScriptBinaryAssetTests"/> — never
/// an extracted snippet, so these tests cannot silently drift from what actually gates a publish.
/// </remarks>
public sealed class PublishScriptPluginRootGuardTests : IDisposable
{
    private readonly string _repo = Path.Combine(
        Path.GetTempPath(), "nt-publish-pluginroot-" + Guid.NewGuid().ToString("N"));

    [SkippableFact]
    public void Publish_succeeds_when_the_staged_plugin_root_holds_only_skills()
    {
        if (!BashAvailable) return;
        RepositoryPath.SkipUnlessPresent("scripts/publish-public.sh");
        // A fresh, minimal fixture repo has no accumulated trace-gate allowlist, so this fixture
        // carries its own narrow .publishallow for the paths it stages — the point under test is
        // the plugin-root guard's own verdict, not this repo's real (much larger) allowlist.
        GivenRepositoryWith(
            ("Sample.cs", "class Sample { }\n"),
            (".claude-plugin/marketplace.json", "{}"),
            (".claude/skills/narrativetrace-doctor/SKILL.md", "page"),
            (".publishallow", "^\\./\\.claude-plugin$\n^\\./\\.claude-plugin/marketplace\\.json$\n"
                + "^\\./\\.claude$\n^\\./\\.claude/skills$\n"
                + "^\\./\\.claude/skills/narrativetrace-doctor$\n"
                + "^\\./\\.claude/skills/narrativetrace-doctor/SKILL\\.md$\n"));

        var (exitCode, output) = WhenPublishingDryRun();

        Assert.Equal(0, exitCode);
        Assert.Contains("the plugin root holds only skills/", output, StringComparison.Ordinal);
    }

    [SkippableFact]
    public void Publish_aborts_naming_a_sibling_directory_that_would_ship_as_plugin_content()
    {
        if (!BashAvailable) return;
        RepositoryPath.SkipUnlessPresent("scripts/publish-public.sh");
        GivenRepositoryWith(
            ("Sample.cs", "class Sample { }\n"),
            (".claude-plugin/marketplace.json", "{}"),
            (".claude/skills/narrativetrace-doctor/SKILL.md", "page"),
            (".claude/commands/stray.md", "a vendor directory that slipped the strip"));

        var (exitCode, output) = WhenPublishingDryRun();

        Assert.NotEqual(0, exitCode);
        Assert.Contains("holds more than skills/", output, StringComparison.Ordinal);
        Assert.Contains(".claude/commands", output, StringComparison.Ordinal);
    }

    [SkippableFact]
    public void Publish_aborts_naming_a_stray_file_next_to_skills()
    {
        if (!BashAvailable) return;
        RepositoryPath.SkipUnlessPresent("scripts/publish-public.sh");
        GivenRepositoryWith(
            ("Sample.cs", "class Sample { }\n"),
            (".claude-plugin/marketplace.json", "{}"),
            (".claude/skills/narrativetrace-doctor/SKILL.md", "page"),
            (".claude/settings.local.json", "{\"a\":1}"));

        var (exitCode, output) = WhenPublishingDryRun();

        Assert.NotEqual(0, exitCode);
        Assert.Contains(".claude/settings.local.json", output, StringComparison.Ordinal);
    }

    [SkippableFact]
    public void Publish_aborts_when_the_plugin_root_is_missing_entirely()
    {
        if (!BashAvailable) return;
        RepositoryPath.SkipUnlessPresent("scripts/publish-public.sh");
        GivenRepositoryWith(
            ("Sample.cs", "class Sample { }\n"),
            (".claude-plugin/marketplace.json", "{}"));

        var (exitCode, output) = WhenPublishingDryRun();

        Assert.NotEqual(0, exitCode);
        Assert.Contains("plugin root", output, StringComparison.Ordinal);
        Assert.Contains("missing", output, StringComparison.Ordinal);
    }

    [SkippableFact]
    public void Publish_skips_the_guard_when_no_marketplace_file_is_staged()
    {
        if (!BashAvailable) return;
        RepositoryPath.SkipUnlessPresent("scripts/publish-public.sh");
        GivenRepositoryWith(("Sample.cs", "class Sample { }\n"));

        var (exitCode, output) = WhenPublishingDryRun();

        Assert.Equal(0, exitCode);
        Assert.Contains("skipped (no .claude-plugin/marketplace.json staged)", output, StringComparison.Ordinal);
    }

    /// <summary>
    /// This port's own <c>.publishignore</c> strip removes <c>.claude/settings.local.json</c> and
    /// <c>.claude/projects</c> by exact, literal path — never a glob — so a sibling path with a
    /// longer name (<c>.claude-plugin</c>) can never be swept up by accident. Proven here by
    /// applying the real patterns from this repository's own <c>.publishignore</c> (never a
    /// hand-copied duplicate) to a fixture carrying both.
    /// </summary>
    [SkippableFact]
    public void The_real_publishignore_patterns_for_claude_leave_the_marketplace_directory_alone()
    {
        if (!BashAvailable) return;
        RepositoryPath.SkipUnlessPresent(".publishignore");
        var stage = Path.Combine(_repo, "stage");
        Directory.CreateDirectory(Path.Combine(stage, ".claude-plugin"));
        File.WriteAllText(Path.Combine(stage, ".claude-plugin", "marketplace.json"), "{}");
        Directory.CreateDirectory(Path.Combine(stage, ".claude", "skills"));
        File.WriteAllText(Path.Combine(stage, ".claude", "settings.local.json"), "{}");
        Directory.CreateDirectory(Path.Combine(stage, ".claude", "projects"));
        File.WriteAllText(Path.Combine(stage, ".claude", "projects", "x.json"), "{}");

        var publishignore = Path.Combine(RepositoryPath.Root(), ".publishignore");
        foreach (var pattern in File.ReadAllLines(publishignore))
        {
            if (pattern.Length == 0 || pattern.StartsWith('#'))
            {
                continue;
            }

            Run("bash", "-c", $"cd \"{stage}\" && rm -rf {pattern} 2>/dev/null || true");
        }

        Assert.True(File.Exists(Path.Combine(stage, ".claude-plugin", "marketplace.json")));
        Assert.False(File.Exists(Path.Combine(stage, ".claude", "settings.local.json")));
        Assert.False(Directory.Exists(Path.Combine(stage, ".claude", "projects")));
    }

    public void Dispose()
    {
        if (Directory.Exists(_repo))
            Directory.Delete(_repo, recursive: true);
    }

    private static bool BashAvailable => !OperatingSystem.IsWindows();

    /// <summary>
    /// Builds a throwaway git repository holding the real publish script plus every
    /// <paramref name="files"/> entry (relative path, content) — the same boilerplate
    /// <see cref="PublishScriptLicenseTests"/> builds for a single file, generalized to several.
    /// </summary>
    private void GivenRepositoryWith(params (string Path, string Content)[] files)
    {
        Directory.CreateDirectory(Path.Combine(_repo, "scripts"));
        File.Copy(
            Path.Combine(RepositoryPath.Root(), "scripts", "publish-public.sh"),
            Path.Combine(_repo, "scripts", "publish-public.sh"));
        // .publishallow never ships either (the real .publishignore strips it under "This
        // machinery + its docs") — a fixture that stages one but forgets to strip it would trip
        // the trace gate on its own allowlist text, which is the mistake this comment heads off.
        File.WriteAllText(Path.Combine(_repo, ".publishignore"), "scripts/publish-public.sh\n.publishallow\n");
        File.WriteAllText(
            Path.Combine(_repo, "LICENSE"),
            "Business Source License 1.1\n\nLicensed Work: fixture version {{VERSION}}.\n"
                + "Change Date: ({{CHANGE_DATE}} for this version)\n");
        File.WriteAllText(
            Path.Combine(_repo, "Directory.Build.props"),
            "<Project><PropertyGroup><VersionPrefix>0.0.1</VersionPrefix></PropertyGroup></Project>\n");
        foreach (var (path, content) in files)
        {
            var full = Path.Combine(_repo, path);
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, content);
        }

        Git("init", "-q", "-b", "main");
        Git("add", "-A");
        Git("-c", "user.email=fixture@example.invalid", "-c", "user.name=Fixture",
            "commit", "-q", "--no-verify", "-m", "fixture");
    }

    private (int ExitCode, string Output) WhenPublishingDryRun() =>
        Run("bash", "scripts/publish-public.sh", "--dry-run");

    private void Git(params string[] arguments)
    {
        var (exitCode, output) = Run("git", arguments);
        if (exitCode != 0)
            throw new InvalidOperationException($"git {string.Join(' ', arguments)} failed: {output}");
    }

    private (int ExitCode, string Output) Run(string fileName, params string[] arguments)
    {
        var info = new ProcessStartInfo(fileName)
        {
            WorkingDirectory = _repo,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        info.Environment["PUBLISH_SKIP_LEGAL_PREFLIGHT"] = "1";
        foreach (var argument in arguments)
            info.ArgumentList.Add(argument);

        using var process = Process.Start(info)
            ?? throw new InvalidOperationException($"could not start {fileName}");
        var output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
        process.WaitForExit();
        return (process.ExitCode, output);
    }
}
