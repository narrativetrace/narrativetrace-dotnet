// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Reflection;

namespace NarrativeTrace.ContractProbe.Probes;

/// <summary>
/// <c>reflectable-default</c>, since 0.1.4: two invocation identities sharing the SAME display
/// label but different 1-based indices must produce different file slugs — the index, not the
/// label, is what structural-trace-format.md's "Artifact identity" section says makes the scheme
/// collision-proof.
/// </summary>
/// <remarks>
/// <c>NarrativeTrace.Core.ArtifactIdentity</c> does not exist in the 0.1.3 package this project
/// compiles against by default — contract-probe compiles every probe class together against
/// whichever single published version is under test (documentation/contract-gate.md), so a probe
/// for a type that only exists in a newer release must never name that type at compile time. Found
/// entirely through <see cref="Type.GetType(string)"/> and <see cref="MethodInfo.Invoke"/>
/// instead; this entry's own <c>since</c> keeps it unreached before the type actually ships.
/// </remarks>
internal static class ReflectablePerInvocationIdentityProbe
{
    public static string Observe()
    {
        var type = Type.GetType("NarrativeTrace.Core.ArtifactIdentity, NarrativeTrace.Core")
            ?? throw new InvalidOperationException(
                "NarrativeTrace.Core.ArtifactIdentity not found in the installed package — "
                    + "this probe should never run before its contract entry's since version");
        var ofInvocation = type.GetMethod("OfInvocation", BindingFlags.Public | BindingFlags.Static)!;
        var fileSlug = type.GetMethod("FileSlug", BindingFlags.Public | BindingFlags.Instance)!;

        var first = ofInvocation.Invoke(null, ["ContractProbeFixture", "Scenario", 1, "same-label"]);
        var second = ofInvocation.Invoke(null, ["ContractProbeFixture", "Scenario", 2, "same-label"]);
        var firstSlug = (string)fileSlug.Invoke(first, null)!;
        var secondSlug = (string)fileSlug.Invoke(second, null)!;

        return firstSlug != secondSlug ? "index-suffixed" : "collided";
    }
}
