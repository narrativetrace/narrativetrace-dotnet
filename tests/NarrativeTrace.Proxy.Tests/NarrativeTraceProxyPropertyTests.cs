// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using FsCheck;
using FsCheck.Xunit;
using NarrativeTrace.Core;
using NarrativeTrace.Runtime;

namespace NarrativeTrace.Proxy.Tests;

public class NarrativeTraceProxyPropertyTests
{
    public interface IIdentity
    {
        int Echo(int x);
    }

    private sealed class Identity : IIdentity
    {
        public int Echo(int x) => x;
    }

    [Property]
    public bool Proxy_never_alters_return_value(int input)
    {
        var ctx = new SyncNarrativeContext(
            new NarrativeTraceConfig());
        var proxy = NarrativeTraceProxy.Create<IIdentity>(
            new Identity(), ctx);

        return proxy.Echo(input) == input;
    }

    [Property]
    public bool Parameter_count_matches_method_signature(
        int a, int b)
    {
        var ctx = new SyncNarrativeContext(
            new NarrativeTraceConfig());
        var proxy = NarrativeTraceProxy.Create<IIdentity>(
            new Identity(), ctx);

        proxy.Echo(a);

        var node = ctx.CaptureTrace().Roots[0];
        return node.Signature.Parameters.Count == 1;
    }

    public interface IFailing
    {
        void Fail();
    }

#pragma warning disable S3877
    private sealed class AlwaysFails : IFailing
    {
        public void Fail() =>
            throw new InvalidOperationException("boom");
    }
#pragma warning restore S3877

    [Property]
    public bool Proxy_always_rethrows_original_exception(
        int seed)
    {
        var ctx = new SyncNarrativeContext(
            new NarrativeTraceConfig());
        var proxy = NarrativeTraceProxy.Create<IFailing>(
            new AlwaysFails(), ctx);

        try
        {
            proxy.Fail();
            return false;
        }
        catch (InvalidOperationException ex)
        {
            return ex.Message == "boom";
        }
    }
}
