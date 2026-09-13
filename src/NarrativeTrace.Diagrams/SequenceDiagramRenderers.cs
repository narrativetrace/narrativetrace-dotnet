// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;

namespace NarrativeTrace.Diagrams;

/// <summary>
/// The one place the NUnit and xUnit test integrations, and the flat trace
/// output writer, get the two sequence-diagram renderers from — instead of
/// each naming <see cref="MermaidSequenceRenderer"/> and
/// <see cref="PlantUmlSequenceRenderer"/> itself.
/// </summary>
/// <remarks>
/// <c>NarrativeTrace.Core</c> cannot depend on this module — Diagrams
/// depends on Core, never the reverse — so a Core type such as
/// <see cref="TraceArtifactRenderers"/> takes the two renderers as
/// <c>Func&lt;TraceTree, string&gt;</c> parameters (this runtime's
/// structurally-typed equivalent of a single-method interface — a static
/// method whose signature matches converts to the delegate without an
/// explicit <c>implements</c>) rather than naming their concrete types.
/// <c>NarrativeTestBase</c> (NUnit),
/// <c>NarrativeFixture</c> and <c>TraceOutputWriter</c> (xUnit) already
/// depend on this module to supply those parameters; before this class
/// existed, each referenced <c>MermaidSequenceRenderer.Render</c> and
/// <c>PlantUmlSequenceRenderer.Render</c> directly — the duplication cluster
/// this class replaces with one shared source.
/// </remarks>
public static class SequenceDiagramRenderers
{
    /// <summary>A Mermaid renderer, stateless — every call is equivalent to any other.</summary>
    public static Func<TraceTree, string> Mermaid => MermaidSequenceRenderer.Render;

    /// <summary>A PlantUML renderer, stateless — every call is equivalent to any other.</summary>
    public static Func<TraceTree, string> PlantUml => PlantUmlSequenceRenderer.Render;
}
