// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.SkillsEvals;
using Xunit;

namespace NarrativeTrace.SkillsEvals.Tests;

public sealed class RunLedgerRowTests
{
    private static readonly RunLedgerRow Sample = new(
        "2026-09-26T07:00:00.0000000Z", "add-narrative-tracing", "init-prompt-empty-project",
        Platform.Claude, "haiku", 1, true);

    [Fact]
    public void Serializes_the_family_schema_with_case_not_casename()
    {
        var line = Sample.ToJsonLine();

        Assert.Contains("\"case\":\"init-prompt-empty-project\"", line, StringComparison.Ordinal);
        Assert.Contains("\"platform\":\"claude\"", line, StringComparison.Ordinal);
        Assert.Contains("\"result\":\"pass\"", line, StringComparison.Ordinal);
        Assert.DoesNotContain("caseName", line, StringComparison.Ordinal);
    }

    [Fact]
    public void Round_trips_through_json()
    {
        var parsed = RunLedgerRow.ParseJsonLine(Sample.ToJsonLine());

        Assert.Equal(Sample, parsed);
    }

    [Fact]
    public void A_failing_trial_serializes_result_fail()
    {
        Assert.Contains("\"result\":\"fail\"", (Sample with { Pass = false }).ToJsonLine(), StringComparison.Ordinal);
    }

    [Fact]
    public void Escapes_quotes_and_backslashes_in_a_field()
    {
        var row = Sample with { Model = "a\"b\\c" };

        Assert.Equal(row, RunLedgerRow.ParseJsonLine(row.ToJsonLine()));
    }

    [Fact]
    public void Parses_a_multi_row_ledger_skipping_blank_lines()
    {
        var content = Sample.ToJsonLine() + "\n\n" + (Sample with { Trial = 2, Pass = false }).ToJsonLine() + "\n";

        var rows = RunLedgerRow.ParseJsonl(content);

        Assert.Equal(2, rows.Count);
        Assert.Equal(2, rows[1].Trial);
        Assert.False(rows[1].Pass);
    }
}
