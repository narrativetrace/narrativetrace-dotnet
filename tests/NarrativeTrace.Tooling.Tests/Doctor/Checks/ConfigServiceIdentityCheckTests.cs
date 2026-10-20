// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Tooling.Doctor;
using NarrativeTrace.Tooling.Doctor.Checks;
using Xunit;

namespace NarrativeTrace.Tooling.Tests.Doctor.Checks;

public sealed class ConfigServiceIdentityCheckTests
{
    private const string Wired =
        "var orders = NarrativeTraceProxy.Create<IOrderService>(new OrderService(), context);";

    private static DoctorSnapshot Source(string path, string content) =>
        DoctorSnapshotFixtures.WithSource((path, content));

    [Fact]
    public void Passes_when_tracing_is_not_wired_up_in_non_test_source()
    {
        Assert.True(ConfigServiceIdentityCheck.Run(DoctorSnapshotFixtures.Empty()).Passed);
    }

    [Fact]
    public void Passes_when_the_only_wiring_is_in_a_test_project()
    {
        Assert.True(ConfigServiceIdentityCheck.Run(Source("tests/Orders.Tests/OrderTests.cs", Wired)).Passed);
    }

    [Fact]
    public void Fails_when_wired_in_non_test_source_and_no_identity_is_set()
    {
        var finding = ConfigServiceIdentityCheck.Run(Source("src/Orders/Program.cs", Wired));

        Assert.False(finding.Passed);
        Assert.Contains("unknown_service:dotnet", finding.Message, StringComparison.Ordinal);
        Assert.Contains("NARRATIVETRACE_SERVICE_NAME", finding.Fix, StringComparison.Ordinal);
        Assert.Contains("new ServiceIdentity(", finding.Fix, StringComparison.Ordinal);
    }

    [Fact]
    public void Passes_when_the_environment_names_the_service()
    {
        var snapshot = Source("src/Orders/Program.cs", Wired) with
        {
            Env = new Dictionary<string, string?> { ["NARRATIVETRACE_SERVICE_NAME"] = "order-service" },
        };

        Assert.True(ConfigServiceIdentityCheck.Run(snapshot).Passed);
    }

    [Fact]
    public void A_blank_environment_value_names_nothing()
    {
        var snapshot = Source("src/Orders/Program.cs", Wired) with
        {
            Env = new Dictionary<string, string?> { ["NARRATIVETRACE_SERVICE_NAME"] = "  " },
        };

        Assert.False(ConfigServiceIdentityCheck.Run(snapshot).Passed);
    }

    [Theory]
    [InlineData("new NarrativeTraceConfig(serviceIdentity: new ServiceIdentity(\"order-service\"))")]
    [InlineData("o.ServiceIdentity = new ServiceIdentity(\"order-service\");")]
    public void Passes_when_non_test_source_builds_a_service_identity(string call)
    {
        var snapshot = Source("src/Orders/Program.cs", Wired + "\n" + call);

        Assert.True(ConfigServiceIdentityCheck.Run(snapshot).Passed);
    }

    [Fact]
    public void An_identity_built_only_in_a_test_project_does_not_count()
    {
        var snapshot = DoctorSnapshotFixtures.WithSource(
            ("src/Orders/Program.cs", Wired),
            ("tests/Orders.Tests/IdentityTests.cs", "new ServiceIdentity(\"order-service\")"));

        Assert.False(ConfigServiceIdentityCheck.Run(snapshot).Passed);
    }

    [Fact]
    public void An_identity_in_a_comment_does_not_count()
    {
        var snapshot = Source("src/Orders/Program.cs", Wired + "\n// new ServiceIdentity(\"order-service\")");

        Assert.False(ConfigServiceIdentityCheck.Run(snapshot).Passed);
    }

    [Fact]
    public void Passes_when_appsettings_names_the_service()
    {
        var snapshot = Source("src/Orders/Program.cs", Wired) with
        {
            AppSettingsFiles = new Dictionary<string, string>
            {
                ["appsettings.json"] = "{ \"NarrativeTrace\": { \"ServiceName\": \"order-service\" } }",
            },
        };

        Assert.True(ConfigServiceIdentityCheck.Run(snapshot).Passed);
    }

    [Theory]
    [InlineData("src/Orders.Tests/Program.cs")]
    [InlineData("test/Orders/Program.cs")]
    [InlineData("src/OrdersTests/Program.cs")]
    public void Test_paths_are_recognised_by_a_whole_segment(string path)
    {
        Assert.True(ConfigServiceIdentityCheck.Run(Source(path, Wired)).Passed);
    }

    [Fact]
    public void A_segment_that_merely_contains_test_is_not_a_test_path()
    {
        Assert.False(ConfigServiceIdentityCheck.Run(Source("src/Contest/Program.cs", Wired)).Passed);
    }
}
