// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Build;
using Xunit;

namespace Build.Tests;

/// <summary>
/// Covers the in-tree header ban behind the <c>HeaderAbsenceCheck</c> build
/// target — the inverse of <c>scripts/publish-public.sh</c>'s stamping step,
/// which <c>PublishScriptLicenseTests</c> covers from the other direction.
/// </summary>
public sealed class HeaderAbsenceSupportTests : IDisposable
{
    private readonly string _repo = Directory.CreateTempSubdirectory("nt-header-absence").FullName;

    public void Dispose() => Directory.Delete(_repo, recursive: true);

    private void Write(string relativePath, string content)
    {
        var file = Path.Combine(_repo, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, content);
    }

    [Fact]
    public void A_tree_with_no_headers_is_clean()
    {
        Write("src/Sample.cs", "namespace Sample;\n\nclass Widget { }\n");

        Assert.Empty(HeaderAbsenceSupport.Check(_repo));
    }

    [Fact]
    public void A_file_carrying_the_SPDX_line_is_reported()
    {
        Write("src/Sample.cs", "// SPDX-License-Identifier: BUSL-1.1\nclass Sample { }\n");

        var problem = Assert.Single(HeaderAbsenceSupport.Check(_repo));

        Assert.Contains("src/Sample.cs", problem, StringComparison.Ordinal);
        Assert.Contains("publish time only", problem, StringComparison.Ordinal);
    }

    [Fact]
    public void A_file_carrying_the_BSL_notice_without_an_SPDX_line_is_reported()
    {
        Write(
            "src/Sample.cs",
            "// Licensed under the Business Source License 1.1 (see LICENSE)\nclass Sample { }\n");

        Assert.Single(HeaderAbsenceSupport.Check(_repo));
    }

    [Fact]
    public void A_file_carrying_only_the_superseded_Apache_boilerplate_is_reported()
    {
        Write("src/Sample.cs", "// Licensed under the Apache License, Version 2.0\nclass Sample { }\n");

        Assert.Single(HeaderAbsenceSupport.Check(_repo));
    }

    [Fact]
    public void A_header_anywhere_in_the_first_ten_lines_is_reported()
    {
        var padding = string.Concat(Enumerable.Repeat("// pad\n", 8));
        Write("src/Sample.cs", padding + "// SPDX-License-Identifier: BUSL-1.1\nclass Sample { }\n");

        Assert.Single(HeaderAbsenceSupport.Check(_repo));
    }

    [Fact]
    public void A_header_past_the_first_ten_lines_is_not_reported()
    {
        var padding = string.Concat(Enumerable.Repeat("// pad\n", 10));
        Write("src/Sample.cs", padding + "// SPDX-License-Identifier: BUSL-1.1\nclass Sample { }\n");

        Assert.Empty(HeaderAbsenceSupport.Check(_repo));
    }

    [Fact]
    public void A_header_in_a_non_stamped_extension_is_not_reported()
    {
        Write("documentation/guide.md", "SPDX-License-Identifier: BUSL-1.1\n");

        Assert.Empty(HeaderAbsenceSupport.Check(_repo));
    }

    [Theory]
    [InlineData("src/obj/Sample.cs")]
    [InlineData("src/bin/Debug/Sample.cs")]
    [InlineData("artifacts/Sample.cs")]
    [InlineData("StrykerOutput/Sample.cs")]
    public void A_header_in_a_generated_output_directory_is_not_reported(string relativePath)
    {
        Write(relativePath, "// SPDX-License-Identifier: BUSL-1.1\nclass Sample { }\n");

        Assert.Empty(HeaderAbsenceSupport.Check(_repo));
    }

    [Fact]
    public void A_sibling_directory_that_merely_shares_the_excluded_name_as_a_prefix_is_still_scanned()
    {
        // "obj" must match as a whole path segment, not a prefix — "objects" is a real source folder.
        Write("src/objects/Sample.cs", "// SPDX-License-Identifier: BUSL-1.1\nclass Sample { }\n");

        Assert.Single(HeaderAbsenceSupport.Check(_repo));
    }

    [Fact]
    public void Multiple_offending_files_are_all_reported_sorted()
    {
        Write("src/B.cs", "// SPDX-License-Identifier: BUSL-1.1\nclass B { }\n");
        Write("src/A.cs", "// SPDX-License-Identifier: BUSL-1.1\nclass A { }\n");

        var problems = HeaderAbsenceSupport.Check(_repo);

        Assert.Equal(2, problems.Count);
        Assert.True(string.CompareOrdinal(problems[0], problems[1]) < 0);
    }

    [Fact]
    public void A_shell_script_header_is_reported_too()
    {
        Write("scripts/deploy.sh", "#!/usr/bin/env bash\n# SPDX-License-Identifier: BUSL-1.1\necho hi\n");

        Assert.Single(HeaderAbsenceSupport.Check(_repo));
    }
}
