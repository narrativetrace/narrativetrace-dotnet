// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.ContractProbe.Fixtures;

namespace NarrativeTrace.ContractProbe.Probes;

/// <summary>
/// <c>probed-default</c>: a proxy built with <c>new ProxyOptions(Redaction: RedactionPolicy.Disabled)</c>
/// replaces the default name-based redaction decision for that proxy's own captures — a
/// <c>password</c> parameter, deny-listed under the default policy, renders in the clear —
/// privacy-and-redaction.md "Redaction, surface by surface". <see cref="RedactionDisabledOptions"/>
/// builds the options without naming <c>Redaction:</c> at compile time — see its own remarks.
/// </summary>
internal static class ProxyOptionsRedactionProbe
{
    public interface ILoginService
    {
        string Login(string username, string password);
    }

    private sealed class LoginService : ILoginService
    {
        public string Login(string username, string password) => "ok";
    }

    public static string Observe()
    {
        var options = RedactionDisabledOptions.Build();
        var rendered = TracedRender.Render<ILoginService>(
            new LoginService(), proxy => proxy.Login("alice", "hunter2"), options);

        if (rendered.Contains("hunter2", StringComparison.Ordinal))
            return "true";

        return rendered.Contains("[REDACTED]", StringComparison.Ordinal) ? "false" : "no-value";
    }
}
