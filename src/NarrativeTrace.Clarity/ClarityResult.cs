// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Clarity;

/// <summary>
/// The clarity scores for one analyzed trace, dimension by dimension, with the
/// issues behind them.
/// </summary>
/// <remarks>
/// <para>
/// Every score is a fraction from <c>0.0</c> (unreadable) to <c>1.0</c>
/// (ideal) — <b>not</b> a percentage. Multiply by 100 for display.
/// </para>
/// <para>
/// The dimensions are diagnostic and the weighting that combines them into
/// <see cref="Overall"/> is a cross-language contract shared with the Java
/// edition, so <see cref="Overall"/> is not simply the mean of the others and
/// should not be recomputed from them.
/// </para>
/// <para>
/// Scores describe naming, which is a heuristic judgement about readability —
/// treat a low score as a prompt to look, not as a defect.
/// </para>
/// </remarks>
/// <param name="Overall">
/// The weighted composite of the other dimensions, 0.0–1.0. The single number a
/// CI gate compares against a threshold.
/// </param>
/// <param name="Method">Mean method-name score: how well names read as verb + noun.</param>
/// <param name="Class">
/// Mean class-name score, computed over <b>distinct</b> class names — a class
/// called often does not weigh more heavily than one called once.
/// </param>
/// <param name="Parameter">
/// Mean parameter-name score. <b>Exactly <c>1.0</c> when the trace captured no
/// parameters at all</b>, since a trace of parameterless calls should not be
/// penalized — so a perfect score here can mean "nothing to judge" rather than
/// "judged and excellent".
/// </param>
/// <param name="Structural">
/// Shape rather than naming: how deep the call nesting ran and how wide the
/// widest parameter list was. The one dimension a rename cannot improve.
/// </param>
/// <param name="Cohesion">How well each class's method names hang together as one responsibility.</param>
/// <param name="Issues">
/// The actionable findings, ranked by <see cref="ClarityIssue.ImpactScore"/>.
/// Empty when nothing crossed the reporting threshold — which is not the same
/// as a perfect score, since scores are continuous and issues are not.
/// </param>
public sealed record ClarityResult(
    double Overall,
    double Method,
    double Class,
    double Parameter,
    double Structural,
    double Cohesion,
    IReadOnlyList<ClarityIssue> Issues);
