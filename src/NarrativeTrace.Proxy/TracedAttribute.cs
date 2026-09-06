// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Proxy;

/// <summary>
/// Overrides the captured parameter names of a method, positionally.
/// </summary>
/// <remarks>
/// .NET retains parameter names in metadata by default, so — unlike the
/// JVM edition — this attribute is rarely needed; reach for it to give a
/// clearer domain name than the source identifier. Names are applied by
/// position: the first name overrides the first parameter, and so on.
/// Supplying fewer names than parameters is allowed — unlisted positions
/// keep their reflected name.
/// </remarks>
/// <example>
/// <code>
/// [Traced("messageId", "payload")]
/// void Publish(string id, object body);
/// </code>
/// The <c>id</c> parameter is captured as <c>messageId</c>, <c>body</c> as
/// <c>payload</c>.
/// </example>
[AttributeUsage(AttributeTargets.Method)]
public sealed class TracedAttribute : Attribute
{
    /// <summary>
    /// Initializes the attribute with positional parameter-name overrides.
    /// </summary>
    /// <param name="parameterNames">
    /// The replacement names, in parameter order. Positions beyond the
    /// supplied names keep their reflected parameter name.
    /// </param>
    public TracedAttribute(params string[] parameterNames)
    {
        ParameterNames = parameterNames;
    }

    /// <summary>
    /// The positional parameter-name overrides supplied to the attribute.
    /// </summary>
    public string[] ParameterNames { get; }
}
