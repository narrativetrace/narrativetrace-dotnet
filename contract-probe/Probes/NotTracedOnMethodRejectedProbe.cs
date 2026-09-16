// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Reflection;
using System.Reflection.Emit;
using NarrativeTrace.Core;
using NarrativeTrace.Core.Annotation;
using NarrativeTrace.Proxy;
using NarrativeTrace.Runtime;

namespace NarrativeTrace.ContractProbe.Probes;

/// <summary>
/// <c>probed-default</c>: a proxy created over an interface carrying <c>[NotTraced]</c> on a
/// METHOD (rather than a parameter/property/record component) throws
/// <see cref="InvalidOperationException"/> at proxy-creation time, naming the attribute and the
/// offending method — annotations.md "[NotTraced]" section.
/// </summary>
/// <remarks>
/// The interface is built at the IL level via <see cref="TypeBuilder"/>/<see cref="CustomAttributeBuilder"/>,
/// never through a literal C# <c>[NotTraced]</c> on a method declaration: contract-probe compiles
/// every probe class together against whichever single published version is under test
/// (documentation/contract-gate.md), and <c>NotTracedAttribute</c>'s own <c>[AttributeUsage]</c>
/// only started permitting <see cref="AttributeTargets.Method"/> in the same 0.1.5 release this
/// entry's <c>since</c> names — a source-level attribute application would be a COMPILE error
/// against the older 0.1.3 package the contract still has to build against for its other entries.
/// <see cref="CustomAttributeBuilder"/> writes the attribute metadata directly, bypassing that
/// compiler-only check, so this probe's own source is stable across every version.
/// </remarks>
internal static class NotTracedOnMethodRejectedProbe
{
    public static string Observe()
    {
        var interfaceType = BuildInterfaceWithMethodLevelNotTraced();
        var context = new SyncNarrativeContext(new NarrativeTraceConfig());
        try
        {
            NarrativeTraceProxy.Create(interfaceType, new object(), context);
            return "not-rejected";
        }
        catch (InvalidOperationException e)
        {
            return e.Message.Contains("NotTraced", StringComparison.Ordinal)
                && e.Message.Contains("Authenticate", StringComparison.Ordinal)
                ? "true"
                : "wrong-message";
        }
    }

    private static Type BuildInterfaceWithMethodLevelNotTraced()
    {
        var module = AssemblyBuilder
            .DefineDynamicAssembly(new AssemblyName("ContractProbeDynamic"), AssemblyBuilderAccess.Run)
            .DefineDynamicModule("MainModule");
        var type = module.DefineType(
            "IDynamicAccountService", TypeAttributes.Public | TypeAttributes.Interface | TypeAttributes.Abstract);
        var method = type.DefineMethod(
            "Authenticate", MethodAttributes.Public | MethodAttributes.Abstract | MethodAttributes.Virtual,
            typeof(void), [typeof(string), typeof(string)]);
        var ctor = typeof(NotTracedAttribute).GetConstructor(Type.EmptyTypes)!;
        method.SetCustomAttribute(new CustomAttributeBuilder(ctor, []));
        return type.CreateType();
    }
}
