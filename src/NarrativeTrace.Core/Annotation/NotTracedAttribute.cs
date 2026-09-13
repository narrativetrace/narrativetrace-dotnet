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
/// <remarks>
/// <see cref="AttributeTargets.Method"/> is accepted syntactically — so a
/// misplaced <c>[NotTraced]</c> on a whole method compiles rather than
/// failing with a generic <c>CS0592</c> that names neither this attribute
/// nor the fix — but carries no meaning of its own: the JVM edition's
/// <c>@NotTraced</c> has no <c>METHOD</c> target either, so there is no
/// documented "whole call redacted" behavior to mirror. A tracing proxy
/// created over an interface with <c>[NotTraced]</c> on one of its methods
/// throws at proxy-creation time (<c>NarrativeTraceProxy.Create</c>, in
/// <c>NarrativeTrace.Proxy</c>), naming the method and pointing at
/// annotating its parameters, property, or record component instead.
/// </remarks>
[AttributeUsage(
    AttributeTargets.Method
    | AttributeTargets.Parameter
    | AttributeTargets.Property
    | AttributeTargets.Field)]
public sealed class NotTracedAttribute : Attribute;
