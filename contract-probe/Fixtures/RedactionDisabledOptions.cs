// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Reflection;
using NarrativeTrace.Core;
using NarrativeTrace.Proxy;

namespace NarrativeTrace.ContractProbe.Fixtures;

/// <summary>
/// A <see cref="ProxyOptions"/> instance with <c>Redaction</c> set to
/// <c>RedactionPolicy.Disabled</c>, built without a source-level <c>Redaction:</c> reference.
/// </summary>
/// <remarks>
/// <c>ProxyOptions.Redaction</c> does not exist on the 0.1.3 package this project compiles against
/// by default — contract-probe compiles every probe together against one published version
/// (documentation/contract-gate.md), so a probe for a parameter that only exists in a newer
/// release must never name it at compile time. <see cref="ProxyOptions"/> the TYPE has existed
/// since before that, so its (sole) constructor is found and invoked by reflection instead,
/// substituting the compiler's own default for every parameter except <c>Redaction</c> — the one
/// this helper exists to set.
/// </remarks>
internal static class RedactionDisabledOptions
{
    public static ProxyOptions Build()
    {
        var ctor = typeof(ProxyOptions).GetConstructors().Single();
        var parameters = ctor.GetParameters();
        if (parameters.All(p => p.Name != "Redaction"))
        {
            throw new InvalidOperationException(
                "ProxyOptions.Redaction not found — this probe should never run before its contract entry's since version");
        }

        var arguments = parameters
            .Select(parameter => parameter.Name == "Redaction" ? RedactionPolicy.Disabled : parameter.DefaultValue)
            .ToArray();
        return (ProxyOptions)ctor.Invoke(arguments);
    }
}
