// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Cli.Feedback;
using NarrativeTrace.Tooling.Doctor;
using Xunit;

namespace NarrativeTrace.Cli.Tests.Feedback;

/// <summary>
/// The verb's remaining branches: the JSON forms of every channel, the help and argument-error
/// paths, the doctor-category-without-a-report refusal, and the real two-argument entry point. The
/// one thing left uncovered on purpose is <see cref="GhAuthProbe"/>'s real-process method — a test
/// never starts a process.
/// </summary>
public sealed class FeedbackVerbBranchTests : IDisposable
{
    private readonly string _cwd = Path.Combine(Path.GetTempPath(), "nt-fb-branch-" + Guid.NewGuid().ToString("N"));
    private readonly StringWriter _out = new();
    private readonly StringWriter _err = new();

    private static readonly string[] LibraryArgs =
    [
        "--category", "library", "--step", "rendering",
        "--did", "ran a trace", "--happened", "it looked wrong", "--expected", "it looks right",
    ];

    public FeedbackVerbBranchTests() => Directory.CreateDirectory(_cwd);

    public void Dispose()
    {
        if (Directory.Exists(_cwd))
        {
            Directory.Delete(_cwd, recursive: true);
        }
    }

    private static DoctorSnapshot Snapshot() => DoctorSnapshotFixture.With();

    private int Run(Func<bool> gh, params string[] args) =>
        FeedbackCommand.Run(args, _cwd, _ => Snapshot(), gh, _out, _err);

    [Fact]
    public void Help_prints_the_usage_and_exits_zero()
    {
        var exit = Run(() => false, "--help");

        Assert.Equal(0, exit);
        Assert.Contains("feedback <draft|url|gh>", _out.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void A_doctor_category_without_a_report_on_disk_refuses_with_exit_two()
    {
        // No doctor-report.json in this cwd, so the doctor category's constructor rule throws and is
        // caught as a bad report rather than a crash.
        var exit = Run(
            () => false, "draft", "--category", "doctor", "--step", "trap.redaction-proof",
            "--did", "d", "--happened", "h", "--expected", "e");

        Assert.Equal(2, exit);
    }

    [Fact]
    public void A_json_url_envelope_is_printed()
    {
        var exit = Run(() => false, [.. Prepend("url"), "--json"]);

        Assert.Equal(0, exit);
        Assert.Contains("\"verb\": \"url\"", _out.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void A_json_gh_envelope_is_printed_when_authenticated()
    {
        var exit = Run(() => true, [.. Prepend("gh"), "--json"]);

        Assert.Equal(0, exit);
        Assert.Contains("\"verb\": \"gh\"", _out.ToString(), StringComparison.Ordinal);
        Assert.Contains("\"status\": \"ready\"", _out.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void A_json_gh_unavailable_envelope_goes_to_stderr_with_exit_one()
    {
        var exit = Run(() => false, [.. Prepend("gh"), "--json"]);

        Assert.Equal(1, exit);
        Assert.Contains("\"status\": \"unavailable\"", _err.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void The_real_two_argument_entry_drafts_against_the_working_directory()
    {
        // Exercises the convenience overload that wires the real snapshot builder and gh probe. A
        // library-category draft never consults gh and never starts a process. It writes under the
        // process working directory, so the artifacts are cleaned up afterwards.
        var feedbackDir = Path.Combine(Directory.GetCurrentDirectory(), "TestResults", "narrativetrace");
        try
        {
            var exit = FeedbackCommand.Run([.. Prepend("draft")], _out, _err);

            Assert.Equal(0, exit);
            Assert.Contains("problem report", _out.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(feedbackDir))
            {
                Directory.Delete(feedbackDir, recursive: true);
            }
        }
    }

    private static string[] Prepend(string channel) => [channel, .. LibraryArgs];
}
