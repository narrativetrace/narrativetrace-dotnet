// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;

namespace NarrativeTrace.ContractProbe.Probes;

/// <summary>
/// <c>reflectable-default</c>: <see cref="ConfigResolver.Resolve(System.Func{string, string?}, TracingLevel)"/>
/// with every variable reading as unset returns <c>Output: true</c> — the on-by-default claim
/// installation-guide.md's "Configure trace output" section makes. The injected-reader overload is
/// used specifically so this probe never touches the real process environment (a real
/// <c>NARRATIVETRACE_OUTPUT</c> set on the machine running the check would otherwise silently
/// change what this observes).
/// </summary>
internal static class OutputDefaultProbe
{
    public static string Observe() => ConfigResolver.Resolve(_ => null).Output ? "true" : "false";
}
