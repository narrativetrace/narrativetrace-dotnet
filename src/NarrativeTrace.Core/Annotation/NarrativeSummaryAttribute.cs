// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Core.Annotation;

/// <summary>
/// Marks a public parameterless method (or property) as the preferred
/// summary for its declaring type.
/// </summary>
/// <remarks>
/// When <see cref="ValueRenderer"/> encounters an object whose type
/// exposes a member annotated with <see cref="NarrativeSummaryAttribute"/>,
/// it invokes that member before falling back to record rendering,
/// reflective property introspection, or <c>ToString()</c>. The member
/// must be public and take no parameters; its result is converted with
/// <c>ToString()</c>. If invocation throws, the whole value degrades to a
/// typed <c>&lt;error: TypeName&gt;</c> placeholder naming the caught
/// exception — it never falls back to rendering the type's fields, which
/// the summary was curated specifically to replace.
/// </remarks>
[AttributeUsage(
    AttributeTargets.Method | AttributeTargets.Property,
    AllowMultiple = false, Inherited = true)]
public sealed class NarrativeSummaryAttribute : Attribute
{
}
