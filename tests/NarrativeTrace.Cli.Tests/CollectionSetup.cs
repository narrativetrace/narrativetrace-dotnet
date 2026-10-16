// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using Xunit;

// Every class in this assembly spawns processes — dotnet replays, the trial environment's recording
// stand-ins, the real curl behind them — and xUnit runs classes in parallel by default. Two classes
// forking at once is the classic exec race: the child of one fork still holds the script the other
// thread has just written until its own exec, so the exec of that script fails with "text file busy"
// (a shell reports 126), and two replays of the same fixture can discard each other's results. The
// 0.4.0 publish run and two CI runs on the public main each lost a different test to it on
// 2026-10-16, after a full-history checkout shifted the timings. BuildScript.Tests runs serialized
// for the same reason; so does this assembly.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
