// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Glossary;
using Xunit;

namespace NarrativeTrace.Glossary.Tests;

/// <summary>
/// Backs the Configuration Guide's worked examples for
/// <c>NARRATIVETRACE_GLOSSARY</c> and <c>NARRATIVETRACE_GLOSSARY_PATH</c>
/// (<c>documentation/guides/configuration.md</c>) — rule 8 (docs as tests):
/// each fragment the page embeds is the exact fragment run here, through
/// <see cref="GlossarySettings"/>/<see cref="GlossaryLoader"/>'s
/// injected-reader overloads rather than the real process environment.
/// </summary>
public sealed class DocGlossaryExamples : IDisposable
{
    private readonly string root = Path.Combine(
        Path.GetTempPath(), $"doc-glossary-examples-{Guid.NewGuid():N}");

    private static readonly string RepoRoot =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));

    public DocGlossaryExamples()
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

    private static void Write(string name, string content)
    {
        var directory = Path.Combine(RepoRoot, "artifacts", "config-envvars");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, name), content);
    }

    private static string Capture(Action run)
    {
        var originalOut = Console.Out;
        var captured = new StringWriter { NewLine = "\n" };
        Console.SetOut(captured);
        try
        {
            run();
            return captured.ToString();
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public void Glossary_worked_example()
    {
        File.WriteAllText(Path.Combine(root, "glossary.json"), "{}");
        var output = Capture(() =>
        {
            // snippet:begin glossary
            var resolved = GlossarySettings.ResolveFile(
                key => key == "NARRATIVETRACE_GLOSSARY" ? "off" : null, root);
            Console.WriteLine($"resolved == {(resolved is null ? "null (harvesting disabled)" : resolved)}");
            // snippet:end glossary
        });

        Write("glossary.txt", output);
        Assert.Equal("resolved == null (harvesting disabled)\n", output);
    }

    [Fact]
    public void GlossaryPath_worked_example()
    {
        var overridePath = Path.Combine(root, "committed-glossary.json");
        File.WriteAllText(
            overridePath,
            GlossaryJsonWriter.Write(new Glossary(1, new Dictionary<string, BoundedContext>(), [])));
        var output = Capture(() =>
        {
            // snippet:begin glossary-path
            var loaded = GlossaryLoader.Load(
                key => key == "NARRATIVETRACE_GLOSSARY_PATH" ? overridePath : null, root);
            Console.WriteLine($"loaded from override == {loaded is not null}");
            // snippet:end glossary-path
        });

        Write("glossary-path.txt", output);
        Assert.Equal("loaded from override == True\n", output);
    }
}
