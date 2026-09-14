// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using Xunit;

namespace NarrativeTrace.Core.Tests;

/// <summary>
/// Value-shape masking (JWT/PAN/<c>Set-Cookie</c>) applied at render time,
/// independent of field name — pinned on both the flat and structured
/// render paths, since a fix on one alone leaves the other leaking
/// (found by mirroring an adversarial audit).
/// </summary>
public class RedactionValueShapeRenderingTests
{
    private const string CardPan = "4111111111111111";
    private const string NonCardOrderNumber = "4111111111111112";

    private sealed record Blob(string Description);

    [Fact]
    public void A_card_number_in_an_innocuously_named_field_is_masked_in_flat_rendering()
    {
        var blob = new Blob(CardPan);

        var output = ValueRenderer.Render(blob);

        Assert.Contains("Description: [REDACTED]", output);
        Assert.DoesNotContain(CardPan, output);
    }

    [Fact]
    public void A_card_number_in_an_innocuously_named_field_is_masked_in_structured_rendering()
    {
        var blob = new Blob(CardPan);

        var structured = ValueRenderer.RenderStructured(blob);

        var obj = Assert.IsType<RenderedValue.ObjectVal>(structured);
        Assert.Equal(
            new RenderedValue.StringVal(RedactionPolicy.Marker),
            obj.Fields["Description"]);
    }

    [Fact]
    public void A_bare_top_level_card_number_string_is_masked_in_flat_rendering()
    {
        var output = ValueRenderer.Render(CardPan);

        Assert.Equal(RedactionPolicy.Marker, output);
    }

    [Fact]
    public void A_bare_top_level_card_number_string_is_masked_in_structured_rendering()
    {
        var structured = ValueRenderer.RenderStructured(CardPan);

        Assert.Equal(
            new RenderedValue.StringVal(RedactionPolicy.Marker), structured);
    }

    [Fact]
    public void An_order_number_that_fails_luhn_stays_visible_in_flat_rendering()
    {
        var output = ValueRenderer.Render(NonCardOrderNumber);

        Assert.Equal($"\"{NonCardOrderNumber}\"", output);
    }

    [Fact]
    public void An_order_number_that_fails_luhn_stays_visible_in_structured_rendering()
    {
        var structured = ValueRenderer.RenderStructured(NonCardOrderNumber);

        Assert.Equal(new RenderedValue.StringVal(NonCardOrderNumber), structured);
    }

    [Fact]
    public void Value_shape_masking_is_off_under_the_disabled_policy_in_both_paths()
    {
        var opts = new RenderOptions(Redaction: RedactionPolicy.Disabled);

        var flat = ValueRenderer.Render(CardPan, opts);
        var structured = ValueRenderer.RenderStructured(CardPan, opts);

        Assert.Equal($"\"{CardPan}\"", flat);
        Assert.Equal(new RenderedValue.StringVal(CardPan), structured);
    }

    [Fact]
    public void A_jwt_shaped_list_item_is_masked_in_both_paths()
    {
        const string jwt =
            "eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxMjM0NTY3ODkwIn0.dGhpc2lzYXNpZ25hdHVyZQ";
        var headers = new List<string> { jwt };

        var flat = ValueRenderer.Render(headers);
        var structured = ValueRenderer.RenderStructured(headers);

        Assert.Contains(RedactionPolicy.Marker, flat);
        Assert.DoesNotContain(jwt, flat);
        var list = Assert.IsType<RenderedValue.ListVal>(structured);
        Assert.Equal(
            new RenderedValue.StringVal(RedactionPolicy.Marker), list.Elements[0]);
    }

    // "Bearer" and "JWT" name the same structural shape in this codebase
    // (SecretValueShapes.IsJwt) — a bearer token is carried as a JWT, so the
    // shape check that catches one catches the other.
    private const string BearerToken =
        "eyJhbGciOiJIUzI1NiJ9.eyJzY29wZSI6InJlYWQifQ.c2lnbmF0dXJlYnl0ZXM";

    [Fact]
    public void A_bearer_shaped_map_key_is_masked_on_both_paths()
    {
        var map = new Dictionary<string, object> { [BearerToken] = "ok" };

        var flat = ValueRenderer.Render(map);
        var structured = ValueRenderer.RenderStructured(map);

        Assert.DoesNotContain(BearerToken, flat);
        Assert.Contains(RedactionPolicy.Marker, flat);
        var obj = Assert.IsType<RenderedValue.ObjectVal>(structured);
        var field = Assert.Single(obj.Fields);
        Assert.Equal(RedactionPolicy.Marker, field.Key);
        Assert.Equal(new RenderedValue.StringVal("ok"), field.Value);
    }

    [Fact]
    public void A_pan_shaped_map_key_is_masked_on_both_paths()
    {
        var map = new Dictionary<string, object> { [CardPan] = "ok" };

        var flat = ValueRenderer.Render(map);
        var structured = ValueRenderer.RenderStructured(map);

        Assert.DoesNotContain(CardPan, flat);
        Assert.Contains(RedactionPolicy.Marker, flat);
        var obj = Assert.IsType<RenderedValue.ObjectVal>(structured);
        var field = Assert.Single(obj.Fields);
        Assert.Equal(RedactionPolicy.Marker, field.Key);
        Assert.Equal(new RenderedValue.StringVal("ok"), field.Value);
    }

    [Fact]
    public void An_ordinary_map_key_is_untouched_and_the_key_name_deny_list_still_wins_on_both_paths()
    {
        var map = new Dictionary<string, object>
        {
            ["note"] = "hello",
            ["cardNumber"] = "not-a-real-card",
        };

        var flat = ValueRenderer.Render(map);
        var structured = ValueRenderer.RenderStructured(map);

        Assert.Contains("note=\"hello\"", flat);
        Assert.Contains($"cardNumber={RedactionPolicy.Marker}", flat);
        var obj = Assert.IsType<RenderedValue.ObjectVal>(structured);
        Assert.Equal(new RenderedValue.StringVal("hello"), obj.Fields["note"]);
        Assert.Equal(
            new RenderedValue.StringVal(RedactionPolicy.Marker), obj.Fields["cardNumber"]);
    }
}
