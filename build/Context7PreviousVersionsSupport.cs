// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace NarrativeTrace.Build;

/// <summary>
/// The <c>context7.json</c> <c>previousVersions</c> rule: the list equals the <c>v*</c> tags, newest
/// first, plus this build's own version (<c>v&lt;VersionPrefix&gt;</c>), de-duplicated. The release tag
/// is minted on a tree that must already list it, so the own version is listed before its tag exists
/// and stays exact on the tag's own checkout once it does.
/// </summary>
internal static class Context7PreviousVersionsSupport
{
    private static readonly Regex VersionPrefix = new(
        @"<VersionPrefix>\s*(?<v>[^<\s]+)\s*</VersionPrefix>",
        RegexOptions.CultureInvariant);

    /// <summary>The rule over a repository: no git or no <c>v*</c> tags is a skip, never a failure.</summary>
    /// <param name="repoRoot">The repository root, holding <c>Directory.Build.props</c>.</param>
    /// <param name="listed">The tags <c>previousVersions</c> lists, in file order.</param>
    /// <returns>The problems, or <c>null</c> when the rule does not apply (no git history with tags).</returns>
    /// <exception cref="InvalidOperationException">Tags exist but the version source is missing or has no <c>VersionPrefix</c>.</exception>
    public static IReadOnlyList<string>? Check(string repoRoot, IReadOnlyList<string> listed)
    {
        if (repoRoot is null)
            throw new ArgumentNullException(nameof(repoRoot));
        if (listed is null)
            throw new ArgumentNullException(nameof(listed));
        var tags = VersionTags(repoRoot);
        if (tags.Count == 0)
            return null;
        return Problems(tags, BuildVersion(repoRoot), listed);
    }

    /// <summary>The pure rule: <paramref name="listed"/> against <paramref name="tags"/> plus <paramref name="buildVersion"/>.</summary>
    /// <param name="tags">The <c>v*</c> tags in <c>git tag --sort=-v:refname</c> order.</param>
    /// <param name="buildVersion">This build's version without the <c>v</c>.</param>
    /// <param name="listed">The tags <c>previousVersions</c> lists, in file order.</param>
    /// <returns>One problem naming expected and listed, or empty when they match exactly.</returns>
    public static IReadOnlyList<string> Problems(
        IReadOnlyList<string> tags, string buildVersion, IReadOnlyList<string> listed)
    {
        if (tags is null)
            throw new ArgumentNullException(nameof(tags));
        if (string.IsNullOrWhiteSpace(buildVersion))
            throw new ArgumentException("The build version must not be blank.", nameof(buildVersion));
        if (listed is null)
            throw new ArgumentNullException(nameof(listed));
        var expected = ExpectedTags(tags, "v" + buildVersion);
        if (expected.SequenceEqual(listed))
            return [];
        return
        [
            $"context7.json previousVersions [{string.Join(", ", listed)}] must equal the v* tags plus this " +
            $"build's version v{buildVersion}, newest first: [{string.Join(", ", expected)}]",
        ];
    }

    private static List<string> ExpectedTags(IReadOnlyList<string> tags, string ownTag)
    {
        var expected = tags.ToList();
        if (!expected.Contains(ownTag))
            expected.Insert(NewestFirstIndex(expected, ownTag), ownTag);
        return expected;
    }

    private static int NewestFirstIndex(List<string> newestFirst, string tag)
    {
        var index = newestFirst.FindIndex(existing => Compare(tag, existing) > 0);
        return index < 0 ? newestFirst.Count : index;
    }

    private static int Compare(string left, string right)
    {
        var l = Numbers(left);
        var r = Numbers(right);
        for (var i = 0; i < Math.Max(l.Length, r.Length); i++)
        {
            var order = (i < l.Length ? l[i] : 0).CompareTo(i < r.Length ? r[i] : 0);
            if (order != 0)
                return order;
        }
        return 0;
    }

    private static int[] Numbers(string tag) =>
        tag.TrimStart('v').Split('.')
            .Select(part => int.TryParse(new string(part.TakeWhile(char.IsDigit).ToArray()), out var n) ? n : 0)
            .ToArray();

    private static string BuildVersion(string repoRoot)
    {
        var props = Path.Combine(repoRoot, "Directory.Build.props");
        var match = File.Exists(props) ? VersionPrefix.Match(File.ReadAllText(props)) : Match.Empty;
        if (!match.Success)
            throw new InvalidOperationException(
                $"context7 previousVersions gate: {props} carries no <VersionPrefix> — the rule needs the build's version");
        return match.Groups["v"].Value;
    }

    private static List<string> VersionTags(string repoRoot)
    {
        if (!Directory.Exists(Path.Combine(repoRoot, ".git")))
            return [];
        var info = new ProcessStartInfo("git", ["tag", "--list", "v*", "--sort=-v:refname"])
        {
            WorkingDirectory = repoRoot,
            RedirectStandardOutput = true,
        };
        using var git = Process.Start(info)!;
        var tags = git.StandardOutput.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        git.WaitForExit();
        return [.. tags];
    }
}
