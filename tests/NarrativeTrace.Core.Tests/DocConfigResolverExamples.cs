// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using Xunit;

namespace NarrativeTrace.Core.Tests;

/// <summary>
/// Backs the Configuration Guide's "Worked examples" section
/// (<c>documentation/guides/configuration.md</c>) — rule 8 (docs as tests):
/// every <c>ConfigResolver</c>-owned <c>NARRATIVETRACE_*</c> variable gets a
/// real, compiled, tested demonstration, embedded into the page via
/// <c>snippet-check</c> rather than typed there by hand. Each test runs the
/// exact fragment its <c>snippet:begin</c>/<c>snippet:end</c> region marks —
/// through <see cref="ConfigResolver"/>'s injected-reader overload, the seam
/// this runtime ships specifically so a demonstration never has to touch the
/// real process environment (and therefore never risks bleeding into a
/// sibling test run in the same process) — then saves its console output
/// beside the code so the page's output block is the real, captured text,
/// not a hand-typed guess.
/// </summary>
public sealed class DocConfigResolverExamples
{
    private static readonly string RepoRoot =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));

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
    public void Level_worked_example()
    {
        var output = Capture(() =>
        {
            // snippet:begin level
            var resolved = ConfigResolver.Resolve(key => key == "NARRATIVETRACE_LEVEL" ? "Narrative" : null);
            Console.WriteLine($"resolved.Level == TracingLevel.{resolved.Level}");
            // snippet:end level
        });

        Write("level.txt", output);
        Assert.Equal("resolved.Level == TracingLevel.Narrative\n", output);
    }

    [Fact]
    public void Output_worked_example()
    {
        var output = Capture(() =>
        {
            // snippet:begin output
            var resolved = ConfigResolver.Resolve(key => key == "NARRATIVETRACE_OUTPUT" ? "false" : null);
            Console.WriteLine($"resolved.Output == {resolved.Output}");
            // snippet:end output
        });

        Write("output.txt", output);
        Assert.Equal("resolved.Output == False\n", output);
    }

    [Fact]
    public void OutputDir_worked_example()
    {
        var output = Capture(() =>
        {
            // snippet:begin output-dir
            var resolved = ConfigResolver.Resolve(key => key == "NARRATIVETRACE_OUTPUT_DIR" ? "artifacts/traces" : null);
            Console.WriteLine($"resolved.OutputDir == \"{resolved.OutputDir}\"");
            // snippet:end output-dir
        });

        Write("output-dir.txt", output);
        Assert.Equal("resolved.OutputDir == \"artifacts/traces\"\n", output);
    }

    [Fact]
    public void Format_worked_example()
    {
        var output = Capture(() =>
        {
            // snippet:begin format
            var resolved = ConfigResolver.Resolve(key => key == "NARRATIVETRACE_FORMAT" ? "Json" : null);
            Console.WriteLine($"resolved.Format == OutputFormat.{resolved.Format}");
            // snippet:end format
        });

        Write("format.txt", output);
        Assert.Equal("resolved.Format == OutputFormat.Json\n", output);
    }

    [Fact]
    public void CanonicalJson_worked_example()
    {
        var output = Capture(() =>
        {
            // snippet:begin canonical-json
            var resolved = ConfigResolver.Resolve(key => key == "NARRATIVETRACE_CANONICAL_JSON" ? "true" : null);
            Console.WriteLine($"resolved.CanonicalJson == {resolved.CanonicalJson}");
            // snippet:end canonical-json
        });

        Write("canonical-json.txt", output);
        Assert.Equal("resolved.CanonicalJson == True\n", output);
    }

    [Fact]
    public void StructuralJson_worked_example()
    {
        var output = Capture(() =>
        {
            // snippet:begin structural-json
            var resolved = ConfigResolver.Resolve(key => key == "NARRATIVETRACE_STRUCTURAL_JSON" ? "true" : null);
            Console.WriteLine($"resolved.StructuralJson == {resolved.StructuralJson}");
            // snippet:end structural-json
        });

        Write("structural-json.txt", output);
        Assert.Equal("resolved.StructuralJson == True\n", output);
    }

    [Fact]
    public void Approval_worked_example()
    {
        var output = Capture(() =>
        {
            // snippet:begin approval
            var resolved = ConfigResolver.Resolve(key => key == "NARRATIVETRACE_APPROVAL" ? "true" : null);
            Console.WriteLine($"resolved.Approval == {resolved.Approval}");
            // snippet:end approval
        });

        Write("approval.txt", output);
        Assert.Equal("resolved.Approval == True\n", output);
    }

    [Fact]
    public void ApprovedDir_worked_example()
    {
        var output = Capture(() =>
        {
            // snippet:begin approved-dir
            var resolved = ConfigResolver.Resolve(key => key == "NARRATIVETRACE_APPROVED_DIR" ? "baselines" : null);
            Console.WriteLine($"resolved.ApprovedDir == \"{resolved.ApprovedDir}\"");
            // snippet:end approved-dir
        });

        Write("approved-dir.txt", output);
        Assert.Equal("resolved.ApprovedDir == \"baselines\"\n", output);
    }
}
