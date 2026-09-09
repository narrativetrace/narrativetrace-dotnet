// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.TestingXunit;
using Xunit;

namespace NarrativeTrace.Testing.Xunit.Tests;

/// <summary>
/// Exercises <see cref="NarrativeFixture"/> exactly the way every doc teaches
/// it (first-10-minutes.md, README.md): registered as
/// <c>IClassFixture&lt;NarrativeFixture&gt;</c> and injected through the test
/// class constructor.
/// </summary>
/// <remarks>
/// This is the regression test, not the assertions inside it. xUnit's fixture
/// activator (<c>Xunit.Sdk.TestClassRunner</c>) requires a fixture type to
/// declare exactly one public constructor and throws
/// <c>TestClassException</c> — "Class fixture type ... may only define a
/// single public constructor" — while instantiating this very class if
/// <see cref="NarrativeFixture"/> ever regains a second one. Every other test
/// in this project constructs <c>NarrativeFixture</c> directly (<c>new
/// NarrativeFixture()</c>, the internal env-reading constructor, or
/// <see cref="NarrativeFixture.Create"/>), none of which go through that
/// activator — this class is what would have caught the 0.1.0 defect where
/// <c>NarrativeFixture</c> shipped with two public constructors and
/// <c>IClassFixture&lt;NarrativeFixture&gt;</c> failed for every adopter.
/// </remarks>
public sealed class NarrativeFixtureAsClassFixtureTests : IClassFixture<NarrativeFixture>
{
    private readonly NarrativeFixture _fixture;

    public NarrativeFixtureAsClassFixtureTests(NarrativeFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void Fixture_traces_a_call_when_used_as_a_class_fixture()
    {
        _fixture.Run("Places_an_order", ctx =>
        {
            ctx.EnterMethod("OrderService", "PlaceOrder", []);
            ctx.ExitMethodWithReturn(null);
        });

        Assert.Single(_fixture.CaptureTrace().Roots);
        Assert.Equal("PlaceOrder", _fixture.CaptureTrace().Roots[0].Signature.MethodName);
    }
}
