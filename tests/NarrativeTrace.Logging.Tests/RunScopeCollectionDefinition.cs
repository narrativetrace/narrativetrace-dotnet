// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using Xunit;

namespace NarrativeTrace.Logging.Tests;

/// <summary>
/// Serializes every test class that begins/ends the process-wide
/// <see cref="NarrativeTrace.Core.RunScope"/> ambient, so xUnit's default
/// cross-class parallelism cannot let one class observe another's active run
/// mid-assertion. Classes outside this collection are unaffected.
/// </summary>
[CollectionDefinition("RunScope", DisableParallelization = true)]
public sealed class RunScopeCollectionDefinition;
