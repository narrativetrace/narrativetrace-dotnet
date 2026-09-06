// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using Xunit;

namespace NarrativeTrace.Core.Tests;

public class ParameterCaptureTests
{
    [Fact]
    public void Stores_name_value_and_redacted_flag()
    {
        var capture = new ParameterCapture("userId", "42", true);

        Assert.Equal("userId", capture.Name);
        Assert.Equal("42", capture.RenderedValue);
        Assert.True(capture.Redacted);
    }
}
