// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using NUnit.Framework;

namespace NarrativeTrace.TestingNUnit;

/// <summary>
/// A namespace-scoped <c>[SetUpFixture]</c> that brackets a whole NUnit suite:
/// its one-time set-up opens a <see cref="NarrativeSuiteReport"/> and its one-time
/// tear-down flushes the accumulated <c>clarity-results.json</c> and footer.
/// Derive an empty class from it at the desired namespace to activate suite
/// reporting; tests deriving from <see cref="NarrativeTestBase"/> contribute
/// automatically.
/// </summary>
[SetUpFixture]
public abstract class NarrativeSuiteSetup
{
    /// <summary>Opens the suite-wide report before any test in the namespace runs.</summary>
    /// <remarks>Invoked by NUnit; do not call it directly.</remarks>
    [OneTimeSetUp]
    public void BeginNarrativeSuite()
    {
        var directory = TestArtifactSettings
            .Resolve(Environment.GetEnvironmentVariable).Directory;
        NarrativeSuiteScope.Begin(new NarrativeSuiteReport(directory));
    }

    /// <summary>
    /// Flushes the accumulated suite report — clarity results, glossary harvest
    /// and summary footer — after the last test in the namespace.
    /// </summary>
    /// <remarks>
    /// Invoked by NUnit; do not call it directly. This is where suite artifacts
    /// are produced, so a run that never reaches tear-down produces none.
    /// </remarks>
    [OneTimeTearDown]
    public void EndNarrativeSuite()
    {
        NarrativeSuiteScope.End(TestContext.Progress);
    }
}
