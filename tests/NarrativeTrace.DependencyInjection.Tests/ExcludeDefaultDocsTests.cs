// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Text.RegularExpressions;
using Xunit;

namespace NarrativeTrace.DependencyInjection.Tests;

/// <summary>The published default exclude list is the code's, not a second typed copy.</summary>
public class ExcludeDefaultDocsTests
{
    private static readonly string Guide = Path.Combine("documentation", "guides", "dependency-injection.md");

    [Fact]
    public void Guide_publishes_the_shipped_default_verbatim_and_in_order()
    {
        var published = PublishedPrefixes(File.ReadAllText(RepoFile(Guide)));

        Assert.Equal(InstrumentationExclusions.DefaultExclude, published);
    }

    [Fact]
    public void Guide_and_configuration_guide_show_include_and_exclude_together()
    {
        foreach (var page in new[] { Guide, Path.Combine("documentation", "guides", "configuration.md") })
        {
            var text = File.ReadAllText(RepoFile(page));
            Assert.Contains(".Namespaces(", text, StringComparison.Ordinal);
            Assert.Contains("ExcludeNamespaces(", text, StringComparison.Ordinal);
            Assert.Contains(InstrumentationExclusions.EnvironmentKey, text, StringComparison.Ordinal);
        }
    }

    private static List<string> PublishedPrefixes(string guide)
    {
        var start = guide.IndexOf("## Instrumentation exclusions", StringComparison.Ordinal);
        var end = guide.IndexOf("## See also", start, StringComparison.Ordinal);
        return Regex.Matches(guide[start..end], @"^\| `([^`]+)` \|", RegexOptions.Multiline)
            .Select(m => m.Groups[1].Value)
            .ToList();
    }

    private static string RepoFile(string relative)
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "NarrativeTrace.sln")))
            {
                return Path.Combine(dir.FullName, relative);
            }
        }

        throw new InvalidOperationException("NarrativeTrace.sln not found above " + AppContext.BaseDirectory);
    }
}
