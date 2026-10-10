// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Runtime.InteropServices;

namespace NarrativeTrace.SkillsEvals;

/// <summary>
/// A throwaway vendor-tool configuration a trial owns, in BOTH directions.
///
/// <para>Outward: a marketplace and a plugin are USER-level state, the only scope the documentation
/// describes, so a registry trial's installs land in the trial's own directory and never in the
/// configuration of the person running it.</para>
///
/// <para>Inward, and just as load-bearing: no trial inherits that person's configuration either. A
/// trial driven against the operator's own configuration is driven against the operator's own
/// skills — the skill under test one line among forty — and a red row would not reproduce
/// anywhere else.</para>
///
/// <para>Two things stay deliberately OUTSIDE the isolation. <c>HOME</c> is untouched: it is where
/// the NuGet/SDK caches live, and a trial that moved it would spend its first minutes
/// re-downloading instead of measuring a skill. And the subscription LOGIN is copied in, because a
/// fresh configuration is a logged-out one — the agent CLI answers "Not logged in" and the trial
/// would measure the harness. Nothing else of the real configuration is read, and nothing at all is
/// written back to it.</para>
/// </summary>
public static class IsolatedAgentConfig
{
    /// <summary>The vendor CLI's own configuration-directory override.</summary>
    private const string ConfigDirVariable = "CLAUDE_CONFIG_DIR";

    /// <summary>The subscription login, the one file a fresh configuration cannot do without.</summary>
    private const string LoginFile = ".credentials.json";

    /// <summary>The isolated configuration directory inside a trial's work directory.</summary>
    public static string ConfigDir(string workDir) => Path.Combine(workDir, "config");

    /// <summary>Where the package runner caches its downloads — the ambient one may not be writable.</summary>
    public static string NpmCacheDir(string workDir) => Path.Combine(workDir, "npm-cache");

    /// <summary>
    /// The isolated configuration, for EVERY trial — the one entry that keeps a trial from
    /// inheriting the configuration of the person running it.
    /// </summary>
    public static IReadOnlyDictionary<string, string> AgentConfigEnv(string workDir) =>
        new Dictionary<string, string>(StringComparer.Ordinal) { [ConfigDirVariable] = ConfigDir(workDir) };

    /// <summary>
    /// The above, plus what only a REGISTRY trial needs: a writable package cache and a best-effort
    /// telemetry opt-out for the registry tool that has one.
    /// </summary>
    public static IReadOnlyDictionary<string, string> Env(string workDir) =>
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [ConfigDirVariable] = ConfigDir(workDir),
            ["npm_config_cache"] = NpmCacheDir(workDir),
            ["DO_NOT_TRACK"] = "1",
        };

    /// <summary>The configuration directory the ambient environment points the vendor CLI at.</summary>
    public static string RealConfigDir() =>
        RealConfigDir(
            Environment.GetEnvironmentVariable(ConfigDirVariable),
            Environment.GetEnvironmentVariable("HOME"),
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

    /// <summary>
    /// The override when one is set; otherwise the conventional directory under the home the TOOLS
    /// use. <c>HOME</c> is asked before the runtime's own user home on purpose: every CLI this
    /// harness drives reads the environment variable, and a container whose uid has no passwd entry
    /// hands the runtime a user home that holds no login file — a logged-out agent two steps later.
    /// </summary>
    public static string RealConfigDir(string? configDirVariable, string? homeVariable, string userHome)
    {
        if (!string.IsNullOrWhiteSpace(configDirVariable))
        {
            return configDirVariable;
        }

        var home = string.IsNullOrWhiteSpace(homeVariable) ? userHome : homeVariable;
        return Path.Combine(home, ".claude");
    }

    /// <summary>
    /// Creates the isolated configuration directory and copies the subscription login into it when
    /// <paramref name="realConfigDir"/> has one.
    /// </summary>
    /// <returns>
    /// Whether a login was found and copied. A caller SAYS which it was: a trial with no login does
    /// not fail here, it fails three steps later when the agent answers "Not logged in", and that is
    /// a long way from the cause.
    /// </returns>
    public static bool SeedLogin(string realConfigDir, string workDir)
    {
        var isolated = Directory.CreateDirectory(ConfigDir(workDir)).FullName;
        Directory.CreateDirectory(NpmCacheDir(workDir));
        var login = Path.Combine(realConfigDir, LoginFile);
        if (!File.Exists(login))
        {
            return false;
        }

        var copy = Path.Combine(isolated, LoginFile);
        File.Copy(login, copy, overwrite: true);
        OwnerOnly(copy);
        return true;
    }

    /// <summary>
    /// The copy is a credential, so it is readable by its owner and nobody else. Best effort by
    /// necessity: a filesystem with no POSIX permission view is not a reason to refuse a trial, and
    /// the directory it sits in is deleted when the trial ends either way.
    /// </summary>
    private static void OwnerOnly(string path)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }
}
