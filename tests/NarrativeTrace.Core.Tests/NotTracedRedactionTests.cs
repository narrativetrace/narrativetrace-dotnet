// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using NarrativeTrace.Core.Annotation;
using Xunit;

namespace NarrativeTrace.Core.Tests;

/// <summary>
/// Field-level redaction parity with the JVM edition: a member annotated
/// [NotTraced] is redacted during reflective introspection even when its
/// name matches no deny-list pattern.
/// </summary>
public class NotTracedRedactionTests
{
    private sealed class PaymentCard
    {
        public string Last4 { get; init; } = "";

        [NotTraced]
        public string Pan { get; init; } = "";
    }

    [Fact]
    public void NotTraced_property_is_redacted_in_flat_rendering()
    {
        var card = new PaymentCard
        {
            Last4 = "1111",
            Pan = "4111111111111111",
        };

        var output = ValueRenderer.Render(card);

        Assert.Contains("Pan: [REDACTED]", output);
        Assert.Contains("Last4: \"1111\"", output);
        Assert.DoesNotContain("4111111111111111", output);
    }

    [Fact]
    public void NotTraced_property_is_redacted_in_structured_rendering()
    {
        var card = new PaymentCard
        {
            Last4 = "1111",
            Pan = "4111111111111111",
        };

        var structured = ValueRenderer.RenderStructured(card);

        var obj = Assert.IsType<RenderedValue.ObjectVal>(structured);
        Assert.Equal(
            new RenderedValue.StringVal(RedactionPolicy.Marker),
            obj.Fields["Pan"]);
        Assert.Equal(
            new RenderedValue.StringVal("1111"),
            obj.Fields["Last4"]);
    }

    // S1104: public fields are exactly what these cases cover — the [NotTraced]
    // Field target only means something because fields are introspected.
#pragma warning disable S1104
    private sealed class ApiClient
    {
        public string Endpoint = "";

        [NotTraced]
        public string ApiKey = "";
    }
#pragma warning restore S1104

    [Fact]
    public void NotTraced_field_is_redacted_in_flat_rendering()
    {
        var client = new ApiClient
        {
            Endpoint = "https://api.example.com",
            ApiKey = "sk-live-secret",
        };

        var output = ValueRenderer.Render(client);

        Assert.Contains("ApiKey: [REDACTED]", output);
        Assert.Contains("Endpoint: \"https://api.example.com\"", output);
        Assert.DoesNotContain("sk-live-secret", output);
    }

    [Fact]
    public void NotTraced_field_is_redacted_in_structured_rendering()
    {
        var client = new ApiClient
        {
            Endpoint = "https://api.example.com",
            ApiKey = "sk-live-secret",
        };

        var structured = ValueRenderer.RenderStructured(client);

        var obj = Assert.IsType<RenderedValue.ObjectVal>(structured);
        Assert.Equal(
            new RenderedValue.StringVal(RedactionPolicy.Marker),
            obj.Fields["ApiKey"]);
        Assert.Equal(
            new RenderedValue.StringVal("https://api.example.com"),
            obj.Fields["Endpoint"]);
    }

    private sealed record Credentials(
        string User, [NotTraced] string Passphrase);

    [Fact]
    public void NotTraced_on_positional_record_parameter_redacts_component()
    {
        var creds = new Credentials("alice", "correct horse battery");

        var flat = ValueRenderer.Render(creds);
        var structured = ValueRenderer.RenderStructured(creds);

        Assert.Contains("Passphrase: [REDACTED]", flat);
        Assert.DoesNotContain("correct horse battery", flat);
        var obj = Assert.IsType<RenderedValue.ObjectVal>(structured);
        Assert.Equal(
            new RenderedValue.StringVal(RedactionPolicy.Marker),
            obj.Fields["Passphrase"]);
    }

    [Fact]
    public void Attribute_redaction_survives_a_disabled_name_policy()
    {
        var card = new PaymentCard
        {
            Last4 = "1111",
            Pan = "4111111111111111",
        };
        var opts = new RenderOptions(
            Redaction: RedactionPolicy.Disabled);

        var flat = ValueRenderer.Render(card, opts);
        var structured = ValueRenderer.RenderStructured(card, opts);

        Assert.Contains("Pan: [REDACTED]", flat);
        var obj = Assert.IsType<RenderedValue.ObjectVal>(structured);
        Assert.Equal(
            new RenderedValue.StringVal(RedactionPolicy.Marker),
            obj.Fields["Pan"]);
    }
}
