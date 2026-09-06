// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using Xunit;

namespace NarrativeTrace.Core.Tests;

public class RequestValuesTests
{
    [Fact]
    public void ToString_returns_raw_value_for_transparent_export()
    {
        Assert.Equal("/api/orders", new HttpRoute("/api/orders").ToString());
        Assert.Equal("203.0.113.5", new ClientIp("203.0.113.5").ToString());
        Assert.Equal("user-42", new EnduserId("user-42").ToString());
        Assert.Equal("sess-9", new SessionId("sess-9").ToString());
        Assert.Equal("tenant-x", new TenantId("tenant-x").ToString());
    }

    [Fact]
    public void Value_objects_have_value_equality()
    {
        Assert.Equal(new TenantId("acme"), new TenantId("acme"));
        Assert.NotEqual(new TenantId("acme"), new TenantId("other"));
    }
}
