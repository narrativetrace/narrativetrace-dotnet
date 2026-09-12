// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Text;
using NarrativeTrace.Core;
using NarrativeTrace.SecurityTests.Corpus;
using NarrativeTrace.SecurityTests.Oracle;
using Xunit;

namespace NarrativeTrace.SecurityTests;

/// <summary>
/// The writers' other input: not the value, the <em>name</em>. The .NET mirror of java's
/// <c>ArtifactNamingPropertyTest</c>.
/// </summary>
/// <remarks>
/// <para>
/// INTENT: Every other target in this suite feeds hostile data through a renderer. This one feeds
/// it through the path builder, because a trace artifact's location is derived from a test class
/// and method name, and the writer is public API whose callers do not all derive those from a
/// reflected type name — a scenario name, an HTTP route or a display name reaches these methods in
/// real integrations.
/// </para>
/// <para>
/// @llmNote The limit asserted here is 255 <b>bytes</b>, not characters: ext4, APFS and every other
/// mainstream filesystem count bytes, so 200 three-byte characters overflow a component that 200
/// ASCII ones fit inside. The corpus carries that exact case, and a character-counting cap passes
/// it while the filesystem refuses the write.
/// </para>
/// <para>
/// @edgeCase This runtime now mirrors the java runtime's byte-cap-plus-hash disambiguator
/// (<see cref="OutputDirectoryResolver"/>), so the full corpus — over-length and noncharacter cases
/// included — runs unconditionally through every property below; nothing is excluded any more.
/// </para>
/// <para>
/// @edgeCase The "noncharacter" case (U+FFFE/U+FFFF) is legal UTF-8 that ext4/NTFS accept but
/// macOS/APFS refuses outright at the syscall level — see
/// <see cref="FilesystemRefusesNoncharacters"/> on the two properties that touch a real
/// filesystem. Still exercised everywhere, just against the platform-correct expectation
/// instead of silently skipped.
/// </para>
/// </remarks>
public class ArtifactNamingPropertyTests
{
    /// <summary>The per-component limit ext4, APFS and NTFS share.</summary>
    private const int MaxComponentBytes = 255;

    public static IEnumerable<object[]> Names() =>
        HostileCorpus.Names().Select(c => new object[] { c });

    [Theory]
    [MemberData(nameof(Names))]
    public void Every_corpus_name_writes_every_artifact_without_throwing(CorpusCase name)
    {
        var tree = Emitters.TreeOf("\"probe\"", "\"result\"");
        var sandbox = SandboxFor(name.Id);
        var output = Path.Combine(sandbox, "out");

        var classException = Record.Exception(
            () => Emitters.WrittenArtifacts(tree, output, name.Value, "m"));
        var methodException = Record.Exception(
            () => Emitters.WrittenArtifacts(tree, output, "cls", name.Value));

        if (FilesystemRefusesNoncharacters(name))
        {
            // Only the CLASS-name path carries the raw noncharacters to disk: it
            // feeds OutputDirectoryResolver.ToDirectorySlug, which strips only what
            // every path cannot carry at all (separators, control characters, lone
            // surrogates) and otherwise keeps a name's own spelling — U+FFFE/U+FFFF
            // pass through unfiltered into the directory CreateDirectory then
            // refuses. ToFileSlug (the method-name path) is the narrower slug used
            // for the leaf file name; its allow-list regex already reduces anything
            // outside [a-z0-9_] to an underscore, so the same noncharacters never
            // reach disk when they arrive as a method name instead — methodException
            // is expected to stay null.
            AssertRefusedByFilesystem(classException, name);
            Assert.Null(methodException);
        }
        else
        {
            Assert.Null(classException);
            Assert.Null(methodException);
        }

        Directory.Delete(sandbox, recursive: true);
    }

    /// <summary>
    /// The escape this found in java: <c>diagrams/</c> and <c>structural/</c> resolved the class
    /// name verbatim, so <c>../../../tmp/x</c> put artifacts at an <b>absolute</b> path outside the
    /// output directory the caller gave. The sandbox is one level above the output directory, so
    /// anything that walked out of it lands somewhere this assertion can see.
    /// </summary>
    [Theory]
    [MemberData(nameof(Names))]
    public void Every_artifact_a_hostile_name_produces_stays_inside_the_output_directory(CorpusCase name)
    {
        var tree = Emitters.TreeOf("\"probe\"", "\"result\"");
        var enclosure = SandboxFor(name.Id);
        var output = Path.Combine(enclosure, "out");

        if (FilesystemRefusesNoncharacters(name))
        {
            // The filesystem itself refuses to create the path (see
            // FilesystemRefusesNoncharacters), so nothing reaches disk at all —
            // trivially nothing escaped the sandbox either. Assert both halves of
            // that explicitly rather than silently declaring victory.
            var exception = Record.Exception(
                () => Emitters.WrittenArtifacts(tree, output, name.Value, name.Value));
            AssertRefusedByFilesystem(exception, name);
            Assert.Empty(FilesUnder(enclosure));
        }
        else
        {
            Emitters.WrittenArtifacts(tree, output, name.Value, name.Value);

            var written = FilesUnder(enclosure);
            Assert.True(written.Count > 0, $"{name.Id} wrote nothing, so nothing was checked");
            foreach (var file in written)
            {
                Assert.StartsWith(output, file, StringComparison.Ordinal);
            }
        }

        Directory.Delete(enclosure, recursive: true);
    }

