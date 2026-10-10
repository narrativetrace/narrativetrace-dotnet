// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Diagnostics;
using NarrativeTrace.SkillsDevTool;
using Xunit;

namespace NarrativeTrace.Cli.Tests;

/// <summary>
/// Rehearses the <c>init-prompt-aspnetcore-project</c> grader on trees a trial could leave behind,
/// without spending a trial: one solved tree it must PASS, and the untouched project and each near miss
/// it must FAIL — for the right reason, by message.
/// </summary>
/// <remarks>
/// The trees reference NarrativeTrace's BUILT assemblies (copied into this test project's output by
/// its test-only project references) rather than packages, so no packed feed is needed. The doctor
/// then sees no package reference at all, which makes the solved tree the stricter rehearsal: it can
/// only pass on the wiring it finds in source.
/// </remarks>
public sealed class AspNetCoreGraderRehearsalTests : IDisposable
{
    private static readonly string Repo = RepoRoot.Find();

    private static readonly string Fixture = Path.Combine(Repo, "skills-evals", "fixtures", "aspnetcore-project");

    private static readonly string Grader = Path.Combine(
        Repo, "skills-evals", "add-narrative-tracing", "init-prompt-aspnetcore-project", "graders", "verify.sh");

    private static readonly string[] BuiltAssemblies =
    [
        "NarrativeTrace.AspNetCore", "NarrativeTrace.Core", "NarrativeTrace.Runtime", "NarrativeTrace.Proxy",
        "NarrativeTrace.Testing.Xunit", "NarrativeTrace.Diagrams", "NarrativeTrace.Clarity", "NarrativeTrace.Glossary",
    ];

    private const string Wiring =
        "builder.Services.AddNarrativeTrace(builder.Configuration);\n";

    private const string WrappedEndpoint =
        "app.UseNarrativeTrace();\n\n" +
        "app.MapGet(\"/orders/{customerId}\", (string customerId, IOrderService orders, HttpContext http) =>\n" +
        "    NarrativeTraceProxy.Create<IOrderService>(orders, http.GetNarrativeContext())\n" +
        "        .PlaceOrder(customerId, \"book-123\", 2));";

    private const string OriginalEndpoint =
        "app.MapGet(\"/orders/{customerId}\", (string customerId, IOrderService orders) =>\n" +
        "    orders.PlaceOrder(customerId, \"book-123\", 2));";

    private const string RedactionTest = """
        using NarrativeTrace.Core;
        using NarrativeTrace.Proxy;
        using NarrativeTrace.TestingXunit;
        using Xunit;

        public sealed class RedactionTests
        {
            [Fact]
            public void Redacts_a_deny_listed_parameter()
            {
                using var narrative = new NarrativeFixture();
                narrative.Run("pay", context =>
                    NarrativeTraceProxy.Create<IPayments>(new Payments(), context).Pay("p-1", "tok-secret"));

                var rendered = IndentedTextRenderer.Render(narrative.CaptureTrace());
                Assert.Contains("paymentToken: [REDACTED]", rendered);
            }
        }

        public interface IPayments { string Pay(string policyId, string paymentToken); }

        public sealed class Payments : IPayments { public string Pay(string policyId, string paymentToken) => policyId; }
        """;

    private readonly string _work = Path.Combine(Path.GetTempPath(), "nt-aspnetcore-rehearsal-" + Guid.NewGuid().ToString("N"));

    public void Dispose() => Directory.Delete(_work, recursive: true);

    private static string BuiltCli() => Path.Combine(
        Repo, "src", "NarrativeTrace.Cli", "bin", SkillReplayRegistry.BuildConfiguration, "net10.0", "dotnet-narrativetrace.dll");

    private void Scaffold()
    {
        Directory.CreateDirectory(_work);
        foreach (var file in new[] { "OrdersApi.csproj", "Program.cs" })
        {
            File.Copy(Path.Combine(Fixture, file), Path.Combine(_work, file));
        }
    }

