// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.SecurityTests.Corpus;

/// <summary>
/// One row of <c>redaction.json</c>: a sensitive field name, or a sensitive value shape.
/// </summary>
/// <remarks>
/// The .NET mirror of java's <c>RedactionCase</c>. The two redaction axes are one corpus, because a
/// runtime that implements the name deny-list and forgets the value shapes has half a control and no
/// way to notice. A row names either a field (<see cref="Name"/> plus the <see cref="Canary"/>
/// planted behind it) or a value (<see cref="Value"/>, which is its own canary because the shape
/// <em>is</em> the secret), and <see cref="Expect"/> says which way the assertion runs.
/// </remarks>
/// <param name="Id">Stable kebab-case identifier, quoted by a failing assertion so the case is findable.</param>
/// <param name="Description">What breaks, not what the bytes are.</param>
/// <param name="Name">The field name for a name case, <see langword="null"/> for a value case.</param>
/// <param name="Value">The value for a value case, <see langword="null"/> for a name case.</param>
/// <param name="Canary">The string planted behind <see cref="Name"/>; <see langword="null"/> for a value case.</param>
/// <param name="Expect"><c>"redacted"</c> or <c>"visible"</c>.</param>
/// <param name="Position">
/// <c>"mapKey"</c> to place a value case's <see cref="Value"/> as the key of a one-entry map rather
/// than rendering it bare; <see langword="null"/> (the default) for every other row. ADV-2026-09-14-1:
/// a value case's payload defaults to a bare top-level scalar, and every name case already places its
/// canary behind a field name in a one-entry map (see <see cref="Payload"/>), so without this field the
/// corpus could only ever put a value-shaped secret where the renderer's value-shape axis was already
/// known to look — never in a map KEY position, which is exactly where a dedicated map-key renderer
/// was found (family-wide) to skip that axis.
/// </param>
/// <param name="Kind">
/// The name of one of <see cref="HostileGraphs"/>'s composite builders (a curated <c>ToString</c>, a
/// sensitive map key, a throwing summary), or <see langword="null"/> for a name or value row. Unlike a
/// name or value row — which the value renderer alone can settle — a <c>kind</c> row is meaningless
/// replayed through <see cref="NarrativeTrace.Core.ValueRenderer"/> directly; what it adds is the
/// capture path one layer up: a traced method call, parameter binding and every rendered artifact.
/// </param>
public sealed record RedactionCase(
    string Id,
    string Description,
    string? Name,
    string? Value,
    string? Canary,
    string Expect,
    string? Position = null,
    string? Kind = null)
{
    private const string MapKeyPosition = "mapKey";

    /// <summary>The ordinary, visible value paired with a map-key case's secret key — it must survive.</summary>
    public const string MapKeyCompanionValue = "visible-value";

    /// <summary>Whether the canary must appear in no byte of any output.</summary>
    public bool ExpectsRedaction => Expect == "redacted";

    /// <summary>Whether this row names a field rather than carrying a bare value or a composite kind.</summary>
    public bool IsName => Name is not null;

    /// <summary>Whether this row names one of <see cref="HostileGraphs"/>'s composite builders.</summary>
    public bool IsKind => Kind is not null;

    /// <summary>Whether a value case places <see cref="Value"/> as a map key rather than rendering it bare.</summary>
    public bool IsMapKeyCase => Position == MapKeyPosition;

    /// <summary>The string the oracle looks for: the canary for a name or kind case, the value itself for a value case.</summary>
    public string Secret => IsName || IsKind ? Canary! : Value!;

    /// <summary>
    /// The object to render: the value alone, the value as a map key, a one-entry dictionary under
    /// the sensitive field name, or the composite <see cref="Kind"/> builds around <see cref="Canary"/>.
    /// </summary>
    /// <remarks>
    /// A dictionary is the vehicle for name cases because a record component has to be a
    /// compile-time identifier and these names are data — including two spellings of the same
    /// Spanish word that differ only by Unicode normalization form. A value case opts into the same
    /// vehicle, as the KEY rather than the value, via <see cref="IsMapKeyCase"/>. A kind row delegates
    /// to <see cref="HostileGraphs.Build"/>, the same builder <c>graphs.json</c>'s own rows use, via a
    /// <see cref="GraphCase"/> that carries only <see cref="Kind"/> — the other <see cref="GraphCase"/>
    /// fields go unused by every kind these four rows name.
    /// </remarks>
    public object Payload
    {
        get
        {
            if (IsName)
            {
                return new Dictionary<string, object> { [Name!] = Canary! };
            }

            if (IsKind)
            {
                return HostileGraphs.Build(KindGraphCase(), Canary!);
            }

            return IsMapKeyCase
                ? new Dictionary<string, object> { [Value!] = MapKeyCompanionValue }
                : Value!;
        }
    }

    private GraphCase KindGraphCase() =>
        new(Id, Description, Kind, Array.Empty<string>(), null, null, null, null, "secret-record", 0);

    /// <inheritdoc />
    public override string ToString() => Id;
}
