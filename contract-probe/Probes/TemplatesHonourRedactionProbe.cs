// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core.Annotation;
using NarrativeTrace.ContractProbe.Fixtures;

namespace NarrativeTrace.ContractProbe.Probes;

/// <summary>
/// <c>probed-default</c>: a <c>[Narrated]</c> template placeholder resolved on a proxy built with
/// a custom <c>ProxyOptions.Redaction</c> checks that same policy — a <c>{password}</c> placeholder
/// resolves to the real value when the proxy's own policy disables redaction, rather than always
/// falling back to the default policy — annotations.md "Templates honor the proxy's own redaction
/// policy". <see cref="RedactionDisabledOptions"/> builds the options without naming
/// <c>Redaction:</c> at compile time — see its own remarks.
/// </summary>
internal static class TemplatesHonourRedactionProbe
{
    public interface ILoginService
    {
        [Narrated("login attempt with password {password}")]
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

        return rendered.Contains("hunter2", StringComparison.Ordinal) ? "true" : "false";
    }
}
