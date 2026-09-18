// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace NarrativeTrace.Build;

/// <summary>
/// The structural gate behind <c>stryker-config.clarity.json</c>'s vocabulary-data
/// <c>mutate</c> excludes (owner ruling 2026-09-18, TODO §35, "the vocabulary data files are
/// excluded from mutation — data is not logic"): a source file earns a spot in that exclude
/// list ONLY when it structurally IS data, never because a "logic" file happens to score low.
/// Prevents the exclude list from quietly growing to cover real behaviour — a silent glob edit
/// could otherwise hide a computed method behind the same "it's just vocabulary" justification.
/// </summary>
/// <remarks>
/// A file is DATA when every field it declares is <c>static readonly</c> (no mutable state to
/// mutate around), and nowhere in the file — field initializer, method body, property accessor —
/// does a loop, an arithmetic operator (<c>+ - * / %</c>), or an ordering comparison
/// (<c>&lt; &lt;= &gt; &gt;=</c>, including a relational pattern like <c>&gt;= 2 and &lt;= 4</c>)
/// appear. A data file MAY still carry a thin lookup method — <c>.Contains</c>,
/// <c>.TryGetValue</c>, an indexer, a <c>switch</c>/ternary mapping to fixed constants — because
/// that is membership testing over the file's own literal sets, not computation. What it forfeits
/// is exactly the shape <c>MethodNameScorer</c> uses to turn measurements into a score
/// (<c>count is &gt;= 2 and &lt;= 4</c>, <c>token.Length &lt; 4</c>, a <c>for</c> loop summing
/// suffix lengths) — which is why pointing this check at a real logic file (see
/// <c>MutationVocabularyExclusionTests.A_logic_file_is_rejected_...</c>) comes back red.
/// </remarks>
internal static class MutationVocabularyExclusion
{
    /// <param name="IsDataFile">Whether the source qualifies for a mutate exclude.</param>
    /// <param name="Violation">
    /// Null when <see cref="IsDataFile"/> is true; otherwise the first disqualifying construct
    /// found, for a build failure message that names what to fix (or reconsider excluding).
    /// </param>
    public readonly record struct Verdict(bool IsDataFile, string? Violation);

    private static readonly SyntaxKind[] DisqualifyingBinaryOperators =
    [
        SyntaxKind.AddExpression, SyntaxKind.SubtractExpression,
        SyntaxKind.MultiplyExpression, SyntaxKind.DivideExpression, SyntaxKind.ModuloExpression,
        SyntaxKind.LessThanExpression, SyntaxKind.LessThanOrEqualExpression,
        SyntaxKind.GreaterThanExpression, SyntaxKind.GreaterThanOrEqualExpression,
    ];

    /// <summary>Evaluates the structural criterion against one C# source file's text.</summary>
    public static Verdict Evaluate(string sourceText)
    {
        var root = CSharpSyntaxTree.ParseText(sourceText).GetCompilationUnitRoot();

        foreach (var field in root.DescendantNodes().OfType<FieldDeclarationSyntax>())
        {
            if (!IsStaticReadonly(field))
            {
                var name = field.Declaration.Variables.First().Identifier.Text;
                return new Verdict(false, $"field '{name}' is not static readonly — mutable state is not data");
            }
        }

        var disqualifying = root.DescendantNodesAndSelf()
            .Select(DescribeIfDisqualifying)
            .FirstOrDefault(description => description is not null);

        return disqualifying is null
            ? new Verdict(true, null)
            : new Verdict(false, disqualifying);
    }

    private static bool IsStaticReadonly(FieldDeclarationSyntax field)
    {
        var hasStatic = field.Modifiers.Any(SyntaxKind.StaticKeyword);
        var hasReadonly = field.Modifiers.Any(SyntaxKind.ReadOnlyKeyword);
        return hasStatic && hasReadonly;
    }

    private static string? DescribeIfDisqualifying(SyntaxNode node)
    {
        switch (node)
        {
            case ForStatementSyntax or WhileStatementSyntax or DoStatementSyntax
                or ForEachStatementSyntax or ForEachVariableStatementSyntax:
                return $"a loop ({node.Kind()}) at '{Excerpt(node)}'";

            case BinaryExpressionSyntax binary when DisqualifyingBinaryOperators.Contains(binary.Kind()):
                return $"operator '{binary.OperatorToken.Text}' in '{Excerpt(binary)}'";

            case RelationalPatternSyntax relational:
                return $"a relational pattern '{Excerpt(relational)}'";

            default:
                return null;
        }
    }

    private static string Excerpt(SyntaxNode node)
    {
        var text = node.ToString();
        return text.Length <= 80 ? text : text[..80] + "…";
    }

    /// <param name="Reason">
    /// The one-line reason from the config's sibling <c>mutate-exclusion-reasons</c> block; empty
    /// when the excluded file name carries none — <see cref="MutationVocabularyExclusionTests"/>
    /// fails the build on that, so this can never silently ship empty.
    /// </param>
    public readonly record struct ExcludedFile(string FileName, string Reason);

    /// <summary>
    /// Every file a stryker-config's <c>mutate</c> array excludes (a <c>"!**/Name.cs"</c> glob
    /// entry), paired with its reason from the config's own sibling <c>mutate-exclusion-reasons</c>
    /// object — read fresh out of the config file, never a second hand-copied list, so the two
    /// cannot drift apart the way independently-maintained lists always eventually do.
    /// </summary>
    public static IReadOnlyList<ExcludedFile> ExcludedFiles(string configPath)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(configPath));
        var root = document.RootElement;

        var names = root.GetProperty("stryker-config").GetProperty("mutate")
            .EnumerateArray()
            .Select(entry => entry.GetString() ?? "")
            .Where(pattern => pattern.StartsWith('!') && pattern.EndsWith(".cs", StringComparison.Ordinal))
            .Select(pattern => pattern.TrimStart('!').TrimStart('*', '/'))
            .ToList();

        var reasons = root.TryGetProperty("mutate-exclusion-reasons", out var reasonsElement)
            ? reasonsElement.EnumerateObject()
                .ToDictionary(property => property.Name, property => property.Value.GetString() ?? "")
            : new Dictionary<string, string>();

        return names
            .Select(name => new ExcludedFile(name, reasons.TryGetValue(name, out var reason) ? reason : ""))
            .ToList();
    }
}
