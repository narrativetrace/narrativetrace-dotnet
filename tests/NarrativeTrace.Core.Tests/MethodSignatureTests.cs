// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using Xunit;

namespace NarrativeTrace.Core.Tests;

public class MethodSignatureTests
{
    [Fact]
    public void Stores_class_method_and_parameters()
    {
        var parameters = new[]
        {
            new ParameterCapture("id", "42", false),
        };

        var signature = new MethodSignature(
            "OrderService", "PlaceOrder", parameters);

        Assert.Equal("OrderService", signature.ClassName);
        Assert.Equal("PlaceOrder", signature.MethodName);
        Assert.Single(signature.Parameters);
        Assert.Null(signature.Narration);
        Assert.Null(signature.ErrorContext);
    }
}
