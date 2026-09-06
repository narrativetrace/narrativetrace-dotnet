// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using FsCheck;
using FsCheck.Xunit;
using NarrativeTrace.Core;
using NarrativeTrace.Runtime;
using Xunit;

namespace NarrativeTrace.Core.Tests;

public class VersionPropertyTests
{
    [Property(MaxTest = 100)]
    public void VersionIsStableAcrossReads(PositiveInt readCount)
    {
        var first = NarrativeTrace.Runtime.NarrativeTrace.Version;
        for (var i = 0; i < readCount.Get; i++)
            Assert.Equal(first, NarrativeTrace.Runtime.NarrativeTrace.Version);
    }

    [Property(MaxTest = 100)]
    public void VersionHasThreeNumericSegments(NonEmptyString _)
    {
        var segments = NarrativeTrace.Runtime.NarrativeTrace.Version.Split('.');
        Assert.Equal(3, segments.Length);
        Assert.All(segments, segment => Assert.True(int.TryParse(segment, out var _)));
    }
}
