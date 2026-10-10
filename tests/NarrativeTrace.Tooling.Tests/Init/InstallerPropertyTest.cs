// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Text;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using NarrativeTrace.Tooling.Init;
using Xunit;
using Action = NarrativeTrace.Tooling.Init.Action;

namespace NarrativeTrace.Tooling.Tests.Init;

/// <summary>A generated context file: markers, fences, import lines, a BOM, no final newline.</summary>
public sealed record ContextFile(string Text);

/// <summary>The same minus anything of ours: the shape the round trip is defined for.</summary>
public sealed record UntouchedContextFile(string Text);

/// <summary>FsCheck generators for the two context-file shapes, mirroring the Java jqwik providers.</summary>
public static class ContextFileArbitraries
{
    private static readonly string[] Anything =
    [
        "# Title",
        "",
        "some prose about the project",
        "```",
        "@AGENTS.md",
        "@AGENTS.md   ",
        "  <!-- narrativetrace:start -->",
        "<!-- narrativetrace:start NarrativeTrace.Skills/0.0.1 -->",
        "<!-- narrativetrace:end -->",
        "<!-- narrativetrace:created -->",
        "<!-- narrativetrace:skills:start -->",
    ];

    private static readonly string[] Untouched =
    [
        "# Title",
        "",
        "some prose about the project",
        "```",
        "  indented",
        "<!-- an unrelated comment -->",
        "@SOMETHING.md",
        "tail",
    ];

    /// <summary>Context files as they come.</summary>
    public static Arbitrary<ContextFile> ContextFile() =>
        Assemble(Anything).Select(text => new ContextFile(text)).ToArbitrary();

    /// <summary>Context files with nothing of ours in them.</summary>
    public static Arbitrary<UntouchedContextFile> UntouchedContextFile() =>
        Assemble(Untouched).Select(text => new UntouchedContextFile(text)).ToArbitrary();

    private static Gen<string> Assemble(string[] palette) =>
        from count in Gen.Choose(0, 10)
        from lines in Gen.ListOf(Gen.Elements<string>(palette), count)
        from eol in Gen.Elements("\n", "\r\n")
        from finalNewline in Gen.Elements(true, false)
        from bom in Gen.Elements(true, false)
        select Text([.. lines], eol, finalNewline, bom);

    private static string Text(IReadOnlyList<string> lines, string eol, bool finalNewline, bool bom)
    {
        if (lines.Count == 0)
        {
            return "";
        }

        var joined = string.Join(eol, lines) + (finalNewline ? eol : "");
        return bom ? "﻿" + joined : joined;
    }
}

/// <summary>
/// The properties an installer has to hold for files nobody wrote by hand: planning after applying
/// finds nothing left to do, installing then uninstalling leaves the project as it was (but for one
/// final newline an append had to add), a plan never touches one path twice, and what was planned is
/// what the files say. Each runs against a real temp directory. Mirrors the Java
/// <c>InstallerPropertyTest</c> (FsCheck here, jqwik there).
/// </summary>
public class InstallerPropertyTest
{
    private static readonly InitOptions Permissive =
        InitOptions.Defaults().WithWriteExisting(true).WithForce(true);

    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    [Property(Arbitrary = [typeof(ContextFileArbitraries)], MaxTest = 200)]
    public void PlanningAfterApplyingFindsNothingLeftToDo(ContextFile agentsMd, ContextFile claudeMd)
    {
        InATemporaryProject(project =>
        {
            Write(Path.Combine(project, "AGENTS.md"), agentsMd.Text);
            Write(Path.Combine(project, "CLAUDE.md"), claudeMd.Text);

            Apply(project, InitPlanner.Plan(Read(project), Carriers.Shared(), Permissive));
            var second = InitPlanner.Plan(Read(project), Carriers.Shared(), Permissive);

            Assert.Empty(Edits(second));
        });
    }

