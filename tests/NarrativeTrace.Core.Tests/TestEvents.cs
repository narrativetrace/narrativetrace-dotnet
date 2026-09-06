// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Globalization;
using NarrativeTrace.Core;

namespace NarrativeTrace.Core.Tests;

/// <summary>
/// Builds trace events from small integer handles for tests, mapping each
/// handle to a deterministic <see cref="SpanId"/> under a shared
/// <see cref="TraceId"/>. Preserves the original int-handle test style
/// after the SpanContext migration: handle -1 means "no parent" (root).
/// </summary>
internal static class TestEvents
{
    private static readonly TraceId Trace =
        new("4bf92f3577b34da6a3ce929d0e0e4736");

    public static SpanId SpanId(int handle) =>
        new((handle + 1).ToString(
            "x16", CultureInfo.InvariantCulture));

    public static SpanContext Context(int handle, int parentHandle) =>
        new(
            Trace,
            SpanId(handle),
            parentHandle < 0 ? null : SpanId(parentHandle));

    public static EnterEvent Enter(
        int handle, int parentHandle, long timestampTicks,
        MethodSignature signature) =>
        new(Context(handle, parentHandle), timestampTicks, signature);

    public static ExitEvent Exit(
        int handle, long timestampTicks, TraceOutcome outcome) =>
        new(Context(handle, -1), timestampTicks, outcome);

    public static GraftEvent Graft(
        int parentHandle, long timestampTicks, TraceNode node) =>
        new(
            parentHandle < 0 ? null : SpanId(parentHandle),
            timestampTicks,
            node);
}
