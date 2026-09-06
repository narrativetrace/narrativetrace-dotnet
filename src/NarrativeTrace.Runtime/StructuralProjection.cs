// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;

using System.Diagnostics;

namespace NarrativeTrace.Runtime;

/// <summary>
/// Projects a <see cref="CanonicalEntry"/> to its AI-safe structural form —
/// Level 1 ("Structure Only") of the AI output ladder (ADR-002).
/// </summary>
/// <remarks>
/// <para>
/// The structural artifact is the value-free projection of the same capture the
/// canonical export serializes: developer-authored structure (names, call
/// shape, outcomes, timing) with every runtime-value field elided. Safety is
/// architectural — the value fields do not exist in the output, so neither a
/// prompt-injection payload nor private data can reach an AI consumer.
/// </para>
/// <para>
/// This projection is deliberately the <b>last</b> step before serialization:
/// it consumes fully-valued canonical entries. Higher output levels
/// (pseudonymized, selective) replace this per-field policy while reading the
/// same input, so values must never be stripped upstream of this seam.
/// </para>
/// <para>
/// Parameters keep their names — a name is source code, not data — and carry
/// the self-describing <c>[ELIDED]</c> value, mirroring the <c>[REDACTED]</c>
/// convention, because the schema requires a string value per parameter.
/// </para>
/// <para>
/// <b>Stance for new schema fields:</b> fields flow through by default, which
/// is right for identity-shaped ones (declared types, thread and resource
/// identity, source location — source or environment identity, never runtime
/// content). Any new <em>value</em>-shaped field must be nulled here
/// explicitly and seeded with hostile content in the property test.
/// </para>
/// </remarks>
public static class StructuralProjection
{
    /// <summary>The value every parameter carries once projected.</summary>
    public const string Elided = "[ELIDED]";

    /// <summary>Returns the value-free structural form of a canonical entry.</summary>
    /// <param name="entry">The fully-valued entry to project; must not be null.</param>
    /// <returns>
    /// The same entry with the return value, the exception message and every
    /// parameter value removed, and the message rebuilt from names alone.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="entry"/> is null.</exception>
    public static CanonicalEntry Project(CanonicalEntry entry)
    {
        if (entry is null)
        {
            throw new ArgumentNullException(nameof(entry));
        }

        var projected = entry with
        {
            Message = StructuralMessage(entry),
            NtParameters = Elide(entry.NtParameters),
            NtReturnValue = null,
            ExceptionMessage = null,
        };
        Debug.Assert(
            projected.NtReturnValue is null, "postcondition: return value elided");
        Debug.Assert(
            projected.ExceptionMessage is null, "postcondition: exception message elided");
        return projected;
    }

    private static string StructuralMessage(CanonicalEntry entry)
    {
        return entry.NtEventType switch
        {
            "method_enter" => "→ " + QualifiedName(entry) + "(" + ParameterNames(entry) + ")",
            "method_exit" => StructuralExitMessage(entry),
            _ => entry.Message,
        };
    }

    private static string StructuralExitMessage(CanonicalEntry entry)
    {
        if (entry.ExceptionType is { } type)
        {
            return "!! " + type;
        }

        var name = QualifiedName(entry);
        return entry.NtOutcome == "incomplete"
            ? "← " + name + " incomplete"
            : "← " + name + " returned";
    }

    private static string QualifiedName(CanonicalEntry entry)
    {
        return entry.CodeNamespace + "." + entry.CodeFunction;
    }

    private static string ParameterNames(CanonicalEntry entry)
    {
        return entry.NtParameters is null
            ? string.Empty
            : string.Join(", ", entry.NtParameters.Select(p => p.Name));
    }

    private static IReadOnlyList<ParameterCapture>? Elide(
        IReadOnlyList<ParameterCapture>? parameters)
    {
        if (parameters is null)
        {
            return null;
        }

        var elided = new List<ParameterCapture>(parameters.Count);
        foreach (var parameter in parameters)
        {
            elided.Add(
                new ParameterCapture(parameter.Name, Elided, parameter.Redacted));
        }

        return elided;
    }
}
