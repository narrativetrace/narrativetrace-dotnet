// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Tooling.Platforms;

/// <summary>
/// What <c>init</c> writes so a platform's agent finds the skills before it happens to look — the
/// pointer column of the platform table.
/// </summary>
/// <remarks>
/// <para>INTENT: a pointer is never a copy of the skill pages (no third page flavour) and never a
/// symbolic link; it is one small file the platform already reads first. A platform that reads
/// <c>.agents/skills</c> and <c>AGENTS.md</c> on its own has <see cref="None"/>.</para>
/// <para>A closed hierarchy of records: the planners switch over it, so a new kind of pointer is a
/// compile-visible decision everywhere it matters.</para>
/// </remarks>
public abstract record Pointer
{
    private Pointer()
    {
    }

    /// <summary>The pointer file, project-relative with <c>/</c> separators; empty for <see cref="None"/>.</summary>
    public abstract string Path { get; }

    /// <summary>The platform needs no pointer: it reads <c>.agents/skills</c> and <c>AGENTS.md</c>.</summary>
    public sealed record None : Pointer
    {
        /// <inheritdoc />
        public override string Path => string.Empty;
    }

    /// <summary>
    /// The same managed section <c>AGENTS.md</c> carries, in a platform's own always-on memory file
    /// (Replit's <c>replit.md</c>) — same markers, same coordinate stamp.
    /// </summary>
    /// <param name="File">The memory file, project-relative.</param>
    public sealed record ManagedSection(string File) : Pointer
    {
        /// <inheritdoc />
        public override string Path { get; } = Relative(File);
    }

    /// <summary>
    /// One rule file of the platform's own format (a Cursor <c>.mdc</c> rule) carrying the pointer
    /// sentence inside a managed section.
    /// </summary>
    /// <param name="File">The rule file, project-relative.</param>
    /// <param name="Text">The byte-stable pointer sentence the rule carries.</param>
    public sealed record Rule(string File, string Text) : Pointer
    {
        /// <inheritdoc />
        public override string Path { get; } = Relative(File);

        /// <summary>The pointer sentence, validated: not blank.</summary>
        public string Text { get; init; } = string.IsNullOrWhiteSpace(Text)
            ? throw new ArgumentException("a rule pointer carries text", nameof(Text))
            : Text;
    }

    private static string Relative(string path) =>
        string.IsNullOrWhiteSpace(path) || path.StartsWith('/') || path.EndsWith('/') || path.Contains('\\')
            || path.Contains("..", StringComparison.Ordinal) || (path.Length > 1 && path[1] == ':')
            ? throw new ArgumentException($"a pointer path is project-relative and stays inside it, got {path}", nameof(path))
            : path;
}
