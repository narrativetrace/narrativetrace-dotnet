// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using NarrativeTrace.Core.Annotation;
using Xunit;

namespace NarrativeTrace.Core.Tests;

public class ValueRendererTests
{
    [Fact]
    public void Null_renders_as_null()
    {
        Assert.Equal("null", ValueRenderer.Render(null));
    }

    [Fact]
    public void String_renders_quoted()
    {
        Assert.Equal("\"hello\"", ValueRenderer.Render("hello"));
    }

    [Fact]
    public void String_with_control_chars_renders_escaped_on_one_line()
    {
        Assert.Equal("\"a\\nb\\rc\"", ValueRenderer.Render("a\nb\rc"));
    }

    [Fact]
    public void Long_string_is_truncated()
    {
        var opts = new RenderOptions(MaxStringLength: 5);

        Assert.Equal("\"hello...\"", ValueRenderer.Render("hello world", opts));
    }

    [Theory]
    [InlineData(true, "true")]
    [InlineData(false, "false")]
    public void Boolean_renders_lowercase(bool input, string expected)
    {
        Assert.Equal(expected, ValueRenderer.Render(input));
    }

    [Fact]
    public void Integer_renders_as_number()
    {
        Assert.Equal("42", ValueRenderer.Render(42));
    }

    [Fact]
    public void Double_renders_as_number()
    {
        Assert.Equal("3.14", ValueRenderer.Render(3.14));
    }

    [Fact]
    public void Float_renders_as_number_with_shortest_representation()
    {
        Assert.Equal("2.5", ValueRenderer.Render(2.5f));
        Assert.Equal("0.1", ValueRenderer.Render(0.1f));
    }

    private sealed class ChattyToString
    {
        public override string ToString() => new('x', 300);
    }

    [Fact]
    public void Custom_ToString_output_is_truncated_to_max_string_length()
    {
        var opts = new RenderOptions(MaxStringLength: 10);

        var flat = ValueRenderer.Render(new ChattyToString(), opts);
        var structured = ValueRenderer.RenderStructured(
            new ChattyToString(), opts);

        Assert.Equal("xxxxxxxxxx...", flat);
        Assert.Equal(
            new RenderedValue.StringVal("xxxxxxxxxx..."),
            structured);
    }

    private sealed class DescribedOrder
    {
        public int Id { get; init; }

        public override string ToString() => "order text";
    }

    // Intentional divergence from the JVM edition: Java prefers a custom
    // toString over field introspection; .NET keeps introspection-first so
    // records and DTOs render structurally. Pinned so a change is a
    // conscious decision, not drift.
    [Fact]
    public void Object_with_properties_prefers_introspection_over_ToString()
    {
        var flat = ValueRenderer.Render(new DescribedOrder { Id = 7 });

        Assert.Equal("DescribedOrder{Id: 7}", flat);
    }

    private sealed class NullToString
    {
        public override string? ToString() => null;
    }

    [Fact]
    public void Null_ToString_renders_type_name_marker()
    {
        Assert.Equal(
            "<NullToString>",
            ValueRenderer.Render(new NullToString()));
        Assert.Equal(
            new RenderedValue.StringVal("<NullToString>"),
            ValueRenderer.RenderStructured(new NullToString()));
    }

    [Fact]
    public void Numeric_rendering_is_culture_invariant()
    {
        using var _ = new CultureScope("de-DE");

        Assert.Equal("3.14", ValueRenderer.Render(3.14));
        Assert.Equal("2.5", ValueRenderer.Render(2.5f));
        Assert.Equal("9.99", ValueRenderer.Render(9.99m));
        Assert.Equal(
            new RenderedValue.DoubleVal(3.14),
            ValueRenderer.RenderStructured(3.14));
    }

    [Fact]
    public void DateTime_renders_iso_8601()
    {
        var dt = new DateTime(2026, 3, 3, 10, 30, 0, DateTimeKind.Utc);

        Assert.Equal("2026-03-03T10:30:00.0000000Z", ValueRenderer.Render(dt));
    }

    [Fact]
    public void Enum_renders_name()
    {
        Assert.Equal("Detail", ValueRenderer.Render(TracingLevel.Detail));
    }

