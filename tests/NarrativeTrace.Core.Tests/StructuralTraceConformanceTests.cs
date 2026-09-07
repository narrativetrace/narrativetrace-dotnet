// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using System.Text;
using Xunit;

namespace NarrativeTrace.Core.Tests;

/// <summary>
/// The exception the Java dogfood scenario throws. Named to match, because the
/// artifact records the exception's simple type name as structure.
/// </summary>
public sealed class InvalidExpenseException : Exception
{
    public InvalidExpenseException()
    {
    }

    public InvalidExpenseException(string message)
        : base(message)
    {
    }

    public InvalidExpenseException(string message, Exception inner)
        : base(message, inner)
    {
    }
}

/// <summary>
/// Cross-platform conformance: the <c>.nt</c> format is normative and shared by
/// every NarrativeTrace runtime, because these files are the approval baselines
/// and conformance fixtures that travel between platforms. This pins the .NET
/// renderer against a golden file emitted by the Java runtime
/// (<c>fairsplit</c> dogfood, <c>TripSettlementServiceTest</c>) — byte for byte.
/// </summary>
public sealed class StructuralTraceConformanceTests
{
    private static TraceNode Call(
        string className, string methodName, string[] parameters,
        TraceOutcome outcome, params TraceNode[] children)
    {
        var captures = new List<ParameterCapture>();
        foreach (var name in parameters)
        {
            captures.Add(new ParameterCapture(name, "\"redacted-by-design\"", false));
        }

        return new TraceNode(
            new MethodSignature(className, methodName, captures),
            outcome, children, 7 * TimeSpan.TicksPerMillisecond);
    }

    private static TraceTree FairSplitNegativeExpense()
    {
        var failure = new Threw(
            new InvalidExpenseException("amount -12.00 EUR is not positive"));
        return new TraceTree([
            Call(
                "TripSettlementService", "recordExpense",
                ["tripName", "expense"], failure,
                Call("ExpenseValidator", "ensureValid", ["expense"], failure)),
            Call(
                "TripSettlementService", "settleTrip", ["tripName"],
                new Returned("SettlementPlan[transfers=0]"),
                Call(
                    "TripLedger", "expensesOf", ["tripName"],
                    new Returned("[]")),
                Call(
                    "BalanceCalculator", "computeBalances", ["expenses"],
                    new Returned("{}")),
                Call(
                    "SettlementPlanner", "planTransfers", ["balances"],
                    new Returned("[]"))),
        ]);
    }

    [Fact]
    public void Renderer_reproduces_the_java_golden_artifact_byte_for_byte()
    {
        var golden = File.ReadAllBytes(
            Path.Combine("Fixtures", "java-negative-expense.nt"));

        var rendered = new UTF8Encoding(false).GetBytes(
            StructuralTraceRenderer.RenderDocument(
                FairSplitNegativeExpense(),
                "Negative expense is rejected before touching the ledger"));

        Assert.Equal(golden, rendered);
    }
}
