// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Tooling.Init;
using Xunit;
using Applied = NarrativeTrace.Tooling.Init.ExecutionReport.Applied;
using Status = NarrativeTrace.Tooling.Init.ExecutionReport.Status;

namespace NarrativeTrace.Tooling.Tests.Init;

/// <summary>
/// What a person and a script each see. The JSON envelope mirrors the doctor's, on purpose. Method
/// names mirror the Java <c>PlanRendererTest</c>. Reports are built directly here rather than through
/// <c>PlanExecutor</c> (Java uses the executor); the executor's own tests cover its report-building.
/// </summary>
public class PlanRendererTest
{
    private const string Coordinate = "NarrativeTrace.Skills/1.2.3";

    private static InitPlan Plan(params NarrativeTrace.Tooling.Init.Action[] actions) =>
        new(Coordinate, false, actions);

    private static ExecutionReport Report(params Applied[] results) => new(Coordinate, results);

    // --- text ------------------------------------------------------------------------------------

    [Fact]
    public void TextNamesTheCarrierAndOneLinePerAction()
    {
        var text = PlanRenderer.RenderText(Plan(
            new CreateFile(".agents/skills/doctor/SKILL.md", "page\n"),
            new Refuse("AGENTS.md", "two sections")));

        Assert.Contains(Coordinate, text, StringComparison.Ordinal);
        Assert.Contains("create  .agents/skills/doctor/SKILL.md", text, StringComparison.Ordinal);
        Assert.Contains("refuse  AGENTS.md — two sections", text, StringComparison.Ordinal);
        Assert.Contains("2 action(s), 1 refusal(s)", text, StringComparison.Ordinal);
    }

    [Fact]
    public void TextSaysSoWhenThereIsNothingToDo()
    {
        Assert.Contains("nothing to do", PlanRenderer.RenderText(Plan()), StringComparison.Ordinal);
    }

