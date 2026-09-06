// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.SecurityTests.Corpus;

/// <summary>
/// One template string from <c>templates.json</c>.
/// </summary>
/// <param name="Id">Stable kebab-case identifier.</param>
/// <param name="Description">What the case is testing.</param>
/// <param name="Template">The materialized template.</param>
/// <param name="Values">
/// Names the fixture graph the template is resolved against (<c>card</c>, <c>user</c>,
/// <c>order</c>, <c>deep</c>, <c>unicode</c>, <c>wide</c>, <c>chain</c>); see
/// <see cref="HostileGraphs.TemplateValues"/>.
/// </param>
/// <param name="Expect">
/// <c>"redacted"</c> when the result must carry the redaction marker and not the secret,
/// <c>null</c> when only the universal oracles apply.
/// </param>
public sealed record TemplateCase(string Id, string Description, string Template, string? Values, string? Expect)
{
    /// <summary>Whether this case pins the redaction invariant rather than only the universal oracles.</summary>
    public bool ExpectsRedaction => Expect == "redacted";

    /// <inheritdoc />
    public override string ToString() => Id;
}
