// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core.Annotation;
using Xunit;

namespace NarrativeTrace.Core.Tests;

/// <summary>
/// The declared contract of the narrative attributes. They carry no behavior
/// of their own — the proxy and <see cref="ValueRenderer"/> read them — so what
/// there is to pin is exactly what a reader has to be able to rely on: the
/// template survives construction verbatim, and a bare <c>[OnError]</c> matches
/// every exception.
/// </summary>
public class AnnotationAttributeTests
{
    [Fact]
    public void Narrated_exposes_the_template_it_was_given()
    {
        var attribute = new NarratedAttribute("Placing order of {quantity} for {customerId}");

        Assert.Equal("Placing order of {quantity} for {customerId}", attribute.Template);
    }

    [Fact]
    public void A_bare_OnError_keeps_its_template_and_matches_every_exception()
    {
        var attribute = new OnErrorAttribute("Payment declined for {customerId}");

        Assert.Equal("Payment declined for {customerId}", attribute.Template);
        Assert.Equal(typeof(Exception), attribute.ExceptionType);
    }

    [Fact]
    public void OnError_can_narrow_itself_to_one_exception_type()
    {
        var attribute = new OnErrorAttribute("Out of stock for {productId}")
        {
            ExceptionType = typeof(InvalidOperationException),
        };

        Assert.Equal(typeof(InvalidOperationException), attribute.ExceptionType);
    }

    [Fact]
    public void OnError_repeats_on_a_method_and_the_others_do_not()
    {
        Assert.True(Usage<OnErrorAttribute>().AllowMultiple);
        Assert.False(Usage<NarratedAttribute>().AllowMultiple);
        Assert.False(Usage<NarrativeSummaryAttribute>().AllowMultiple);
    }

    /// <summary>The declared <see cref="AttributeUsageAttribute"/> of an attribute type.</summary>
    private static AttributeUsageAttribute Usage<T>() where T : Attribute =>
        typeof(T).GetCustomAttributes(typeof(AttributeUsageAttribute), inherit: false)
            .Cast<AttributeUsageAttribute>()
            .Single();
}
