// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Xml.Linq;
using Xunit;

namespace Build.Tests;

/// <summary>
/// Guards the shipped NarrativeTrace.MSBuild props/targets. These files are
/// imported by every consuming project but never by this repo's own build, so
/// an XML defect in them (for example a '--' inside a comment) is invisible to
/// the rest of the suite and breaks the package for all consumers.
/// </summary>
public class MSBuildPackageTests
{
    private static readonly string RepoRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "../../../../../"));

    private static string BuildTransitive(string file) => Path.Combine(
        RepoRoot, "src", "NarrativeTrace.MSBuild", "buildTransitive", file);

    [Theory]
    [InlineData("NarrativeTrace.MSBuild.props")]
    [InlineData("NarrativeTrace.MSBuild.targets")]
    public void Shipped_buildTransitive_file_is_well_formed_xml(string file)
    {
        var path = BuildTransitive(file);
        Assert.True(File.Exists(path), $"missing shipped file: {path}");

        var doc = XDocument.Load(path);

        Assert.Equal("Project", doc.Root?.Name.LocalName);
    }
}
