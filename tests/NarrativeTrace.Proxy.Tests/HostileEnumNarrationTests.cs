// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System;
using System.Reflection;
using System.Reflection.Emit;
using NarrativeTrace.Core;
using NarrativeTrace.Core.Annotation;
using NarrativeTrace.Runtime;
using Xunit;

namespace NarrativeTrace.Proxy.Tests;

/// <summary>
/// A hostile enum's own <c>ToString()</c> reaching a scalar template placeholder unsanitized —
/// the .NET shape of the Java flagship's "<c>Number</c> subclass with a hostile
/// <c>toString()</c>" finding. A C#-compiled enum member name is a language identifier and can
/// never carry a control character, but nothing at the CLR level enforces that: an IL-authored
/// assembly (a hostile plugin, a different CLR language, a hand-crafted payload) can define one
/// that does, and <see cref="Enum"/> is not sealed against it the way a struct is.
/// </summary>
/// <remarks>
/// See <c>NarrativeTrace.Core.Tests.ValueRendererStructuredTests</c> for the matching fix on the
/// structured-render side (<c>ValueRenderer.RenderStructured</c>). Both routes shared the exact
/// same unsanitized <c>value.ToString()!</c> shape before this fix.
/// </remarks>
public class HostileEnumNarrationTests
{
    private static object HostileEnumValue(string memberName)
    {
        var assemblyName = new AssemblyName($"HostileEnum_{Guid.NewGuid():N}");
        var assembly = AssemblyBuilder.DefineDynamicAssembly(
            assemblyName, AssemblyBuilderAccess.Run);
        var module = assembly.DefineDynamicModule(assemblyName.Name!);
        var enumBuilder = module.DefineEnum(
            "HostileEnum", TypeAttributes.Public, typeof(int));
        enumBuilder.DefineLiteral(memberName, 1);
        var enumType = enumBuilder.CreateType();
        return Enum.ToObject(enumType, 1);
    }

    [Fact]
    public void Hostile_enum_member_name_is_sanitized_in_a_scalar_placeholder()
    {
        var ctx = new SyncNarrativeContext(new NarrativeTraceConfig());
        var proxy = NarrativeTraceProxy.Create<IHostileEnumNarrated>(
            new HostileEnumNarratedService(), ctx);

        proxy.Handle(HostileEnumValue("evil\n## forged\n"));

        var narration = ctx.CaptureTrace().Roots[0].Signature.Narration;
        Assert.DoesNotContain("\n", narration, StringComparison.Ordinal);
        Assert.Contains("\\n", narration, StringComparison.Ordinal);
    }

    public interface IHostileEnumNarrated
    {
        [Narrated("Processing {payload}")]
        void Handle(object payload);
    }

    private sealed class HostileEnumNarratedService : IHostileEnumNarrated
    {
        public void Handle(object payload)
        {
        }
    }
}
