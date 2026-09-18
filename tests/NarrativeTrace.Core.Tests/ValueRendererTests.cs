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
    public void A_leafs_own_text_is_truncated_to_max_string_length()
    {
        var opts = new RenderOptions(MaxStringLength: 10);
        var leaf = new Uri("https://example.com/" + new string('x', 300));

        var flat = ValueRenderer.Render(leaf, opts);
        var structured = ValueRenderer.RenderStructured(leaf, opts);

        Assert.Equal("https://ex...", flat);
        Assert.Equal(
            new RenderedValue.StringVal("https://ex..."),
            structured);
    }

    // A type with no readable member is not thereby a stateless leaf — only a
    // platform scalar or an enum is (PlatformTypes.IsStatelessLeaf). Everything
    // else renders as the object it is, which with no members is an empty dump.
    [Fact]
    public void A_member_less_class_never_renders_through_its_own_ToString()
    {
        var flat = ValueRenderer.Render(new ChattyToString());
        var structured = ValueRenderer.RenderStructured(new ChattyToString());

        Assert.Equal("ChattyToString{}", flat);
        var objVal = Assert.IsType<RenderedValue.ObjectVal>(structured);
        Assert.Equal("ChattyToString", objVal.TypeName);
        Assert.Empty(objVal.Fields);
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

    // Even a ToString that could only ever answer null is left uncalled: the
    // decision is the type's, made before any of its code could run.
    [Fact]
    public void Null_ToString_is_never_called_for_a_member_less_class()
    {
        Assert.Equal(
            "NullToString{}",
            ValueRenderer.Render(new NullToString()));
        var structured = ValueRenderer.RenderStructured(new NullToString());
        Assert.Equal(
            "NullToString",
            Assert.IsType<RenderedValue.ObjectVal>(structured).TypeName);
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
    public void NarrativeSummary_method_that_throws_renders_typed_error_marker()
    {
        var obj = new ThrowingSummary { Value = 7 };

        var result = ValueRenderer.Render(obj);

        // A curated summary was written precisely so the fields wouldn't be
        // shown raw — a throwing summary must not fall back to dumping them.
        Assert.Equal("<error: InvalidOperationException>", result);
        Assert.DoesNotContain("Value: 7", result);
        Assert.DoesNotContain("boom", result, StringComparison.Ordinal);
    }

    [Fact]
    public void NarrativeSummary_property_overrides_field_rendering()
    {
        var order = new PropertySummarizedOrder { Id = "B2", Total = 7 };

        Assert.Equal("Order B2 ($7)", ValueRenderer.Render(order));
    }

    [Fact]
    public void NarrativeSummary_property_that_throws_renders_typed_error_marker()
    {
        var obj = new ThrowingPropertySummary { Value = 7 };

        var result = ValueRenderer.Render(obj);

        Assert.Equal("<error: InvalidOperationException>", result);
        Assert.DoesNotContain("Value: 7", result);
        Assert.DoesNotContain("boom", result, StringComparison.Ordinal);
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

    // The control character never reaches an output because the text carrying
    // it is never produced — a stronger guarantee than escaping it would be.
    // The escape itself is pinned where user text still legitimately arrives:
    // Exception_message_control_chars_are_sanitized above, and the
    // [NarrativeSummary] hook's own sanitize.
    [Fact]
    public void A_member_less_classes_control_chars_never_reach_the_output()
    {
        Assert.Equal(
            "ControlCharToString{}", ValueRenderer.Render(new ControlCharToString()));
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

    // A throwing ToString on a member-less class no longer produces even an
    // error marker: nothing threw, because nothing ran. The typed marker is
    // still pinned where user code does run — see
    // NarrativeSummary_method_that_throws_renders_typed_error_marker.
    [Fact]
    public void A_member_less_classes_throwing_ToString_is_never_entered()
    {
        var obj = new ThrowingToString();

        var result = ValueRenderer.Render(obj);

        Assert.Equal("ThrowingToString{}", result);
        Assert.DoesNotContain("boom", result, StringComparison.Ordinal);
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
    public void A_computed_property_with_no_backing_field_is_never_invoked_and_never_appears_as_a_rendered_member()
    {
        // Rendering reads state, never runs behaviour: Boom has no compiler-
        // generated or conventionally named backing field, so it is not a
        // rendering candidate at all — the getter is never called, so it
        // cannot produce the error marker it used to. With no renderable
        // members left, ThrowingProperty renders as an empty object dump —
        // it is not a stateless leaf, so its ToString is not consulted either.
        var obj = new ThrowingProperty();

        var result = ValueRenderer.Render(obj);

        Assert.DoesNotContain("<error", result, StringComparison.Ordinal);
        Assert.DoesNotContain("boom", result, StringComparison.Ordinal);
        Assert.DoesNotContain("Boom", result, StringComparison.Ordinal);
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
    public void Throwing_dictionary_key_renders_a_typed_error_placeholder()
    {
        var dict = new System.Collections.Hashtable();
        dict[new ThrowingSummary { Value = 7 }] = "value";

        var result = ValueRenderer.Render(dict);

        // The guarded path names the caught exception's type (Java:
        // `<ExplodingKey>`); the old bare-ToString path could only say
        // `<error>`. The key is a throwing [NarrativeSummary] rather than a
        // throwing ToString because a key's ToString is no longer called: a
        // composite is not a stateless leaf.
        Assert.Equal("{<error: InvalidOperationException>=\"value\"}", result);
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
    public void Renders_a_completed_non_generic_Task_as_null_since_it_carries_no_result()
    {
        // TryTaskResult's non-generic branch: Task.CompletedTask is
        // deceptive here (verified, not assumed) — its runtime type is
        // Task`1[VoidTaskResult], not the plain Task this branch is for.
        // The non-generic TaskCompletionSource (added net5.0) is what
        // actually produces a Task whose runtime type is not generic, so
        // there is no Result property to reflect at all — the method
        // answers "read nothing, no error" rather than reaching for a
        // property that does not exist.
        var tcs = new TaskCompletionSource();
        tcs.SetResult();

        var result = ValueRenderer.Render(tcs.Task);

        Assert.Equal("null", result);
    }

    [Fact]
    public void DateTimeOffset_renders_in_round_trip_format()
    {
        var value = new DateTimeOffset(2026, 9, 17, 12, 30, 0, TimeSpan.Zero);

        var result = ValueRenderer.Render(value);

        Assert.Equal(value.ToString("O"), result);
    }

    [Fact]
    public void RenderNarrationText_sanitizes_and_caps_without_quoting()
    {
        var result = ValueRenderer.RenderNarrationText("hello\tworld");

        Assert.DoesNotContain("\"", result, StringComparison.Ordinal);
        Assert.Contains("hello", result, StringComparison.Ordinal);
    }

    [Fact]
    public void RenderNarrationText_redacts_a_credential_shaped_value_the_same_way_a_captured_string_would()
    {
        var jwt = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9."
            + "eyJzdWIiOiIxMjM0NTY3ODkwIn0."
            + "dozjgNryP4J3jVmNHl0w5N_XgL0n3I9PlFUP0THsR8U";

        var narrationText = ValueRenderer.RenderNarrationText(jwt);
        var capturedText = ValueRenderer.Render(jwt);

        Assert.Equal(RedactionPolicy.Marker, narrationText);
        Assert.Contains(RedactionPolicy.Marker, capturedText, StringComparison.Ordinal);
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
