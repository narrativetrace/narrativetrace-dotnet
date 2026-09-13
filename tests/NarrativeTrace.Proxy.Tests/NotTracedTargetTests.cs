// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using NarrativeTrace.Core.Annotation;
using NarrativeTrace.Runtime;
using Xunit;

namespace NarrativeTrace.Proxy.Tests;

/// <summary>
/// <c>[NotTraced]</c> has no METHOD-level meaning (JVM-edition parity: its
/// <c>@NotTraced</c> has no <c>METHOD</c> target either — see
/// <see cref="NotTracedAttribute"/>'s own remarks). Applying it to a whole
/// method compiles (register row 32: the previous <c>AttributeUsage</c>
/// turned this into an opaque <c>CS0592</c> naming neither the attribute
/// nor the fix), but fails fast at proxy-creation time with a message
/// naming the attribute, the offending method, and the fix — rather than
/// silently ignoring the misplaced attribute or surfacing a confusing
/// failure the first time that method is invoked.
/// </summary>
public class NotTracedTargetTests
{
    public interface IAccountServiceWithMethodLevelNotTraced
    {
        [NotTraced]
        void Authenticate(string user, string password);

        void Ordinary();
    }

    private sealed class AccountService
        : IAccountServiceWithMethodLevelNotTraced
    {
        public void Authenticate(string user, string password)
        {
        }

        public void Ordinary()
        {
        }
    }

    public interface IBase
    {
        [NotTraced]
        void Legacy();
    }

    public interface IDerived : IBase
    {
        void Current();
    }

    private sealed class DerivedService : IDerived
    {
        public void Legacy()
        {
        }

        public void Current()
        {
        }
    }

    [Fact]
    public void Create_generic_rejects_method_level_NotTraced_at_creation()
    {
        var ctx = new SyncNarrativeContext(new NarrativeTraceConfig());

        var ex = Assert.Throws<InvalidOperationException>(() =>
            NarrativeTraceProxy.Create<
                IAccountServiceWithMethodLevelNotTraced>(
                new AccountService(), ctx));

        Assert.Contains("NotTraced", ex.Message, StringComparison.Ordinal);
        Assert.Contains("Authenticate", ex.Message, StringComparison.Ordinal);
        Assert.Contains("method", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // The non-generic Create(Type, ...) overload used by DI auto-wrap must
    // reject the same way — the misuse is a property of the interface
    // type, not of which factory overload constructs the proxy.
    [Fact]
    public void Create_non_generic_rejects_method_level_NotTraced_at_creation()
    {
        var ctx = new SyncNarrativeContext(new NarrativeTraceConfig());

        // The non-generic overload under test is exactly what DI auto-wrap
        // calls (the interface type is only known at runtime there) — the
        // generic overload CA2263 prefers is not usable from that call site.
#pragma warning disable CA2263
        var ex = Assert.Throws<InvalidOperationException>(() =>
            NarrativeTraceProxy.Create(
                typeof(IAccountServiceWithMethodLevelNotTraced),
                new AccountService(), ctx));
#pragma warning restore CA2263

        Assert.Contains("NotTraced", ex.Message, StringComparison.Ordinal);
        Assert.Contains("Authenticate", ex.Message, StringComparison.Ordinal);
    }

    // A method-level [NotTraced] declared on a BASE interface must be
    // caught too: DispatchProxy intercepts calls arriving through any
    // interface in the proxied type's transitive closure, not only the one
    // named at Create<T> time.
    [Fact]
    public void Rejects_method_level_NotTraced_declared_on_a_base_interface()
    {
        var ctx = new SyncNarrativeContext(new NarrativeTraceConfig());

        var ex = Assert.Throws<InvalidOperationException>(() =>
            NarrativeTraceProxy.Create<IDerived>(
                new DerivedService(), ctx));

        Assert.Contains("Legacy", ex.Message, StringComparison.Ordinal);
    }

    // Control: an interface with no method-level [NotTraced] anywhere
    // still creates a proxy normally — proves the rejection is specific to
    // the misuse, not a blanket failure on every Create call.
    [Fact]
    public void An_interface_with_no_method_level_NotTraced_creates_normally()
    {
        var ctx = new SyncNarrativeContext(new NarrativeTraceConfig());

        var proxy = NarrativeTraceProxy.Create<IPlainDerived>(
            new PlainDerivedService(), ctx);

        Assert.NotNull(proxy);
        proxy.Current();
    }

    private sealed class PlainDerivedService : IPlainDerived
    {
        public void Current()
        {
        }
    }

    public interface IPlainDerived
    {
        void Current();
    }
}
