// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.TestingXunit;

/// <summary>
/// Artifact formats <see cref="TraceOutputWriter"/> can emit for one test.
/// </summary>
/// <remarks>
/// A writer-level selector, not a renderer setting: each member names both a
/// rendering and the file extension it is written under, and several may be
/// requested for the same trace.
/// </remarks>
public enum TraceFormat
{
    /// <summary>Markdown narrative (<c>.md</c>) — the readable default.</summary>
    Markdown,

    /// <summary>Mermaid sequence diagram (<c>.mmd</c>), rendered inline by GitLab and GitHub.</summary>
    Mermaid,

    /// <summary>PlantUML sequence diagram (<c>.puml</c>).</summary>
    PlantUml,

    /// <summary>Canonical trace JSON (<c>.json</c>) — the machine-readable interchange format.</summary>
    Json,

    /// <summary>
    /// Clarity scores and issues for the trace (<c>.clarity.json</c>). Note this
    /// exports the naming analysis, not the trace itself.
    /// </summary>
    ClarityJson,
}
