// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Diagnostics;
using NarrativeTrace.SkillsDevTool;
using Xunit;

namespace NarrativeTrace.Cli.Tests;

/// <summary>
/// Rehearses the clarity happy-path grader on trees a trial could leave behind, without spending a
/// trial: one solved tree it must PASS, and the untouched project and each near miss it must FAIL — for
/// the right reason, by message. A grader that fails a compliant trial measures the harness; one that
/// passes a near miss measures nothing.
/// </summary>
public sealed class ClarityGraderRehearsalTests : IDisposable
{
    private static readonly string Repo = RepoRoot.Find();

    private static readonly string Fixture = Path.Combine(Repo, "skills-evals", "fixtures", "clarity-consumer");

    private static readonly string Grader =
        Path.Combine(Repo, "skills-evals", "add-narrativetrace-clarity", "happy-path", "graders", "verify.sh");

    private readonly string _work = Path.Combine(Path.GetTempPath(), "nt-clarity-rehearsal-" + Guid.NewGuid().ToString("N"));

    public void Dispose() => Directory.Delete(_work, recursive: true);

    private static string BuiltCli() => Path.Combine(
        Repo, "src", "NarrativeTrace.Cli", "bin", SkillReplayRegistry.BuildConfiguration, "net10.0", "dotnet-narrativetrace.dll");

    private string Scaffold()
    {
        Directory.CreateDirectory(_work);
        foreach (var file in new[] { "Orders.csproj", "OrderHandler.cs", "Program.cs" })
        {
            File.Copy(Path.Combine(Fixture, file), Path.Combine(_work, file));
        }

        return _work;
    }

    private void Edit(string file, string from, string to)
    {
        var path = Path.Combine(_work, file);
        var text = File.ReadAllText(path);
        Assert.Contains(from, text, StringComparison.Ordinal);
        File.WriteAllText(path, text.Replace(from, to, StringComparison.Ordinal));
    }

    private (int Exit, string Output) Grade()
    {
        var start = new ProcessStartInfo("bash", Grader)
        {
            WorkingDirectory = _work,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        start.Environment["NARRATIVETRACE_CLI_DLL"] = BuiltCli();
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
        process.WaitForExit();
        return (process.ExitCode, output);
    }

    [PythonFact]
    public void A_tree_renamed_by_the_reports_suggestions_with_its_caller_updated_passes()
    {
        Scaffold();
        Edit("OrderHandler.cs", "string Handle(string data);", "string PlaceOrder(string customerId);");
        Edit("OrderHandler.cs", "public string Handle(string data) => $\"order:{data.Trim()}\";", "public string PlaceOrder(string customerId) => $\"order:{customerId.Trim()}\";");
        Edit("Program.cs", ".Handle(", ".PlaceOrder(");

        var (exit, output) = Grade();

        Assert.True(exit == 0, output);
        Assert.Contains("PASS", output, StringComparison.Ordinal);
    }

    [PythonFact]
    public void The_project_as_it_stands_fails_on_the_gate_itself()
    {
        Scaffold();

        var (exit, output) = Grade();

        Assert.NotEqual(0, exit);
        Assert.Contains("clarity-check --max-high-issues 0 exits 1", output, StringComparison.Ordinal);
    }

    [PythonFact]
    public void Renaming_the_method_but_leaving_the_vague_parameter_still_fails_the_gate()
    {
        Scaffold();
        Edit("OrderHandler.cs", "string Handle(", "string PlaceOrder(");
        Edit("Program.cs", ".Handle(", ".PlaceOrder(");

        var (exit, output) = Grade();

        Assert.NotEqual(0, exit);
        Assert.Contains("clarity-check", output, StringComparison.Ordinal);
    }

    [PythonFact]
    public void Renaming_a_member_and_leaving_a_caller_behind_fails_the_build()
    {
        Scaffold();
        Edit("OrderHandler.cs", "string Handle(string data);", "string PlaceOrder(string customerId);");
        Edit("OrderHandler.cs", "public string Handle(string data) => $\"order:{data.Trim()}\";", "public string PlaceOrder(string customerId) => $\"order:{customerId.Trim()}\";");

        var (exit, output) = Grade();

        Assert.NotEqual(0, exit);
        Assert.Contains("does not build", output, StringComparison.Ordinal);
    }

    [PythonFact]
    public void Hiding_the_types_from_the_scan_passes_the_gate_and_fails_the_grader()
    {
        Scaffold();
        Edit("OrderHandler.cs", "public interface", "internal interface");
        Edit("OrderHandler.cs", "public sealed class", "internal sealed class");

        var (exit, output) = Grade();

        Assert.NotEqual(0, exit);
        Assert.Contains("only 0 scenario(s) scanned", output, StringComparison.Ordinal);
    }

    [PythonFact]
    public void Hard_coding_the_output_instead_of_calling_the_boundary_fails_the_grader()
    {
        Scaffold();
        Edit("OrderHandler.cs", "string Handle(string data);", "string PlaceOrder(string customerId);");
        Edit("OrderHandler.cs", "public string Handle(string data) => $\"order:{data.Trim()}\";", "public string PlaceOrder(string customerId) => $\"order:{customerId.Trim()}\";");
        File.WriteAllText(Path.Combine(_work, "Program.cs"), "Console.WriteLine(\"order:customer-42\");\n");

        var (exit, output) = Grade();

        Assert.NotEqual(0, exit);
        Assert.Contains("no longer calls a method", output, StringComparison.Ordinal);
    }

    [PythonFact]
    public void Changing_what_the_program_prints_fails_the_grader()
    {
        Scaffold();
        Edit("OrderHandler.cs", "string Handle(string data);", "string PlaceOrder(string customerId);");
        Edit("OrderHandler.cs", "public string Handle(string data) => $\"order:{data.Trim()}\";", "public string PlaceOrder(string customerId) => $\"placed:{customerId.Trim()}\";");
        Edit("Program.cs", ".Handle(", ".PlaceOrder(");

        var (exit, output) = Grade();

        Assert.NotEqual(0, exit);
        Assert.Contains("the program prints", output, StringComparison.Ordinal);
    }
}
