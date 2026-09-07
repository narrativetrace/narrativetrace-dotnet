// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System;
using System.Reflection;
using System.Reflection.Emit;
using NarrativeTrace.Core;
using Xunit;

namespace NarrativeTrace.Core.Tests;

public class ValueRendererStructuredTests
{
    // A C#-compiled enum member name is a language identifier and can never
    // carry a control character. Nothing about the CLR enforces that at the
    // metadata level, though: an IL-authored assembly (a hostile plugin, a
    // different CLR language, a hand-crafted payload) can define a member
    // whose literal name is anything at all. This is the .NET shape of the
    // "hostile ToString() on a type the renderer trusts" defect class every
    // NarrativeTrace runtime pins — Enum.ToString() is not sealed against it
    // the way a struct's is.
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
    public void Hostile_enum_member_name_is_sanitized_in_structured_output()
    {
        var hostile = HostileEnumValue("evil\n## forged\n");

        var result = ValueRenderer.RenderStructured(hostile);

        var stringVal = Assert.IsType<RenderedValue.StringVal>(result);
        Assert.DoesNotContain("\n", stringVal.Value, StringComparison.Ordinal);
        Assert.Contains("\\n", stringVal.Value, StringComparison.Ordinal);
    }

    [Fact]
    public void Null_renders_as_NullVal()
    {
        Assert.IsType<RenderedValue.NullVal>(
            ValueRenderer.RenderStructured(null));
    }

    [Theory]
    [InlineData(42)]
    [InlineData(0)]
    [InlineData(-7)]
    public void Integers_render_as_LongVal(int input)
    {
        var result = ValueRenderer.RenderStructured(input);

        var longVal = Assert.IsType<RenderedValue.LongVal>(result);
        Assert.Equal(input, longVal.Value);
    }

    [Fact]
    public void Long_and_short_and_byte_render_as_LongVal()
    {
        Assert.Equal(9L,
            Assert.IsType<RenderedValue.LongVal>(
                ValueRenderer.RenderStructured(9L)).Value);
        Assert.Equal(3L,
            Assert.IsType<RenderedValue.LongVal>(
                ValueRenderer.RenderStructured((short)3)).Value);
        Assert.Equal(5L,
            Assert.IsType<RenderedValue.LongVal>(
                ValueRenderer.RenderStructured((byte)5)).Value);
    }

