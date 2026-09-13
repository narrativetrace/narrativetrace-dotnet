// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using Xunit;

namespace NarrativeTrace.Testing.Xunit.Tests;

/// <summary>
/// Serializes every test class that touches the process-wide
/// <see cref="NarrativeTrace.Core.RunScope"/> — <see cref="NarrativeSuiteFixture"/>
/// begins it for its whole lifetime, so two such classes running in xUnit's
/// default cross-class parallelism could otherwise observe each other's active
/// run mid-assertion. Classes outside this collection are unaffected and keep
/// running in parallel with it and with each other.
/// </summary>
[CollectionDefinition("RunScope", DisableParallelization = true)]
public sealed class RunScopeCollectionDefinition;
