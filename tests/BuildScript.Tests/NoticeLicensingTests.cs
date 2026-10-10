// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using Xunit;

namespace Build.Tests;

/// <summary>
/// NOTICE is the file a reader consults to learn which licence covers what; each package's own
/// <c>PackageLicenseExpression</c> is the file the build actually packs by. Gated against each
/// other rather than reviewed by eye, mirroring the golden Java repo's own <c>licensingCheck</c>
/// (this port carries no central licensing-categories file — the source of truth is the set of
/// <c>.csproj</c> files declaring <c>PackageLicenseExpression&gt;Apache-2.0</c>).
/// </summary>
public sealed class NoticeLicensingTests
{
    private static string NoticeText => File.ReadAllText(Path.Combine(RepositoryPath.Root(), "NOTICE"));

    [Fact]
    public void Every_package_declaring_PackageLicenseExpression_Apache_2_0_is_named_in_NOTICE()
    {
        var notice = NoticeText;
        var omissions = ApacheLicensedProjectNames()
            .Where(name => !notice.Contains(name, StringComparison.Ordinal))
            .ToList();

        Assert.Empty(omissions);
    }

    [Fact]
    public void NOTICE_names_the_rendered_page_roots_and_the_marketplace_file()
    {
        var notice = NoticeText;

        Assert.Contains(".claude/skills/**", notice, StringComparison.Ordinal);
        Assert.Contains(".agents/skills/**", notice, StringComparison.Ordinal);
        Assert.Contains(".claude-plugin/marketplace.json", notice, StringComparison.Ordinal);
    }

    [Fact]
    public void NOTICE_still_names_every_other_package_as_BSL()
    {
        Assert.Contains("Business Source License 1.1", NoticeText, StringComparison.Ordinal);
        Assert.Contains("see LICENSE.", NoticeText, StringComparison.Ordinal);
    }

    /// <summary>Every <c>src/*/*.csproj</c> project name whose own file declares Apache-2.0.</summary>
    private static List<string> ApacheLicensedProjectNames()
    {
        var srcRoot = Path.Combine(RepositoryPath.Root(), "src");
        return Directory.EnumerateFiles(srcRoot, "*.csproj", SearchOption.AllDirectories)
            .Where(path => File.ReadAllText(path).Contains(
                "<PackageLicenseExpression>Apache-2.0</PackageLicenseExpression>", StringComparison.Ordinal))
            .Select(path => Path.GetFileNameWithoutExtension(path))
            .ToList();
    }
}
