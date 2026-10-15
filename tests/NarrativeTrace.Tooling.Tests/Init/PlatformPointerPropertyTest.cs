// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Text;
using FsCheck.Xunit;
using NarrativeTrace.Tooling.Init;
using Xunit;
using Action = NarrativeTrace.Tooling.Init.Action;

namespace NarrativeTrace.Tooling.Tests.Init;

/// <summary>
/// The installer's properties, held for the platform pointers too (the bug CLASS behind Java's two
/// findings — a byte-order mark swallowed by a section edit, a file the round trip did not restore —
/// not just their instances): for any <c>replit.md</c> a person could have written, with the Cursor
/// and Replit markers both present, apply-then-plan finds nothing, install-then-uninstall restores the
/// project, and a plan never names one path twice.
/// </summary>
public class PlatformPointerPropertyTest
{
    private static readonly InitOptions Permissive = InitOptions.Defaults().WithWriteExisting(true).WithForce(true);

    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    [Property(Arbitrary = [typeof(ContextFileArbitraries)], MaxTest = 200)]
    public void PlanningAfterApplyingFindsNothingLeftToDoForAnyReplitMd(ContextFile replitMd)
    {
        InAProject(replitMd.Text, project =>
        {
            Apply(project, InitPlanner.Plan(Read(project), Carriers.Shared(), Permissive));

            Assert.Empty(InitPlanner.Plan(Read(project), Carriers.Shared(), Permissive).Actions.OfType<FileEdit>());
        });
    }

    [Property(Arbitrary = [typeof(ContextFileArbitraries)], MaxTest = 200)]
    public void InstallingThenUninstallingRestoresAnyReplitMdWeDidNotWriteInto(UntouchedContextFile replitMd)
    {
        InAProject(replitMd.Text, project =>
        {
            var before = Projects.SnapshotOf(project);

            Apply(project, InitPlanner.Plan(Read(project), Carriers.Shared(), Permissive));
            Apply(project, UninstallPlanner.Plan(Read(project), Permissive));

            var after = Projects.SnapshotOf(project);
            Assert.Equal(before.Keys.OrderBy(k => k, StringComparer.Ordinal), after.Keys.OrderBy(k => k, StringComparer.Ordinal));
            foreach (var (path, content) in after)
            {
                Assert.Contains(content, new[] { before[path], EndingWithNewline(before[path]) });
            }
        });
    }

    [Property(Arbitrary = [typeof(ContextFileArbitraries)], MaxTest = 200)]
    public void APlanNeverNamesOnePathTwiceWhateverReplitMdHolds(ContextFile replitMd)
    {
        InAProject(replitMd.Text, project =>
        {
            var install = InitPlanner.Plan(Read(project), Carriers.Shared(), Permissive);
            Apply(project, install);
            var uninstall = UninstallPlanner.Plan(Read(project), Permissive);

            Assert.Equal(install.Actions.Select(a => a.Path).Distinct(StringComparer.Ordinal).Count(), install.Actions.Count);
            Assert.Equal(uninstall.Actions.Select(a => a.Path).Distinct(StringComparer.Ordinal).Count(), uninstall.Actions.Count);
        });
    }

    private static void InAProject(string replitMd, System.Action<string> body) =>
        Projects.InATemporaryOne("nt-pointer-prop-", project =>
        {
            Directory.CreateDirectory(Path.Combine(project, ".cursor"));
            if (replitMd.Length > 0)
            {
                File.WriteAllText(Path.Combine(project, "replit.md"), replitMd, Utf8NoBom);
            }
            else
            {
                File.WriteAllText(Path.Combine(project, ".replit"), "run = \"x\"\n", Utf8NoBom);
            }

            body(project);
        });

    private static ProjectState Read(string project) => ProjectStateReader.Read(project);

    private static void Apply(string project, InitPlan plan) => PlanExecutor.Execute(plan, project);

    /// <summary>The one documented exception: an append leaves a file ending with the newline a block needs.</summary>
    private static string EndingWithNewline(string content) =>
        content.Length == 0 || content.EndsWith('\n') || content.EndsWith('\r')
            ? content
            : content + MarkedBlock.EolOf(content);
}
