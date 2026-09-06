// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Examples.Common;
using Xunit;

namespace NarrativeTrace.Examples.Common.Tests;

public sealed class DemoOptionsTests
{
    [Fact]
    public void No_arguments_means_the_bare_demo_format()
    {
        var options = DemoOptions.Parse([]);

        Assert.False(options.Classic);
    }

    [Fact]
    public void Classic_switch_selects_the_timestamped_log_format()
    {
        var options = DemoOptions.Parse(["--classic"]);

        Assert.True(options.Classic);
    }

    [Theory]
    [InlineData("--lang")]
    [InlineData("--classic=yes")]
    [InlineData("classic")]
    public void Unknown_argument_is_rejected_by_name(string argument)
    {
        var error = Assert.Throws<ArgumentException>(() => DemoOptions.Parse([argument]));

        Assert.Contains(argument, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Null_arguments_are_rejected()
    {
        Assert.Throws<ArgumentNullException>(() => DemoOptions.Parse(null!));
    }
}
