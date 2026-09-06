// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.SecurityTests.Corpus;

/// <summary>
/// One declarative <see cref="NarrativeTrace.Core.TraceNode"/> call-tree shape from
/// <c>tree-shapes.json</c>.
/// </summary>
/// <param name="Id">Stable kebab-case identifier.</param>
/// <param name="Description">What breaks.</param>
/// <param name="Kind">The shape: <c>chain</c> (a linear call chain <see cref="N"/> nodes deep) or <c>cycle</c> (a ring of <see cref="N"/> nodes; <c>n: 1</c> is a self-holding node).</param>
/// <param name="N">The shape's size — chain depth or ring length.</param>
public sealed record TreeShapeCase(string Id, string Description, string Kind, int N)
{
    /// <inheritdoc />
    public override string ToString() => Id;
}
