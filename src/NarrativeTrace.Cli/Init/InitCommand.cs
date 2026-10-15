// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Tooling.Init;

namespace NarrativeTrace.Cli.Init;

/// <summary>
/// The <c>init</c> and <c>uninstall</c> verbs: same flags, same output, one plans an install and one
/// its removal. Thin over <see cref="NarrativeTrace.Tooling.Init"/> — every decision lives in the
/// library's planners, this only reads a command line, opens a carrier, and renders the plan.
/// </summary>
/// <remarks>
/// <para>Exit codes (D12): <c>0</c> applied or previewed (or <c>--help</c>); <c>1</c> at least one
/// action was refused, or the carrier could not be opened, or the project directory could not be
/// read; <c>2</c> the command line could not be read at all.</para>
/// <para><b>@llmNote</b> This verb makes NO network call. <c>init</c> installs from the carrier
/// embedded in this CLI, or from whatever <c>--from</c> names locally — never from a feed it fetches.</para>
/// </remarks>
public static class InitCommand
{
    private const string InstallerOptions =
        "  --dry-run         Show the plan and the unified diff. Writes nothing, always exits 0.\n"
        + "  --write-existing  Permission to touch an AGENTS.md or CLAUDE.md that is already there.\n"
        + "  --force           Permission to overwrite a skill directory somebody else owns.\n"
        + "  --only <half>     skills | agents-md. Both halves by default.\n"
        + "  --vendor <vendor> claude | none. claude by default: Claude Code loads .claude/skills only.\n"
        + "  --from <carrier>  A directory, a .nupkg, or an Id/Version coordinate already in the local\n"
        + "                    NuGet cache. The carrier embedded in this CLI by default.\n"
        + "  --dir <path>      The project directory. The current directory by default.\n"
        + "  --json            Machine-readable output instead of human text.\n\n"
        + "Exit 0 = applied (or a dry run), 1 = something was refused, 2 = could not run.\n";

    private const string InitUsage =
        "dotnet-narrativetrace init [options]\n\n"
        + "Copies the NarrativeTrace agent skills into .agents/skills/ and .claude/skills/ (the only\n"
        + "directory Claude Code loads; --vendor none leaves it out) and writes one marked section into AGENTS.md. Where the project\n"
        + "shows a platform that has a pointer file, it writes that too: a marked section in replit.md\n"
        + "(replit.md or .replit present) and .cursor/rules/narrativetrace.mdc (.cursor/ present). Zero network.\n\n"
        + InstallerOptions;

    private const string UninstallUsage =
        "dotnet-narrativetrace uninstall [options]\n\n"
        + "Removes exactly what init wrote: skill directories carrying its provenance line, the marked\n"
        + "section, the platform pointers, and the one @AGENTS.md import line. Never touches anything else.\n\n"
        + InstallerOptions;

    /// <summary>What to type when the carrier cannot be found — the CLI itself never fetches anything.</summary>
    private const string FetchHint =
        "--from takes a directory, a .nupkg, or an Id/Version coordinate already in the local NuGet\n"
        + "cache (~/.nuget/packages). Omit --from to install from the carrier embedded in this CLI.";

    /// <summary>Runs the verb against the process's working directory and the embedded carrier.</summary>
    public static int Run(bool install, IReadOnlyList<string> args, TextWriter output, TextWriter error) =>
        Run(install, args, Directory.GetCurrentDirectory(), CarrierLocator.Open, output, error);

    internal static int Run(
        bool install, IReadOnlyList<string> args, string cwd,
        Func<string?, Carrier> openCarrier, TextWriter output, TextWriter error)
    {
        var parsed = InstallerArguments.Parse(args);
        var usage = install ? InitUsage : UninstallUsage;
        if (parsed.Help)
        {
            output.Write(usage);
            return 0;
        }

        if (parsed.Error is not null)
        {
            error.WriteLine(parsed.Error + "\n\n" + usage);
            return 2;
        }

        var directory = parsed.Dir ?? cwd;
        return install
            ? RunInit(parsed, directory, openCarrier, output, error)
            : RunUninstall(parsed, directory, output, error);
    }

    /// <summary>
    /// The carrier is opened BEFORE the project is read: a run that cannot find its skills has nothing
    /// to plan, and finding that out after walking the project would only delay the same message.
    /// </summary>
    private static int RunInit(
        InstallerArguments parsed, string directory, Func<string?, Carrier> openCarrier,
        TextWriter output, TextWriter error)
    {
        Carrier carrier;
        try
        {
            carrier = openCarrier(parsed.From);
        }
        catch (ArgumentException exception)
        {
            error.WriteLine(exception.Message + "\n\n" + FetchHint);
            return 1;
        }

        return RunPlan(
            state => parsed.Refresh
                ? RefreshPlanner.Plan(state, carrier)
                : InitPlanner.Plan(state, carrier, parsed.Options),
            parsed, directory, output, error);
    }

    private static int RunUninstall(
        InstallerArguments parsed, string directory, TextWriter output, TextWriter error) =>
        RunPlan(state => UninstallPlanner.Plan(state, parsed.Options), parsed, directory, output, error);

    private static int RunPlan(
        Func<ProjectState, InitPlan> planner, InstallerArguments parsed, string directory,
        TextWriter output, TextWriter error)
    {
        try
        {
            return Report(planner(ProjectStateReader.Read(directory)), parsed, directory, output);
        }
        catch (ArgumentException exception)
        {
            error.WriteLine(exception.Message);
            return 1;
        }
    }

    /// <summary>
    /// What the plan did, or what it would do. Both forms come from <see cref="PlanRenderer"/> so this
    /// launcher and the MSBuild refresh target cannot drift apart on what a dry run shows.
    /// </summary>
    private static int Report(InitPlan plan, InstallerArguments parsed, string directory, TextWriter output)
    {
        // --dry-run previews even the refresh plan (whose planner hardcodes DryRun=false), so the
        // hidden --refresh combined with --dry-run still writes nothing — the --dry-run contract holds.
        if (plan.DryRun || parsed.Options.DryRun)
        {
            output.Write(PlanRenderer.Render(plan, parsed.Json));
            return plan.ExitCode;
        }

        var executed = PlanExecutor.Execute(plan, directory);
        output.Write(PlanRenderer.Render(executed, parsed.Json));
        return executed.ExitCode;
    }
}
