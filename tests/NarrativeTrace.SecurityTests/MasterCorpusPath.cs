// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Diagnostics;

namespace NarrativeTrace.SecurityTests;

/// <summary>
/// A resolved master-corpus file's bytes plus which git ref they were read from — printed by the
/// master-sync tests so a reader can see whether the comparison used the committed <c>HEAD</c>
/// (the crash-safe default) or fell back to the working tree (only when the resolved repo is not
/// a git checkout at all).
/// </summary>
/// <param name="Content">The file's raw bytes, exactly as <see cref="MasterCorpusPath"/> read them.</param>
/// <param name="ComparedRef">A human-readable description of what was read — <c>"HEAD"</c>, or a working-tree fallback reason.</param>
internal readonly record struct MasterCorpusFile(byte[] Content, string ComparedRef);

/// <summary>
/// Resolves the shared hostile-corpus master copy (Pro ledger §6.7) these fixtures must stay
/// byte-identical to — the canonical Java repo's
/// <c>narrativetrace-security-tests/src/test/resources/hostile-corpus/</c>. Never assumed
/// present — a checkout without that repo as a sibling, or a dev container without the mount,
/// must stay green with a loud skip rather than fail or silently pass nothing. TS and Python
/// carry the identical search order (<c>master-corpus-path.ts</c>,
/// <c>test_hostile_corpus.py</c>'s <c>_master_corpus_dir</c>) — this is this runtime's twin.
/// </summary>
/// <remarks>
/// <para>
/// Candidates, in order: <c>JAVA_REPO</c> (an explicit override, the same env var the dev-
/// container tooling already uses for the read-only mount), <c>/workspace-java</c> (that mount's
/// path inside the dev container), then the canonical repo checked out as a host sibling —
/// <see cref="RepositoryPath.Root"/> plus one <c>..</c>.
/// </para>
/// <para>
/// @llmNote Design flaw fixed 2026-09-18: the earlier resolver read the master's WORKING-TREE
/// file directly, so an in-progress, uncommitted master edit turned this port's gate red before
/// that edit was ever committed — a false failure this port has no way to fix. The file is now
/// read from the candidate repo's committed <c>HEAD</c> via <c>git show HEAD:&lt;path&gt;</c>,
/// which is exactly what the "master" in "master corpus" is supposed to mean: the committed
/// state, not whatever happens to be sitting in a working tree at the moment this port's tests
/// run. The working tree is read only when the candidate repo is not a git checkout at all (no
/// <c>.git</c>) — there is no committed state to compare against in that case, so the working
/// file is the only thing available, and the fallback says so via <see cref="MasterCorpusFile.ComparedRef"/>.
/// </para>
/// </remarks>
internal static class MasterCorpusPath
{
    private const string JavaRepoEnv = "JAVA_REPO";
    private const string WorkspaceMount = "/workspace-java";

    // The canonical repo's directory name, checked out as a host sibling — see the type remarks.
    private const string CanonicalRepoDirName = "narrative-trace-java";

    private const string MasterCorpusRelativeDir =
        "narrativetrace-security-tests/src/test/resources/hostile-corpus";

    /// <summary>The master <c>graphs.json</c> file, or <see langword="null"/> when no candidate java repo is found on disk.</summary>
    internal static MasterCorpusFile? ResolveMasterGraphsFile() => ResolveMasterCorpusFile("graphs.json");

    /// <summary>The master <c>redaction.json</c> file, or <see langword="null"/> when no candidate java repo is found on disk.</summary>
    internal static MasterCorpusFile? ResolveMasterRedactionFile() => ResolveMasterCorpusFile("redaction.json");

    /// <summary>
    /// The master copy of one named hostile-corpus file (e.g. <c>graphs.json</c>), read from its
    /// repo's committed <c>HEAD</c> — or <see langword="null"/> when no candidate java repo is
    /// found on disk at all.
    /// </summary>
    private static MasterCorpusFile? ResolveMasterCorpusFile(string fileName)
    {
        var relativePath = $"{MasterCorpusRelativeDir}/{fileName}".Replace('/', Path.DirectorySeparatorChar);
        foreach (var repo in RepoCandidates())
        {
            var workingTreeFile = Path.Combine(repo, relativePath);
            if (File.Exists(workingTreeFile))
                return ReadCommittedOrWorkingTree(repo, relativePath, workingTreeFile);
        }

        return null;
    }

    /// <summary>
    /// Reads <paramref name="relativePath"/> from <paramref name="repo"/>'s committed <c>HEAD</c>.
    /// Falls back to <paramref name="workingTreeFile"/> only when <paramref name="repo"/> is not a
    /// git checkout at all — see the type remarks for why an in-progress edit must never do this.
    /// </summary>
    private static MasterCorpusFile ReadCommittedOrWorkingTree(
        string repo, string relativePath, string workingTreeFile)
    {
        if (!IsGitCheckout(repo))
        {
            return new MasterCorpusFile(
                File.ReadAllBytes(workingTreeFile),
                $"working tree ({repo} is not a git checkout)");
        }

        var gitRelativePath = relativePath.Replace(Path.DirectorySeparatorChar, '/');
        return new MasterCorpusFile(GitShowHead(repo, gitRelativePath), "HEAD");
    }

    private static bool IsGitCheckout(string repo)
    {
        var (exitCode, stdOut) = RunGit(repo, "rev-parse", "--is-inside-work-tree");
        return exitCode == 0 && stdOut.Trim() == "true";
    }

    /// <summary>The file's bytes at the repo's committed <c>HEAD</c> — never the working tree.</summary>
    private static byte[] GitShowHead(string repo, string gitRelativePath)
    {
        using var process = Process.Start(NewGitProcessStartInfo(repo, "show", $"HEAD:{gitRelativePath}"))
            ?? throw new InvalidOperationException($"failed to start git in {repo}");

        // Read stdout synchronously while stderr drains on its own task — reading both
        // synchronously in sequence can deadlock if either pipe's buffer fills first.
        var stdErrTask = process.StandardError.ReadToEndAsync();
        using var stdOut = new MemoryStream();
        process.StandardOutput.BaseStream.CopyTo(stdOut);
        process.WaitForExit();
        var stdErr = stdErrTask.GetAwaiter().GetResult();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"git show HEAD:{gitRelativePath} failed in {repo} (exit {process.ExitCode}): {stdErr}");
        }

        return stdOut.ToArray();
    }

    private static (int ExitCode, string StdOut) RunGit(string repo, params string[] args)
    {
        using var process = Process.Start(NewGitProcessStartInfo(repo, args))
            ?? throw new InvalidOperationException($"failed to start git in {repo}");
        var stdOut = process.StandardOutput.ReadToEnd();
        process.StandardError.ReadToEnd();
        process.WaitForExit();
        return (process.ExitCode, stdOut);
    }

    private static ProcessStartInfo NewGitProcessStartInfo(string repo, params string[] args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "git",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        psi.ArgumentList.Add("-C");
        psi.ArgumentList.Add(repo);
        foreach (var arg in args)
            psi.ArgumentList.Add(arg);
        return psi;
    }

    private static IEnumerable<string> RepoCandidates()
    {
        var env = Environment.GetEnvironmentVariable(JavaRepoEnv);
        if (!string.IsNullOrEmpty(env))
            yield return env;

        yield return WorkspaceMount;
        yield return Path.GetFullPath(Path.Combine(RepositoryPath.Root(), "..", CanonicalRepoDirName));
    }
}
