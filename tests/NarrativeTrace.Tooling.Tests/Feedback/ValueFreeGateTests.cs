// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Tooling.Feedback;
using Xunit;

namespace NarrativeTrace.Tooling.Tests.Feedback;

/// <summary>
/// The value-free gate's own behaviour, independent of the shared corpus replay (which lives in the
/// security suite). The accepted half is asserted BEFORE the rejected half, deliberately: the
/// accepted rows are where a port discovers its rules are unusable, and a gate that refuses an
/// install coordinate, a doctor finding id or a hyphenated sentence is one an agent learns to route
/// around.
/// </summary>
public sealed class ValueFreeGateTests
{
    // ---- accepted: the gate must let these through, every rule silent ----

    [Theory]
    [InlineData("- OrderService.placeOrder(customerId, total)")]
    [InlineData("- OrderService.placeOrder(customerId) → value")]
    [InlineData("- Inventory.reserve(sku) !! IllegalStateException")]
    [InlineData("- Login.authenticate(username, password)")]
    [InlineData("ai.narrativetrace:narrativetrace-core:0.2.4")]
    [InlineData("trap.redaction-proof")]
    [InlineData("https://narrativetrace.ai/docs/privacy-and-redaction#redaction-surface-by-surface")]
    [InlineData("build/narrativetrace/structural/OrderServiceTest/order_is_placed.nt")]
    [InlineData("~/work/orders/build/narrativetrace")]
    [InlineData("the authorization header: it never arrived at all")]
    [InlineData("the @NotTraced import was never applied to the field")]
    [InlineData("the-quick-brown-fox-jumps-over-the-lazy-dog")]
    [InlineData("placeOrder(id: \"C-1\")")]
    [InlineData("- **OrderService.placeOrder**(customerId, total)")]
    public void No_rule_refuses_a_value_free_report(string text)
    {
        Assert.Empty(ValueFreeCheck.RulesRefusing(text));
    }

    // ---- rejected: each shape named by the rule that must fire ----

    /// <summary>
    /// Every runtime's Markdown renderer bolds the qualified name, so an emphasis marker sits between the
    /// method and the parenthesis. Pins the class (every marker the grammar allows), not the one line.
    /// </summary>
    [Theory]
    [InlineData("**")]
    [InlineData("__")]
    [InlineData("*")]
    [InlineData("_")]
    public void The_rendered_call_rule_crosses_every_markdown_emphasis_on_the_qualified_name(string mark)
    {
        Assert.Contains(
            ValueFreeCheck.RulesRefusing($"- {mark}Order.place{mark}(customerId: `\"C-1\"`)"),
            rule => rule.Id == "vf.rendered-call");
    }

    /// <summary>
    /// The emphasis marker is a bounded class of up to two marks, not an alternation of the four
    /// markers: a stray mixed pair no renderer writes is admitted too, which only makes a deny rule
    /// stricter — and the bounded class is the shape a ReDoS gate accepts.
    /// </summary>
    [Fact]
    public void The_rendered_call_rule_also_crosses_a_stray_mixed_emphasis_pair()
    {
        Assert.Contains(
            ValueFreeCheck.RulesRefusing("- Order.place*_(customerId: `\"C-1\"`)"),
            rule => rule.Id == "vf.rendered-call");
    }

    [Theory]
    [InlineData("OrderService.placeOrder(customerId: \"C-1234\", total: 19.99)", "vf.rendered-call")]
    [InlineData("OrderService.placeOrder(customerId) → \"ORD-9001\"", "vf.rendered-outcome")]
    [InlineData("OrderService.placeOrder(customerId) — 1ms", "vf.duration")]
    [InlineData("Login.authenticate(user: \"ada\", password: [REDACTED])", "vf.marker")]
    [InlineData("userPassword=hunter2", "vf.named-secret")]
    [InlineData("ghp_0123456789abcdefghij", "vf.value-shape")]
    [InlineData("a3f5c8e9d2b14706a3f5c8e9d2b14706", "vf.entropy")]
    [InlineData("ada@example.com saw it first on the staging build", "vf.email")]
    [InlineData("/Users/ada/work/orders/build/narrativetrace", "vf.home-path")]
    [InlineData("trap.\u202eredaction-proof", "vf.control")]
    public void The_named_rule_refuses_the_shape_it_is_for(string text, string ruleId)
    {
        Assert.Contains(ValueFreeCheck.RulesRefusing(text), rule => rule.Id == ruleId);
    }

