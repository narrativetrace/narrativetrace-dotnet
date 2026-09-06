// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
/// Tutorial runner for the F# example: the same NarrativeTraceProxy API as
/// the C# examples, on F# interfaces and records — a successful borrow and a
/// BookUnavailableException failure, rendered as tree, prose, and Mermaid.
module NarrativeTrace.Examples.Library.LibraryExample

open NarrativeTrace.Examples.Common
open NarrativeTrace.Proxy

let private expectUnavailable (action: unit -> LoanReceipt) =
    try
        action () |> ignore
    with :? BookUnavailableException ->
        () // expected: the trace records it

/// Runs both scenarios against the shared demo driver.
let run (run: DemoRun) =
    let context = run.Context
    let catalog = NarrativeTraceProxy.Create<ICatalogService>(InMemoryCatalogService(), context)
    let members = NarrativeTraceProxy.Create<IMemberService>(InMemoryMemberService(), context)

    let lending =
        NarrativeTraceProxy.Create<ILendingService>(DefaultLendingService(catalog, members), context)

    run.BeginScenario "Scenario 1: Successful Book Borrow"
    let receipt = lending.BorrowBook("M-001", "978-0-13-468599-1")
    run.Info(sprintf "Received: %s" receipt.NarrativeSummary)
    let trace1 = context.CaptureTrace()
    run.TraceTree trace1
    run.Prose trace1
    run.Mermaid trace1

    run.BeginScenario "Scenario 2: Book Unavailable"
    expectUnavailable (fun () -> lending.BorrowBook("M-001", "978-0-13-235088-4"))
    let trace2 = context.CaptureTrace()
    run.TraceTree trace2
    run.Prose trace2
