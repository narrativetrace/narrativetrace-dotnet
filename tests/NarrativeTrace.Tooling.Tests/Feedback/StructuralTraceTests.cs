// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Tooling.Feedback;
using Xunit;

namespace NarrativeTrace.Tooling.Tests.Feedback;

public sealed class StructuralTraceTests
{
    [Fact]
    public void A_well_formed_trace_with_every_outcome_marker_is_structural()
    {
        const string trace =
            "scenario: Weekend trip settles with three transfers\n"
            + "- OrderService.placeOrder(customerId, total)\n"
            + "- OrderService.placeOrder(customerId) → value\n"
            + "- Inventory.reserve(sku) !! IllegalStateException\n"
            + "- Inventory.reserve(sku) ?? incomplete\n"
            + "~ fork [2]\n";

        Assert.True(StructuralTrace.LooksStructural(trace));
    }

    [Fact]
    public void A_file_with_no_header_is_not_structural()
    {
        Assert.False(StructuralTrace.LooksStructural("- OrderService.placeOrder(customerId, total)\n"));
    }

    [Fact]
    public void A_header_whose_value_is_blank_is_not_a_header()
    {
        Assert.False(StructuralTrace.LooksStructural("scenario: \n"));
    }

    [Fact]
    public void A_rendered_call_line_with_a_value_is_not_structural()
    {
        const string rendered =
            "scenario: order is placed\n"
            + "- OrderService.placeOrder(customerId: \"C-1\")\n";

        Assert.False(StructuralTrace.LooksStructural(rendered));
    }

    [Fact]
    public void A_body_line_that_is_neither_call_nor_marker_fails_the_whole_file()
    {
        const string prose =
            "scenario: order is placed\n"
            + "just some prose that is not a call line\n";

        Assert.False(StructuralTrace.LooksStructural(prose));
    }

    [Fact]
    public void Null_is_a_programming_error()
    {
        Assert.Throws<ArgumentNullException>(() => StructuralTrace.LooksStructural(null!));
    }
}
