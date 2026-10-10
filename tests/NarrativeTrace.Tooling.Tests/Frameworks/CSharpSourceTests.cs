// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Tooling.Frameworks;
using Xunit;

namespace NarrativeTrace.Tooling.Tests.Frameworks;

/// <summary>
/// Commented-out wiring is not wiring, but a comment marker inside a literal is text: the scanner must
/// tell the two apart for every literal form C# has.
/// </summary>
public sealed class CSharpSourceTests
{
    [Theory]
    [InlineData("a(); // b();", "a(); ")]
    [InlineData("a(); /* b(); */ c();", "a();   c();")]
    [InlineData("a();\n// b();\nc();", "a();\n\nc();")]
    [InlineData("a(); /* never closed b();", "a();  ")]
    public void Line_and_block_comments_are_removed(string source, string expected)
    {
        Assert.Equal(expected, CSharpSource.WithoutComments(source));
    }

    [Theory]
    [InlineData("var u = \"http://example.com\"; // gone")]
    [InlineData("var u = @\"C:\\\\x // \"\"quoted\"\" /* still text */\"; // gone")]
    [InlineData("var u = \"\"\"\n  raw // text \"\" /* here */\n  \"\"\"; // gone")]
    [InlineData("var u = $\"{x} // text\"; // gone")]
    [InlineData("var u = @$\"{x} // text\"; // gone")]
    [InlineData("var c = '/'; var d = '\\''; // gone")]
    [InlineData("var e = \"escaped \\\" // still text\"; // gone")]
    public void A_comment_marker_inside_a_literal_is_text_and_the_comment_after_it_is_not(string source)
    {
        var stripped = CSharpSource.WithoutComments(source);

        Assert.Equal(source[..source.LastIndexOf(" // gone", StringComparison.Ordinal)] + " ", stripped);
    }

    [Fact]
    public void An_unterminated_string_ends_at_its_line_so_the_next_lines_comment_is_still_a_comment()
    {
        Assert.Equal("var s = \"open\n", CSharpSource.WithoutComments("var s = \"open\n// gone"));
    }

    [Fact]
    public void A_long_run_of_comment_openers_is_scanned_in_linear_time()
    {
        var source = string.Concat(Enumerable.Repeat("/* ", 100_000));

        Assert.Equal(" ", CSharpSource.WithoutComments(source));
    }
}
