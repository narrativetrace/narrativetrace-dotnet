// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Examples.Common;
using NarrativeTrace.Examples.Library;
using Xunit;

namespace NarrativeTrace.Examples.Library.Tests;

/// <summary>The F# example, driven and asserted from C# across the language boundary.</summary>
public sealed class LibraryExampleTests
{
    private static string RunExample()
    {
        using var output = new StringWriter();
        using var run = new DemoRun(new ConsoleLoggerFactory(output, LogFormat.Bare), "library");
        LibraryExample.run(run);
        return output.ToString();
    }

    [Fact]
    public void Run_borrows_a_book_then_fails_on_an_unavailable_one()
    {
        var text = RunExample();

        var headers = text.Split('\n').Where(l => l.StartsWith("=== ", StringComparison.Ordinal)).ToList();
        Assert.Equal(["=== Scenario 1: Successful Book Borrow ===", "=== Scenario 2: Book Unavailable ==="], headers);
        Assert.Equal(2, text.Split("--- Trace tree ---").Length - 1);
        Assert.Equal(2, text.Split("--- Prose ---").Length - 1);
        Assert.Equal(1, text.Split("--- Mermaid ---").Length - 1);
    }

    [Fact]
    public void FSharp_interfaces_carry_narration_redaction_and_error_context_into_the_trace()
    {
        var text = RunExample();

        Assert.Contains("→ ILendingService.BorrowBook(memberId: \"M-001\", isbn: \"978-0-13-468599-1\")", text, StringComparison.Ordinal);
        Assert.Contains("// Borrowing book 978-0-13-468599-1 for member M-001", text, StringComparison.Ordinal);
        Assert.Contains("cardNumber: [REDACTED]", text, StringComparison.Ordinal);
        Assert.Contains("The Pragmatic Programmer by David Thomas & Andrew Hunt", text, StringComparison.Ordinal);
        Assert.Contains("!! BookUnavailableException: Book not available: 978-0-13-235088-4", text, StringComparison.Ordinal);
        Assert.DoesNotContain("4111-XXXX", text, StringComparison.Ordinal);
    }
}