    [Fact]
    public void Array_renders_with_brackets()
    {
        Assert.Equal("[1, 2, 3]", ValueRenderer.Render(new[] { 1, 2, 3 }));
    }

    [Fact]
    public void Large_array_is_truncated_with_count()
    {
        var opts = new RenderOptions(MaxArrayItems: 2);

        Assert.Equal(
            "[1, 2, ... (5 total)]",
            ValueRenderer.Render(new[] { 1, 2, 3, 4, 5 }, opts));
    }

    [Fact]
    public void Empty_array_renders_as_empty_brackets()
    {
        Assert.Equal("[]", ValueRenderer.Render(Array.Empty<int>()));
    }

    [Fact]
    public void Dictionary_renders_with_braces()
    {
        var dict = new Dictionary<string, int> { ["a"] = 1, ["b"] = 2 };

        Assert.Equal("{a=1, b=2}", ValueRenderer.Render(dict));
    }

    [Fact]
    public void Large_dictionary_is_truncated()
    {
        var opts = new RenderOptions(MaxObjectKeys: 1);
        var dict = new Dictionary<string, int> { ["a"] = 1, ["b"] = 2 };

        Assert.Equal("{a=1, …}", ValueRenderer.Render(dict, opts));
    }

    [Fact]
    public void Record_renders_with_name_and_fields()
    {
        var capture = new ParameterCapture("id", "42", false);

        Assert.Equal(
            "ParameterCapture(Name: \"id\", RenderedValue: \"42\","
            + " Redacted: false, StructuredValue: null, DeclaredType: null)",
            ValueRenderer.Render(capture));
    }

    [Fact]
    public void NarrativeSummary_method_overrides_field_rendering()
    {
        var order = new SummarizedOrder { Id = "A1", Total = 42 };

        Assert.Equal("Order A1 ($42)", ValueRenderer.Render(order));
    }

    [Fact]
    public void NarrativeSummary_method_with_parameters_is_ignored()
    {
        var obj = new SummaryWithParams { Value = 9 };

        var result = ValueRenderer.Render(obj);

        // The parameterized method is not a valid summary; fall to fields.
        Assert.Contains("Value: 9", result);
    }

    [Fact]
    public void NarrativeSummary_that_throws_falls_through_to_fields()
    {
        var obj = new ThrowingSummary { Value = 7 };

        var result = ValueRenderer.Render(obj);

        Assert.Contains("Value: 7", result);
    }

    [Fact]
    public void NarrativeSummary_property_overrides_field_rendering()
    {
        var order = new PropertySummarizedOrder { Id = "B2", Total = 7 };

        Assert.Equal("Order B2 ($7)", ValueRenderer.Render(order));
    }

    [Fact]
    public void NarrativeSummary_property_that_throws_falls_through_to_fields()
    {
        var obj = new ThrowingPropertySummary { Value = 7 };

        Assert.Contains("Value: 7", ValueRenderer.Render(obj));
    }

    [Fact]
    public void NarrativeSummary_method_wins_over_an_annotated_property()
    {
        var obj = new DoublySummarized();

        Assert.Equal("from-method", ValueRenderer.Render(obj));
    }

    [Fact]
    public void NarrativeSummary_write_only_property_falls_through_to_fields()
    {
        var obj = new WriteOnlySummary { Value = 5 };

        Assert.Contains("Value: 5", ValueRenderer.Render(obj));
    }

    [Fact]
    public void Poco_with_only_public_fields_renders_them()
    {
        var obj = new FieldOnlyPoco { Name = "test", Value = 42 };

        Assert.Equal(
            "FieldOnlyPoco{Name: \"test\", Value: 42}",
            ValueRenderer.Render(obj));
    }

    [Fact]
    public void Static_and_const_fields_are_not_rendered()
    {
        var obj = new FieldsWithStatics { Instance = "kept" };

        Assert.Equal(
            "FieldsWithStatics{Instance: \"kept\"}",
            ValueRenderer.Render(obj));
    }

    [Fact]
    public void Sensitively_named_field_is_redacted_by_the_name_policy()
    {
        var obj = new FieldOnlyCredentials
        {
            User = "alice",
            Password = "hunter2",
        };

        var output = ValueRenderer.Render(obj);

        Assert.Contains("Password: [REDACTED]", output);
        Assert.DoesNotContain("hunter2", output);
    }

