// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using NarrativeTrace.TestingNUnit;
using NUnit.Framework;

namespace NarrativeTrace.Testing.NUnit.EngineTests;

/// <summary>
/// The namespace-scoped suite bracket the docs teach: derive an empty class
/// from <see cref="NarrativeSuiteSetup"/> to activate suite reporting.
/// </summary>
[SetUpFixture]
public sealed class SuiteSetup : NarrativeSuiteSetup
{
}

/// <summary>
/// Exercises <see cref="NarrativeTestBase"/> exactly as documented
/// (choosing-an-integration.md, installation.md): derive from it and let
/// NUnit's own <c>[SetUp]</c>/<c>[TearDown]</c> lifecycle drive it —
/// nothing in this class calls <c>SetUpTrace()</c>/<c>TearDownTrace()</c>
/// directly.
/// </summary>
/// <remarks>
/// Every test elsewhere in the solution that touches <c>NarrativeTestBase</c>
/// (<c>NarrativeTestBaseTests</c> in the sibling xUnit-based
/// <c>NarrativeTrace.Testing.NUnit.Tests</c> project) calls
/// <c>SetUpTrace</c>/<c>TearDownTrace</c> by hand, which proves the methods'
/// own logic but never proves NUnit actually discovers and invokes them —
/// the same gap that let <c>NarrativeFixture</c> ship with two public
/// constructors nobody's suite ran through xUnit's real
/// <c>IClassFixture&lt;T&gt;</c> activator. This project runs under the real
/// NUnit3TestAdapter, so a regression that breaks the
/// <c>[SetUp]</c>/<c>[TearDown]</c>/<c>[SetUpFixture]</c> wiring for this
/// type — a typo'd attribute, a constructor NUnit can no longer activate,
/// <see cref="NarrativeSuiteSetup"/> losing its parameterless constructor —
/// fails here even though every xUnit-based test in the solution would still
/// pass.
/// <para>
/// The regression assertion below runs as an ordinary <c>[Test]</c>, ordered
/// after the two it counts, rather than an <c>[OneTimeTearDown]</c>: a
/// failure inside a fixture-level tear-down is reported by the NUnit engine
/// but does not fail the vstest run (verified against this exact project — a
/// removed <c>[TearDown]</c> attribute left the overall <c>dotnet test</c>
/// exit code at 0 even though the console showed "TearDown failed for test
/// fixture"). An ordinary failing <c>[Test]</c> does not have that gap.
/// </para>
/// </remarks>
[TestFixture]
public sealed class OrderServiceTests : NarrativeTestBase
{
    private static int _tracesCompleted;

    protected override void OnTraceComplete(TraceTree tree)
    {
        Interlocked.Increment(ref _tracesCompleted);
    }

    // S2699: the assertion for these two is deliberately deferred to
    // TearDown_ran_automatically_for_every_test below — that count is the
    // thing under test, not the trace calls themselves.
#pragma warning disable S2699
    [Test]
    [Order(1)]
    public void Places_an_order()
    {
        Context.EnterMethod("OrderService", "PlaceOrder", []);
        Context.ExitMethodWithReturn(null);
    }

    [Test]
    [Order(2)]
    public void Cancels_an_order()
    {
        Context.EnterMethod("OrderService", "CancelOrder", []);
        Context.ExitMethodWithReturn(null);
    }
#pragma warning restore S2699

    /// <summary>
    /// The regression assertion: <see cref="OnTraceComplete"/> — only called
    /// from <c>TearDownTrace</c> — must have fired once per <c>[Test]</c>
    /// above by the time this one runs. Ordered last, and NUnit finishes a
    /// test's <c>[TearDown]</c> before starting the next test in the same
    /// fixture, so if NUnit ever stopped invoking <c>[TearDown]</c> for this
    /// class, this count would stay 0 and this test would fail (and, unlike
    /// an <c>[OneTimeTearDown]</c> failure, actually fail the build).
    /// </summary>
    [Test]
    [Order(3)]
    public void TearDown_ran_automatically_for_every_test_above()
    {
        Assert.That(_tracesCompleted, Is.EqualTo(2));
    }
}
