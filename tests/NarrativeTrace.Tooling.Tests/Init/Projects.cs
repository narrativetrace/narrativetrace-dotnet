// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Text;

namespace NarrativeTrace.Tooling.Tests.Init;

/// <summary>
/// A real temporary project for a property to run in, and the readings a property takes of one.
/// </summary>
/// <remarks>
/// <para>The generative properties need a filesystem — "apply" means nothing without one — and they
/// need a clean one per try, so this owns the create-run-delete shape and the two whole-tree readings
/// both property classes compare against: every file with its exact bytes, and every symbolic link
/// left behind. The .NET equivalent of Java's <c>Projects</c> helper.</para>
/// <para>Neither the snapshot walk nor the clean-up DESCENDS through a link: <see cref="SnapshotOf"/>
/// runs only when no link remains, and <see cref="LinksUnder"/> and the clean-up check each entry's
/// link target and never recurse into one — so a project holding a link to a directory outside the
/// temp directory never costs that tree a file. <see cref="LinksUnder"/> is the one reading that
/// distinguishes a link from a real file.</para>
/// </remarks>
internal static class Projects
{
    /// <summary>Runs one case in a fresh temp directory, and deletes it whatever happened.</summary>
    internal static void InATemporaryOne(string prefix, Action<string> body)
    {
        var project = Path.Combine(Path.GetTempPath(), prefix + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(project);
        try
        {
            body(project);
        }
        finally
        {
            DeleteRecursively(project);
        }
    }

    /// <summary>Every file under the project, by relative path, with its exact bytes.</summary>
    internal static Dictionary<string, string> SnapshotOf(string project)
    {
        var files = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var file in Directory.EnumerateFiles(project, "*", SearchOption.AllDirectories))
        {
            files[Path.GetRelativePath(project, file).Replace(Path.DirectorySeparatorChar, '/')] =
                Encoding.UTF8.GetString(File.ReadAllBytes(file));
        }

        return files;
    }

    /// <summary>Every symbolic link left anywhere under the project — walked WITHOUT following one.</summary>
    internal static List<string> LinksUnder(string project)
    {
        var links = new List<string>();
        CollectLinks(project, links);
        return links;
    }

    private static void CollectLinks(string directory, List<string> links)
    {
        foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
        {
            if (new FileInfo(entry).LinkTarget is not null)
            {
                links.Add(entry);
            }
            else if (Directory.Exists(entry))
            {
                CollectLinks(entry, links);
            }
        }
    }

    internal static void DeleteRecursively(string directory)
    {
        foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
        {
            if (new FileInfo(entry).LinkTarget is not null)
            {
                DeleteLinkEntry(entry);
            }
            else if (Directory.Exists(entry))
            {
                DeleteRecursively(entry);
            }
            else
            {
                File.Delete(entry);
            }
        }

        Directory.Delete(directory, recursive: false);
    }

    private static void DeleteLinkEntry(string entry)
    {
        if (Directory.Exists(entry))
        {
            Directory.Delete(entry, recursive: false);
        }
        else
        {
            File.Delete(entry);
        }
    }
}