    [Fact]
    public void Double_float_decimal_render_as_DoubleVal()
    {
        Assert.Equal(3.14,
            Assert.IsType<RenderedValue.DoubleVal>(
                ValueRenderer.RenderStructured(3.14)).Value);
        Assert.Equal(1.5,
            Assert.IsType<RenderedValue.DoubleVal>(
                ValueRenderer.RenderStructured(1.5f)).Value, 3);
        Assert.Equal(2.5,
            Assert.IsType<RenderedValue.DoubleVal>(
                ValueRenderer.RenderStructured(2.5m)).Value);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Bool_renders_as_BooleanVal(bool input)
    {
        var result = ValueRenderer.RenderStructured(input);

        Assert.Equal(input,
            Assert.IsType<RenderedValue.BooleanVal>(result).Value);
    }

    [Fact]
    public void DateTime_renders_as_InstantVal_epoch_millis()
    {
        var dt = new DateTime(2026, 3, 3, 10, 30, 0, DateTimeKind.Utc);

        var result = ValueRenderer.RenderStructured(dt);

        var instant = Assert.IsType<RenderedValue.InstantVal>(result);
        Assert.Equal(
            new DateTimeOffset(dt).ToUnixTimeMilliseconds(),
            instant.EpochMillis);
    }

    [Fact]
    public void String_enum_char_render_as_StringVal()
    {
        Assert.Equal("hi",
            Assert.IsType<RenderedValue.StringVal>(
                ValueRenderer.RenderStructured("hi")).Value);
        Assert.Equal("Detail",
            Assert.IsType<RenderedValue.StringVal>(
                ValueRenderer.RenderStructured(
                    TracingLevel.Detail)).Value);
        Assert.Equal("c",
            Assert.IsType<RenderedValue.StringVal>(
                ValueRenderer.RenderStructured('c')).Value);
    }

    [Fact]
    public void Array_renders_as_ListVal_of_typed_elements()
    {
        var result = ValueRenderer.RenderStructured(new[] { 1, 2, 3 });

        var list = Assert.IsType<RenderedValue.ListVal>(result);
        Assert.Equal(3, list.Elements.Count);
        Assert.All(list.Elements,
            e => Assert.IsType<RenderedValue.LongVal>(e));
    }

    [Fact]
    public void Null_element_in_list_renders_as_NullVal()
    {
        var result = ValueRenderer.RenderStructured(
            new object?[] { "x", null });

        var list = Assert.IsType<RenderedValue.ListVal>(result);
        Assert.IsType<RenderedValue.NullVal>(list.Elements[1]);
    }

    [Fact]
    public void List_is_truncated_to_max_array_items()
    {
        var opts = new RenderOptions(MaxArrayItems: 2);

        var result = ValueRenderer.RenderStructured(
            new[] { 1, 2, 3, 4 }, opts);

        var list = Assert.IsType<RenderedValue.ListVal>(result);
        Assert.Equal(2, list.Elements.Count);
    }

    [Fact]
    public void Object_renders_as_ObjectVal_with_typed_fields()
    {
        var obj = new StructPoco { Name = "x", Count = 5 };

        var result = ValueRenderer.RenderStructured(obj);

        var objVal = Assert.IsType<RenderedValue.ObjectVal>(result);
        Assert.Equal("StructPoco", objVal.TypeName);
        Assert.Equal("x",
            Assert.IsType<RenderedValue.StringVal>(
                objVal.Fields["Name"]).Value);
        Assert.Equal(5L,
            Assert.IsType<RenderedValue.LongVal>(
                objVal.Fields["Count"]).Value);
    }

    [Fact]
    public void Dictionary_renders_as_ObjectVal_named_Map()
    {
        var dict = new Dictionary<string, int> { ["a"] = 1, ["b"] = 2 };

        var result = ValueRenderer.RenderStructured(dict);

        var objVal = Assert.IsType<RenderedValue.ObjectVal>(result);
        Assert.Equal("Map", objVal.TypeName);
        Assert.Equal(1L,
            Assert.IsType<RenderedValue.LongVal>(
                objVal.Fields["a"]).Value);
    }

    [Fact]
    public void Structured_dictionary_key_object_renders_through_redaction()
    {
        var balances = new Dictionary<StructSecret, int>
        {
            [new StructSecret { User = "alice", Token = "secret" }] = 42,
        };

        var result = ValueRenderer.RenderStructured(balances);

        var objVal = Assert.IsType<RenderedValue.ObjectVal>(result);
        var key = Assert.Single(objVal.Fields.Keys);
        Assert.Contains("User: \"alice\"", key);
        Assert.Contains("Token: [REDACTED]", key);
        Assert.DoesNotContain("secret", key);
    }

    [Fact]
    public void Structured_dictionary_string_key_is_sanitized()
    {
        var dict = new Dictionary<string, int> { ["a\nb"] = 1 };

        var result = ValueRenderer.RenderStructured(dict);

        var objVal = Assert.IsType<RenderedValue.ObjectVal>(result);
        Assert.True(objVal.Fields.ContainsKey("a\\nb"));
    }

    [Fact]
    public void Object_redacts_sensitive_fields_as_redacted_string()
    {
        var obj = new StructSecret { User = "a", Token = "secret" };

        var result = ValueRenderer.RenderStructured(obj);

        var objVal = Assert.IsType<RenderedValue.ObjectVal>(result);
        Assert.Equal("[REDACTED]",
            Assert.IsType<RenderedValue.StringVal>(
                objVal.Fields["Token"]).Value);
    }

    [Fact]
    public void Completed_task_result_renders_structured()
    {
        var task = System.Threading.Tasks.Task.FromResult(42);

        var result = ValueRenderer.RenderStructured(task);

        Assert.Equal(42L,
            Assert.IsType<RenderedValue.LongVal>(result).Value);
    }

    [Fact]
    public void Pending_task_renders_as_pending_string()
    {
        var tcs = new System.Threading.Tasks
            .TaskCompletionSource<int>();

        var result = ValueRenderer.RenderStructured(tcs.Task);

        Assert.Equal("<pending>",
            Assert.IsType<RenderedValue.StringVal>(result).Value);
    }

    private class StructPoco
    {
        public string Name { get; set; } = "";
        public int Count { get; set; }
    }

    private class StructSecret
    {
        public string User { get; set; } = "";
        public string Token { get; set; } = "";
    }
}
