// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.ContractProbe.Fixtures;

namespace NarrativeTrace.ContractProbe.Probes;

/// <summary>
/// <c>probed-default</c>: a class exposing public state is rendered by reflecting over its
/// members through the redaction-checked path — a hand-written <c>ToString()</c> that
/// interpolates a deny-listed field directly can never bypass that walk, at any depth —
/// privacy-and-redaction.md "Guarantees".
/// </summary>
internal static class NativeStringificationNotTrustedProbe
{
    public sealed class Login
    {
        public Login(string username, string password)
        {
            Username = username;
            Password = password;
        }

        public string Username { get; }

        public string Password { get; }

        // Curated, hand-written — deliberately the leak this probe proves is closed.
        public override string ToString() => $"Login{{username={Username}, password={Password}}}";
    }

    public interface IFactory
    {
        Login Make(string username, string password);
    }

    private sealed class Factory : IFactory
    {
        public Login Make(string username, string password) => new(username, password);
    }

    public static string Observe()
    {
        var rendered = TracedRender.Render<IFactory>(new Factory(), proxy => proxy.Make("alice", "hunter2"));

        if (rendered.Contains("hunter2", StringComparison.Ordinal))
            return "leaked";

        return rendered.Contains("[REDACTED]", StringComparison.Ordinal) ? "field-walked-not-tostring" : "no-marker";
    }
}
