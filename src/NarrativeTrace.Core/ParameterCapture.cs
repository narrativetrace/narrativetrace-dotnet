// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Core;

/// <summary>
/// One captured argument: its name and its value already rendered to a string.
/// </summary>
/// <remarks>
/// Holds no reference to the live argument — capture renders eagerly, so a
/// finished trace never keeps a domain object alive and never observes a
/// mutation that happened after the call. Values are subject to the
/// <see cref="RenderOptions"/> limits in force at capture time, so a rendered
/// value may be truncated.
/// </remarks>
/// <param name="Name">The parameter's declared name, used as its label in the narrative.</param>
/// <param name="RenderedValue">
/// The argument rendered to a string. Empty when capture ran below
/// <see cref="TracingLevel.Detail"/>, which suppresses values — so empty means
/// "not captured", not "was empty". <b>Do not render this directly</b> when
/// <paramref name="Redacted"/> is set; see that parameter.
/// </param>
/// <param name="Redacted">
/// Whether the value was withheld as sensitive. When set, renderers substitute
/// the redaction marker regardless of what <paramref name="RenderedValue"/>
/// holds — a deliberate backstop, since a capture path that flags a value but
/// forgets to blank it must still not leak. Any code reading
/// <paramref name="RenderedValue"/> for display must honour this flag.
/// </param>
/// <param name="StructuredValue">
/// The argument with its .NET type retained, for typed OTel export, or
/// <see langword="null"/> when only the flat rendering was captured.
/// </param>
/// <param name="DeclaredType">
/// The parameter's <em>declared</em> type, as <see cref="Type.ToString()"/>
/// renders it (<c>System.String</c>, <c>System.Int32[]</c>,
/// <c>System.Collections.Generic.List`1[System.Int32]</c>), or
/// <see langword="null"/> when the capture path had no reflection to read it
/// from. Declared, not runtime: it is source identity — with the return type it
/// is what makes overloads distinguishable — and it never depends on what was
/// passed. <see cref="Type.ToString()"/> rather than <see cref="Type.FullName"/>
/// because FullName assembly-qualifies generic arguments, which would put
/// assembly versions into an artifact that must not change between runs.
/// </param>
public sealed record ParameterCapture(
    string Name,
    string RenderedValue,
    bool Redacted,
    RenderedValue? StructuredValue = null,
    string? DeclaredType = null);

/// <summary>
/// Renderer-level redaction backstop: redacted captures always surface the
/// redaction marker, so a capture path that sets
/// <see cref="ParameterCapture.Redacted"/> but leaves a real value in
/// <see cref="ParameterCapture.RenderedValue"/> cannot leak it
/// (JVM-edition parity: every renderer re-checks the redaction flag).
/// Kept as an extension so the data record exposes no derived property to
/// reflective introspection.
/// </summary>
internal static class ParameterCaptureExtensions
{
    public static string DisplayValue(this ParameterCapture parameter)
    {
        return parameter.Redacted
            ? RedactionPolicy.Marker
            : parameter.RenderedValue;
    }
}
