// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using Xunit;

namespace NarrativeTrace.Core.Tests;

public class SpanContextFieldsTests
{
    [Theory]
    [InlineData("ServiceName", AttributeTier.Resource)]
    [InlineData("Environment", AttributeTier.Resource)]
    [InlineData("HttpRoute", AttributeTier.Trace)]
    [InlineData("TenantId", AttributeTier.Trace)]
    [InlineData("StoryId", AttributeTier.Trace)]
    [InlineData("TraceId", AttributeTier.Span)]
    [InlineData("SpanName", AttributeTier.Span)]
    public void Tier_classifies_known_fields(
        string field, AttributeTier expected)
    {
        Assert.Equal(expected, SpanContextFields.Tier(field));
    }

    [Fact]
    public void Tier_throws_for_unknown_field()
    {
        var ex = Assert.Throws<ArgumentException>(
            () => SpanContextFields.Tier("nope"));
        Assert.Contains("nope", ex.Message);
    }

    [Fact]
    public void All_classifies_seventeen_fields_exhaustively()
    {
        Assert.Equal(17, SpanContextFields.All.Count);
    }
}
