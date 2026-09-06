// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Core;

/// <summary>
/// The identity and narration of one traced call: who was called, with what,
/// and how it should read.
/// </summary>
/// <remarks>
/// The captured form of a call, not a reflection handle — it holds rendered
/// strings rather than a <c>MethodInfo</c> or live argument objects, so a
/// finished trace never keeps domain objects alive and can be serialized long
/// after the call returned. Overloads are <b>not</b> distinguished: two methods
/// of the same name on the same class differ only by their captured parameter
/// list.
/// </remarks>
/// <param name="ClassName">The declaring type's simple name, as it reads in the narrative.</param>
/// <param name="MethodName">The method's name, used verbatim.</param>
/// <param name="Parameters">
/// The captured arguments in declaration order; empty for a no-arg call. Values
/// are blanked unless capture ran at <see cref="TracingLevel.Detail"/>, so an
/// empty rendered value means "not captured", not "was empty".
/// </param>
/// <param name="Narration">Prose overriding the name-derived phrase, or <see langword="null"/>.</param>
/// <param name="ErrorContext">What a failure here means, or <see langword="null"/>.</param>
/// <param name="Namespace">
/// The declaring type's namespace, or <see langword="null"/> when the capture
/// path had none to read. <paramref name="ClassName"/> stays the simple name
/// because that is what reads well in a narrative; this completes the identity
/// for machine consumers.
/// </param>
/// <param name="ReturnType">
/// The declared return type as <see cref="Type.ToString()"/> renders it
/// (<c>System.Void</c> for a void method), or <see langword="null"/> when
/// unknown. Declared, not the runtime type of the value that came back.
/// </param>
/// <param name="NarrationTemplate">
/// The raw <c>[Narrated]</c> template with its placeholders intact
/// (<c>"Opening for {customerId}"</c>), or <see langword="null"/> when the
/// method carried no template. <paramref name="Narration"/> is the same
/// template already resolved against the arguments; both are kept because a
/// translated view has to re-render the template per locale, which the resolved
/// prose can no longer support.
/// </param>
public sealed record MethodSignature(
    string ClassName,
    string MethodName,
    IReadOnlyList<ParameterCapture> Parameters,
    string? Narration = null,
    string? ErrorContext = null,
    string? Namespace = null,
    string? ReturnType = null,
    string? NarrationTemplate = null);
