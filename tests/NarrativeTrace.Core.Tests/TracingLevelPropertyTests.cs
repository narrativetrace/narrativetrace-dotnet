// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using FsCheck.Xunit;
using NarrativeTrace.Core;

namespace NarrativeTrace.Core.Tests;

public class TracingLevelPropertyTests
{
    [Property]
    public bool IsEnabled_is_reflexive(TracingLevel level)
    {
        return level.IsEnabled(level);
    }

    [Property]
    public bool Detail_enables_all_non_Off_levels(TracingLevel required)
    {
        return required == TracingLevel.Off
            || TracingLevel.Detail.IsEnabled(required);
    }
}
