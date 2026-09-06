// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Core;

/// <summary>
/// Bounds on how far a captured value is rendered before it is truncated, and
/// which values are redacted outright.
/// </summary>
/// <remarks>
/// <para>
/// These limits are what keep a trace bounded when instrumentation meets a large
/// object graph, a long string, or a cycle: every one is a hard stop applied
/// during rendering, so a pathological argument costs a predictable amount of
/// output rather than exhausting memory. Truncation is lossy and not reversible
/// from the trace — raise the limits if you need the detail, rather than trying
/// to reconstruct it downstream.
/// </para>
/// <para>
/// Governs individual values only. What surrounds the trace is
/// <see cref="MarkdownOptions"/>'s job.
/// </para>
/// </remarks>
/// <param name="MaxStringLength">Longest rendered string before truncation, in characters.</param>
/// <param name="MaxArrayItems">How many elements of a sequence are rendered before the rest are elided.</param>
/// <param name="MaxObjectKeys">How many properties of an object are rendered before the rest are elided.</param>
/// <param name="MaxDepth">
/// How deep nested objects are followed. This is also the cycle guard: a
/// self-referencing graph terminates here rather than recursing forever, so
/// lowering it too far silently flattens legitimately deep values.
/// </param>
/// <param name="Redaction">
/// The redaction policy, or <see langword="null"/> to use the secure default.
/// Read it back through <see cref="RedactionPolicy"/>, which resolves the
/// default — reading this property directly yields <see langword="null"/> and
/// would skip redaction entirely.
/// </param>
public sealed record RenderOptions(
    int MaxStringLength = 200,
    int MaxArrayItems = 5,
    int MaxObjectKeys = 5,
    int MaxDepth = 4,
    RedactionPolicy? Redaction = null)
{
    /// <summary>
    /// The name-based redaction policy; defaults to the secure
    /// <see cref="RedactionPolicy.Default"/> when unset.
    /// </summary>
    public RedactionPolicy RedactionPolicy =>
        Redaction ?? RedactionPolicy.Default;
}
