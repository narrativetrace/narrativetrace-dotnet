// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Reflection;
using NarrativeTrace.Core;

namespace NarrativeTrace.ContractProbe.Probes;

/// <summary>
/// <c>reflectable-default</c>: <c>ConfigResolver.Resolve(...).Approval</c> with every variable
/// reading as unset is <see langword="false"/> — approval mode is opt-in.
/// </summary>
/// <remarks>
/// <c>ResolvedConfig.Approval</c> does not exist on the 0.1.3 package this project compiles
/// against by default (contract-probe compiles every probe together against one published
/// version, documentation/contract-gate.md), so the property is read by name via
/// <see cref="PropertyInfo"/> rather than a direct <c>.Approval</c> reference, which would be a
/// compile error against that older release.
/// </remarks>
internal static class ApprovalDefaultProbe
{
    public static string Observe()
    {
        var config = ConfigResolver.Resolve(_ => null);
        var property = typeof(ResolvedConfig).GetProperty("Approval", BindingFlags.Public | BindingFlags.Instance)
            ?? throw new InvalidOperationException(
                "ResolvedConfig.Approval not found — this probe should never run before its contract entry's since version");
        return (bool)property.GetValue(config)! ? "true" : "false";
    }
}
