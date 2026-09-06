// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Core.Annotation;

/// <summary>
/// Adds explicit, human-readable narration text to a traced method,
/// rendered in place of relying on the method name and parameters alone.
/// </summary>
/// <remarks>
/// Layer: metadata only — this assembly declares the attribute, and the
/// tracing proxy (<c>NarrativeTrace.Proxy</c>) is what reads it.
/// The template is resolved by the proxy's interceptor when
/// the method is entered and stored as the node's narration. Placeholders
/// use <b>parameter names</b> (<c>{customerId}</c>); one level of property
/// access with a <b>PascalCase</b> member is supported
/// (<c>{customer.Name}</c>). A placeholder that matches no parameter is
/// left literal in the output, so template typos stay visible as a built-in
/// signal. Redacted (<c>[NotTraced]</c>) parameters resolve to the
/// redaction marker rather than their value.
/// </remarks>
/// <example>
/// <code>
/// [Narrated("Placing order of {quantity} units for customer {customerId}")]
/// OrderResult PlaceOrder(string customerId, string productId, int quantity);
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Method)]
public sealed class NarratedAttribute : Attribute
{
    /// <summary>
    /// Initializes the attribute with a narration template.
    /// </summary>
    /// <param name="template">
    /// The narration text, optionally containing <c>{paramName}</c> and
    /// <c>{paramName.Property}</c> placeholders.
    /// </param>
    public NarratedAttribute(string template)
    {
        Template = template;
    }

    /// <summary>
    /// The narration template, with placeholders resolved against the
    /// method's arguments at call time.
    /// </summary>
    public string Template { get; }
}
