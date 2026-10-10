// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Text;

namespace NarrativeTrace.Tooling.Init;

/// <summary>
/// Applies a plan to a project directory. The only class in the installer that writes.
/// </summary>
/// <remarks>
/// <para>INTENT: keeps the decisions and the writing apart. The executor asks the project NOTHING — a
/// plan carries every file's whole text — so what a person reviewed as a diff is exactly what lands,
/// and the planner stays testable without a filesystem.</para>
/// <para>Every write is temp-file-then-atomic-move inside the TARGET directory, so a failed write
/// leaves the original file exactly as it was; a rename across filesystems, which a temp directory
/// elsewhere would force, is not atomic. Nothing here throws on a file that will not cooperate: the
/// action is reported as refused, the rest of the plan runs, and the exit code carries the news. A
/// dry-run plan is the one exception — applying one is a programming error, not a filesystem one.
/// Files are written UTF-8 with no byte-order mark added, so a BOM the plan's text carries survives
/// and one it does not is not invented.</para>
/// </remarks>
public static class PlanExecutor
{
    private const string TemporaryPrefix = ".narrativetrace-";

    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>Applies every action, in order.</summary>
    /// <exception cref="ArgumentException">When the plan is a dry run, or the directory is not one.</exception>
    public static ExecutionReport Execute(InitPlan plan, string projectDirectory)
    {
        if (plan is null || projectDirectory is null)
        {
            throw new ArgumentException("applying a plan needs the plan and a project directory");
        }

        if (plan.DryRun)
        {
            throw new ArgumentException("a dry run is shown, never applied");
        }

        if (!Directory.Exists(projectDirectory))
        {
            throw new ArgumentException(projectDirectory + " is not a directory");
        }

        var results = plan.Actions.Select(action => Apply(action, projectDirectory)).ToList();
        return new ExecutionReport(plan.Carrier, results);
    }

    private static ExecutionReport.Applied Apply(Action action, string projectDirectory)
    {
        try
        {
            return action switch
            {
                Refuse refusal => ExecutionReport.Applied.Refused(action, refusal.Reason),
                ReplaceLink replacement => ReplaceTheLink(replacement, projectDirectory),
                _ => ApplyFilesystem(action, projectDirectory),
            };
        }
        catch (IOException exception)
        {
            return ExecutionReport.Applied.Refused(action, exception.GetType().Name + ": " + exception.Message);
        }
    }

    /// <summary>Every action but a refusal and a link replacement: a delete or a write, never through a link.</summary>
    private static ExecutionReport.Applied ApplyFilesystem(Action action, string projectDirectory)
    {
        var target = Path.Combine(
            projectDirectory, action.Path.Replace('/', Path.DirectorySeparatorChar));
        RequireNoLinkOnTheWay(projectDirectory, action.Path);
        return action switch
        {
            DeleteDirectory => DeleteTheDirectory(action, target),
            DeleteFile => DeleteTheFile(action, target),
            FileEdit edit => WriteEdit(action, target, edit),
            _ => throw new InvalidOperationException("an action kind the executor does not know"),
        };
    }

    /// <summary>
    /// The one action that begins by deleting: the link goes first — the link itself, never what it
    /// points at — so the write that follows creates a real directory or file of the project's own.
    /// </summary>
    private static ExecutionReport.Applied ReplaceTheLink(ReplaceLink action, string projectDirectory)
    {
        DeleteLink(Path.Combine(projectDirectory, action.Link.Replace('/', Path.DirectorySeparatorChar)));
        RequireNoLinkOnTheWay(projectDirectory, action.Path);
        Write(Path.Combine(projectDirectory, action.Path.Replace('/', Path.DirectorySeparatorChar)), action.After);
        return ExecutionReport.Applied.Ran(action);
    }

    /// <summary>
    /// No write and no delete ever passes THROUGH a symbolic link. The planners refuse every link they
    /// can see; this is the guarantee for one they cannot — a link made between the read and the write,
    /// or one further up the path than a planner looks. The action is refused like any other filesystem
    /// refusal, and the rest of the plan still runs.
    /// </summary>
    private static void RequireNoLinkOnTheWay(string projectDirectory, string relative)
    {
        var walked = projectDirectory;
        foreach (var element in relative.Split('/'))
        {
            walked = Path.Combine(walked, element);
            if (IsSymbolicLink(walked))
            {
                throw new IOException(walked + " is a symbolic link, and nothing is written through one");
            }
        }
    }

    /// <summary>Removes the link itself — never what it points at. A directory link and a page link both unlink.</summary>
    private static void DeleteLink(string link)
    {
        if (!IsSymbolicLink(link))
        {
            return;
        }

        if (Directory.Exists(link))
        {
            Directory.Delete(link, recursive: false);
        }
        else
        {
            File.Delete(link);
        }
    }

    /// <summary>Whether the path itself is a symbolic link, without following it.</summary>
    private static bool IsSymbolicLink(string path) => new FileInfo(path).LinkTarget is not null;

    private static ExecutionReport.Applied WriteEdit(Action action, string target, FileEdit edit)
    {
        Write(target, edit.After);
        return ExecutionReport.Applied.Ran(action);
    }

    private static ExecutionReport.Applied DeleteTheFile(Action action, string target)
    {
        if (File.Exists(target))
        {
            File.Delete(target);
        }

        return ExecutionReport.Applied.Ran(action);
    }

    /// <summary>Removes a directory that is now empty; one that is not is reported, never emptied.</summary>
    private static ExecutionReport.Applied DeleteTheDirectory(Action action, string target)
    {
        if (!Directory.Exists(target))
        {
            return ExecutionReport.Applied.Ran(action);
        }

        if (Directory.EnumerateFileSystemEntries(target).Any())
        {
            return ExecutionReport.Applied.Refused(
                action, target + " is not empty — something else is in it, so it was left alone");
        }

        Directory.Delete(target);
        return ExecutionReport.Applied.Ran(action);
    }

    /// <summary>Temp file beside the target, then an atomic move over it.</summary>
    private static void Write(string target, string content)
    {
        var directory = Path.GetDirectoryName(target)!;
        Directory.CreateDirectory(directory);
        var temporary = Path.Combine(directory, TemporaryPrefix + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            File.WriteAllText(temporary, content, Utf8NoBom);
            File.Move(temporary, target, overwrite: true);
        }
        finally
        {
            File.Delete(temporary);
        }
    }
}
