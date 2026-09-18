// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Core.Annotation;

/// <summary>
/// Marks a type's own elements safe to enumerate during rendering, even
/// though the type is not platform-defined.
/// </summary>
/// <remarks>
/// The third sanctioned rendering hook, alongside <see cref="NarrativeSummaryAttribute"/>
/// and a stateless leaf's own <c>ToString()</c>: rendering never enumerates a
/// hand-rolled <see cref="System.Collections.IEnumerable"/> (see
/// <c>PlatformTypes</c> and the collection-origin dispatch in
/// <see cref="ValueRenderer"/>), because a type's own iterator is code the
/// renderer does not otherwise trust. Declaring this attribute is the
/// author's own assertion that the type's <c>GetEnumerator()</c> is a pure
/// state read with no side effects — the renderer then walks it exactly
/// like a platform collection: under the rendering guard, bounded by the
/// same element cap as every other collection walk, with a throwing
/// enumerator degrading to the typed failure marker like any other hook.
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
public sealed class NarrativeElementsAttribute : Attribute
{
}
