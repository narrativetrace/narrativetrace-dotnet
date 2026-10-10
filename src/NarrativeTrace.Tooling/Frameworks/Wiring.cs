// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Text.RegularExpressions;

namespace NarrativeTrace.Tooling.Frameworks;

/// <summary>
/// The third column of the table: what a project does, beyond referencing the package, for the
/// integration to take effect — and how the doctor can tell it did.
/// </summary>
/// <param name="Description">The wiring in a reader's words.</param>
public abstract record Wiring(string Description)
{
    /// <summary>
    /// Wiring a project writes into its own source. The lines are NEVER typed here:
    /// <paramref name="Fixture"/> (repository-relative) and <paramref name="Region"/> name a compiled,
    /// tested fixture, and the text a reader sees is that fixture's, carried by
    /// <see cref="WiringSnippets"/>.
    /// </summary>
    /// <param name="Description">The wiring in a reader's words.</param>
    /// <param name="Fixture">The repository-relative path of the compiled fixture holding the lines.</param>
    /// <param name="Region">The <c>snippet:begin</c> region inside it, or <see langword="null"/> for the whole file.</param>
    /// <param name="Language">The fence language the lines render with.</param>
    /// <param name="Evidence">
    /// What in the project's sources proves the wiring was applied: EVERY pattern must be found, each
    /// in any source file (a registration in <c>Program.cs</c> and its counterpart in a startup class
    /// are one wiring). Alternatives a project may write instead belong inside one pattern.
    /// </param>
    public sealed record Snippet(
        string Description, string Fixture, string? Region, string Language, IReadOnlyList<Regex> Evidence)
        : Wiring(Description)
    {
        /// <summary>The fixture path, validated: not blank.</summary>
        public string Fixture { get; init; } = string.IsNullOrWhiteSpace(Fixture)
            ? throw new ArgumentException("a snippet names the fixture it comes from", nameof(Fixture))
            : Fixture;

        /// <summary>The evidence, validated: at least one pattern.</summary>
        public IReadOnlyList<Regex> Evidence { get; init; } = Evidence is { Count: > 0 }
            ? Evidence
            : throw new ArgumentException("source wiring needs evidence the doctor can look for", nameof(Evidence));

        /// <summary>Whether the project's source texts show the wiring applied: every pattern found somewhere.</summary>
        public bool AppliedIn(IReadOnlyCollection<string> sources) =>
            Evidence.All(pattern => sources.Any(pattern.IsMatch));
    }

    /// <summary>Wiring nobody applies on a project's behalf: the owner decides, outside the doctor's reach.</summary>
    /// <param name="Description">The opt-in in a reader's words.</param>
    public sealed record OwnerOptIn(string Description) : Wiring(Description);
}
