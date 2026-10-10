// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.IO.Compression;
using System.Text;
using Xunit;

namespace Build.Tests;

public sealed class FixturePackageSupportTests
{
    private static string Gzip64(string bundle)
    {
        using var compressed = new MemoryStream();
        using (var gzip = new GZipStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
        {
            var bytes = Encoding.UTF8.GetBytes(bundle);
            gzip.Write(bytes, 0, bytes.Length);
        }

        return Convert.ToBase64String(compressed.ToArray());
    }

    [Fact]
    public void A_bundle_round_trips_every_file_and_hides_its_text()
    {
        var files = new Dictionary<string, string>
        {
            ["Lib/Lib.csproj"] = "<Project />",
            ["Lib/Thing.cs"] = "namespace Lib;\npublic sealed class Thing { }",
        };

        var encoded = FixturePackageSupport.Encode(files);

        Assert.DoesNotContain("Thing", encoded, StringComparison.Ordinal);
        Assert.Equal(files, FixturePackageSupport.Decode(encoded));
    }

    [Fact]
    public void Unpack_writes_the_files_under_the_target()
    {
        var target = Directory.CreateTempSubdirectory("nt-bundle-").FullName;
        try
        {
            FixturePackageSupport.Unpack(FixturePackageSupport.Encode(new Dictionary<string, string> { ["A/B.cs"] = "x" }), target);

            Assert.Equal("x\n", File.ReadAllText(Path.Combine(target, "A", "B.cs")));
        }
        finally
        {
            Directory.Delete(target, recursive: true);
        }
    }

    [Theory]
    [InlineData("@@file ../escape.cs\nx\n@@end\n")]
    [InlineData("@@file /etc/passwd\nx\n@@end\n")]
    [InlineData("@@file A.cs\nx\n")]
    [InlineData("not a header\n")]
    public void A_malformed_or_escaping_bundle_is_refused(string bundle)
    {
        Assert.Throws<InvalidDataException>(() => FixturePackageSupport.Decode(Gzip64(bundle)));
    }

    [Fact]
    public void Every_committed_bundle_decodes_and_carries_one_project()
    {
        var dir = Path.Combine(RepoRootFinder(), "skills-evals", "fixture-packages");
        var bundles = Directory.GetFiles(dir, "*.bundle");

        Assert.NotEmpty(bundles);
        Assert.All(bundles, bundle =>
            Assert.Single(FixturePackageSupport.Decode(File.ReadAllText(bundle)).Keys, k => k.EndsWith(".csproj", StringComparison.Ordinal)));
    }

    private static string RepoRootFinder()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "NarrativeTrace.sln")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("repository root not found");
    }
}
