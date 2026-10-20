// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace NarrativeTrace.Build;

/// <summary>
/// Backs the <c>RecipeArtifacts</c> target — rule 8 (docs as tests) for the recipe pages: every
/// code block and every output block on <c>documentation/recipes/*.md</c> is embedded from a
/// project this build compiles, tests and runs. The scenarios that must FAIL by design (a stale
/// expectation, the bug before its fix, a refactoring the approval rejects) live in
/// <c>recipes/checkout/Checkout.Fixtures</c>, which the <c>Test</c> sweep never runs; this support
/// runs them through the real test runner and keeps their artifacts under
/// <c>artifacts/recipes/&lt;slug&gt;/</c> for <c>SnippetCheck</c> to embed.
/// </summary>
/// <remarks>
/// A run whose verdict is not the one its page claims is a build failure: a fixture that was meant
/// to fail and passed would otherwise embed a trace of the wrong story.
/// </remarks>
internal static class RecipeArtifactsSupport
{
    private const string FixturesProject = "recipes/checkout/Checkout.Fixtures/Checkout.Fixtures.csproj";
    private const string Namespace = "Checkout.Fixtures.";

    /// <summary>One fixture run: which class, under which environment, into which directory.</summary>
    internal sealed record Scenario(
        string Slug,
        string Subdirectory,
        string ClassName,
        IReadOnlyDictionary<string, string> Environment,
        IReadOnlyList<string> ExpectedFailures);

    /// <summary>The runs the recipe pages read, in dependency order.</summary>
    public static IReadOnlyList<Scenario> Scenarios(string outputRoot) =>
    [
        new("help-me-debug-and-fix-this-bug", "", "DeclinedPaymentFixture",
            Env(outputRoot, "help-me-debug-and-fix-this-bug", "", approval: true),
            [Namespace + "DeclinedPaymentFixture.The_stock_is_released_when_the_card_is_declined_before_the_fix"]),
        new("why-did-this-test-fail", "", "StaleExpectationFixture",
            Env(outputRoot, "why-did-this-test-fail", "", approval: false),
            [Namespace + "StaleExpectationFixture.The_customer_gets_an_order_id_for_their_keyboards"]),
        new("review-a-complex-flow-before-changing-it", "detail", "CheckoutReviewFixture",
            Env(outputRoot, "review-a-complex-flow-before-changing-it", "detail", approval: false, level: "Detail"),
            []),
        new("review-a-complex-flow-before-changing-it", "summary", "CheckoutReviewFixture",
            Env(outputRoot, "review-a-complex-flow-before-changing-it", "summary", approval: false, level: "Summary"),
            []),
        new("trace-in-production-without-the-cost", "detail", "CostFixture",
            Env(outputRoot, "trace-in-production-without-the-cost", "detail", approval: false, level: "Detail"),
            []),
        new("trace-in-production-without-the-cost", "errors", "CostFixture",
            Env(outputRoot, "trace-in-production-without-the-cost", "errors", approval: false, level: "Errors"),
            []),
        new("review-a-refactoring-did-behaviour-change", "approved-before-the-refactoring", "RefactoredCheckoutFixture",
            Env(outputRoot, "review-a-refactoring-did-behaviour-change", "approved-before-the-refactoring", approval: true),
            []),
        new("review-a-refactoring-did-behaviour-change", "approved-after-the-refactoring", "RefactoredCheckoutFixture",
            Env(outputRoot, "review-a-refactoring-did-behaviour-change", "approved-after-the-refactoring", approval: true,
                refactoring: "dropped-release"),
            [Namespace + "RefactoredCheckoutFixture.A_declined_card_saves_no_order"]),
    ];

    private static Dictionary<string, string> Env(
        string outputRoot, string slug, string subdirectory, bool approval, string? level = null, string? refactoring = null)
    {
        var env = new Dictionary<string, string>
        {
            ["NARRATIVETRACE_OUTPUT_DIR"] = Path.Combine(outputRoot, slug, subdirectory),
            ["NARRATIVETRACE_APPROVAL"] = approval ? "true" : "false",
        };
        if (level is not null)
        {
            env["NARRATIVETRACE_LEVEL"] = level;
        }

        if (refactoring is not null)
        {
            env["CHECKOUT_RECIPE_REFACTORING"] = refactoring;
        }

        return env;
    }

    /// <summary>
    /// What is wrong with a run's verdict: no test ran, a test expected to fail passed, or one
    /// expected to pass failed. Empty when exactly <paramref name="expectedFailures"/> failed.
    /// </summary>
    public static IReadOnlyList<string> VerdictProblems(
        string slug, IReadOnlyList<TrxTestResult> results, IReadOnlyList<string> expectedFailures)
    {
        if (results.Count == 0)
        {
            return [$"{slug}: no test ran"];
        }

        return results
            .Select(result => (result.TestName, Expected: expectedFailures.Contains(result.TestName), result.Failed))
            .Where(r => r.Expected != r.Failed)
            .Select(r => r.Expected
                ? $"{slug}: {r.TestName} should have failed (the page reads its failure) but passed"
                : $"{slug}: {r.TestName} should have passed but failed")
            .ToList();
    }