    [Fact]
    public void Public_fields_count_towards_the_object_key_limit()
    {
        var obj = new FieldOnlyPoco { Name = "test", Value = 42 };
        var opts = new RenderOptions(MaxObjectKeys: 1);

        Assert.Equal(
            "FieldOnlyPoco{Name: \"test\", ... (2 total)}",
            ValueRenderer.Render(obj, opts));
    }

    [Fact]
    public void Poco_renders_with_name_and_fields()
    {
        var obj = new SimplePoco { Name = "test", Value = 42 };

        Assert.Equal(
            "SimplePoco{Name: \"test\", Value: 42}",
            ValueRenderer.Render(obj));
    }

    [Fact]
    public void Sensitively_named_property_is_redacted_by_default()
    {
        var obj = new AccountPoco { Username = "alice", Password = "hunter2" };

        var result = ValueRenderer.Render(obj);

        Assert.Contains("Username: \"alice\"", result);
        Assert.Contains("Password: [REDACTED]", result);
        Assert.DoesNotContain("hunter2", result);
    }

    [Fact]
    public void Sensitively_named_dictionary_key_is_redacted_by_default()
    {
        var dict = new Dictionary<string, string>
        {
            ["user"] = "alice",
            ["apiKey"] = "sk-secret",
        };

        var result = ValueRenderer.Render(dict);

        Assert.Contains("user=\"alice\"", result);
        Assert.Contains("apiKey=[REDACTED]", result);
        Assert.DoesNotContain("sk-secret", result);
    }

    [Fact]
    public void Dictionary_key_object_renders_through_redaction_not_ToString()
    {
        var balances = new Dictionary<AccountPoco, int>
        {
            [new AccountPoco
            {
                Username = "alice",
                Password = "hunter2",
            }] = 42,
        };

        var result = ValueRenderer.Render(balances);

        Assert.Contains("Username: \"alice\"", result);
        Assert.Contains("Password: [REDACTED]", result);
        Assert.DoesNotContain("hunter2", result);
    }

    [Fact]
    public void Dictionary_string_key_is_sanitized_against_control_characters()
    {
        var dict = new Dictionary<string, int> { ["a\nb"] = 1 };

        var result = ValueRenderer.Render(dict);

        Assert.Equal("{a\\nb=1}", result);
        Assert.DoesNotContain("\n", result);
    }

    [Fact]
    public void Dictionary_string_key_is_truncated_at_the_string_limit()
    {
        var opts = new RenderOptions(MaxStringLength: 5);
        var dict = new Dictionary<string, int> { [new string('k', 20)] = 1 };

        var result = ValueRenderer.Render(dict, opts);

        Assert.Equal("{kkkkk...=1}", result);
    }

    [Fact]
    public void Dictionary_keyed_by_itself_renders_a_cycle_marker()
    {
        var dict = new System.Collections.Hashtable();
        dict[dict] = "v";

        var result = ValueRenderer.Render(dict);

        Assert.Contains("<Hashtable@", result);
        Assert.Contains("=\"v\"", result);
    }

    [Fact]
    public void Nested_object_renders_recursively()
    {
        var inner = new ParameterCapture("x", "1", false);
        var outer = new Wrapper { Inner = inner, Label = "wrap" };

        Assert.Equal(
            "Wrapper{Inner: ParameterCapture(Name: \"x\","
            + " RenderedValue: \"1\", Redacted: false, StructuredValue: null,"
            + " DeclaredType: null),"
            + " Label: \"wrap\"}",
            ValueRenderer.Render(outer));
    }

    [Fact]
    public void Null_property_renders_as_null()
    {
        var obj = new Wrapper { Inner = null, Label = "test" };

        Assert.Equal(
            "Wrapper{Inner: null, Label: \"test\"}",
            ValueRenderer.Render(obj));
    }

    [Fact]
    public void Exception_renders_type_and_message()
    {
        var ex = new InvalidOperationException("something broke");

        Assert.Equal(
            "InvalidOperationException: something broke",
            ValueRenderer.Render(ex));
    }

    [Fact]
    public void Exception_message_control_chars_are_sanitized()
    {
        var ex = new InvalidOperationException("line1\nline2");

        Assert.Equal(
            "InvalidOperationException: line1\\nline2",
            ValueRenderer.Render(ex));
    }

