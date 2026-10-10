// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.ComponentModel;
using System.Diagnostics;

namespace NarrativeTrace.Cli.Feedback;

/// <summary>
/// What an already-installed <c>gh</c> reported when asked whether it is signed in: whether its
/// process finished inside the timeout, and with what exit code.
/// </summary>
/// <param name="Exited">Whether the probe process finished before the timeout.</param>
/// <param name="ExitCode">Its exit code when it finished; undefined when it did not.</param>
internal readonly record struct GhProbeResult(bool Exited, int ExitCode);

/// <summary>
/// Asks an already-installed <c>gh</c> whether it is signed in — the one question that decides
/// whether the <c>gh</c> channel is offered at all.
/// </summary>
/// <remarks>
/// <para>
/// INTENT: <c>gh issue create</c> is the convenience path, and offering it to somebody who has no
/// <c>gh</c>, or an unauthenticated one, is offering them an error message. <c>gh auth status</c>
/// answers exactly that question, using that tool's own credential; this probe holds none and never
/// will.
/// </para>
/// <para>
/// <b>@llmNote</b> The probe is the ONLY outward-facing thing the launcher's own process causes, and
/// it causes it indirectly: <c>gh</c> validates its token against the host. That is the user's tool
/// making the user's call. The launcher still makes no request of its own, holds no credential and
/// files nothing.
/// </para>
/// <para>
/// <b>@llmNote</b> Absence, a non-zero exit, a timeout and a platform that cannot start a process all
/// answer the SAME way: not available. An unavailable channel is a fact, never an error — the verb
/// says "use the URL instead" and exits 1.
/// </para>
/// <para>
/// <b>@llmNote</b> The <see cref="Func{GhProbeResult}"/> is the seam: the DECISION — which outcomes
/// mean "signed in" — is the part worth testing, and testing it by installing a tool is testing the
/// machine instead. <see cref="RunGhAuthStatus"/> is the one method that touches a real process, and
/// it is the single entry point from which the probe is ever started.
/// </para>
/// <para>
/// <b>@sideEffects</b> Starts one short-lived subprocess, with its output discarded.
/// </para>
/// </remarks>
internal static class GhAuthProbe
{
    /// <summary>Long enough for a token check, short enough that nobody waits on a wedged network.</summary>
    private const int TimeoutSeconds = 10;

    /// <summary>Whether <c>gh</c> is present AND signed in. Any doubt answers false.</summary>
    internal static bool Authenticated() => Authenticated(RunGhAuthStatus);

    /// <summary>The decision, over whatever probed the tool. Every outcome but a clean exit 0 is false.</summary>
    internal static bool Authenticated(Func<GhProbeResult> probe)
    {
        try
        {
            var result = probe();
            return result is { Exited: true, ExitCode: 0 };
        }
        catch (Exception exception) when (
            exception is Win32Exception or InvalidOperationException or IOException or PlatformNotSupportedException)
        {
            return false;
        }
    }

    /// <summary>
    /// The real probe. A fixed argv of three literals — nothing a project, a flag or a report can
    /// reach, which is what makes this one method safe to have at all.
    /// </summary>
    private static GhProbeResult RunGhAuthStatus()
    {
        var startInfo = new ProcessStartInfo("gh")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add("auth");
        startInfo.ArgumentList.Add("status");

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("gh could not be started");
        if (!process.WaitForExit(TimeSpan.FromSeconds(TimeoutSeconds)))
        {
            process.Kill(entireProcessTree: true);
            return new GhProbeResult(Exited: false, ExitCode: 0);
        }

        return new GhProbeResult(Exited: true, process.ExitCode);
    }
}
