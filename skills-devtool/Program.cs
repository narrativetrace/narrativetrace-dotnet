// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.SkillsDevTool;

// The skills dev tool's verbs, moved out of the shipped dotnet-narrativetrace tool (Phase 3 Q3,
// 2026-09-25) so the CLI's NuGet package no longer carries the NarrativeTrace.Skills.Catalogue
// assembly. Same three verbs, same behaviour, same --dir convention as before; the NUKE build and
// repo docs invoke them here instead. Argument parsing stays deliberately thin — every decision
// worth a test lives in the command classes, source-linked and unit-tested by Cli.Tests.
return SkillsDevToolRunner.Run(args, Console.Out, Console.Error);
