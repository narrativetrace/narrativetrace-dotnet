// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Skills.Catalogue;
using NarrativeTrace.SkillsEvals;

// S6966: this is a synchronous console entry point returning an exit code; the async Console
// overloads would buy nothing and only force an async Main around a purely sequential dispatch.
#pragma warning disable S6966

// Thin entry point for the Tier B runner (Phase 3 .NET milestone 0). Two subcommands:
//   eval        run trials of a case against a platform, appending rows to ledger/runs.jsonl
//   promotion   regenerate ledger/promotion.md from the catalogue and ledger/runs.jsonl
// Every decision worth a test lives in the injectable classes these paths call, unit-tested by
// tests/NarrativeTrace.Skills.Catalogue.Tests.
if (args.Length == 0)
{
    Console.Error.WriteLine("usage: <eval|promotion> ...");
    return 2;
}

switch (args[0])
{
    case "eval":
        return EvalRunner.Run(args[1..], Console.Out, Console.Error);
    case "promotion":
        return RegeneratePromotion();
    default:
        Console.Error.WriteLine($"unknown subcommand '{args[0]}' (expected eval|promotion)");
        return 2;
}

static int RegeneratePromotion()
{
    var repoRoot = RepoRoot.Locate();
    var runsPath = Path.Combine(repoRoot, "skills-evals", "ledger", "runs.jsonl");
    var promotionPath = Path.Combine(repoRoot, "skills-evals", "ledger", "promotion.md");
    var runs = File.Exists(runsPath)
        ? RunLedgerRow.ParseJsonl(File.ReadAllText(runsPath))
        : [];
    File.WriteAllText(promotionPath, PromotionRenderer.Render(SkillCatalogue.Skills, runs));
    Console.Out.WriteLine($"Regenerated {promotionPath} from {runs.Count} ledger row(s).");
    return 0;
}
