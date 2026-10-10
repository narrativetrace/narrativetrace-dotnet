// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Tooling.Frameworks;

namespace NarrativeTrace.Tooling.Doctor.Checks;

/// <summary>
/// One framework-table row, observed from manifest and source: the framework is present but its
/// NarrativeTrace package is not referenced, or the package is in use but its wiring is never (or only
/// partly) applied. Generalises the ASP.NET Core registration pair — "AddNarrativeTrace without
/// UseNarrativeTrace" — to every row whose wiring is source-level; that row keeps its
/// <c>config.registration-pair</c> id.
/// </summary>
/// <remarks>
/// <para>INTENT: the <c>add-narrative-tracing</c> skill lists no framework; its one framework step runs
/// the doctor and applies every framework fix in order. So every failing fix here is complete on its
/// own: the add command (pinned to the project's family version when known), then the row's wiring
/// snippet verbatim — the compiled fixture's text from <see cref="WiringSnippets"/>.</para>
/// <para><b>@llmNote</b> Text and manifest only, never a build. Fully applied wiring passes whatever
/// the manifest says (source that compiles is the strongest evidence there is); any one evidence
/// pattern found counts as "in use" even where no manifest is visible, which is how the old
/// registration-pair check failed Add-without-Use. A project that neither uses the framework nor the
/// package passes — a check fails a project for what it got wrong, never for what it does not use.</para>
/// </remarks>
public sealed class FrameworkWiringCheck
{
    private readonly FrameworkRow _row;
    private readonly Wiring.Snippet _wiring;
    private readonly string _id;

    /// <summary>Builds the check for one row.</summary>
    /// <exception cref="ArgumentException">The row earns no wiring check of its own.</exception>
    public FrameworkWiringCheck(FrameworkRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        if (row.Check is not CheckBinding.WiringCheck check || row.Wiring is not Wiring.Snippet snippet)
        {
            throw new ArgumentException($"row {row.Id} has no source-level wiring check", nameof(row));
        }

        _row = row;
        _wiring = snippet;
        _id = check.Id;
    }

    /// <summary>Runs the check.</summary>
    public DoctorFinding Run(DoctorSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var sources = snapshot.SourceTexts();
        if (_wiring.AppliedIn(sources))
        {
            return Pass($"{_row.Package.PackageId} is wired: {_wiring.Description}");
        }

        if (_wiring.Evidence.Any(pattern => sources.Any(pattern.IsMatch)))
        {
            return Incomplete("is only partly applied");
        }

        var manifest = snapshot.ManifestText();
        if (_row.Package.ReferencedIn(manifest))
        {
            return Incomplete("is never applied");
        }

        return FrameworkTable.Detected(_row, manifest)
            ? NotReferenced(snapshot.ProjectResolvedFamilyVersion)
            : Pass($"{_row.Name} not detected — nothing to wire");
    }

    private DoctorFinding Incomplete(string state) => DoctorFinding.Fail(
        _id,
        $"{_row.Package.PackageId} is in use but its wiring ({_wiring.Description}) {state} — nothing in it is traced",
        $"Apply the wiring: {WiringFix()}",
        DoctorDocUrls.ForPage(_row.Guide));

    private DoctorFinding NotReferenced(string? familyVersion) => DoctorFinding.Fail(
        _id,
        $"{_row.Name} detected ({_row.Marker.Description}) but {_row.Package.PackageId} is not referenced " +
        "— nothing in it is traced",
        $"Add {_row.Package.PackageId}: `{_row.Package.AddCommand(familyVersion)}`. Then {WiringFix()}",
        DoctorDocUrls.ForPage(_row.Guide));

    private DoctorFinding Pass(string message) => DoctorFinding.Pass(_id, message, DoctorDocUrls.ForPage(_row.Guide));

    private string WiringFix() =>
        $"add {_wiring.Description}, adapted to this project's own names (from {_wiring.Fixture}):\n" +
        WiringSnippets.Text(_row.Id);
}
