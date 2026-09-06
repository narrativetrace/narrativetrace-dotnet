// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.SecurityTests.Corpus;

/// <summary>
/// One declarative object-graph shape from <c>graphs.json</c>.
/// </summary>
/// <remarks>
/// Exactly one of <see cref="Layers"/> and <see cref="Kind"/> drives construction:
/// <see cref="Layers"/> stacks wrappers outward around the payload, <see cref="Kind"/> names a
/// shape a stack cannot express. See the corpus README for the table.
/// </remarks>
/// <param name="Id">Stable kebab-case identifier.</param>
/// <param name="Description">What breaks.</param>
/// <param name="Kind">Shape name, or <c>null</c> when <see cref="Layers"/> applies.</param>
/// <param name="Layers">Wrapper kinds, innermost first.</param>
/// <param name="Layer">The wrapper repeated by <c>repeatLayer</c>.</param>
/// <param name="Container">The container used by <c>width</c> and <c>selfInCollection</c>.</param>
/// <param name="Member">The misbehaving member used by <c>hostileMember</c>.</param>
/// <param name="State">The <c>future</c>/<c>throwable</c> variant.</param>
/// <param name="Payload"><c>"secret-record"</c> when the builder must plant a sentinel-bearing record.</param>
/// <param name="N">The shape's size — depth, width, ring length or field count.</param>
public sealed record GraphCase(
    string Id,
    string Description,
    string? Kind,
    IReadOnlyList<string> Layers,
    string? Layer,
    string? Container,
    string? Member,
    string? State,
    string? Payload,
    int N)
{
    /// <summary>Whether this shape carries a <c>[NotTraced]</c> sentinel the redaction oracle can look for.</summary>
    public bool CarriesSecret => Payload == "secret-record";

    /// <inheritdoc />
    public override string ToString() => Id;
}
