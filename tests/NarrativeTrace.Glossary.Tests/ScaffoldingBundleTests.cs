// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Glossary;
using Xunit;

namespace NarrativeTrace.Glossary.Tests;

/// <summary>Mirrors Java's <c>ScaffoldingBundleTest</c>.</summary>
public class ScaffoldingBundleTests
{
    [Fact]
    public void Spanish_bundle_translates_renderer_scaffolding()
    {
        var bundle = ScaffoldingBundle.ForLocale("es");

        Assert.Equal("devuelve", bundle.Returns);
        Assert.Equal("lanza", bundle.Throws);
        Assert.Equal("Vacíos del glosario", bundle.GapsHeading);
    }

    [Fact]
    public void Chinese_bundle_translates_renderer_scaffolding()
    {
        var bundle = ScaffoldingBundle.ForLocale("zh-CN");

        Assert.Equal("返回", bundle.Returns);
        Assert.Equal("在后台:", bundle.Background);
    }

    [Fact]
    public void Unknown_locale_falls_back_to_english_not_the_host_culture()
    {
        var bundle = ScaffoldingBundle.ForLocale("de");

        Assert.Equal("returns", bundle.Returns);
        Assert.Equal("throws", bundle.Throws);
        Assert.Equal("incomplete", bundle.Incomplete);
        Assert.Equal("fork", bundle.Fork);
        Assert.Equal("join", bundle.Join);
        Assert.Equal("In the background:", bundle.Background);
        Assert.Equal("Glossary gaps", bundle.GapsHeading);
    }

    /// <summary>
    /// A regional variant resolves through its parent, the way Java's
    /// resource-bundle chain does — <c>es-MX</c> reads the <c>es</c> bundle.
    /// </summary>
    [Fact]
    public void Regional_variant_resolves_through_its_parent_locale()
    {
        Assert.Equal("devuelve", ScaffoldingBundle.ForLocale("es-MX").Returns);
    }

    /// <summary>
    /// An unparseable tag is a locale with no bundle, not a crash — Java's
    /// <c>Locale.forLanguageTag</c> never throws either.
    /// </summary>
    [Fact]
    public void Unparseable_locale_tag_falls_back_to_english()
    {
        Assert.Equal("returns", ScaffoldingBundle.ForLocale("not a locale").Returns);
    }

    [Theory]
    [InlineData(" ")]
    [InlineData("")]
    [InlineData(null)]
    public void Rejects_a_blank_locale(string? localeTag)
    {
        Assert.Throws<ArgumentException>(() => ScaffoldingBundle.ForLocale(localeTag!));
    }
}