    // ---- the adjacency bug class: the scan may not consume the next token, and reads a quoted key ----

    [Fact]
    public void An_indented_yaml_password_is_caught_though_it_follows_another_key()
    {
        // datasource: ... then password: — consuming the value's first char resumed inside "assword".
        const string yaml = "spring:\n  datasource:\n    password: hunter2";

        Assert.Contains(ValueFreeCheck.RulesRefusing(yaml), rule => rule.Id == "vf.named-secret");
    }

    [Fact]
    public void A_json_quoted_password_key_is_read_despite_the_closing_quote_before_the_colon()
    {
        const string json = "{\n  \"datasource\": {\n    \"password\": \"hunter2\"\n  }\n}";

        Assert.Contains(ValueFreeCheck.RulesRefusing(json), rule => rule.Id == "vf.named-secret");
    }

    // ---- the field-scoped exemption: the marker is ignored ONLY in the doctor-report field ----

    [Fact]
    public void The_marker_refuses_an_ordinary_field()
    {
        var fields = new Dictionary<string, string> { ["happened"] = "the value came back [REDACTED]" };

        Assert.Contains(ValueFreeCheck.Violations(fields), v => v.Rule.Id == "vf.marker");
    }

    [Fact]
    public void The_marker_is_exempt_in_the_doctor_report_field_only()
    {
        var fields = new Dictionary<string, string>
        {
            [ValueFreeCheck.DoctorReportField] = "{\"fix\":\"assert the literal [REDACTED]\"}",
        };

        Assert.DoesNotContain(ValueFreeCheck.Violations(fields), v => v.Rule.Id == "vf.marker");
    }

    [Fact]
    public void The_exemption_is_the_marker_and_nothing_else_in_the_doctor_report_field()
    {
        // An email inside the doctor's JSON is still refused — the exemption is one rule wide.
        var fields = new Dictionary<string, string>
        {
            [ValueFreeCheck.DoctorReportField] = "{\"message\":\"ada@example.com and [REDACTED]\"}",
        };

        var rules = ValueFreeCheck.Violations(fields).Select(v => v.Rule.Id).ToList();
        Assert.Contains("vf.email", rules);
        Assert.DoesNotContain("vf.marker", rules);
    }

    [Fact]
    public void Violations_are_ordered_by_field_then_rule_declaration_order()
    {
        var fields = new Dictionary<string, string>
        {
            ["did"] = "ada@example.com",
            ["happened"] = "Login.authenticate(user: \"ada\")",
        };

        var described = ValueFreeCheck.Violations(fields).Select(v => v.Field).ToList();
        Assert.Equal(["did", "happened"], described);
    }

    [Fact]
    public void A_null_field_value_is_a_caller_bug_not_empty_input()
    {
        var fields = new Dictionary<string, string> { ["did"] = null! };

        Assert.Throws<ArgumentException>(() => ValueFreeCheck.Violations(fields));
    }

    [Fact]
    public void A_rule_reads_text_never_null()
    {
        Assert.Throws<ArgumentNullException>(() => ValueFreeRule.Marker.Rejects(null!));
    }

    [Fact]
    public void There_are_exactly_ten_rules_in_declaration_order()
    {
        var ids = ValueFreeRule.All().Select(rule => rule.Id).ToList();

        Assert.Equal(
            [
                "vf.rendered-call", "vf.rendered-outcome", "vf.duration", "vf.marker", "vf.named-secret",
                "vf.value-shape", "vf.entropy", "vf.email", "vf.home-path", "vf.control",
            ],
            ids);
    }
}
