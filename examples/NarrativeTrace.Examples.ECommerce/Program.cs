// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Examples.Common;
using NarrativeTrace.Examples.ECommerce;

using var run = DemoRun.Create("ecommerce", DemoOptions.Parse(args));
await ECommerceDemo.RunAsync(run).ConfigureAwait(false);
