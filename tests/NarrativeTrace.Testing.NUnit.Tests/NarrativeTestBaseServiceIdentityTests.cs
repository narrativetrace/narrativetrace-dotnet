// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using NarrativeTrace.TestingNUnit;
using Xunit;

namespace NarrativeTrace.Testing.NUnit.Tests;

[Collection(SuiteScopeCollection.Name)]
public class NarrativeTestBaseServiceIdentityTests
{
    [Fact]
    public void Captured_spans_carry_the_service_name_from_the_environment()
    {
        var test = new EnvironmentSubject("order-service");
        test.SetUpTrace();
        test.Context.EnterMethod("Svc", "run", []);
        test.Context.ExitMethodWithReturn(null);
        test.TearDownTrace();

        Assert.Equal("order-service", test.Captured.Roots[0].SpanContext?.ServiceName);
    }

    private sealed class EnvironmentSubject : NarrativeTestBase
    {
        private readonly string _serviceName;

        public EnvironmentSubject(string serviceName)
        {
            _serviceName = serviceName;
        }

        public TraceTree Captured { get; private set; } = new([]);

        protected override string? ReadEnvironment(string key) =>
            key == ConfigResolver.ServiceNameKey ? _serviceName : null;

        protected override void OnTraceComplete(TraceTree tree)
        {
            Captured = tree;
        }
    }
}
