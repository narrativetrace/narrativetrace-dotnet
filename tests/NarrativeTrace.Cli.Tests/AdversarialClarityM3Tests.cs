// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using NarrativeTrace.SkillsDevTool;
using Xunit;

namespace NarrativeTrace.Cli.Tests;

/// <summary>
/// Adversarial checks on the clarity grader and replay executors: comment and near-miss inputs to the
/// boundary-call rule, a program that prints the right text but fails, a zero-type scan that must not
/// count as a clean report, and fixture lookups that must not match siblings.
/// </summary>
public sealed class AdversarialClarityM3Tests : IDisposable
{
    private static readonly string Repo = RepoRoot.Find();

    private static readonly string Grader =
        Path.Combine(Repo, "skills-evals", "add-narrativetrace-clarity", "grade_the_gate.py");

    private readonly string _work = Path.Combine(Path.GetTempPath(), "nt-clarity-m3-" + Guid.NewGuid().ToString("N"));

    public AdversarialClarityM3Tests() => Directory.CreateDirectory(_work);

    public void Dispose() => Directory.Delete(_work, recursive: true);

    private void WriteProgram(string text) => File.WriteAllText(Path.Combine(_work, "Program.cs"), text);

    private (int Exit, string Output) RunGraderModule(string snippet)
    {
        // Loads the grader as a module (its main() is guarded) and runs one check in the work directory.
        const string Loader =
            "import importlib.util, json, sys\n" +
            "spec = importlib.util.spec_from_file_location('grader', sys.argv[1])\n" +
            "g = importlib.util.module_from_spec(spec)\n" +
            "spec.loader.exec_module(g)\n";
        var start = new ProcessStartInfo("python3")
        {
            WorkingDirectory = _work,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        start.ArgumentList.Add("-I");
        start.ArgumentList.Add("-B");
        start.ArgumentList.Add("-c");
        start.ArgumentList.Add(Loader + snippet);
        start.ArgumentList.Add(Grader);
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
        process.WaitForExit();
        return (process.ExitCode, output);
    }

    [PythonFact]
    public void A_boundary_call_that_only_appears_inside_a_comment_does_not_satisfy_the_grader()
    {
        WriteProgram("Console.WriteLine(\"order:customer-42\"); // handler.Handle(\"customer-42\")\n");

        var (_, output) = RunGraderModule("g.require_the_boundary_is_still_called()\nprint(json.dumps(g.failures))\n");

        Assert.Contains("no longer calls a method", output, StringComparison.Ordinal);
    }

    [PythonFact]
    public void Calls_with_a_longer_or_shorter_literal_do_not_satisfy_the_grader_but_the_exact_call_does()
    {
        var snippet = "g.require_the_boundary_is_still_called()\nprint(json.dumps(g.failures))\n";

        WriteProgram("handler.PlaceOrder(\"customer-420\");\n");
        Assert.Contains("no longer calls", RunGraderModule(snippet).Output, StringComparison.Ordinal);

        WriteProgram("handler.PlaceOrder(\"customer-4\");\n");
        Assert.Contains("no longer calls", RunGraderModule(snippet).Output, StringComparison.Ordinal);

        WriteProgram("handler.PlaceOrder( \"customer-42\" );\n");
        Assert.DoesNotContain("no longer calls", RunGraderModule(snippet).Output, StringComparison.Ordinal);
    }

    [PythonFact]
    public void A_program_that_prints_the_expected_text_but_exits_nonzero_fails_the_grader()
    {
        // A fake dotnet: builds succeed, the program prints the right line then exits 3, the CLI scan writes
        // two scenarios and the check passes. Only the program's exit status should fail the grade.
        var fakeBin = Path.Combine(_work, "fakebin");
        Directory.CreateDirectory(fakeBin);
        var fakeDotnet = Path.Combine(fakeBin, "dotnet");
        File.WriteAllText(fakeDotnet, """
            #!/bin/bash
            if [ "$1" = "build" ]; then exit 0; fi
            if [ "$1" = "bin/Release/net10.0/Orders.dll" ]; then echo "order:customer-42"; exit 3; fi
            if [ "$2" = "clarity-scan" ]; then
              out=""
              while [ $# -gt 0 ]; do
                if [ "$1" = "--output-dir" ]; then out="$2"; fi
                shift
              done
              mkdir -p "$out"
              printf '{"scenarios":[{"name":"a"},{"name":"b"}]}' > "$out/clarity-scan-results.json"
              exit 0
            fi
            exit 0
            """);
        File.SetUnixFileMode(fakeDotnet, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        WriteProgram("handler.Handle(\"customer-42\");\n");

        var start = new ProcessStartInfo("python3")
        {
            WorkingDirectory = _work,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        start.ArgumentList.Add("-I");
        start.ArgumentList.Add("-B");
        start.ArgumentList.Add(Grader);
        start.Environment["PATH"] = fakeBin + Path.PathSeparator + Environment.GetEnvironmentVariable("PATH");
        start.Environment["NARRATIVETRACE_CLI_DLL"] = "/nonexistent/dotnet-narrativetrace.dll";
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
        process.WaitForExit();

        Assert.True(process.ExitCode != 0, output);
        Assert.Contains("exit", output, StringComparison.Ordinal);
    }

    [Fact]
    public void A_scan_of_an_assembly_with_no_public_types_does_not_count_as_a_successful_clarity_report()
    {
        // The skill's scan verify demands a nonzero type count; the replay must not accept an empty report.
        var fixture = Path.Combine(_work, "Hollow");
        var release = Path.Combine(fixture, "bin", SkillReplayRegistry.BuildConfiguration, "net10.0");
        Directory.CreateDirectory(release);
        var assembly = new PersistedAssemblyBuilder(new AssemblyName("Hollow"), typeof(object).Assembly);
        var module = assembly.DefineDynamicModule("Hollow");
        module.DefineType("Internal", TypeAttributes.NotPublic | TypeAttributes.Class).CreateType();
        assembly.Save(Path.Combine(release, "Hollow.dll"));

        Assert.False(ClarityReplay.ScanSucceeds(Repo, fixture), "a zero-type scan was counted as a successful report");
    }

    [Fact]
    public void A_fixture_assembly_is_found_only_under_its_exact_name_configuration_and_target_framework()
    {
        var configuration = SkillReplayRegistry.BuildConfiguration;
        var otherConfiguration = configuration == "Release" ? "Debug" : "Release";
        var fixture = Path.Combine(_work, "Orders");
        var release = Path.Combine(fixture, "bin", configuration, "net10.0");
        Directory.CreateDirectory(release);
        File.WriteAllText(Path.Combine(release, "OrdersX.dll"), string.Empty);
        Directory.CreateDirectory(Path.Combine(fixture, "bin", otherConfiguration, "net10.0"));
        File.WriteAllText(Path.Combine(fixture, "bin", otherConfiguration, "net10.0", "Orders.dll"), string.Empty);
        Directory.CreateDirectory(Path.Combine(fixture, "bin", configuration, "net9.0"));
        File.WriteAllText(Path.Combine(fixture, "bin", configuration, "net9.0", "Orders.dll"), string.Empty);
        Assert.Null(ClarityReplay.FixtureAssembly(fixture));

        File.WriteAllText(Path.Combine(release, "Orders.dll"), string.Empty);
        Assert.NotNull(ClarityReplay.FixtureAssembly(fixture));
    }
}
