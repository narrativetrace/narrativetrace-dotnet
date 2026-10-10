// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Cli.Feedback;
using NarrativeTrace.Tooling.Doctor;
using Xunit;

namespace NarrativeTrace.Cli.Tests.Feedback;

public sealed class FeedbackCommandTests : IDisposable
{
    private readonly string _cwd = Path.Combine(Path.GetTempPath(), "nt-feedback-" + Guid.NewGuid().ToString("N"));
    private readonly StringWriter _out = new();
    private readonly StringWriter _err = new();

    // The real doctor's JSON names the redaction marker; a report must still draft (field exemption).
    private const string DoctorReportJson =
        "{\"findings\":[{\"id\":\"trap.redaction-proof\",\"status\":\"pass\","
        + "\"message\":\"a test asserts the literal \\\"[REDACTED]\\\" marker\"}],\"exitCode\":0}";

    private static readonly string[] DoctorReportArgs =
    [
        "--category", "doctor", "--step", "trap.redaction-proof",
        "--did", "ran the doctor", "--happened", "the fix did not clear it", "--expected", "it clears",
    ];

    public FeedbackCommandTests()
    {
        Directory.CreateDirectory(_cwd);
        var reportDir = Path.Combine(_cwd, "TestResults", "narrativetrace");
        Directory.CreateDirectory(reportDir);
        File.WriteAllText(Path.Combine(reportDir, "doctor-report.json"), DoctorReportJson);
    }

    public void Dispose()
    {
        if (Directory.Exists(_cwd))
        {
            Directory.Delete(_cwd, recursive: true);
        }
    }

    private static DoctorSnapshot Snapshot() =>
        DoctorSnapshotFixture.With(
            installed: new Dictionary<string, string> { ["NarrativeTrace.Core"] = "1.0.0" });

    private int Run(Func<bool> gh, params string[] args) =>
        FeedbackCommand.Run(args, _cwd, _ => Snapshot(), gh, _out, _err);

    [Fact]
    public void Draft_writes_the_two_files_and_exits_zero()
    {
        var exit = Run(() => false, [.. Prepend("draft")]);

        Assert.Equal(0, exit);
        Assert.True(File.Exists(Path.Combine(_cwd, FeedbackCommand.DraftFile.Replace('/', Path.DirectorySeparatorChar))));
        Assert.True(File.Exists(Path.Combine(_cwd, FeedbackCommand.BodyFile.Replace('/', Path.DirectorySeparatorChar))));
    }

    [Fact]
    public void A_doctor_report_naming_the_marker_still_drafts()
    {
        // End-to-end proof of the field-scoped exemption through the CLI: the on-disk doctor JSON
        // quotes "[REDACTED]", and the verb must still draft rather than refuse every install.
        Assert.Equal(0, Run(() => false, [.. Prepend("draft")]));
    }

    [Fact]
    public void A_leaked_value_refuses_with_exit_two_and_writes_no_files()
    {
        var args = new[]
        {
            "draft", "--category", "doctor", "--step", "trap.redaction-proof",
            "--did", "ran it", "--happened", "the token ghp_0123456789abcdef leaked", "--expected", "redaction",
        };

        var exit = FeedbackCommand.Run(args, _cwd, _ => Snapshot(), () => false, _out, _err);

        Assert.Equal(2, exit);
        Assert.Contains("vf.value-shape", _err.ToString(), StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(_cwd, FeedbackCommand.DraftFile.Replace('/', Path.DirectorySeparatorChar))));
    }

    [Fact]
    public void Url_prints_the_prefilled_issue_form_url()
    {
        var exit = Run(() => false, [.. Prepend("url")]);

        Assert.Equal(0, exit);
        Assert.Contains("https://github.com/narrativetrace/narrativetrace-dotnet/issues/new", _out.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Gh_prints_the_command_when_authenticated()
    {
        var exit = Run(() => true, [.. Prepend("gh")]);

        Assert.Equal(0, exit);
        Assert.Contains("gh issue create", _out.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Gh_refuses_with_exit_one_when_not_authenticated()
    {
        var exit = Run(() => false, [.. Prepend("gh")]);

        Assert.Equal(1, exit);
        Assert.DoesNotContain("gh issue create", _out.ToString(), StringComparison.Ordinal);
        Assert.Contains("url", _err.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void A_bad_command_line_exits_two()
    {
        var exit = FeedbackCommand.Run(["draft"], _cwd, _ => Snapshot(), () => false, _out, _err);

        Assert.Equal(2, exit);
    }

    [Fact]
    public void Json_draft_names_the_verb_and_exit_code()
    {
        var exit = Run(() => false, [.. Prepend("draft"), "--json"]);

        Assert.Equal(0, exit);
        Assert.Contains("\"verb\": \"draft\"", _out.ToString(), StringComparison.Ordinal);
    }

    private static string[] Prepend(string channel) => [channel, .. DoctorReportArgs];
}
