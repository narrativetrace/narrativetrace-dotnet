// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Glossary;
using Xunit;

namespace NarrativeTrace.Glossary.Tests;

/// <summary>Mirrors Java's <c>GlossaryLoaderTest</c>.</summary>
public sealed class GlossaryLoaderTests : IDisposable
{
    private readonly string root = Path.Combine(
        Path.GetTempPath(), $"glossary-loader-{Guid.NewGuid():N}");

    public GlossaryLoaderTests()
    {
        Directory.CreateDirectory(root);
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Loads_the_glossary_beside_the_application()
    {
        File.WriteAllText(
            Path.Combine(root, "glossary.json"), Json(GlossaryWithTerm("charge")));

        var loaded = GlossaryLoader.Load(_ => null, root);

        AssertSameGlossary(GlossaryWithTerm("charge"), loaded);
    }

    [Fact]
    public void Returns_null_when_no_glossary_is_present_anywhere()
    {
        Assert.Null(GlossaryLoader.Load(_ => null, root));
    }

    [Fact]
    public void Configured_path_override_wins_over_the_file_beside_the_application()
    {
        File.WriteAllText(
            Path.Combine(root, "glossary.json"), Json(GlossaryWithTerm("beside")));
        var overridePath = Path.Combine(root, "committed-glossary.json");
        File.WriteAllText(overridePath, Json(GlossaryWithTerm("override")));

        var loaded = GlossaryLoader.Load(
            key => key == GlossaryLoader.PathKey ? overridePath : null, root);

        AssertSameGlossary(GlossaryWithTerm("override"), loaded);
    }

    [Fact]
    public void An_override_pointing_to_a_missing_file_fails_fast()
    {
        var missing = Path.Combine(root, "nowhere.json");

        var thrown = Assert.Throws<InvalidOperationException>(
            () => GlossaryLoader.Load(
                key => key == GlossaryLoader.PathKey ? missing : null, root));

        Assert.Contains(GlossaryLoader.PathKey, thrown.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Invalid_glossary_json_propagates_as_an_error()
    {
        File.WriteAllText(Path.Combine(root, "glossary.json"), "{ not json");

        Assert.Throws<ArgumentException>(() => GlossaryLoader.Load(_ => null, root));
    }

    /// <summary>
    /// The zero-argument overload reads the process environment and the
    /// application's own base directory — the shape a deployed application
    /// uses, with no plumbing at the call site.
    /// </summary>
    [Fact]
    public void Parameterless_load_reads_the_environment_and_the_base_directory()
    {
        var overridePath = Path.Combine(root, "env-glossary.json");
        File.WriteAllText(overridePath, Json(GlossaryWithTerm("environment")));
        Environment.SetEnvironmentVariable(GlossaryLoader.PathKey, overridePath);
        try
        {
            AssertSameGlossary(GlossaryWithTerm("environment"), GlossaryLoader.Load());
        }
        finally
        {
            Environment.SetEnvironmentVariable(GlossaryLoader.PathKey, null);
        }
    }

    [Fact]
    public void Rejects_null_and_blank_arguments()
    {
        Assert.Throws<ArgumentNullException>(() => GlossaryLoader.Load(null!, root));
        Assert.Throws<ArgumentException>(() => GlossaryLoader.Load(_ => null, " "));
    }

    /// <summary>
    /// Compared through the canonical serializer rather than by value: this
    /// port's <see cref="Glossary"/> is a record over dictionaries and arrays,
    /// so record equality would compare collection references. The writer is
    /// deterministic, which makes its output the stronger identity anyway.
    /// </summary>
    private static void AssertSameGlossary(Glossary expected, Glossary? actual)
    {
        Assert.NotNull(actual);
        Assert.Equal(GlossaryJsonWriter.Write(expected), GlossaryJsonWriter.Write(actual!));
    }

    private static string Json(Glossary glossary)
    {
        return GlossaryJsonWriter.Write(glossary);
    }

    private static Glossary GlossaryWithTerm(string termText)
    {
        return new Glossary(
            1,
            new Dictionary<string, BoundedContext>
            {
                ["billing"] = new("billing", ["Acme.Billing"]),
            },
            [
                new GlossaryTerm(
                    termText,
                    "billing",
                    TermKind.Word,
                    TermStatus.Curated,
                    null,
                    new Dictionary<string, string> { ["es"] = "traducción" },
                    [],
                    [],
                    new DateTime(2026, 8, 15, 0, 0, 0, DateTimeKind.Utc)),
            ]);
    }
}
