// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using Xunit;

namespace NarrativeTrace.Testing.NUnit.Tests;

/// <summary>
/// Serializes every test class that begins a <see cref="NarrativeTrace.TestingNUnit.NarrativeSuiteScope"/>.
/// </summary>
/// <remarks>
/// The scope is one process-global slot — that is the point of it, since NUnit's
/// <c>[SetUpFixture]</c> is per-process — so two test classes that each
/// <c>Begin</c> a report are writing the same variable. xUnit runs classes in
/// parallel by default, which made the winner of that write a matter of thread
/// scheduling: the loser's report was never flushed and its assertion on the
/// written artifact failed. Scheduling is never a test input (release rule 3),
/// so the classes share one collection and run one after another.
/// <see cref="SuiteScopeIsolationTests"/> fails the build if a file using the
/// scope forgets to join.
/// </remarks>
[CollectionDefinition(Name)]
public sealed class SuiteScopeCollection
{
    /// <summary>The collection name every scope-using test class must carry.</summary>
    public const string Name = "NarrativeSuiteScope";
}
