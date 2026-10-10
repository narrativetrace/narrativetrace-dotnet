// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Tooling.Feedback;
using Xunit;

namespace NarrativeTrace.Tooling.Tests.Feedback;

public sealed class HomePathsTests
{
    [Theory]
    [InlineData("/Users/ada/work/orders", "~/work/orders")]
    [InlineData("/home/ada/work/orders", "~/work/orders")]
    [InlineData("C:\\Users\\ada\\work", "~\\work")]
    public void A_home_directory_is_rewritten_to_tilde(string path, string expected)
    {
        Assert.Equal(expected, HomePaths.ToTilde(path));
    }

    [Fact]
    public void The_rewrite_is_the_one_the_accepted_corpus_row_expects()
    {
        // After rewriting, the result must then pass the gate rather than be refused as a home path.
        var rewritten = HomePaths.ToTilde("/Users/ada/work/orders/build/narrativetrace");

        Assert.Equal("~/work/orders/build/narrativetrace", rewritten);
        Assert.DoesNotContain(ValueFreeCheck.RulesRefusing(rewritten), r => r.Id == "vf.home-path");
    }

    [Theory]
    [InlineData("/home")]
    [InlineData("/usr/share/ada")]
    [InlineData("build/narrativetrace")]
    public void An_ordinary_path_is_left_alone(string path)
    {
        Assert.Equal(path, HomePaths.ToTilde(path));
    }

    [Fact]
    public void Null_is_a_programming_error()
    {
        Assert.Throws<ArgumentNullException>(() => HomePaths.ToTilde(null!));
    }
}
