// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Cli;

/// <summary>
/// The <c>approve</c> verb: promotes every reviewed <c>*.received.nt</c> under a directory onto the
/// <c>*.approved.nt</c> beside it — an adopter's counterpart of this repository's own
/// <c>Approve</c> build target, and the pin's last act.
/// </summary>
/// <remarks>
/// <para>INTENT: approval mode writes a <c>.received.nt</c> beside the baseline when a run's structure
/// differs from it (or none exists yet); promoting it is the decision that the new shape is meant.
/// That decision is the user's, so this verb only ever runs after it — it promotes whatever review
/// copies are there, which is why the copies must have been shown first.</para>
/// <para><b>@edgeCase</b> <c>bin</c>, <c>obj</c>, <c>.git</c> and <c>node_modules</c> are never
/// searched: a review copy inside build output is a stale artifact of a run, never a baseline to
/// commit. Nor is a directory symlink followed.</para>
/// </remarks>
public static class ApproveCommand
{
    private const string ReceivedSuffix = ".received.nt";
    private const string ApprovedSuffix = ".approved.nt";
    private static readonly string[] Skipped = ["bin", "obj", ".git", "node_modules"];

    /// <summary>Promotes every review copy under <paramref name="root"/>; returns the exit code.</summary>
    public static int Run(string root, TextWriter output, TextWriter error)
    {
        if (!Directory.Exists(root))
        {
            error.WriteLine($"error: approve: no such directory: {root}");
            return 2;
        }

        var received = ReceivedUnder(root).OrderBy(path => path, StringComparer.Ordinal).ToList();
        if (received.Count == 0)
        {
            output.WriteLine($"nothing to approve: no {ReceivedSuffix} under {root}");
            return 0;
        }

        foreach (var file in received)
        {
            output.WriteLine("approved: " + Path.GetRelativePath(root, Promote(file)));
        }

        return 0;
    }

    private static string Promote(string receivedFile)
    {
        var approved = receivedFile[..^ReceivedSuffix.Length] + ApprovedSuffix;
        File.Move(receivedFile, approved, overwrite: true);
        return approved;
    }

    /// <summary>
    /// A directory symlink is never followed: one pointing back up loops forever, and one pointing out
    /// of the directory would promote review copies that belong to another project.
    /// </summary>
    private static bool IsLink(string directory) =>
        new DirectoryInfo(directory).Attributes.HasFlag(FileAttributes.ReparsePoint);

    private static IEnumerable<string> ReceivedUnder(string directory)
    {
        foreach (var file in Directory.EnumerateFiles(directory))
        {
            if (file.EndsWith(ReceivedSuffix, StringComparison.Ordinal))
            {
                yield return file;
            }
        }

        foreach (var child in Directory.EnumerateDirectories(directory))
        {
            if (Skipped.Contains(Path.GetFileName(child), StringComparer.Ordinal) || IsLink(child))
            {
                continue;
            }

            foreach (var file in ReceivedUnder(child))
            {
                yield return file;
            }
        }
    }
}