    [Property(Arbitrary = [typeof(ContextFileArbitraries)], MaxTest = 200)]
    public void InstallingThenUninstallingLeavesTheProjectAsItWas(
        UntouchedContextFile agentsMd, UntouchedContextFile claudeMd)
    {
        InATemporaryProject(project =>
        {
            Write(Path.Combine(project, "AGENTS.md"), agentsMd.Text);
            Write(Path.Combine(project, "CLAUDE.md"), claudeMd.Text);
            var before = Projects.SnapshotOf(project);

            Apply(project, InitPlanner.Plan(Read(project), Carriers.Shared(), Permissive));
            Apply(project, UninstallPlanner.Plan(Read(project), Permissive));

            var after = Projects.SnapshotOf(project);
            Assert.Equal(before.Keys.OrderBy(k => k, StringComparer.Ordinal),
                after.Keys.OrderBy(k => k, StringComparer.Ordinal));
            foreach (var (path, content) in after)
            {
                Assert.Contains(content, new[] { before[path], EndingWithNewline(before[path]) });
            }
        });
    }

    [Property(Arbitrary = [typeof(ContextFileArbitraries)], MaxTest = 200)]
    public void APlanNeverTouchesOnePathTwice(ContextFile agentsMd, ContextFile claudeMd)
    {
        InATemporaryProject(project =>
        {
            Write(Path.Combine(project, "AGENTS.md"), agentsMd.Text);
            Write(Path.Combine(project, "CLAUDE.md"), claudeMd.Text);
            Directory.CreateDirectory(Path.Combine(project, ".claude"));

            var install = InitPlanner.Plan(Read(project), Carriers.Shared(), Permissive);
            Apply(project, install);
            var uninstall = UninstallPlanner.Plan(Read(project), Permissive);

            Assert.Equal(Paths(install).Distinct(StringComparer.Ordinal).Count(), Paths(install).Count);
            Assert.Equal(Paths(uninstall).Distinct(StringComparer.Ordinal).Count(), Paths(uninstall).Count);
        });
    }

    [Property(Arbitrary = [typeof(ContextFileArbitraries)], MaxTest = 100)]
    public void WhatWasPlannedIsWhatTheFilesSay(ContextFile agentsMd)
    {
        InATemporaryProject(project =>
        {
            Write(Path.Combine(project, "AGENTS.md"), agentsMd.Text);

            var plan = InitPlanner.Plan(Read(project), Carriers.Shared(), Permissive);
            Apply(project, plan);

            foreach (var edit in Edits(plan))
            {
                var file = Path.Combine(project, edit.Path.Replace('/', Path.DirectorySeparatorChar));
                Assert.Equal(edit.After, File.Exists(file) ? ReadText(file) : "");
            }
        });
    }

    // --- the project under test --------------------------------------------------------------------

    private static void InATemporaryProject(System.Action<string> body) =>
        Projects.InATemporaryOne("nt-install-prop-", body);

    private static ProjectState Read(string project) => ProjectStateReader.Read(project);

    private static void Apply(string project, InitPlan plan) => PlanExecutor.Execute(plan, project);

    private static List<FileEdit> Edits(InitPlan plan) => plan.Actions.OfType<FileEdit>().ToList();

    private static List<string> Paths(InitPlan plan) => plan.Actions.Select(action => action.Path).ToList();

    /// <summary>
    /// The one documented exception to the round trip: a file the installer APPENDED to is left ending
    /// with a newline, because a block has to start on its own line and nothing records that the file
    /// lacked one.
    /// </summary>
    private static string EndingWithNewline(string content) =>
        content.Length == 0 || content.EndsWith('\n') || content.EndsWith('\r')
            ? content
            : content + MarkedBlock.EolOf(content);

    private static void Write(string file, string content) => File.WriteAllText(file, content, Utf8NoBom);

    private static string ReadText(string file) => Encoding.UTF8.GetString(File.ReadAllBytes(file));
}
