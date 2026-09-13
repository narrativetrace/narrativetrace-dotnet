// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.ContractProbe.Fixtures;
using NarrativeTrace.Core.Annotation;

namespace NarrativeTrace.ContractProbe.Probes;

/// <summary>
/// <c>probed-default</c>: a throwing <c>[NarrativeSummary]</c> member degrades to a typed
/// <c>&lt;error: TypeName&gt;</c> placeholder — the exception's own message is excluded, since a
/// message routinely interpolates the value the render was protecting — privacy-and-redaction.md
/// "Guarantees".
/// </summary>
internal static class TypedErrorMarkerProbe
{
    public sealed class Unrenderable
    {
        [NarrativeSummary]
        public string Summary => throw new InvalidOperationException(
            "cannot render this — leaking a secret here would be a bug");
    }

    public interface IFactory
    {
        Unrenderable Make();
    }

    private sealed class Factory : IFactory
    {
        public Unrenderable Make() => new();
    }

    public static string Observe()
    {
        var rendered = TracedRender.Render<IFactory>(new Factory(), proxy => proxy.Make());

        if (rendered.Contains("leaking a secret here would be a bug", StringComparison.Ordinal))
            return "message-leaked";

        return rendered.Contains("<error: InvalidOperationException>", StringComparison.Ordinal)
            ? "<error: InvalidOperationException>"
            : "no-marker";
    }
}