    [Fact]
    public void ToString_fallback_control_chars_are_sanitized()
    {
        Assert.Equal("a\\nb", ValueRenderer.Render(new ControlCharToString()));
    }

    [Fact]
    public void Type_renders_as_typeof()
    {
        Assert.Equal("typeof(String)", ValueRenderer.Render(typeof(string)));
    }

    [Fact]
    public void Char_renders_single_quoted()
    {
        Assert.Equal("'c'", ValueRenderer.Render('c'));
    }

    [Fact]
    public void Delegate_renders_as_function()
    {
        Func<int, int> fn = x => x + 1;

        Assert.Equal("<function>", ValueRenderer.Render(fn));
    }

    [Fact]
    public void ToString_exception_falls_back_to_type_name()
    {
        var obj = new ThrowingToString();

        Assert.Equal("<ThrowingToString>", ValueRenderer.Render(obj));
    }

    [Fact]
    public void Large_object_is_truncated_with_count()
    {
        var opts = new RenderOptions(MaxObjectKeys: 1);
        var obj = new SimplePoco { Name = "test", Value = 42 };

        Assert.Equal(
            "SimplePoco{Name: \"test\", ... (2 total)}",
            ValueRenderer.Render(obj, opts));
    }

    [Fact]
    public void Circular_reference_renders_as_circular()
    {
        var node = new TreeNode { Name = "root" };
        node.Parent = node;

        var result = ValueRenderer.Render(node);

        Assert.Matches(@"<TreeNode@[0-9a-f]+>", result);
    }

    [Fact]
    public void Throwing_property_renders_as_error()
    {
        var obj = new ThrowingProperty();

        var result = ValueRenderer.Render(obj);

        Assert.Equal("ThrowingProperty{Boom: <error>}", result);
    }

    [Fact]
    public void Self_referential_list_detects_cycle()
    {
        var list = new List<object>();
        list.Add(list);

        var result = ValueRenderer.Render(list);

        Assert.Matches(@"<List`1@[0-9a-f]+>", result);
    }

    [Fact]
    public void Self_referential_dictionary_detects_cycle()
    {
        var dict = new Dictionary<string, object>();
        dict["self"] = dict;

        var result = ValueRenderer.Render(dict);

        Assert.Contains("<Dictionary`2@", result);
    }

    [Fact]
    public void Deep_nesting_beyond_max_depth_renders_placeholder()
    {
        var opts = new RenderOptions(MaxDepth: 2);
        var root = new TreeNode { Name = "L0" };
        root.Child = new TreeNode { Name = "L1" };
        root.Child.Child = new TreeNode { Name = "L2" };

        var result = ValueRenderer.Render(root, opts);

        Assert.Contains("L0", result);
        Assert.Contains("L1", result);
        Assert.Contains("<...>", result);
        Assert.DoesNotContain("L2", result);
    }

    [Fact]
    public void Throwing_dictionary_key_renders_a_type_placeholder()
    {
        var dict = new System.Collections.Hashtable();
        dict[new ThrowingToString()] = "value";

        var result = ValueRenderer.Render(dict);

        // The guarded path names the type (Java: `<ExplodingKey>`); the old
        // bare-ToString path could only say `<error>`.
        Assert.Equal("{<ThrowingToString>=\"value\"}", result);
    }

    [Fact]
    public void Decimal_renders_as_its_numeric_value_not_as_an_object()
    {
        Assert.Equal("19.98", ValueRenderer.Render(19.98m));
    }

    [Fact]
    public void Decimal_renders_invariantly_regardless_of_culture()
    {
        var previous = System.Globalization.CultureInfo.CurrentCulture;
        System.Globalization.CultureInfo.CurrentCulture =
            System.Globalization.CultureInfo.GetCultureInfo("de-DE");
        try
        {
            Assert.Equal("1234.5", ValueRenderer.Render(1234.5m));
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = previous;
        }
    }

    private class TreeNode
    {
        public string Name { get; set; } = "";
        public TreeNode? Parent { get; set; }
        public TreeNode? Child { get; set; }
    }

    private class SimplePoco
    {
        public string Name { get; set; } = "";
        public int Value { get; set; }
    }

