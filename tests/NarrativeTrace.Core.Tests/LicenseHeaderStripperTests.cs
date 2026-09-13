// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using Xunit;

namespace NarrativeTrace.Core.Tests;

/// <summary>
/// The shared publish-time header stripper (docs snippet embeds and the skill-catalogue renderer
/// both call this) — mirrors the coverage <c>SnippetCheckSupportTests</c> already had for the
/// pre-extraction, build-local copy of this exact algorithm.
/// </summary>
public sealed class LicenseHeaderStripperTests
{
    private const string LicenseHeader =
        "// SPDX-License-Identifier: BUSL-1.1\n"
        + "// Licensed under the Business Source License 1.1 (see LICENSE)\n"
        + "// Copyright (c) 2026 Empower Agile";

    [Fact]
    public void A_license_header_is_stripped()
    {
        var stripped = LicenseHeaderStripper.Strip(LicenseHeader + "\nConsole.WriteLine(\"hi\");\n");

        Assert.Equal("Console.WriteLine(\"hi\");\n", stripped);
    }

    [Fact]
    public void A_license_header_with_a_blank_line_after_it_is_stripped_along_with_the_blank_line()
    {
        var stripped = LicenseHeaderStripper.Strip(LicenseHeader + "\n\nConsole.WriteLine(\"hi\");\n");

        Assert.Equal("Console.WriteLine(\"hi\");\n", stripped);
    }

    [Fact]
    public void A_source_without_a_license_header_is_unchanged()
    {
        const string text = "using System;\nConsole.WriteLine(\"hi\");\n";

        Assert.Equal(text, LicenseHeaderStripper.Strip(text));
    }

    [Fact]
    public void A_non_license_leading_comment_is_preserved()
    {
        const string text = "// A worked example, not the license header.\nConsole.WriteLine(\"hi\");\n";

        Assert.Equal(text, LicenseHeaderStripper.Strip(text));
    }

    [Fact]
    public void A_leading_block_comment_carrying_the_spdx_phrase_is_stripped()
    {
        const string text = "/* SPDX-License-Identifier: BUSL-1.1 */\nConsole.WriteLine(\"hi\");\n";

        Assert.Equal("Console.WriteLine(\"hi\");\n", LicenseHeaderStripper.Strip(text));
    }

    [Fact]
    public void An_empty_string_is_unchanged()
    {
        Assert.Equal("", LicenseHeaderStripper.Strip(""));
    }
}