    /// <summary>The solved tree's project: the built assemblies plus the xunit packages the quickstart adds.</summary>
    private void ReferenceNarrativeTraceAndXunit()
    {
        var references = string.Concat(BuiltAssemblies.Select(name =>
            $"    <Reference Include=\"{name}\"><HintPath>{Path.Combine(AppContext.BaseDirectory, name + ".dll")}</HintPath></Reference>\n"));
        Edit("OrdersApi.csproj", "</Project>", "  <ItemGroup>\n" + references +
            "    <PackageReference Include=\"Microsoft.NET.Test.Sdk\" Version=\"17.11.1\" />\n" +
            "    <PackageReference Include=\"xunit\" Version=\"2.9.2\" />\n" +
            "    <PackageReference Include=\"xunit.runner.visualstudio\" Version=\"2.8.2\" />\n" +
            "  </ItemGroup>\n\n</Project>");
        File.WriteAllText(Path.Combine(_work, "RedactionTests.cs"), RedactionTest);
    }

    private void Solve()
    {
        Scaffold();
        ReferenceNarrativeTraceAndXunit();
        Edit("Program.cs", "var builder = WebApplication.CreateBuilder(args);\n",
            "using NarrativeTrace.AspNetCore;\nusing NarrativeTrace.Proxy;\n\nvar builder = WebApplication.CreateBuilder(args);\n" + Wiring);
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
        var stderr = process.StandardError.ReadToEndAsync();
        var output = process.StandardOutput.ReadToEnd() + stderr.Result;
        process.WaitForExit();
        return (process.ExitCode, output);
    }

    [PythonFact]
    public void A_tree_wired_as_the_doctor_prints_it_with_a_redaction_test_passes()
    {
        Solve();
        Edit("Program.cs", OriginalEndpoint, WrappedEndpoint);

        var (exit, output) = Grade();

        Assert.True(exit == 0, output);
        Assert.Contains("PASS", output, StringComparison.Ordinal);
    }

    [PythonFact]
    public void The_project_as_it_stands_fails_on_the_framework_check_the_trace_and_the_test()
    {
        Scaffold();

        var (exit, output) = Grade();

        Assert.NotEqual(0, exit);
        Assert.Contains("config.registration-pair is not green: ASP.NET Core detected", output, StringComparison.Ordinal);
        Assert.Contains("no request-scoped trace", output, StringComparison.Ordinal);
        Assert.Contains("no C# file under the copy asserts [REDACTED]", output, StringComparison.Ordinal);
    }

    [PythonFact]
    public void Registering_the_services_without_the_middleware_fails_the_framework_check()
    {
        Solve();

        var (exit, output) = Grade();

        Assert.NotEqual(0, exit);
        Assert.Contains("config.registration-pair is not green", output, StringComparison.Ordinal);
    }

    /// <summary>
    /// The pair alone turns the check green, and the request's trace is empty: the boundary is still
    /// called directly, so nothing names it. The trace gate is what catches the half-applied fix.
    /// </summary>
    [PythonFact]
    public void The_middleware_without_the_wrapped_service_passes_the_check_and_fails_the_trace()
    {
        Solve();
        Edit("Program.cs", OriginalEndpoint, "app.UseNarrativeTrace();\n\n" + OriginalEndpoint);

        var (exit, output) = Grade();

        Assert.NotEqual(0, exit);
        Assert.DoesNotContain("config.registration-pair is not green", output, StringComparison.Ordinal);
        Assert.Contains("names no IOrderService / PlaceOrder", output, StringComparison.Ordinal);
    }

    [PythonFact]
    public void Leaving_the_web_sdk_makes_the_framework_undetectable_and_fails_the_grader()
    {
        Solve();
        Edit("Program.cs", OriginalEndpoint, WrappedEndpoint);
        Edit("OrdersApi.csproj", "Microsoft.NET.Sdk.Web", "Microsoft.NET.Sdk");

        var (exit, output) = Grade();

        Assert.NotEqual(0, exit);
        Assert.Contains("no project under the copy is an ASP.NET Core", output, StringComparison.Ordinal);
    }
}
