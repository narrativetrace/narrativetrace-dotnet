// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using NarrativeTrace.TestingXunit;
using Xunit;

namespace NarrativeTrace.Testing.Xunit.Tests;

/// <summary>
/// With output on, <see cref="NarrativeFixture.Run(string, Action{INarrativeContext}, string)"/>
/// writes the test's own narrative files when the test ends — the test body
/// calls nothing, the way Java's JUnit 5 extension writes after each test.
/// </summary>
/// <remarks>
/// Every test here goes through the public <c>Run</c> from a real test method,
/// so the class and method the files are named after are the ones the
/// compiler resolved, not ones a test supplied.
/// </remarks>
public sealed class NarrativeFixtureRunWritesArtifactsTests : IDisposable
{
    private readonly string _dir = Path.Combine(
        Path.GetTempPath(), "nt-run-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        foreach (var dir in new[] { _dir, ApprovedDir })
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    private string ApprovedDir => _dir + "-approved";

    [Fact]
    public void A_test_that_only_runs_its_body_gets_its_md_and_nt_files()
    {
        using var fixture = new NarrativeFixture(Output("true"));

        fixture.Run("places an order", PlaceOrder);

        Assert.True(File.Exists(Md(nameof(A_test_that_only_runs_its_body_gets_its_md_and_nt_files))));
        Assert.True(File.Exists(Nt(nameof(A_test_that_only_runs_its_body_gets_its_md_and_nt_files))));
    }

    [Fact]
    public void A_capturing_body_is_still_named_after_its_test_class()
    {
        using var fixture = new NarrativeFixture(Output("true"));
        var service = "OrderService";

        fixture.Run("places an order", ctx =>
        {
            ctx.EnterMethod(service, "PlaceOrder", []);
            ctx.ExitMethodWithReturn(null);
        });

        Assert.True(File.Exists(Md(nameof(A_capturing_body_is_still_named_after_its_test_class))));
    }

    [Fact]
    public void Output_off_writes_nothing()
    {
        using var fixture = new NarrativeFixture(Output("false"));

        fixture.Run("places an order", PlaceOrder);

        Assert.False(Directory.Exists(_dir));
    }

    [Fact]
    public void A_body_that_throws_writes_a_failed_trace_and_leaves_no_last_green_baseline()
    {
        using var fixture = new NarrativeFixture(Output("true"));

        Assert.Throws<InvalidOperationException>(() => fixture.Run("places an order", ctx =>
        {
            PlaceOrder(ctx);
            throw new InvalidOperationException("the assertion that failed the test");
        }, TextWriter.Null));

        var name = nameof(A_body_that_throws_writes_a_failed_trace_and_leaves_no_last_green_baseline);
        Assert.Contains("\"error\"", File.ReadAllText(Companion(name, ".json")), StringComparison.Ordinal);
        Assert.False(File.Exists(Nt(name)));
    }

    [Fact]
    public void A_body_that_passes_records_a_green_result()
    {
        using var fixture = new NarrativeFixture(Output("true"));

        fixture.Run("places an order", PlaceOrder);

        var name = nameof(A_body_that_passes_records_a_green_result);
        Assert.Contains("\"success\"", File.ReadAllText(Companion(name, ".json")), StringComparison.Ordinal);
    }

    [Fact]
    public void An_explicit_write_inside_the_body_is_not_written_twice()
    {
        using var fixture = new NarrativeFixture(Output("true"));
        var console = new StringWriter();

        fixture.Run("places an order", ctx =>
        {
            PlaceOrder(ctx);
            fixture.WriteArtifacts(
                nameof(NarrativeFixtureRunWritesArtifactsTests),
                nameof(An_explicit_write_inside_the_body_is_not_written_twice), failed: false, console);
        }, console);

        Assert.Equal(1, Writes(console));
    }

    [Fact]
    public void An_explicit_write_after_the_run_is_not_written_twice()
    {
        using var fixture = new NarrativeFixture(Output("true"));
        var console = new StringWriter();

        fixture.Run("places an order", PlaceOrder, console);
        fixture.WriteArtifacts(
            nameof(NarrativeFixtureRunWritesArtifactsTests),
            nameof(An_explicit_write_after_the_run_is_not_written_twice), failed: false, console);

        Assert.Equal(1, Writes(console));
    }

    [Fact]
    public void An_explicit_write_after_more_calls_rewrites_with_everything_the_test_did()
    {
        using var fixture = new NarrativeFixture(Output("true"));
        var console = new StringWriter();

        fixture.Run("places an order", PlaceOrder, console);
        PlaceOrder(fixture.Context);
        fixture.WriteArtifacts(
            nameof(NarrativeFixtureRunWritesArtifactsTests),
            nameof(An_explicit_write_after_more_calls_rewrites_with_everything_the_test_did),
            failed: false, console);

        Assert.Equal(2, Writes(console));
    }

    [Fact]
    public void A_fixture_reused_by_the_next_test_writes_only_that_tests_calls()
    {
        using var fixture = new NarrativeFixture(Output("true"));
        fixture.Run("places an order", PlaceOrder, TextWriter.Null, "First_test");

        fixture.Run("cancels an order", ctx =>
        {
            ctx.EnterMethod("OrderService", "CancelOrder", []);
            ctx.ExitMethodWithReturn(null);
        }, TextWriter.Null, "Second_test");

        var second = File.ReadAllText(Md("Second_test"));
        Assert.Contains("CancelOrder", second, StringComparison.Ordinal);
        Assert.DoesNotContain("PlaceOrder", second, StringComparison.Ordinal);
    }

    [Fact]
    public void Two_runs_in_one_test_write_one_file_with_both()
    {
        using var fixture = new NarrativeFixture(Output("true"));

        fixture.Run("places an order", PlaceOrder, TextWriter.Null);
        fixture.Run("cancels an order", ctx =>
        {
            ctx.EnterMethod("OrderService", "CancelOrder", []);
            ctx.ExitMethodWithReturn(null);
        }, TextWriter.Null);

        var md = File.ReadAllText(Md(nameof(Two_runs_in_one_test_write_one_file_with_both)));
        Assert.Contains("PlaceOrder", md, StringComparison.Ordinal);
        Assert.Contains("CancelOrder", md, StringComparison.Ordinal);
    }

    [Fact]
    public void Approval_mode_fails_a_passing_run_whose_structure_has_no_approved_trace()
    {
        using var fixture = new NarrativeFixture(Approval());

        Assert.Throws<NarrativeApprovalException>(
            () => fixture.Run("places an order", PlaceOrder, TextWriter.Null));
    }

    [Fact]
    public void Approval_mode_leaves_a_failing_runs_own_exception_in_place()
    {
        using var fixture = new NarrativeFixture(Approval());

        Assert.Throws<InvalidOperationException>(() => fixture.Run("places an order", ctx =>
        {
            PlaceOrder(ctx);
            throw new InvalidOperationException("the assertion that failed the test");
        }, TextWriter.Null));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void A_blank_test_method_is_rejected_before_the_body_runs(string testMethod)
    {
        using var fixture = new NarrativeFixture(Output("true"));
        var ran = false;

        Assert.Throws<ArgumentException>(
            () => fixture.Run("places an order", _ => ran = true, TextWriter.Null, testMethod));

        Assert.False(ran);
    }

    private static void PlaceOrder(INarrativeContext ctx)
    {
        ctx.EnterMethod("OrderService", "PlaceOrder", []);
        ctx.ExitMethodWithReturn(null);
    }

    private static int Writes(StringWriter console)
    {
        return console.ToString().Split("Trace written: ").Length - 1;
    }

    private Func<string, string?> Output(string enabled)
    {
        return key => key switch
        {
            ConfigResolver.OutputKey => enabled,
            ConfigResolver.OutputDirKey => _dir,
            _ => null,
        };
    }

    private Func<string, string?> Approval()
    {
        return key => key switch
        {
            ConfigResolver.OutputDirKey => _dir,
            ConfigResolver.ApprovalKey => "true",
            ConfigResolver.ApprovedDirKey => ApprovedDir,
            _ => null,
        };
    }

    private string Md(string testMethod)
    {
        return Companion(testMethod, ".md");
    }

    private string Companion(string testMethod, string suffix)
    {
        return new OutputDirectoryResolver(_dir).TraceArtifact(
            ArtifactIdentity.OfMethod(nameof(NarrativeFixtureRunWritesArtifactsTests), testMethod), suffix);
    }

    private string Nt(string testMethod)
    {
        return new OutputDirectoryResolver(_dir).StructuralFile(
            ArtifactIdentity.OfMethod(nameof(NarrativeFixtureRunWritesArtifactsTests), testMethod));
    }
}
