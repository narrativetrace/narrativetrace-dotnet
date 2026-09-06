// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Core;

/// <summary>
/// Presentation settings for <see cref="MarkdownRenderer"/> — what surrounds the
/// trace and when a call is called out as slow.
/// </summary>
/// <remarks>
/// Purely about presentation: none of these affect what was captured, so
/// re-rendering the same <see cref="TraceTree"/> with different options is
/// always safe and lossless. There is no document-header switch — the header
/// needs a scenario outcome, so it lives on
/// <see cref="MarkdownRenderer.RenderDocument"/>, which takes a
/// <see cref="TraceMetadata"/>. Distinct from <see cref="RenderOptions"/>, which
/// governs how individual <i>values</i> are truncated and redacted; the two are
/// independent and usually both supplied.
/// </remarks>
/// <param name="ScenarioName">
/// Heading for the rendered trace, or <see langword="null"/> to omit the
/// heading entirely rather than emit a placeholder.
/// </param>
/// <param name="SlowThresholdMs">
/// Calls at or above this many milliseconds are flagged as slow. Cosmetic only —
/// nothing is filtered out by it. Set it high to suppress the annotation
/// altogether; 0 flags everything.
/// </param>
/// <param name="IncludeFrontmatter">
/// Whether to emit the YAML frontmatter block carrying
/// <see cref="TraceMetadata"/>. On by default because tooling reads it; turn it
/// off when embedding the trace inside a larger document, where a frontmatter
/// block partway down is not valid frontmatter.
/// </param>
public sealed record MarkdownOptions(
    string? ScenarioName = null,
    int SlowThresholdMs = 200,
    bool IncludeFrontmatter = true);
