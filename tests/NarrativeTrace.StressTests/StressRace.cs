// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System;
using System.Threading;
using System.Threading.Tasks;

namespace NarrativeTrace.StressTests;

/// <summary>
/// Hand-rolled interleaving harness: runs a fixed set of actor delegates
/// concurrently, co-started so none gets a head start, and returns only once
/// every actor has finished.
/// </summary>
/// <remarks>
/// The .NET stand-in for a jcstress trial's <c>@Actor</c> methods: the
/// <see cref="Barrier"/> plays the role of jcstress's own low-level scheduler
/// forcing genuine concurrency, and returning only after every
/// <see cref="Task"/> completes plays the role of jcstress guaranteeing the
/// <c>@Arbiter</c> runs strictly after every actor. Callers loop
/// <see cref="RunOnce"/> for <see cref="StressIterations.Count"/> trials,
/// rebuilding the state under test fresh each time — a single trial almost
/// never hits the race, repetition is what does.
/// </remarks>
internal static class StressRace
{
    /// <summary>Runs every actor concurrently and waits for all of them to finish.</summary>
    /// <param name="actors">One delegate per concurrent actor thread.</param>
    internal static void RunOnce(params Action[] actors)
    {
        using var barrier = new Barrier(actors.Length);
        var tasks = new Task[actors.Length];
        for (var i = 0; i < actors.Length; i++)
        {
            var actor = actors[i];
            tasks[i] = Task.Run(() =>
            {
                barrier.SignalAndWait();
                actor();
            });
        }

        Task.WaitAll(tasks);
    }
}
