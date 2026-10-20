// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Globalization;

namespace NarrativeTrace.SkillsEvals;

/// <summary>
/// Parsed <c>--skill --case --platform --model [--agent-command] [--trials]</c> runner arguments.
/// </summary>
public sealed record EvalRunnerArgs(
    string Skill,
    string CaseName,
    Platform Platform,
    string Model,
    string? AgentCommand,
    int Trials)
{
    private const string Usage =
        "Usage: --skill <name> --case <name> --platform <claude|codex|antigravity|cursor> --model <id>"
        + " [--agent-command \"<template with {prompt}>\"] [--trials N]";

    /// <summary>Validates every field; throws <see cref="ArgumentException"/> on a bad value.</summary>
    public void Validate()
    {
        RequireText(Skill, "--skill");
        RequireText(CaseName, "--case");
        RequireText(Model, "--model");
        if (Trials < 1)
        {
            throw new ArgumentException("--trials must be at least 1");
        }
    }

    /// <summary>Parses an argv array, applying the platform preset only where none is overridden.</summary>
    public static EvalRunnerArgs Parse(string[] argv)
    {
        var skill = ValueOf(argv, "--skill");
        var caseName = ValueOf(argv, "--case");
        var platform = Platforms.Parse(ValueOf(argv, "--platform"));
        var model = ValueOf(argv, "--model");
        if (skill is null || caseName is null || platform is null || model is null)
        {
            throw new ArgumentException(Usage);
        }

        var trialsArg = ValueOf(argv, "--trials");
        var trials = trialsArg is null
            ? 1
            : int.Parse(trialsArg, CultureInfo.InvariantCulture);
        var args = new EvalRunnerArgs(
            skill, caseName, platform.Value, ModelIds.Resolve(platform.Value, model), ValueOf(argv, "--agent-command"), trials);
        args.Validate();
        return args;
    }

    private static void RequireText(string value, string flag)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{flag} is required");
        }
    }

    private static string? ValueOf(string[] argv, string flag)
    {
        var i = Array.IndexOf(argv, flag);
        return i < 0 || i + 1 >= argv.Length ? null : argv[i + 1];
    }
}
