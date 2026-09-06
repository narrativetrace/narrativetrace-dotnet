// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Glossary;
using Xunit;

namespace NarrativeTrace.Glossary.Tests;

public sealed class GlossarySettingsTests : IDisposable
{
    private readonly string root = Path.Combine(
        Path.GetTempPath(), $"glossary-settings-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Explicit_env_path_wins_even_when_file_is_missing()
    {
        Assert.Equal(
            "/some/where/glossary.json",
            GlossarySettings.ResolveFile(
                key => key == GlossarySettings.EnvKey ? "/some/where/glossary.json" : null,
                Path.GetTempPath()));
    }

    [Fact]
    public void Finds_glossary_upward_from_a_nested_start_directory()
    {
        var nested = Path.Combine(root, "bin", "Debug", "net10.0");
        Directory.CreateDirectory(nested);
        var glossary = Path.Combine(root, GlossarySettings.DefaultFileName);
        File.WriteAllText(glossary, "{}");

        Assert.Equal(glossary, GlossarySettings.ResolveFile(_ => null, nested));
    }

    [Fact]
    public void Returns_null_when_no_glossary_exists_anywhere_upward()
    {
        var nested = Path.Combine(root, "isolated");
        Directory.CreateDirectory(nested);

        // No ancestor of the temp root declares a glossary, so resolution must
        // stay null — otherwise the feature would silently activate everywhere.
        Assert.Null(GlossarySettings.ResolveFile(_ => null, nested));
    }

    [Theory]
    [InlineData("off")]
    [InlineData("OFF")]
    public void Off_sentinel_disables_the_feature_even_with_a_findable_file(string value)
    {
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, GlossarySettings.DefaultFileName), "{}");

        Assert.Null(GlossarySettings.ResolveFile(
            key => key == GlossarySettings.EnvKey ? value : null, root));
    }

    [Fact]
    public void Rejects_null_reader_and_blank_start_directory()
    {
        Assert.Throws<ArgumentNullException>(
            () => GlossarySettings.ResolveFile(null!, "."));
        Assert.Throws<ArgumentException>(
            () => GlossarySettings.ResolveFile(_ => null, " "));
    }
}
