// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Core.Annotation;

/// <summary>
/// Attaches contextual error text to a method, surfaced on the trace node
/// when the method throws a matching exception. The attribute is
/// <b>repeatable</b>.
/// </summary>
/// <remarks>
/// Layer: metadata only — this assembly declares the attribute, and the
/// tracing proxy (<c>NarrativeTrace.Proxy</c>) is what reads it.
/// The template is resolved by the proxy's interceptor only
/// when an exception is thrown, using the same placeholder rules as
/// <see cref="NarratedAttribute"/>, and stored as the node's error context.
/// Only attributes whose declared <see cref="ExceptionType"/> matches the
/// thrown exception (<see cref="Type.IsInstanceOfType(object)"/>) compete;
/// among the matches, the <b>most specific</b> declared type wins. A bare
/// <c>[OnError("…")]</c> defaults to <see cref="System.Exception"/> and so
/// matches everything. If no declared type matches the thrown exception, no
/// error context is attached, and a method that returns normally never
/// carries one.
/// </remarks>
/// <example>
/// <code>
/// [OnError("Payment declined for {customerId}, amount was {amount}",
///          ExceptionType = typeof(PaymentDeclinedException))]
/// [OnError("Temporary payment failure for {customerId}",
///          ExceptionType = typeof(ExternalServiceException))]
/// PaymentConfirmation Charge(
///     string customerId, decimal amount, [NotTraced] string token);
/// </code>
/// </example>
[AttributeUsage(
    AttributeTargets.Method,
    AllowMultiple = true)]
public sealed class OnErrorAttribute : Attribute
{
    /// <summary>
    /// Initializes the attribute with an error-context template.
    /// </summary>
    /// <param name="template">
    /// The error text, optionally containing <c>{paramName}</c> and
    /// <c>{paramName.Property}</c> placeholders resolved when the exception
    /// is thrown.
    /// </param>
    public OnErrorAttribute(string template)
    {
        Template = template;
    }

    /// <summary>
    /// The error-context template, with placeholders resolved against the
    /// method's arguments at exception time.
    /// </summary>
    public string Template { get; }

    /// <summary>
    /// The exception type this template applies to. Defaults to
    /// <see cref="System.Exception"/>, which matches every exception.
    /// </summary>
    public Type ExceptionType { get; set; }
        = typeof(Exception);
}
