// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.ContractProbe.Fixtures;

namespace NarrativeTrace.ContractProbe.Probes;

/// <summary>
/// <c>probed-default</c>: <c>decimal</c> is the one BCL scalar this runtime's own history flagged
/// (it exposes exactly one public instance member, <c>Scale</c>) — without a carve-out, the
/// generic reflective walk that "a class exposing public state is rendered by reflecting over its
/// members" (<see cref="NativeStringificationNotTrustedProbe"/>) describes would explode it into
/// that one irrelevant field instead of its own value. This proves the carve-out still renders the
/// value itself, invariantly, never the reflected field.
/// </summary>
internal static class PlatformTypeCarveoutProbe
{
    public interface IFactory
    {
        decimal Make();
    }

    private sealed class Factory : IFactory
    {
        public decimal Make() => 449.97m;
    }

    public static string Observe()
    {
        var rendered = TracedRender.Render<IFactory>(new Factory(), proxy => proxy.Make());

        if (rendered.Contains("449.97", StringComparison.Ordinal))
            return "own-representation-used";

        return rendered.Contains("Scale", StringComparison.Ordinal) ? "field-walked" : "no-value";
    }
}
