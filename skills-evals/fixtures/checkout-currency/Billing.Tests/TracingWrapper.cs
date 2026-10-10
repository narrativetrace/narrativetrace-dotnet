// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using Billing;
using NarrativeTrace.Core;
using NarrativeTrace.Proxy;

namespace Billing.Tests;

/// <summary>Wires every service traced, into one test's narrative context.</summary>
public sealed class TracingWrapper(INarrativeContext context) : IServiceWrapper
{
    public T Wrap<T>(T service)
        where T : class => NarrativeTraceProxy.Create(service, context);
}
