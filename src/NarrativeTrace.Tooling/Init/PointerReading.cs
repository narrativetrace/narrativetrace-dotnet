// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Tooling.Platforms;

namespace NarrativeTrace.Tooling.Init;

/// <summary>
/// What one detected platform's pointer file looks like in a project — the installer's own reading,
/// handed to the doctor so both agree on what "present" means.
/// </summary>
/// <remarks>
/// INTENT: the doctor must not scan marker text itself (<see cref="MarkedBlock"/> is the one place
/// that decides what counts as a marker), so the installer's namespace turns a
/// <see cref="ProjectState"/> into these five-field facts and the doctor only judges them.
/// </remarks>
/// <param name="PlatformId">The platform-table row id.</param>
/// <param name="PlatformName">The platform as a reader names it.</param>
/// <param name="Path">The pointer file, project-relative.</param>
/// <param name="Status">What is at that path.</param>
/// <param name="Coordinate">
/// The carrier coordinate on the section's opening marker; empty when the status is not
/// <see cref="PointerStatus.Section"/> or the section carries no stamp.
/// </param>
public sealed record PointerReading(
    string PlatformId, string PlatformName, string Path, PointerReading.PointerStatus Status, string Coordinate)
{
    /// <summary>What is at a pointer's path.</summary>
    public enum PointerStatus
    {
        /// <summary>Nothing is there.</summary>
        Absent,

        /// <summary>The path, or a directory above it, is a symbolic link; nothing is read or written.</summary>
        Linked,

        /// <summary>A file is there, but it does not carry exactly one NarrativeTrace section.</summary>
        Foreign,

        /// <summary>A file with exactly one NarrativeTrace section, stamped or not.</summary>
        Section,
    }

    /// <summary>
    /// One reading per DETECTED platform whose row has a pointer, in platform-table order. A platform
    /// the project is not on has nothing to be missing, and a row with no pointer has nothing to read.
    /// </summary>
    /// <exception cref="ArgumentException">The state is null.</exception>
    public static IReadOnlyList<PointerReading> ReadAll(ProjectState state)
    {
        if (state is null)
        {
            throw new ArgumentException("reading pointers needs a project state");
        }

        return PlatformTable.Rows
            .Where(row => row.Pointer.Path.Length > 0 && state.DetectedPlatforms.Contains(row.Id))
            .Select(row => Read(row, state))
            .ToList();
    }

    private static PointerReading Read(PlatformRow row, ProjectState state)
    {
        var path = row.Pointer.Path;
        if (state.PointerBehindLink(path))
        {
            return new PointerReading(row.Id, row.Name, path, PointerStatus.Linked, "");
        }

        if (state.PointerFile(path) is not { } text)
        {
            return new PointerReading(row.Id, row.Name, path, PointerStatus.Absent, "");
        }

        var scan = MarkedBlock.ScanText(text);
        return scan.HasExactlyOneRegion
            ? new PointerReading(row.Id, row.Name, path, PointerStatus.Section, scan.Regions[0].Coordinate)
            : new PointerReading(row.Id, row.Name, path, PointerStatus.Foreign, "");
    }
}