    /// <summary>
    /// Makes the repository's own location disappear from a captured runner message, so the page
    /// shows the repository-relative path every checkout shares rather than one machine's.
    /// </summary>
    public static string RelativizePaths(string message, string repoRoot)
    {
        var root = repoRoot.TrimEnd('/', '\\');
        return message
            .Replace(root + "/", "", StringComparison.Ordinal)
            .Replace(root + "\\", "", StringComparison.Ordinal)
            .Replace('\\', '/');
    }

    /// <summary>
    /// Runs every scenario and writes the runner-reported failure messages the pages embed.
    /// Returns every verdict problem; empty when each run did what its page says.
    /// </summary>
    public static IReadOnlyList<string> Run(string repoRoot, string configuration)
    {
        var outputRoot = Path.Combine(repoRoot, "artifacts", "recipes");
        PrepareOutput(repoRoot, outputRoot);
        var problems = new List<string>();
        foreach (var scenario in Scenarios(outputRoot))
        {
            var results = RunScenario(repoRoot, configuration, scenario, outputRoot, out var trx);
            problems.AddRange(VerdictProblems(scenario.Slug, results, scenario.ExpectedFailures));
            WriteFailureMessages(repoRoot, scenario, outputRoot, trx);
        }

        return problems;
    }

    private static void PrepareOutput(string repoRoot, string outputRoot)
    {
        foreach (var slug in Scenarios(outputRoot).Select(s => s.Slug).Distinct())
        {
            var directory = Path.Combine(outputRoot, slug);
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        var committed = Path.Combine(repoRoot, "recipes", "checkout", "Checkout.Fixtures", "narratives", "RefactoredCheckoutFixture");
        var copy = Path.Combine(
            outputRoot, "review-a-refactoring-did-behaviour-change", "approved-after-the-refactoring", "approved", "RefactoredCheckoutFixture");
        CopyDirectory(committed, copy);
    }

    private static void CopyDirectory(string source, string target)
    {
        Directory.CreateDirectory(target);
        foreach (var file in Directory.EnumerateFiles(source))
        {
            File.Copy(file, Path.Combine(target, Path.GetFileName(file)), overwrite: true);
        }
    }

    private static IReadOnlyList<TrxTestResult> RunScenario(
        string repoRoot, string configuration, Scenario scenario, string outputRoot, out string trx)
    {
        var resultsDirectory = Path.Combine(outputRoot, scenario.Slug, scenario.Subdirectory, "runner");
        trx = Path.Combine(resultsDirectory, "run.trx");
        var info = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = repoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var argument in new[]
        {
            "test", FixturesProject, "--no-build", "-c", configuration,
            "--filter", $"FullyQualifiedName~{Namespace}{scenario.ClassName}",
            "--results-directory", resultsDirectory, "--logger", "trx;LogFileName=run.trx",
        })
        {
            info.ArgumentList.Add(argument);
        }

        foreach (var (key, value) in ScenarioEnvironment(scenario, outputRoot))
        {
            info.Environment[key] = value;
        }

        using var process = Process.Start(info) ?? throw new InvalidOperationException("dotnet test did not start");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        Console.WriteLine($"RecipeArtifacts: {scenario.Slug}/{scenario.Subdirectory} exit {process.ExitCode}");
        _ = stdout.Result + stderr.Result;
        return File.Exists(trx) ? TrxSupport.ReadResults(trx) : [];
    }

    /// <summary>The environment one run sees; a refactoring run reads the baseline copy made for it.</summary>
    private static IReadOnlyDictionary<string, string> ScenarioEnvironment(Scenario scenario, string outputRoot)
    {
        var env = new Dictionary<string, string>(scenario.Environment);
        if (scenario.Subdirectory == "approved-after-the-refactoring")
        {
            env["NARRATIVETRACE_APPROVED_DIR"] =
                Path.Combine(outputRoot, scenario.Slug, scenario.Subdirectory, "approved");
        }

        return env;
    }

    private static void WriteFailureMessages(string repoRoot, Scenario scenario, string outputRoot, string trx)
    {
        if (!File.Exists(trx))
        {
            return;
        }

        var directory = Path.Combine(outputRoot, scenario.Slug, scenario.Subdirectory);
        foreach (var message in TrxSupport.FailureMessages(trx).Values)
        {
            var file = scenario.Slug switch
            {
                "why-did-this-test-fail" => "assertion-failure.txt",
                "review-a-refactoring-did-behaviour-change" => "approval-rejection.txt",
                _ => "runner-failure.txt",
            };
            File.WriteAllText(Path.Combine(directory, file), RelativizePaths(message, repoRoot) + "\n");
        }
    }
}
