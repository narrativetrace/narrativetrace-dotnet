// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace NarrativeTrace.Build;

/// <summary>
/// Backs the <c>HeaderAbsenceCheck</c> target — the inverse of
/// <c>scripts/publish-public.sh</c>'s stamping step. Per the owner ruling
/// (2026-09-01), in-tree license headers must not exist in a private repo:
/// stamping happens only at publish time, over a throwaway snapshot, and is
/// never committed here. This check fails when a tracked source file
/// carries one anyway.
/// </summary>
/// <remarks>
/// Mirrors, rather than shares code with, <c>publish-public.sh</c>'s own
/// header recognition (its <c>HEADER_SPDX</c>/<c>HEADER_NOTICE</c> constants
/// and the superseded-Apache-boilerplate case <c>PublishScriptLicenseTests</c>
/// exercises): one side is bash, the other C#, and the bash side already
/// accepts that split by testing itself through a subprocess rather than a
/// shared library. Scans the same extensions the publish script stamps
/// (<c>find -name '*.cs' -o -name '*.java' -o ...</c>), over the same first
/// 10 lines (<c>head -10</c>).
/// </remarks>
internal static class HeaderAbsenceSupport
{
    private static readonly Regex HeaderMarker = new(
        @"SPDX-License-Identifier|Copyright \(c\) \d{4} Empower Agile|"
        + @"Licensed under the Business Source License|Licensed under the Apache License, Version 2\.0",
        RegexOptions.CultureInvariant);

    private const int ScannedLines = 10;

    /// <summary>
    /// Extensions <c>publish-public.sh</c> stamps — see its own <c>find</c>
    /// invocation. A file of any other type cannot carry the header block
    /// this check looks for, so scanning it would only be noise.
    /// </summary>
    private static readonly string[] StampedExtensions =
    [
        ".cs", ".java", ".ts", ".tsx", ".js", ".fs", ".fsx", ".kt", ".kts",
        ".vb", ".py", ".sh", ".yml", ".yaml", ".ps1",
    ];

    /// <summary>
    /// Directories excluded because they hold generated or vendored content,
    /// never hand-authored source — matches what <see cref="TranslationCheckSupport"/>
    /// and <see cref="DemoWiringSupport"/> already exclude from their own walks.
    /// </summary>
    private static readonly string[] ExcludedDirectories =
        [".git", "bin", "obj", "artifacts", "StrykerOutput", "node_modules", "TestResults", ".nuke"];

    /// <summary>
    /// Verifies no source file under <paramref name="repoRoot"/> carries a
    /// license header; returns the offending paths, sorted, empty when the
    /// tree is clean.
    /// </summary>
    public static IReadOnlyList<string> Check(string repoRoot)
    {
        var root = Path.GetFullPath(repoRoot);
        // Fill-aware, like legal-check: the same gate ships inside the public
        // snapshot, where every source file IS stamped by design. Which tree
        // this is comes from LICENSE itself — the private template carries
        // {{VERSION}}/{{CHANGE_DATE}}; a published tree carries them filled.
        // Private tree: headers must be absent. Published tree: headers must
        // be PRESENT — the same drift (partial stamping) fails either way.
        var license = Path.Combine(root, "LICENSE");
        var published = File.Exists(license)
            && !File.ReadAllText(license).Contains("{{", StringComparison.Ordinal);
        return CandidateFiles(root)
            .Where(file => HeaderMarker.IsMatch(FirstLines(file)) != published)
            .Select(file => published
                ? $"{RelativePath(root, file)}: missing its license header — every source file "
                    + "in a published snapshot is stamped; a bare file means the stamping step skipped it"
                : $"{RelativePath(root, file)}: carries a license header — "
                    + "headers are stamped at publish time only (scripts/publish-public.sh), never committed in-tree")
            .OrderBy(problem => problem, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// Vendored third-party files the stamper deliberately leaves untouched
    /// (upstream identity kept): NUKE's stock bootstraps at the repo root —
    /// <c>build.sh</c>, <c>build.ps1</c> and <c>build.cmd</c>. <c>demo.ps1</c>
    /// is this repo's own, so it is not listed here and is stamped like any
    /// other first-party <c>.ps1</c> file. Must mirror the stamper's own skip
    /// in scripts/publish-public.sh.
    /// </summary>
    private static bool IsVendored(string root, string file) =>
        RelativePath(root, file) is "build.sh" or "build.ps1" or "build.cmd";

    private static IEnumerable<string> CandidateFiles(string root) =>
        Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Where(file => !IsVendored(root, file))
            .Where(file => StampedExtensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase))
            .Where(file => !file.Split(Path.DirectorySeparatorChar)
                .Any(segment => ExcludedDirectories.Contains(segment, StringComparer.Ordinal)));

    private static string FirstLines(string file)
    {
        using var reader = new StreamReader(file);
        var lines = new List<string>(ScannedLines);
        for (var i = 0; i < ScannedLines && reader.ReadLine() is { } line; i++)
            lines.Add(line);
        return string.Join('\n', lines);
    }

    private static string RelativePath(string root, string file) =>
        Path.GetRelativePath(root, file).Replace('\\', '/');
}
