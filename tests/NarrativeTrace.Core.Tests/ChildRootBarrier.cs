// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Runtime;

namespace NarrativeTrace.Core.Tests;

/// <summary>
/// Polls until a <see cref="FireAndForgetGroup"/>'s launched background work is
/// visible in <see cref="FireAndForgetGroup.ChildRoots"/>, instead of a fixed
/// sleep.
/// </summary>
/// <remarks>
/// Awaiting the launched work's own <c>TaskCompletionSource</c> only proves the
/// test body inside <c>Launch</c> ran to its last statement — it races against
/// <see cref="Task.Run(Action)"/>'s own bookkeeping marking that task
/// <c>Completed</c>, which is what <c>ChildRoots</c> actually checks. A fixed
/// <c>Task.Delay</c> here was found flaky under CPU contention (a concurrent
/// run starves the thread pool just long enough to blow a fixed-ms budget);
/// polling adapts to how loaded the host actually is while still resolving in
/// a couple of milliseconds on a quiet one.
/// </remarks>
internal static class ChildRootBarrier
{
    public static async Task WaitForChildRoots(
        FireAndForgetGroup group, int atLeast = 1)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (group.ChildRoots().Count < atLeast)
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException(
                    $"Fire-and-forget child roots did not reach " +
                    $"{atLeast} within 5s.");
            }

            await Task.Delay(5);
        }
    }
}
