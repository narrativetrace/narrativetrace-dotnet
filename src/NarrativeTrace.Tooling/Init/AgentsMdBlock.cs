// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Diagnostics;
using System.Text;

namespace NarrativeTrace.Tooling.Init;

/// <summary>
/// Renders the managed section the installer writes into a consumer's <c>AGENTS.md</c>.
/// </summary>
/// <remarks>
/// <para>INTENT: the always-on pointer. An agent that never saw an install prompt reads this section
/// at the start of its next session, finds the skills by name, and knows which command diagnoses the
/// project — the discovery channel that costs no description budget and needs no tool.</para>
/// <para>What goes in, in this order: one line on what NarrativeTrace is here and where traces land,
/// the skills with the catalogue's own descriptions, the commands, the documentation pointer, and the
/// rules the evaluations keep tripping over.</para>
/// <para>Nothing project-specific goes in beyond what the snapshot DETECTED — the output directory. No
/// inference, no generated coding rules, and no version talk: the coordinate on the opening marker is
/// a machine-written stamp, and it is the only version this block ever carries.</para>
/// <para>This port names the CLI verbs directly (<c>dotnet tool run dotnet-narrativetrace …</c>);
/// there is no Gradle-style build-tool alternative to choose between. Its Rules list carries two
/// rules, not Java's three: this port's prompt has no compiler-flag / parameter-name rule (D6), so the
/// block does not invent one. Renders with <c>\n</c> throughout; a caller writing into a file that
/// uses another line ending converts with <see cref="MarkedBlock.WithEol"/>.</para>
/// </remarks>
internal static class AgentsMdBlock
{
    /// <summary>The documentation host; split from the page so the whole URI is not one literal (S1075).</summary>
    private const string DocsBase = "https://narrativetrace.ai/dotnet/";

    /// <summary>The runtime's documentation index, the one link an agent needs from here.</summary>
    internal const string DocsUrl = DocsBase + "llms.txt";

    /// <summary>The whole section, opening marker through closing marker, ending with a newline.</summary>
    internal static string Render(Carrier carrier, ProjectState state)
    {
        if (carrier is null || state is null)
        {
            throw new ArgumentException("a carrier and a project state are needed to render");
        }

        var output = new StringBuilder();
        output.Append(MarkedBlock.StartMarker).Append(' ').Append(carrier.Coordinate).Append(" -->\n");
        AppendIntroduction(output, state);
        AppendSkills(output, carrier);
        AppendCommands(output);
        AppendRules(output);
        output.Append(MarkedBlock.EndMarker).Append('\n');
        var rendered = output.ToString();
        Debug.Assert(MarkedBlock.ScanText(rendered).HasExactlyOneRegion, "the block must be one region");
        return rendered;
    }

    private static void AppendIntroduction(StringBuilder output, ProjectState state)
    {
        output.Append("## NarrativeTrace\n\n")
            .Append("NarrativeTrace turns this project's own method names, parameters and return")
            .Append(" values into a readable execution narrative — no log statements. Rendered")
            .Append(" traces land in `")
            .Append(state.OutputDirectory)
            .Append("`.\n\n");
    }

    private static void AppendSkills(StringBuilder output, Carrier carrier)
    {
        output.Append("### Agent skills installed in this project\n\n");
        foreach (var skill in carrier.Skills)
        {
            output.Append("- `").Append(skill.Name).Append("` — ").Append(skill.Description).Append('\n');
        }

        output.Append('\n');
    }

    private static void AppendCommands(StringBuilder output)
    {
        output.Append("### Commands\n\n")
            .Append("- `dotnet tool run dotnet-narrativetrace doctor` — diagnose this install;")
            .Append(" read-only, and every finding names the skill that fixes it\n")
            .Append("- `dotnet tool run dotnet-narrativetrace init --dry-run` — show what")
            .Append(" re-installing the skills would change, as a diff\n")
            .Append("- `dotnet tool run dotnet-narrativetrace uninstall` — remove exactly what the")
            .Append(" installer wrote, this section included\n\n")
            .Append("Documentation: ")
            .Append(DocsUrl)
            .Append("\n\n");
    }

    private static void AppendRules(StringBuilder output)
    {
        output.Append("### Rules\n\n")
            .Append("- Never disable redaction to make a trace easier to read.\n")
            .Append("- Commit `.approved.nt` files; never commit a `.received.nt`.\n\n");
    }
}
