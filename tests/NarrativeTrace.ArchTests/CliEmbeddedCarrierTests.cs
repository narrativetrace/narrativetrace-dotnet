// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Reflection;
using Xunit;

namespace NarrativeTrace.ArchTests;

/// <summary>
/// Proves the carrier is inside the built <c>dotnet-narrativetrace</c> assembly, not just in the
/// source tree (Phase 3 D1's second half — the tool embeds a copy). Reads the real
/// <c>EmbeddedResource</c>s off the built assembly via reflection rather than re-reading
/// <c>src/NarrativeTrace.Skills/skills/**</c> back through the csproj's own glob, the same
/// "open the real artifact" principle <see cref="CliPackagingTests"/> and Java's
/// <c>SkillsCarrierJarTest</c> use.
/// </summary>
/// <remarks>
/// The CLI's <c>EmbeddedResource</c> items and the published <c>NarrativeTrace.Skills</c>
/// package's <c>Content</c> items both point at the SAME physical files
/// (<c>src/NarrativeTrace.Skills/skills/**</c>) — the byte-identity between the two carrier homes
/// is therefore a construction guarantee, not something this test re-derives. What this test DOES
/// prove is the half that construction alone cannot: that the assembly's manifest resources carry
/// exactly what the source tree carries, with the exact logical names
/// <c>NarrativeTrace.Skills/{agents,claude}/&lt;name&gt;/SKILL.md</c> and
/// <c>NarrativeTrace.Skills/catalogue.json</c> milestone 3's carrier reader will look for.
/// </remarks>
public class CliEmbeddedCarrierTests
{
    private const string CarrierPrefix = "NarrativeTrace.Skills/";

    private static Assembly CliAssembly => typeof(NarrativeTrace.Cli.CliRunner).Assembly;

    private static string CarrierSourceRoot => Path.Combine(RepoRoot(), "src", "NarrativeTrace.Skills", "skills");

    private static string RepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "NarrativeTrace.sln")))
        {
            dir = Path.GetDirectoryName(dir);
        }

        return dir ?? throw new InvalidOperationException(
            $"could not find NarrativeTrace.sln above {AppContext.BaseDirectory}");
    }

    [Fact]
    public void The_cli_embeds_exactly_the_carrier_entries_and_nothing_else()
    {
        var expected = new[]
        {
            "NarrativeTrace.Skills/agents/narrativetrace-doctor/SKILL.md",
            "NarrativeTrace.Skills/agents/add-narrative-tracing/SKILL.md",
            "NarrativeTrace.Skills/agents/narrativetrace-feedback/SKILL.md",
            "NarrativeTrace.Skills/agents/add-narrativetrace-clarity/SKILL.md",
            "NarrativeTrace.Skills/agents/narrativetrace-verify/SKILL.md",
            "NarrativeTrace.Skills/agents/narrativetrace-debug/SKILL.md",
            "NarrativeTrace.Skills/claude/narrativetrace-doctor/SKILL.md",
            "NarrativeTrace.Skills/claude/add-narrative-tracing/SKILL.md",
            "NarrativeTrace.Skills/claude/narrativetrace-feedback/SKILL.md",
            "NarrativeTrace.Skills/claude/add-narrativetrace-clarity/SKILL.md",
            "NarrativeTrace.Skills/claude/narrativetrace-verify/SKILL.md",
            "NarrativeTrace.Skills/claude/narrativetrace-debug/SKILL.md",
            "NarrativeTrace.Skills/catalogue.json",
        };

        var actual = CliAssembly.GetManifestResourceNames()
            .Where(name => name.StartsWith(CarrierPrefix, StringComparison.Ordinal));

        Assert.Equal(expected.OrderBy(n => n, StringComparer.Ordinal), actual.OrderBy(n => n, StringComparer.Ordinal));
    }

    [Theory]
    [InlineData("agents/narrativetrace-doctor/SKILL.md")]
    [InlineData("agents/add-narrative-tracing/SKILL.md")]
    [InlineData("agents/narrativetrace-feedback/SKILL.md")]
    [InlineData("agents/add-narrativetrace-clarity/SKILL.md")]
    [InlineData("agents/narrativetrace-verify/SKILL.md")]
    [InlineData("agents/narrativetrace-debug/SKILL.md")]
    [InlineData("claude/narrativetrace-doctor/SKILL.md")]
    [InlineData("claude/add-narrative-tracing/SKILL.md")]
    [InlineData("claude/narrativetrace-feedback/SKILL.md")]
    [InlineData("claude/add-narrativetrace-clarity/SKILL.md")]
    [InlineData("claude/narrativetrace-verify/SKILL.md")]
    [InlineData("claude/narrativetrace-debug/SKILL.md")]
    [InlineData("catalogue.json")]
    public void Each_embedded_page_is_byte_identical_to_the_carrier_source(string relative)
    {
        var onDisk = File.ReadAllText(Path.Combine(CarrierSourceRoot, relative.Replace('/', Path.DirectorySeparatorChar)));

        using var stream = CliAssembly.GetManifestResourceStream(CarrierPrefix + relative);
        Assert.NotNull(stream);
        using var reader = new StreamReader(stream!);
        var embedded = reader.ReadToEnd();

        Assert.Equal(onDisk, embedded, StringComparer.Ordinal);
    }
}
