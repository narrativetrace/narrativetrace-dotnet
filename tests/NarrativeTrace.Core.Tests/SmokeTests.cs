// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using NarrativeTrace.Runtime;
using Xunit;

namespace NarrativeTrace.Core.Tests;

public class SmokeTests
{
    [Fact]
    public void Version_IsSet()
    {
        Assert.False(string.IsNullOrWhiteSpace(NarrativeTrace.Runtime.NarrativeTrace.Version));
    }
}