    [Fact]
    public void TextOfAReportSaysWhatHappened()
    {
        var text = PlanRenderer.RenderText(Report(
            Applied.Ran(new CreateFile("AGENTS.md", "a\n")),
            Applied.Refused(new Refuse("CLAUDE.md", "no flag"), "no flag")));

        Assert.Contains("applied create  AGENTS.md", text, StringComparison.Ordinal);
        Assert.Contains("refused refuse  CLAUDE.md — no flag", text, StringComparison.Ordinal);
        Assert.Contains("1 applied, 1 refused", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// An adoption says so on BOTH surfaces — the preview a person reads and the report of what
    /// happened. Seeing "replace" where nothing of theirs was replaced is the misreading this avoids.
    /// </summary>
    [Fact]
    public void BothThePlanAndTheReportSayAPageWasAdopted()
    {
        var adoption = new AdoptPage(".agents/skills/doctor/SKILL.md", "page\n", "stamped\n");

        var preview = PlanRenderer.RenderText(Plan(adoption));
        var applied = PlanRenderer.RenderText(Report(Applied.Ran(adoption)));

        Assert.Contains("adopt   .agents/skills/doctor/SKILL.md", preview, StringComparison.Ordinal);
        Assert.Contains("adopted:", preview, StringComparison.Ordinal);
        Assert.Contains("applied adopt   .agents/skills/doctor/SKILL.md", applied, StringComparison.Ordinal);
        Assert.Contains("adopted:", applied, StringComparison.Ordinal);
    }

    /// <summary>
    /// A link replacement names, on both surfaces, the link it replaces and what it pointed at — so a
    /// person reading the plan knows a symbolic link, not a file of theirs, is what changes.
    /// </summary>
    [Fact]
    public void BothThePlanAndTheReportNameTheReplacedLink()
    {
        var replacement = new ReplaceLink(
            ".claude/skills/doctor", ".claude/skills/doctor/SKILL.md",
            "../../.agents/skills/doctor", "the vendor page\n");

        var preview = PlanRenderer.RenderText(Plan(replacement));
        var applied = PlanRenderer.RenderText(Report(Applied.Ran(replacement)));

        const string note = "replaces the symbolic link .claude/skills/doctor → ../../.agents/skills/doctor";
        Assert.Contains("replace-link .claude/skills/doctor/SKILL.md", preview, StringComparison.Ordinal);
        Assert.Contains(note, preview, StringComparison.Ordinal);
        Assert.Contains(note, applied, StringComparison.Ordinal);
    }

    // --- JSON ------------------------------------------------------------------------------------

    [Fact]
    public void JsonOfAPlanIsTheDoctorsEnvelopeWithPlannedActions()
    {
        var json = PlanRenderer.RenderJson(Plan(
            new CreateFile(".agents/skills/doctor/SKILL.md", "page\n"),
            new Refuse("AGENTS.md", "two sections")));

        Assert.Equal(
            "{\n  \"carrier\": \"NarrativeTrace.Skills/1.2.3\",\n  \"actions\": [\n"
            + "    {\"kind\": \"create\", \"path\": \".agents/skills/doctor/SKILL.md\", \"status\": \"planned\"},\n"
            + "    {\"kind\": \"refuse\", \"path\": \"AGENTS.md\", \"status\": \"refused\"}\n"
            + "  ],\n  \"exitCode\": 1\n}\n",
            json);
    }

    [Fact]
    public void JsonOfAnEmptyPlanStillCarriesTheEnvelope()
    {
        Assert.Equal(
            "{\n  \"carrier\": \"NarrativeTrace.Skills/1.2.3\",\n  \"actions\": [\n"
            + "  ],\n  \"exitCode\": 0\n}\n",
            PlanRenderer.RenderJson(Plan()));
    }

    [Fact]
    public void JsonOfADryRunExitsZero()
    {
        var dry = new InitPlan(Coordinate, true, [new Refuse("A.md", "x")]);

        Assert.Contains("\"exitCode\": 0", PlanRenderer.RenderJson(dry), StringComparison.Ordinal);
    }

    [Fact]
    public void JsonOfAReportCarriesWhatHappened()
    {
        var json = PlanRenderer.RenderJson(Report(Applied.Ran(new CreateFile("AGENTS.md", "a\n"))));

        Assert.Contains("\"status\": \"applied\"", json, StringComparison.Ordinal);
        Assert.Contains("\"exitCode\": 0", json, StringComparison.Ordinal);
    }

    [Fact]
    public void JsonOfAReportIsTheSameEnvelopeRowForRow()
    {
        var json = PlanRenderer.RenderJson(Report(
            Applied.Ran(new CreateFile("AGENTS.md", "a\n")),
            Applied.Refused(new Refuse("CLAUDE.md", "no flag"), "no flag")));

        Assert.Equal(
            "{\n  \"carrier\": \"NarrativeTrace.Skills/1.2.3\",\n  \"actions\": [\n"
            + "    {\"kind\": \"create\", \"path\": \"AGENTS.md\", \"status\": \"applied\"},\n"
            + "    {\"kind\": \"refuse\", \"path\": \"CLAUDE.md\", \"status\": \"refused\"}\n"
            + "  ],\n  \"exitCode\": 1\n}\n",
            json);
    }

    [Fact]
    public void JsonEscapesAControlCharacterAsAUnicodeEscape()
    {
        var json = PlanRenderer.RenderJson(Plan(new CreateFile("ab.md", "x")));

        Assert.Contains("\"path\": \"a\\u0001b.md\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void JsonLeavesASpaceInAPathAlone()
    {
        var json = PlanRenderer.RenderJson(Plan(new CreateFile("my notes.md", "x")));

        Assert.Contains("\"path\": \"my notes.md\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void JsonEscapesWhateverEndsUpInAPath()
    {
        var json = PlanRenderer.RenderJson(Plan(new CreateFile(".agents/sk\"ill\\x/SKILL.md", "p")));

        Assert.Contains("\"path\": \".agents/sk\\\"ill\\\\x/SKILL.md\"", json, StringComparison.Ordinal);
    }

    // --- diff ------------------------------------------------------------------------------------

    [Fact]
    public void DiffOfACreatedFileComesFromNowhere()
    {
        var diff = PlanRenderer.RenderDiff(Plan(new CreateFile("AGENTS.md", "a\nb\n")));

        Assert.Equal("--- /dev/null\n+++ b/AGENTS.md\n@@ -0,0 +1,2 @@\n+a\n+b\n", diff);
    }

    [Fact]
    public void DiffOfADeletedFileGoesNowhere()
    {
        var diff = PlanRenderer.RenderDiff(Plan(new DeleteFile("AGENTS.md", "a\n")));

        Assert.Equal("--- a/AGENTS.md\n+++ /dev/null\n@@ -1,1 +0,0 @@\n-a\n", diff);
    }

    [Fact]
    public void DiffOfAReplacementShowsOnlyWhatChangedWithContext()
    {
        var before = "1\n2\n3\n4\n5\nold\n6\n7\n8\n9\n";
        var after = "1\n2\n3\n4\n5\nnew\n6\n7\n8\n9\n";

        var diff = PlanRenderer.RenderDiff(Plan(new ReplaceBlock("A.md", before, after)));

        Assert.Equal(
            "--- a/A.md\n+++ b/A.md\n@@ -3,7 +3,7 @@\n 3\n 4\n 5\n-old\n+new\n 6\n 7\n 8\n",
            diff);
    }

    [Fact]
    public void DiffMarksAMissingFinalNewlineOnBothSides()
    {
        var diff = PlanRenderer.RenderDiff(Plan(new ReplaceBlock("A.md", "a", "b")));

        Assert.Equal(
            "--- a/A.md\n+++ b/A.md\n@@ -1,1 +1,1 @@\n"
            + "-a\n\\ No newline at end of file\n+b\n\\ No newline at end of file\n",
            diff);
    }

    [Fact]
    public void DiffShowsALineEndingChangeRatherThanNothing()
    {
        var diff = PlanRenderer.RenderDiff(Plan(new ReplaceBlock("A.md", "a\n", "a\r\n")));

        Assert.Contains("-a", diff, StringComparison.Ordinal);
        Assert.Contains("+a\r", diff, StringComparison.Ordinal);
    }

    [Fact]
    public void DiffDescribesTheActionsThatHaveNoContent()
    {
        var diff = PlanRenderer.RenderDiff(Plan(
            new DeleteDirectory(".agents/skills/doctor"),
            new Refuse("AGENTS.md", "two sections")));

        Assert.Equal(
            "# .agents/skills/doctor — delete-directory\n# AGENTS.md — refused: two sections\n",
            diff);
    }

    [Fact]
    public void DiffOfALineInsertedBesideItsTwinCountsEachSideOnce()
    {
        var diff = PlanRenderer.RenderDiff(Plan(new ReplaceBlock("A.md", "a\n", "a\na\n")));

        Assert.Equal("--- a/A.md\n+++ b/A.md\n@@ -1,1 +1,2 @@\n a\n+a\n", diff);
    }

    [Fact]
    public void DiffOfALineRemovedBesideItsTwinCountsEachSideOnce()
    {
        var diff = PlanRenderer.RenderDiff(Plan(new ReplaceBlock("A.md", "a\na\n", "a\n")));

        Assert.Equal("--- a/A.md\n+++ b/A.md\n@@ -1,2 +1,1 @@\n a\n-a\n", diff);
    }

    [Fact]
    public void DiffOfAnUnchangedFileIsEmpty()
    {
        Assert.Equal("", PlanRenderer.RenderDiff(Plan(new ReplaceBlock("A.md", "a\n", "a\n"))));
    }

    [Fact]
    public void RefusesToRenderNothing()
    {
        Assert.Throws<ArgumentException>(() => PlanRenderer.RenderText((InitPlan)null!));
        Assert.Throws<ArgumentException>(() => PlanRenderer.RenderJson((InitPlan)null!));
        Assert.Throws<ArgumentException>(() => PlanRenderer.RenderDiff(null!));
        Assert.Throws<ArgumentException>(() => PlanRenderer.RenderText((ExecutionReport)null!));
        Assert.Throws<ArgumentException>(() => PlanRenderer.RenderJson((ExecutionReport)null!));
    }

    // --- the one decision every entry point shares -------------------------------------------

    [Fact]
    public void APlanRendersAsTheSummaryAndTheDiffTogether()
    {
        var plan = new InitPlan(Coordinate, true, [Create()]);

        Assert.Equal(PlanRenderer.RenderText(plan) + PlanRenderer.RenderDiff(plan),
            PlanRenderer.Render(plan, false));
    }

    [Fact]
    public void APlanRendersAsNothingButTheEnvelopeWhenJsonIsAsked()
    {
        var plan = new InitPlan(Coordinate, true, [Create()]);

        Assert.Equal(PlanRenderer.RenderJson(plan), PlanRenderer.Render(plan, true));
    }

    [Fact]
    public void AnAppliedRunRendersAsItsOwnSummaryOrItsOwnEnvelope()
    {
        var report = Report(new Applied(Create(), Status.Applied, ""));

        Assert.Equal(PlanRenderer.RenderText(report), PlanRenderer.Render(report, false));
        Assert.Equal(PlanRenderer.RenderJson(report), PlanRenderer.Render(report, true));
    }

    private static CreateFile Create() => new("AGENTS.md", "# Agents\n");
}
