// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Reflection;

namespace NarrativeTrace.ContractProbe.Probes;

/// <summary>
/// <c>probed-default</c>, since 0.1.5: <c>manifest.json</c> carries a top-level <c>run</c> object
/// (<c>id</c>, <c>name</c>) naming the run this manifest belongs to (configuration-guide.md §7).
/// </summary>
/// <remarks>
/// Same reflection constraint as <see cref="RunNameConsoleFooterProbe"/>, extended to every type
/// this probe touches: <c>RunIdentity</c>, <c>ArtifactIdentity</c> and <c>ScenarioManifest.Entry</c>
/// (and the <c>RunIdentity</c>-typed overload of <c>ScenarioManifest.Render</c>) are all new since
/// 0.1.5 and absent from the 0.1.3 package this project compiles against by default — none of them
/// is named at compile time, mirroring <see cref="ReflectablePerInvocationIdentityProbe"/>'s own
/// note.
/// </remarks>
internal static class RunNameManifestFieldProbe
{
    public static string Observe()
    {
        // TraceId always exists (it long predates 0.1.5) — the safe anchor this probe uses to
        // find NarrativeTrace.Core's assembly without naming any of the new types at compile time.
        var core = typeof(NarrativeTrace.Core.TraceId).Assembly;
        var runIdentityType = Required(core, "NarrativeTrace.Core.RunIdentity");
        var manifestType = Required(core, "NarrativeTrace.Core.ScenarioManifest");

        var run = StaticInvoke(runIdentityType, "Generate");
        var name = (string)runIdentityType.GetProperty("Name")!.GetValue(run)!;
        var entries = OneEntryArray(core, Required(core, "NarrativeTrace.Core.ArtifactIdentity"));

        var render = manifestType
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .First(m => m.Name == "Render"
                && m.GetParameters() is [_, var second] && second.ParameterType == runIdentityType);
        var rendered = (string)render.Invoke(null, [entries, run])!;

        return rendered.Contains("\"run\": {", StringComparison.Ordinal)
            && rendered.Contains($"\"name\": \"{name}\"", StringComparison.Ordinal)
            ? "true"
            : "false";
    }

    /// <summary>A one-element <c>ScenarioManifest.Entry[]</c>, built entirely by reflection.</summary>
    private static Array OneEntryArray(System.Reflection.Assembly core, Type artifactIdentityType)
    {
        var entryType = Required(core, "NarrativeTrace.Core.ScenarioManifest+Entry");
        var identity = StaticInvoke(artifactIdentityType, "OfMethod", "OrderTests", "PlacesOrder");
        var noArtifacts = Array.CreateInstance(
            typeof(KeyValuePair<,>).MakeGenericType(typeof(string), typeof(string)), 0);
        var entry = Activator.CreateInstance(entryType, "Places an order", identity, noArtifacts)!;

        var entries = Array.CreateInstance(entryType, 1);
        entries.SetValue(entry, 0);
        return entries;
    }

    private static Type Required(System.Reflection.Assembly assembly, string typeName)
    {
        return assembly.GetType(typeName)
            ?? throw new InvalidOperationException(
                $"{typeName} not found in the installed package — "
                    + "this probe should never run before its contract entry's since version");
    }

    private static object StaticInvoke(Type type, string method, params object?[] args)
    {
        var m = type.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .First(candidate => candidate.Name == method && candidate.GetParameters().Length == args.Length);
        return m.Invoke(null, args)!;
    }
}