    private class AccountPoco
    {
        public string Username { get; set; } = "";
        public string Password { get; set; } = "";
    }

    private class SummarizedOrder
    {
        public string Id { get; set; } = "";
        public int Total { get; set; }

        [NarrativeSummary]
        public string Summary() => $"Order {Id} (${Total})";
    }

    private class PropertySummarizedOrder
    {
        public string Id { get; set; } = "";
        public int Total { get; set; }

        [NarrativeSummary]
        public string Summary => $"Order {Id} (${Total})";
    }

    // S1104: public fields are the subject under test — .NET introspection
    // must cover them the way Java's getDeclaredFields() does.
#pragma warning disable S1104
    private class FieldOnlyPoco
    {
        public string Name = "";
        public int Value;
    }
#pragma warning restore S1104

    // S1144: the const and static are never read on purpose — the test
    // asserts that introspection skips them, so removing them removes the case.
#pragma warning disable S1104, S1144
    private class FieldsWithStatics
    {
        public const string Constant = "const";
        public static string Shared = "static";
        public string Instance = "";
    }

    private class FieldOnlyCredentials
    {
        public string User = "";
        public string Password = "";
    }
#pragma warning restore S1104, S1144

    private class ThrowingPropertySummary
    {
        public int Value { get; set; }

        [NarrativeSummary]
        public string Summary =>
            throw new InvalidOperationException("boom");
    }

    private class DoublySummarized
    {
        [NarrativeSummary]
        public string Property => "from-property";

        [NarrativeSummary]
        public string Method() => "from-method";
    }

    // S2376: the missing getter is the point — a summary member the renderer
    // cannot invoke must degrade to normal rendering, not break it.
#pragma warning disable S2376
    private class WriteOnlySummary
    {
        public int Value { get; set; }

        [NarrativeSummary]
        public string Summary
        {
            set => Value = value.Length;
        }
    }
#pragma warning restore S2376

    private class ThrowingSummary
    {
        public int Value { get; set; }

        [NarrativeSummary]
        public string Summary() =>
            throw new InvalidOperationException("boom");
    }

    private class SummaryWithParams
    {
        public int Value { get; set; }

        [NarrativeSummary]
        public string Summary(int extra) => $"nope {extra}";
    }

    private class Wrapper
    {
        public ParameterCapture? Inner { get; set; }
        public string Label { get; set; } = "";
    }

    [Fact]
    public void Renders_pending_Task_as_pending()
    {
        var tcs = new TaskCompletionSource<int>();
        var result = ValueRenderer.Render(tcs.Task);

        Assert.Equal("<pending>", result);
    }

    [Fact]
    public void Renders_completed_Task_T_with_resolved_value()
    {
        var task = Task.FromResult("hello");
        var result = ValueRenderer.Render(task);

        Assert.Equal("\"hello\"", result);
    }

    [Fact]
    public void Renders_completed_Task_T_with_int_value()
    {
        var task = Task.FromResult(42);
        var result = ValueRenderer.Render(task);

        Assert.Equal("42", result);
    }

    [Fact]
    public void Renders_completed_Task_T_with_null_value()
    {
        var task = Task.FromResult<string?>(null);
        var result = ValueRenderer.Render(task);

        Assert.Equal("null", result);
    }

    [Fact]
    public void Renders_cancelled_Task_as_cancelled()
    {
        var tcs = new TaskCompletionSource<int>();
        tcs.SetCanceled();
        var result = ValueRenderer.Render(tcs.Task);

        Assert.Equal("<cancelled>", result);
    }

    [Fact]
    public void Renders_faulted_Task_as_faulted()
    {
        var tcs = new TaskCompletionSource<int>();
        tcs.SetException(
            new InvalidOperationException("boom"));
        var result = ValueRenderer.Render(tcs.Task);

        Assert.Equal("<faulted>", result);
    }

#pragma warning disable S3877, S2325, S1144
    private class ControlCharToString
    {
        public override string ToString() => "a\nb";
    }

    private class ThrowingToString
    {
        public override string ToString() => throw new InvalidOperationException("boom");
    }

    private class ThrowingProperty
    {
        public string Boom => throw new InvalidOperationException("boom");
    }
#pragma warning restore S3877, S2325, S1144
}
