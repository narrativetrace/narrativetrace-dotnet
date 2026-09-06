// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using Xunit;

namespace NarrativeTrace.Core.Tests;

public class ServiceIdentityTests
{
    [Fact]
    public void Holds_service_name_version_and_environment()
    {
        var identity = new ServiceIdentity("order-service", "2.3.1", "production");

        Assert.Equal("order-service", identity.ServiceName);
        Assert.Equal("2.3.1", identity.ServiceVersion);
        Assert.Equal("production", identity.Environment);
    }

    [Fact]
    public void All_fields_default_to_null()
    {
        var identity = new ServiceIdentity();

        Assert.Null(identity.ServiceName);
        Assert.Null(identity.ServiceVersion);
        Assert.Null(identity.Environment);
    }
}
