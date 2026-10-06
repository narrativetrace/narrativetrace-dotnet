// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Tooling.Init;

namespace NarrativeTrace.Cli.Init;

/// <summary>
/// What <c>init</c> and <c>uninstall</c> were asked to do, parsed out of a command line and nothing
/// else — the .NET analogue of Java's <c>InstallerArguments</c>.
/// </summary>
/// <remarks>
/// <para>INTENT: keeps every flag decision in one pure function, so the verbs themselves stay a
/// handful of lines over the installer library — the same shape the doctor verb has.</para>
/// <para>A bad flag is DATA here (<see cref="Error"/>), never an exception: the launcher prints it
/// with the verb's usage and exits 2, the one code that means "could not run at all". The FIRST
/// problem is the one reported. <see cref="Help"/> wins over an error, so a mistyped command can
/// still ask how it works.</para>
/// <para><c>--refresh</c> is hidden (D8): it is not in the usage text and switches the install to
/// <c>RefreshPlanner</c> semantics, the build-time refresh the MSBuild target drives. <c>--dir</c>
/// is this port's convention, shared with the doctor verb.</para>
/// </remarks>
public sealed record InstallerArguments
{
    private InstallerArguments(
        InitOptions options, string? from, string? dir, bool json, bool refresh, bool help, string? error)
    {
        Options = options;
        From = from;
        Dir = dir;
        Json = json;
        Refresh = refresh;
        Help = help;
        Error = error;
    }

    /// <summary>What the installer library was asked for.</summary>
    public InitOptions Options { get; }

    /// <summary>The carrier a person named, or <see langword="null"/> for the one bundled in this CLI.</summary>
    public string? From { get; }

    /// <summary>The project directory, or <see langword="null"/> for the process's working directory.</summary>
    public string? Dir { get; }

    /// <summary>Whether to print the machine-readable envelope instead of human text.</summary>
    public bool Json { get; }

    /// <summary>Whether this is a build-time refresh (hidden <c>--refresh</c>): rewrite only, never create.</summary>
    public bool Refresh { get; }

    /// <summary>Whether the verb's usage was asked for.</summary>
    public bool Help { get; }

    /// <summary>Why the command line could not be read, or <see langword="null"/> when it could.</summary>
    public string? Error { get; }

    /// <summary>Reads the arguments that follow the verb.</summary>
    public static InstallerArguments Parse(IReadOnlyList<string> arguments)
    {
        var reading = new Reading();
        var index = 0;
        while (index < arguments.Count)
        {
            index = reading.Read(arguments, index) + 1;
        }

        return reading.Result();
    }

    /// <summary>One pass over the arguments; mutable so each flag stays one readable line.</summary>
    private sealed class Reading
    {
        private static readonly string[] ValueFlags = ["--only", "--vendor", "--from", "--dir"];

        private InitOptions _options = InitOptions.Defaults();
        private string? _from;
        private string? _dir;
        private bool _json;
        private bool _refresh;
        private bool _help;
        private string? _error;

        public InstallerArguments Result() =>
            new(_options, _from, _dir, _json, _refresh, _help, _error);

        /// <summary>Reads one argument and returns the index of the last one it consumed.</summary>
        public int Read(IReadOnlyList<string> arguments, int index)
        {
            var argument = arguments[index];
            var equals = argument.IndexOf('=', StringComparison.Ordinal);
            var name = equals < 0 ? argument : argument[..equals];
            if (Switched(name))
            {
                return index;
            }

            if (!ValueFlags.Contains(name))
            {
                Fail("unknown option: \"" + argument + "\"");
                return index;
            }

            var valueIndex = equals < 0 ? index + 1 : index;
            var value = equals < 0 ? At(arguments, valueIndex) : argument[(equals + 1)..];
            return Valued(name, value) ? valueIndex : index;
        }

        private bool Switched(string name)
        {
            switch (name)
            {
                case "--dry-run": _options = _options.WithDryRun(true); return true;
                case "--write-existing": _options = _options.WithWriteExisting(true); return true;
                case "--force": _options = _options.WithForce(true); return true;
                case "--json": _json = true; return true;
                case "--refresh": _refresh = true; return true;
                case "--help" or "-h": _help = true; return true;
                default: return false;
            }
        }

        private bool Valued(string name, string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                Fail(name + " needs a value");
                return false;
            }

            switch (name)
            {
                case "--only": ScopeOf(value); break;
                case "--vendor": VendorOf(value); break;
                case "--dir": _dir = value; break;
                default: _from = value; break;
            }

            return true;
        }

        private void ScopeOf(string value)
        {
            switch (value)
            {
                case "skills": _options = _options.WithScope(Scope.Skills); break;
                case "agents-md": _options = _options.WithScope(Scope.AgentsMd); break;
                default: Fail("--only takes skills or agents-md, got \"" + value + "\""); break;
            }
        }

        private void VendorOf(string value)
        {
            switch (value)
            {
                case "claude": _options = _options.WithVendorClaude(Vendor.On); break;
                case "none": _options = _options.WithVendorClaude(Vendor.Off); break;
                default: Fail("--vendor takes claude or none, got \"" + value + "\""); break;
            }
        }

        private void Fail(string message) => _error ??= message;

        private static string? At(IReadOnlyList<string> arguments, int index) =>
            index < arguments.Count ? arguments[index] : null;
    }
}
