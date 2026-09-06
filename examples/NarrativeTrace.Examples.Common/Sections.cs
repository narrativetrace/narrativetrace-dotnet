// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Examples.Common;

/// <summary>
/// The section markers the demo launcher recognises — identical to the Java
/// examples' markers so <c>demo/colorize.awk</c> serves both platforms.
/// </summary>
public static class Sections
{
    /// <summary>Marks the <c>IndentedTextRenderer</c> rendering.</summary>
    public const string TraceTree = "--- Trace tree ---";

    /// <summary>Marks the <c>ProseRenderer</c> rendering.</summary>
    public const string Prose = "--- Prose ---";

    /// <summary>Marks the Mermaid sequence-diagram markup.</summary>
    public const string Mermaid = "--- Mermaid ---";

    /// <summary>Marks the PlantUML sequence-diagram markup.</summary>
    public const string PlantUml = "--- PlantUML ---";

    /// <summary>The scenario header: <c>=== title ===</c>.</summary>
    public static string Title(string scenario)
    {
        return $"=== {scenario} ===";
    }
}
