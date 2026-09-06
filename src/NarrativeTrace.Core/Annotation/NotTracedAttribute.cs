// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Core.Annotation;

/// <summary>
/// Excludes a value from trace output. On a method parameter, the proxy
/// captures the redaction marker instead of the argument. On a property or
/// field, <see cref="ValueRenderer"/> substitutes the marker during
/// reflective introspection — independent of the name-based
/// <see cref="RedactionPolicy"/>, so secrets whose names match no deny-list
/// pattern stay out of traces (JVM-edition parity: PARAMETER, FIELD,
/// RECORD_COMPONENT targets).
/// </summary>
[AttributeUsage(
    AttributeTargets.Parameter
    | AttributeTargets.Property
    | AttributeTargets.Field)]
public sealed class NotTracedAttribute : Attribute;
