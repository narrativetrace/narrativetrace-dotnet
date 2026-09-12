// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core.Annotation;

namespace NarrativeTrace.Proxy;

/// <summary>
/// Everything reflection can tell us about one traced method, resolved once and
/// cached: the names the narrative uses, the redaction decisions, the
/// annotation templates, and the declared identity the canonical export needs.
/// </summary>
/// <remarks>
/// Declared types are rendered with <see cref="Type.ToString()"/>, not
/// <see cref="Type.FullName"/>: FullName assembly-qualifies generic arguments,
/// which would put assembly versions into an artifact that has to stay
/// byte-identical between runs.
/// </remarks>
/// <param name="ClassName">The name reported for traced methods, absent a <see cref="ProxyOptions.ClassName"/> override.</param>
/// <param name="MethodName">The traced method's own name.</param>
/// <param name="ParameterNames">The display name of each parameter, honoring a <c>[Traced]</c> override.</param>
/// <param name="RedactedIndices">
/// Parameter positions redacted under <see cref="NarrativeTrace.Core.RedactionPolicy.Default"/> —
/// name-deny-listed or <c>[NotTraced]</c>. Used as-is when a proxy carries
/// no custom <see cref="ProxyOptions.Redaction"/>.
/// </param>
/// <param name="NarrationTemplate">The method's <see cref="NarratedAttribute"/> template, or <see langword="null"/>.</param>
/// <param name="ErrorTemplates">Every <see cref="OnErrorAttribute"/> declared on the method.</param>
/// <param name="Namespace">The declaring type's namespace.</param>
/// <param name="ReturnType">The declared return type, as <see cref="Type.ToString()"/> renders it.</param>
/// <param name="ParameterTypes">Each parameter's declared type, as <see cref="Type.ToString()"/> renders it.</param>
/// <param name="NotTracedIndices">
/// Parameter positions carrying <c>[NotTraced]</c> alone, independent of any
/// policy — always redacted, so a custom <see cref="ProxyOptions.Redaction"/>
/// (including <see cref="NarrativeTrace.Core.RedactionPolicy.Disabled"/>) can replace
/// the name-based decision without ever un-redacting an explicit annotation.
/// </param>
/// <param name="ReflectedParameterNames">
/// Each parameter's reflected (never <c>[Traced]</c>-overridden) name, tested
/// against a custom <see cref="ProxyOptions.Redaction"/> policy at capture time —
/// the same input <see cref="RedactedIndices"/> was computed from, so the two
/// paths cannot drift on which name a policy sees.
/// </param>
internal sealed record MethodMetadata(
    string ClassName,
    string MethodName,
    string[] ParameterNames,
    HashSet<int> RedactedIndices,
    string? NarrationTemplate,
    OnErrorAttribute[] ErrorTemplates,
    string? Namespace = null,
    string? ReturnType = null,
    string[]? ParameterTypes = null,
    HashSet<int>? NotTracedIndices = null,
    string?[]? ReflectedParameterNames = null);
