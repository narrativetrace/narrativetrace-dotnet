// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using Xunit;

namespace NarrativeTrace.Core.Tests;

public class NarrativeTraceConfigTests
{
    [Fact]
    public void Defaults_to_detail_level()
    {
        var config = new NarrativeTraceConfig();
        Assert.Equal(TracingLevel.Detail, config.Level);
    }

    [Fact]
    public void Level_can_be_changed()
    {
        var config = new NarrativeTraceConfig();

        config.Level = TracingLevel.Off;

        Assert.Equal(TracingLevel.Off, config.Level);
    }
}
