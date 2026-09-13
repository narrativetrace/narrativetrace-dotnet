// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Text.Json;
using NarrativeTrace.Cli.Doctor;
using Xunit;

namespace NarrativeTrace.Cli.Tests.Doctor;

public sealed class DoctorRendererTests
{
    private static readonly DoctorReport SampleReport = new(
        [
            DoctorFinding.Pass("toolchain.target-framework", "all good", "https://example/doc#a"),
            DoctorFinding.Fail(
                "trap.redaction-proof", "unproven", "write a test", "https://example/doc#b"),
        ],
        ExitCode: 1);

    [Fact]
    public void Human_output_reports_pass_and_fail_lines_with_fix_and_doc_url()
    {
        var text = DoctorRenderer.RenderHuman(SampleReport);

        Assert.Contains("[PASS] toolchain.target-framework: all good", text, StringComparison.Ordinal);
        Assert.Contains("[FAIL] trap.redaction-proof: unproven", text, StringComparison.Ordinal);
        Assert.Contains("fix: write a test", text, StringComparison.Ordinal);
        Assert.Contains("see: https://example/doc#b", text, StringComparison.Ordinal);
        Assert.Contains("1/2 checks passed.", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Human_output_omits_fix_and_doc_url_for_a_passing_finding()
    {
        var text = DoctorRenderer.RenderHuman(SampleReport);

        Assert.DoesNotContain("fix: \n       see: https://example/doc#a", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Json_output_round_trips_every_field()
    {
        var json = DoctorRenderer.RenderJson(SampleReport);

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        Assert.Equal(1, root.GetProperty("exitCode").GetInt32());
        var findings = root.GetProperty("findings");
        Assert.Equal(2, findings.GetArrayLength());
        Assert.Equal("toolchain.target-framework", findings[0].GetProperty("id").GetString());
        Assert.False(findings[1].GetProperty("passed").GetBoolean());
        Assert.Equal("write a test", findings[1].GetProperty("fix").GetString());
    }
}
