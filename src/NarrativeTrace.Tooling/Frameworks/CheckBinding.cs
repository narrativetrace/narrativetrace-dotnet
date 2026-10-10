// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Tooling.Frameworks;

/// <summary>
/// The fourth column of the table: which doctor check observes the row. Every row states one, so
/// "unchecked" is always a decision — a row whose wiring the doctor can see in manifest and source
/// earns its own <see cref="WiringCheck"/>; a row an existing check already covers names it
/// (<see cref="ExistingCheck"/>); a row nothing in text can show says so (<see cref="NoCheck"/>) —
/// never a vague pass.
/// </summary>
public abstract record CheckBinding
{
    private CheckBinding()
    {
    }

    /// <summary>A <c>config.&lt;framework&gt;-&lt;thing&gt;</c> check the doctor runs for this row.</summary>
    /// <param name="Id">The check id.</param>
    public sealed record WiringCheck(string Id) : CheckBinding
    {
        private const string Prefix = "config.";

        /// <summary>The id, validated: <c>config.</c> then kebab-case with at least one hyphen.</summary>
        public string Id { get; init; } =
            Id is not null && Id.StartsWith(Prefix, StringComparison.Ordinal)
                && Kebab.IsKebab(Id[Prefix.Length..]) && Id[Prefix.Length..].Contains('-')
                ? Id
                : throw new ArgumentException($"a framework check id is config.<framework>-<thing>, got {Id}", nameof(Id));
    }

    /// <summary>A check the doctor already runs for its own reasons covers this row.</summary>
    /// <param name="Id">The existing check's id.</param>
    public sealed record ExistingCheck(string Id) : CheckBinding;

    /// <summary>Nothing in a manifest or a source file can show this row's wiring.</summary>
    /// <param name="Reason">Why, in a reader's words.</param>
    public sealed record NoCheck(string Reason) : CheckBinding;
}
