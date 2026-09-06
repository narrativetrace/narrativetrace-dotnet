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
internal sealed record MethodMetadata(
    string ClassName,
    string MethodName,
    string[] ParameterNames,
    HashSet<int> RedactedIndices,
    string? NarrationTemplate,
    OnErrorAttribute[] ErrorTemplates,
    string? Namespace = null,
    string? ReturnType = null,
    string[]? ParameterTypes = null);
