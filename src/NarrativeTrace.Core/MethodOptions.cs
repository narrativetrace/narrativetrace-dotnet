// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Core;

/// <summary>
/// Per-call overrides for how one traced method reads in the narrative.
/// </summary>
/// <remarks>
/// Everything here is optional: pass <see langword="null"/> for the whole record
/// and the narrative is derived from the method name by splitting it into
/// words. Supply it only where the derived wording is
/// wrong or too thin. The usual source is a <c>[Narrated]</c> / <c>[OnError]</c>
/// attribute, resolved once and handed to
/// <see cref="INarrativeContext.EnterMethod"/>.
/// </remarks>
/// <param name="Narration">
/// Replacement prose for this call, used instead of the name-derived phrase.
/// Written in the same voice as the surrounding narrative ("charges the card",
/// not "ChargeCard called").
/// </param>
/// <param name="ErrorContext">
/// What it means when this call fails, used only on the error path. Set at enter
/// time and superseded by any context resolved at throw time — see
/// <see cref="INarrativeContext.ExitMethodWithException(Exception?, string?, SpanId?)"/>.
/// </param>
/// <param name="Namespace">
/// The declaring type's namespace, for the identity fields of the canonical
/// export. Narrative rendering never uses it.
/// </param>
/// <param name="ReturnType">
/// The declared return type as <see cref="Type.ToString()"/> renders it, for
/// the same reason.
/// </param>
/// <param name="NarrationTemplate">
/// The unresolved <c>[Narrated]</c> template behind <paramref name="Narration"/>,
/// carried through so translated views can re-render it per locale.
/// </param>
public sealed record MethodOptions(
    string? Narration = null,
    string? ErrorContext = null,
    string? Namespace = null,
    string? ReturnType = null,
    string? NarrationTemplate = null);
