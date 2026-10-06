// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Tooling.Init;

/// <summary>
/// One thing an install or an uninstall would do to one path.
/// </summary>
/// <remarks>
/// <para>INTENT: the unit a plan is made of, and the reason a plan can be printed as a diff before
/// anything happens. Every action that touches a file carries the file's WHOLE text before and
/// after, so the executor needs no knowledge of the project at all and the renderer can diff without
/// reading the disk.</para>
/// <para>Paths are project-relative and normalized — an absolute path, or one that climbs out with
/// <c>..</c>, is refused at construction. That is the only thing standing between a hand-written plan
/// and a write outside the project.</para>
/// <para>Closed record hierarchy: a renderer or an executor switches over the concrete kinds, and a
/// new kind of action is a change everywhere it matters rather than a silent no-op.</para>
/// </remarks>
public abstract record Action
{
    private protected Action(string path) => Path = Actions.RequireProjectRelative(path);

    /// <summary>The project-relative path this action is about (forward-slash separated).</summary>
    public string Path { get; }

    /// <summary>A stable token naming the kind, for text and JSON rendering.</summary>
    public abstract string Kind { get; }
}

/// <summary>
/// An action that leaves one file with a known text. <see cref="Before"/> is <c>""</c> for a file
/// that does not exist yet, <see cref="After"/> is <c>""</c> for one that is being deleted.
/// </summary>
public abstract record FileEdit : Action
{
    private protected FileEdit(string path)
        : base(path)
    {
    }

    /// <summary>The file's whole text before the action; <c>""</c> when it does not exist.</summary>
    public abstract string Before { get; }

    /// <summary>The file's whole text after the action; <c>""</c> when it is deleted.</summary>
    public abstract string After { get; }
}

/// <summary>Writes a file that is not there yet.</summary>
public sealed record CreateFile : FileEdit
{
    /// <summary>A create action for <paramref name="path"/> with the whole <paramref name="content"/>.</summary>
    public CreateFile(string path, string content)
        : base(path)
    {
        Actions.RequireText(content);
        Content = content;
    }

    /// <summary>The whole content the new file is created with.</summary>
    public string Content { get; }

    /// <inheritdoc/>
    public override string Before => "";

    /// <inheritdoc/>
    public override string After => Content;

    /// <inheritdoc/>
    public override string Kind => "create";
}

/// <summary>
/// Rewrites a file whose NarrativeTrace-owned region changed — the managed block between the
/// markers, or, for a copied <c>SKILL.md</c>, the whole page.
/// </summary>
public sealed record ReplaceBlock : FileEdit
{
    /// <summary>A replace action carrying the file's whole text <paramref name="before"/> and <paramref name="after"/>.</summary>
    public ReplaceBlock(string path, string before, string after)
        : base(path)
    {
        Actions.RequireText(before);
        Actions.RequireText(after);
        Before = before;
        After = after;
    }

    /// <inheritdoc/>
    public override string Before { get; }

    /// <inheritdoc/>
    public override string After { get; }

    /// <inheritdoc/>
    public override string Kind => "replace";
}

/// <summary>Adds the managed block to the end of an existing file, after one blank line.</summary>
public sealed record AppendBlock : FileEdit
{
    /// <summary>An append action adding <paramref name="block"/> to the current <paramref name="before"/> text.</summary>
    public AppendBlock(string path, string before, string block)
        : base(path)
    {
        Actions.RequireText(before);
        Actions.RequireText(block);
        Before = before;
        Block = block;
    }

    /// <inheritdoc/>
    public override string Before { get; }

    /// <summary>The managed block appended to <see cref="Before"/>.</summary>
    public string Block { get; }

    /// <inheritdoc/>
    public override string After => MarkedBlock.Append(Before, Block);

    /// <inheritdoc/>
    public override string Kind => "append";
}

/// <summary>Adds one line to the end of an existing file, after one blank line.</summary>
public sealed record AppendLine : FileEdit
{
    /// <summary>An append action adding one <paramref name="line"/> to the current <paramref name="before"/> text.</summary>
    public AppendLine(string path, string before, string line)
        : base(path)
    {
        Actions.RequireText(before);
        Actions.RequireText(line);
        Before = before;
        NewLine = line;
    }

    /// <inheritdoc/>
    public override string Before { get; }

    /// <summary>The single line appended, without its terminator.</summary>
    public string NewLine { get; }

    /// <inheritdoc/>
    public override string After => MarkedBlock.Append(Before, NewLine + MarkedBlock.EolOf(Before));

    /// <inheritdoc/>
    public override string Kind => "append-line";
}

/// <summary>Removes a file the installer wrote.</summary>
public sealed record DeleteFile : FileEdit
{
    /// <summary>A delete action for the file whose current text is <paramref name="before"/>.</summary>
    public DeleteFile(string path, string before)
        : base(path)
    {
        Actions.RequireText(before);
        Before = before;
    }

    /// <inheritdoc/>
    public override string Before { get; }

    /// <inheritdoc/>
    public override string After => "";

    /// <inheritdoc/>
    public override string Kind => "delete";
}

/// <summary>
/// Removes a directory the installer created, once its files are gone. Never recursive: a directory
/// that still holds somebody else's file is left alone and reported, because deleting it would take
/// that file with it.
/// </summary>
public sealed record DeleteDirectory : Action
{
    /// <summary>A non-recursive delete of the directory at <paramref name="path"/>.</summary>
    public DeleteDirectory(string path)
        : base(path)
    {
    }

    /// <inheritdoc/>
    public override string Kind => "delete-directory";
}

/// <summary>
/// Something the installer will NOT do, and why. A refusal is reported, never thrown: the rest of
/// the plan proceeds, and the run exits 1 so a script notices.
/// </summary>
public sealed record Refuse : Action
{
    /// <summary>A refusal for <paramref name="path"/>, carrying the <paramref name="reason"/> it refused.</summary>
    public Refuse(string path, string reason)
        : base(path)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("a refusal must carry a reason naming what it refused");
        }

        Reason = reason;
    }

    /// <summary>Why the installer refused, naming what it refused.</summary>
    public string Reason { get; }

    /// <inheritdoc/>
    public override string Kind => "refuse";
}
