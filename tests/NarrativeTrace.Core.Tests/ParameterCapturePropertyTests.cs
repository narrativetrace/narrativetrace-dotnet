// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using FsCheck.Xunit;
using NarrativeTrace.Core;

namespace NarrativeTrace.Core.Tests;

public class ParameterCapturePropertyTests
{
    [Property]
    public bool Equal_when_all_fields_match(
        string name, string value, bool redacted)
    {
        var a = new ParameterCapture(name, value, redacted);
        var b = new ParameterCapture(name, value, redacted);
        return a.Equals(b);
    }
}
