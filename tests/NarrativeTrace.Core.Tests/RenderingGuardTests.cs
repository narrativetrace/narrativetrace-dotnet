// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using Xunit;

namespace NarrativeTrace.Core.Tests;

/// <summary>
/// Direct unit coverage for <see cref="RenderingGuard"/> itself — the flag
/// <see cref="NarrativeInterceptor"/> and <see cref="LoggingNarrativeContext"/>
/// share to recognize a reflective read as rendering-in-progress. The
/// integration-level proof that it actually suppresses a phantom span lives
/// in <c>RenderReentrancyGuardTests</c> (NarrativeTrace.Proxy.Tests) and the
/// §127 exception-reentrancy test; this file pins the guard's own state
/// machine in isolation, including the idempotent-dispose path those
/// integration tests never happen to exercise (a scope is disposed exactly
/// once there, via <see langword="using"/>).
/// </summary>
public class RenderingGuardTests
{
    [Fact]
    public void IsActive_is_false_before_any_scope_is_entered()
    {
        Assert.False(RenderingGuard.IsActive);
    }

    [Fact]
    public void Entering_marks_the_flow_active_until_the_scope_is_disposed()
    {
        Assert.False(RenderingGuard.IsActive);

        using (RenderingGuard.Enter())
        {
            Assert.True(RenderingGuard.IsActive);
        }

        Assert.False(RenderingGuard.IsActive);
    }

    [Fact]
    public void Disposing_the_scope_a_second_time_is_a_safe_no_op()
    {
        var scope = RenderingGuard.Enter();
        Assert.True(RenderingGuard.IsActive);

        scope.Dispose();
        Assert.False(RenderingGuard.IsActive);

        // The idempotency guard (Scope._disposed): a second Dispose() must
        // not throw, and — critically — must not clear the flag for an
        // unrelated scope that entered after the first one left, which a
        // naive "always set Rendering.Value = false" second call would.
        using var unrelated = RenderingGuard.Enter();
        Assert.True(RenderingGuard.IsActive);

        scope.Dispose();
        Assert.True(RenderingGuard.IsActive);
    }

    [Fact]
    public void A_throwing_body_still_leaves_the_scope_cleared()
    {
        void ThrowWhileEntered()
        {
            using (RenderingGuard.Enter())
            {
                throw new InvalidOperationException("boom");
            }
        }

        Assert.Throws<InvalidOperationException>(ThrowWhileEntered);

        Assert.False(RenderingGuard.IsActive);
    }
}
