// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Text;

namespace NarrativeTrace.Tooling.Frameworks;

/// <summary>
/// The framework table, rendered for readers: the framework table in <c>llms-full.md</c> and the
/// covered-frameworks line in <c>llms.txt</c>.
/// </summary>
/// <remarks>
/// INTENT: the doctor, the docs and the skill all answer "which frameworks, which package, which lines"
/// — so all three read <see cref="FrameworkTable"/>, and none is typed. Each rendered block sits between
/// <c>&lt;!-- &lt;name&gt;:begin --&gt;</c>/<c>&lt;!-- &lt;name&gt;:end --&gt;</c> markers; the skills
/// devtool's <c>render</c> rewrites them and a drift test fails while they disagree. The wiring snippets
/// inside the table block carry the same <c>snippet:</c> markers as the embedded resource, so
/// <c>SnippetCheck</c> holds the page to the compiled fixtures as well. No NarrativeTrace version is
/// rendered: the one version literal a public page carries is the install coordinate.
/// </remarks>
public static class FrameworkTableDocs
{
    /// <summary>The complete reference, repository-relative.</summary>
    public const string LlmsFull = "documentation/guides/llms-full.md";

    /// <summary>The agent entry point, repository-relative.</summary>
    public const string LlmsTxt = "documentation/guides/llms.txt";

    private const string TableBlock = "framework-table";
    private const string CoveredBlock = "covered-frameworks";

    /// <summary><c>llms-full.md</c> with its framework-table block re-rendered.</summary>
    public static string RenderLlmsFull(string document) => Splice(document, TableBlock, LlmsFullSection());

    /// <summary><c>llms.txt</c> with its covered-frameworks line re-rendered.</summary>
    public static string RenderLlmsTxt(string document) => Splice(document, CoveredBlock, LlmsTxtLine() + "\n");

    /// <summary>The table and every source-wired row's snippet, for <c>llms-full.md</c>.</summary>
    public static string LlmsFullSection()
    {
        var rendered = new StringBuilder()
            .Append("### Framework table — what the doctor checks\n\n")
            .Append("Rendered from the doctor's own framework table. For every row it can observe, ")
            .Append("`dotnet-narrativetrace doctor` runs the named check: the framework is detected but its ")
            .Append("package is not referenced, or the package is in use but its wiring was never applied — ")
            .Append("and the failing fix prints the lines below.\n\n")
            .Append("| Framework | Detected by | Add | Wiring | Doctor check |\n")
            .Append("|---|---|---|---|---|\n");
        foreach (var row in FrameworkTable.Rows)
        {
            rendered.Append(TableRow(row));
        }

        foreach (var row in FrameworkTable.Rows.Where(row => row.Wiring is Wiring.Snippet))
        {
            rendered.Append(SnippetSection(row));
        }

        return rendered.ToString();
    }

    /// <summary>One line naming every row, in table order, for <c>llms.txt</c>.</summary>
    public static string LlmsTxtLine() =>
        "Covered frameworks (each row of the doctor's framework table): " +
        string.Join("; ", FrameworkTable.Rows.Select(row => $"{row.Name} ({Coverage(row.Check)})")) + ".";

    /// <summary>
    /// <paramref name="document"/> with everything between the <paramref name="name"/> markers replaced.
    /// A document checked out with CRLF line endings gets the content in CRLF too.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// The document lacks either marker, has them out of order, or carries the begin marker twice —
    /// which block to rewrite would be a guess.
    /// </exception>
    internal static string Splice(string document, string name, string content)
    {
        ArgumentNullException.ThrowIfNull(document);
        var begin = $"<!-- {name}:begin -->";
        var end = $"<!-- {name}:end -->";
        var from = document.IndexOf(begin, StringComparison.Ordinal);
        var newline = from < 0 ? -1 : LineEndAfter(document, from + begin.Length);
        var to = document.IndexOf(end, StringComparison.Ordinal);
        if (from < 0 || newline < 0 || to < newline)
        {
            throw new ArgumentException($"the document has no {name}:begin / {name}:end marker pair", nameof(document));
        }

        if (document.IndexOf(begin, from + begin.Length, StringComparison.Ordinal) >= 0)
        {
            throw new ArgumentException($"the document carries the {name}:begin marker twice", nameof(document));
        }

        var crlf = document.Contains("\r\n", StringComparison.Ordinal);
        return document[..newline] + (crlf ? content.Replace("\n", "\r\n", StringComparison.Ordinal) : content) + document[to..];
    }

    /// <summary>The index after the line break that ends the marker line, or -1 when none follows it directly.</summary>
    private static int LineEndAfter(string document, int at)
    {
        if (document.AsSpan(at).StartsWith("\r\n"))
        {
            return at + 2;
        }

        return document.AsSpan(at).StartsWith("\n") ? at + 1 : -1;
    }

    private static string TableRow(FrameworkRow row) =>
        "| " + string.Join(
            " | ",
            row.Name,
            row.Marker.Description,
            Code(row.Package.AddCommand(null)),
            row.Wiring.Description,
            Check(row.Check)) + " |\n";

    private static string SnippetSection(FrameworkRow row)
    {
        var snippet = (Wiring.Snippet)row.Wiring;
        var entry = WiringSnippets.Entry(row.Id);
        var region = entry.Region is null ? string.Empty : $" region={entry.Region}";
        return $"\n#### {row.Name} — wiring\n\n<!-- snippet: {entry.Fixture}{region} -->\n" +
            $"```{snippet.Language}\n{entry.Body}```\n<!-- /snippet -->\n";
    }

    private static string Check(CheckBinding binding) => binding switch
    {
        CheckBinding.WiringCheck check => Code(check.Id),
        CheckBinding.ExistingCheck check => Code(check.Id) + " (existing)",
        CheckBinding.NoCheck none => $"none — {none.Reason}",
        _ => throw new ArgumentOutOfRangeException(nameof(binding)),
    };

    private static string Coverage(CheckBinding binding) => binding switch
    {
        CheckBinding.WiringCheck check => Code(check.Id),
        CheckBinding.ExistingCheck check => Code(check.Id),
        CheckBinding.NoCheck none => none.Reason,
        _ => throw new ArgumentOutOfRangeException(nameof(binding)),
    };

    private static string Code(string text) => $"`{text}`";
}
