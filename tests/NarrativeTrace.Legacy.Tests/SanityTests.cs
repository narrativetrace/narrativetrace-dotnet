// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using Xunit;

namespace NarrativeTrace.Legacy.Tests;

public class SanityTests
{
    [Fact]
    public void Placeholder_Exists()
    {
        Assert.NotNull(typeof(NarrativeTrace.Legacy.Placeholder));
    }
}
