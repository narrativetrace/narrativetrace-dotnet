// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Diagnostics;
using System.Text.Json;
using NarrativeTrace.Skills.Tests;
using NarrativeTrace.Cli.Tests;
using Xunit;

namespace NarrativeTrace.SkillsEvals.Tests;

/// <summary>
/// The shared fresh-session grader reads the LAST turn of a transcript: the one-turn case (the
/// harness ran the installer, the agent is a fresh session) and the prompt-path case (install turn,
/// then a fresh session) are one question asked of the final session.
/// </summary>
public sealed class FreshSessionGraderTests : IDisposable
{
    private static readonly string[] AllSkills =
    [
        "add-narrative-tracing", "add-narrativetrace-clarity", "narrativetrace-doctor",
        "narrativetrace-feedback", "narrativetrace-verify", "narrativetrace-debug", "narrativetrace-explain",
    ];

    private readonly string _dir = Path.Combine(Path.GetTempPath(), "nt-fresh-grader-" + Guid.NewGuid().ToString("N"));

    public FreshSessionGraderTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private static string Marker(int turn) => JsonSerializer.Serialize(new { nt_turn = turn, role = "user", text = "q" });

    private static string Init(IEnumerable<string> skills) =>
        JsonSerializer.Serialize(new { type = "system", subtype = "init", skills });

    private static string Doctor() => JsonSerializer.Serialize(new
    {
        type = "assistant",
        message = new { content = new object[] { new { type = "tool_use", id = "t1", name = "Skill", input = new { skill = "narrativetrace-doctor" } } } },
    });

    private (int Exit, string Error) Grade(params string[] lines)
    {
        var transcript = Path.Combine(_dir, "transcript.jsonl");
        File.WriteAllLines(transcript, lines);
        var grader = Path.Combine(TestPaths.RepoRoot(), "skills-evals", "add-narrative-tracing", "grade_the_fresh_session.py");
        var psi = new ProcessStartInfo("python3") { RedirectStandardError = true, RedirectStandardOutput = true, WorkingDirectory = _dir };
        psi.ArgumentList.Add("-I");
        psi.ArgumentList.Add(grader);
        psi.Environment["NARRATIVETRACE_TRANSCRIPT"] = transcript;
        using var process = Process.Start(psi)!;
        var error = process.StandardError.ReadToEnd();
        process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return (process.ExitCode, error);
    }

    [PythonFact]
    public void A_single_fresh_turn_that_lists_every_skill_and_reaches_for_the_doctor_passes()
    {
        Assert.Equal(0, Grade(Marker(1), Init(AllSkills), Doctor()).Exit);
    }

    [PythonFact]
    public void A_single_fresh_turn_missing_a_skill_fails_naming_it()
    {
        var (exit, error) = Grade(Marker(1), Init(AllSkills.Where(s => s != "narrativetrace-verify")), Doctor());

        Assert.Equal(1, exit);
        Assert.Contains("narrativetrace-verify", error, StringComparison.Ordinal);
    }

    [PythonFact]
    public void The_last_turn_is_graded_not_the_install_turn_before_it()
    {
        Assert.Equal(0, Grade(Marker(1), Init([]), Marker(2), Init(AllSkills), Doctor()).Exit);
    }

    [PythonFact]
    public void An_empty_transcript_fails()
    {
        Assert.Equal(1, Grade().Exit);
    }
}