    /// <summary>
    /// macOS/APFS refuses the noncharacters U+FFFE/U+FFFF outright at the syscall level — the
    /// write fails with <see cref="IOException"/> ("Illegal byte sequence"), confirmed
    /// empirically on this filesystem, not assumed — even though they are legal UTF-8 bytes
    /// that ext4 and NTFS accept without complaint (see the corpus row's own description in
    /// <c>HostileCorpus/names.json</c>). This is a filesystem-level refusal outside
    /// <see cref="NarrativeTrace.Core.TraceArtifactWriter"/>/<see cref="OutputDirectoryResolver"/>'s
    /// control, not a defect in either, so it is asserted here explicitly by name rather than
    /// silently skipped: on any filesystem that accepts the byte sequence (this predicate
    /// returns <see langword="false"/> there), the corpus case still runs the normal
    /// no-exception assertion above unchanged.
    /// </summary>
    private static bool FilesystemRefusesNoncharacters(CorpusCase name) =>
        name.Id == "noncharacter" && OperatingSystem.IsMacOS();

    private static void AssertRefusedByFilesystem(Exception? exception, CorpusCase name)
    {
        Assert.True(
            exception is IOException,
            $"{name.Id}: expected macOS/APFS to refuse this name with an IOException, " +
            $"got {exception?.GetType().Name ?? "no exception"}");
        Assert.Contains(
            "Illegal byte sequence", exception!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void No_path_component_a_hostile_name_produces_exceeds_the_filesystem_limit()
    {
        var dir = TempDirectory();
        try
        {
            var resolver = new OutputDirectoryResolver(dir);
            foreach (var name in HostileCorpus.Names())
            {
                AssertComponentsFit(dir, resolver.TraceFile(name.Value, "m"), name);
                AssertComponentsFit(dir, resolver.TraceFile("cls", name.Value), name);
            }
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>
    /// Truncation without disambiguation is a silent overwrite: two 300-character names sharing
    /// their first 240 characters would land on one artifact, and one test's approved baseline
    /// would judge another test's trace.
    /// </summary>
    [Fact]
    public void Names_that_differ_only_past_the_limit_still_resolve_to_different_artifacts()
    {
        var dir = TempDirectory();
        try
        {
            var resolver = new OutputDirectoryResolver(dir);
            var seen = new Dictionary<string, string>();
            foreach (var name in HostileCorpus.Names())
            {
                if (!name.Id.StartsWith("long-", StringComparison.Ordinal))
                {
                    continue;
                }

                var file = resolver.TraceFile("cls", name.Value + name.Id);
                var collidesWith = seen.TryGetValue(file, out var earlier) ? earlier : null;
                Assert.Null(collidesWith);
                seen[file] = name.Id;
            }
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Resolving_the_same_name_twice_always_gives_the_same_path()
    {
        var dir = TempDirectory();
        try
        {
            var resolver = new OutputDirectoryResolver(dir);
            foreach (var name in HostileCorpus.Names())
            {
                Assert.Equal(
                    resolver.TraceFile(name.Value, name.Value),
                    resolver.TraceFile(name.Value, name.Value));
            }
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    private static string SandboxFor(string id) => TempDirectory("case-" + id);

    private static string TempDirectory(string? suffix = null)
    {
        var name = "narrativetrace-artifact-naming-" + Guid.NewGuid().ToString("n")
            + (suffix is null ? "" : "-" + suffix);
        var dir = Path.Combine(Path.GetTempPath(), name);
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static List<string> FilesUnder(string directory)
    {
        return Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories).ToList();
    }

    private static void AssertComponentsFit(string basePath, string resolved, CorpusCase name)
    {
        var relative = Path.GetRelativePath(basePath, resolved);
        foreach (var component in relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
        {
            if (component.Length == 0)
            {
                continue;
            }

            Assert.True(
                Encoding.UTF8.GetByteCount(component) <= MaxComponentBytes,
                $"{name.Id}: component '{component}' must fit a filesystem path element");
        }
    }
}
