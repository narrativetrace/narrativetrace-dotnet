// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
module NarrativeTrace.Examples.Library.Program

open NarrativeTrace.Examples.Common

[<EntryPoint>]
let main argv =
    use run = DemoRun.Create("library", DemoOptions.Parse argv)
    LibraryExample.run run
    0
