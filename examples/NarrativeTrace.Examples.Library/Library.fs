// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
/// A small book-lending domain, traced from F#: records with narrative
/// summaries, interfaces carrying the enrichment attributes, and in-memory
/// implementations. The trace reads the F# names as written — no
/// configuration, and no code in the services knows it is traced.
namespace NarrativeTrace.Examples.Library

open System
open NarrativeTrace.Core.Annotation

/// Thrown by the catalog for an unknown ISBN.
type BookNotFoundException(isbn: string) =
    inherit Exception(sprintf "Book not found: %s" isbn)

/// Thrown by the lending service when the copy is already on loan.
type BookUnavailableException(isbn: string) =
    inherit Exception(sprintf "Book not available: %s" isbn)

type Book =
    { Isbn: string
      Title: string
      Author: string
      Available: bool }

    /// How the book reads inside a trace: title and author, not five fields.
    [<NarrativeSummary>]
    member this.NarrativeSummary = sprintf "%s by %s" this.Title this.Author

type Member =
    { Id: string
      Name: string
      CardNumber: string }

    /// The card number never enters the trace: the summary is the name alone.
    [<NarrativeSummary>]
    member this.NarrativeSummary = this.Name

type LoanReceipt =
    { BookTitle: string
      MemberName: string
      DueDate: DateOnly }

    [<NarrativeSummary>]
    member this.NarrativeSummary =
        sprintf "%s loaned to %s, due %s" this.BookTitle this.MemberName (this.DueDate.ToString("yyyy-MM-dd"))

type ICatalogService =
    [<OnError("Book {isbn} not found in catalog", ExceptionType = typeof<BookNotFoundException>)>]
    abstract FindBook: isbn: string -> Book

type IMemberService =
    abstract LookupMember: memberId: string * [<NotTraced>] cardNumber: string -> Member

type ILendingService =
    [<Narrated("Borrowing book {isbn} for member {memberId}")>]
    abstract BorrowBook: memberId: string * isbn: string -> LoanReceipt

type InMemoryCatalogService() =
    let books =
        Map.ofList
            [ "978-0-13-468599-1",
              { Isbn = "978-0-13-468599-1"
                Title = "The Pragmatic Programmer"
                Author = "David Thomas & Andrew Hunt"
                Available = true }
              "978-0-201-63361-0",
              { Isbn = "978-0-201-63361-0"
                Title = "Design Patterns"
                Author = "Gang of Four"
                Available = true }
              "978-0-13-235088-4",
              { Isbn = "978-0-13-235088-4"
                Title = "Clean Code"
                Author = "Robert C. Martin"
                Available = false } ]

    interface ICatalogService with
        member _.FindBook(isbn) =
            match Map.tryFind isbn books with
            | Some book -> book
            | None -> raise (BookNotFoundException isbn)

type InMemoryMemberService() =
    let members =
        Map.ofList
            [ "M-001",
              { Id = "M-001"
                Name = "Alice"
                CardNumber = "4111-XXXX-XXXX-1234" }
              "M-002",
              { Id = "M-002"
                Name = "Bob"
                CardNumber = "5500-XXXX-XXXX-5678" } ]

    interface IMemberService with
        member _.LookupMember(memberId, _cardNumber) =
            match Map.tryFind memberId members with
            | Some m -> m
            | None -> invalidArg "memberId" (sprintf "Member not found: %s" memberId)

type DefaultLendingService(catalog: ICatalogService, members: IMemberService) =
    interface ILendingService with
        member _.BorrowBook(memberId, isbn) =
            let book = catalog.FindBook isbn

            if not book.Available then
                raise (BookUnavailableException isbn)

            let m = members.LookupMember(memberId, "CARD-VERIFY")

            { BookTitle = book.Title
              MemberName = m.Name
              DueDate = DateOnly.FromDateTime(DateTime.Today).AddDays 14 }
