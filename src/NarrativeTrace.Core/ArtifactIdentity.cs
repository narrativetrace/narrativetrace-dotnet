// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Core;

/// <summary>
/// Which artifact one traced test invocation owns.
/// </summary>
/// <remarks>
/// <para>
/// INTENT: A test method used to be the whole answer to "which file does this
/// trace go in", which is wrong the moment the method runs more than once — a
/// parameterized or repeated test. Every invocation then wrote the same path
/// and the last one won, so earlier evidence was unreachable through the
/// advertised files and one invocation's <c>.approved.nt</c> baseline judged
/// another's structure. This record is the identity every per-test artifact
/// keys by: the trace, the JSON export, the diagram, the structural artifact,
/// and the committed approval baseline beside them.
/// </para>
/// <para>
/// <b>Cross-runtime contract</b> — this naming scheme is shared byte for
/// byte across every NarrativeTrace runtime:
/// </para>
/// <list type="bullet">
/// <item>An ordinary test method keeps its bare method slug —
/// <c>CustomerPlacesOrder</c> → <c>customer_places_order</c>. Nothing that
/// exists today moves.</item>
/// <item>An invocation appends <c>-&lt;index&gt;-&lt;label&gt;</c>: the
/// 1-based invocation index zero-padded to three digits, then the
/// invocation's display name through the same slug rule, with runs of
/// <c>_</c> collapsed and the ends trimmed — <c>equipment_can_be_found-001-find_kayak</c>.
/// The label is dropped when it slugs to nothing.</item>
/// <item><c>-</c> is the separator precisely because the slug alphabet is
/// <c>[a-z0-9_]</c> and can never produce one: an ordinary method can never
/// collide with an invocation artifact, and the name parses back into its
/// three parts.</item>
/// <item>Two invocations of one method always differ in the index, so
/// display names that differ only in characters a path cannot carry still
/// land on different files. The index is what makes the scheme
/// collision-proof; the label is what makes it readable.</item>
/// <item>Nothing here varies per process or per run: the index comes from
/// the test framework's own invocation order and the label from the display
/// name, so an approval baseline recorded on one machine matches the
/// artifact written on the next.</item>
/// </list>
/// </remarks>
public sealed record ArtifactIdentity
{
    /// <summary>The test class, qualified or simple; never trusted to be either.</summary>
    public string TestClassName { get; }

    /// <summary>The test method's own name, shared by every invocation of it.</summary>
    public string MethodName { get; }

    /// <summary>1-based invocation number, or <c>0</c> for a method that runs once.</summary>
    public int InvocationIndex { get; }

    /// <summary>The invocation's display name, <c>""</c> when there is none.</summary>
    public string InvocationLabel { get; }

    /// <summary>Rejects what cannot name a file; normalizes an absent label to the empty string.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="testClassName"/> or <paramref name="methodName"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="invocationIndex"/> is negative.</exception>
    public ArtifactIdentity(
        string testClassName, string methodName, int invocationIndex, string? invocationLabel)
    {
        if (testClassName is null)
        {
            throw new ArgumentNullException(nameof(testClassName));
        }

        if (methodName is null)
        {
            throw new ArgumentNullException(nameof(methodName));
        }

        if (invocationIndex < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(invocationIndex), invocationIndex, "invocationIndex must not be negative");
        }

        TestClassName = testClassName;
        MethodName = methodName;
        InvocationIndex = invocationIndex;
        InvocationLabel = invocationLabel ?? string.Empty;
    }

    /// <summary>The identity of a test method that runs exactly once — the artifact name it has always had.</summary>
    public static ArtifactIdentity OfMethod(string testClassName, string methodName)
    {
        return new ArtifactIdentity(testClassName, methodName, 0, string.Empty);
    }

    /// <summary>The identity of one invocation of a test method that runs more than once.</summary>
    /// <param name="testClassName">The test class.</param>
    /// <param name="methodName">The test method.</param>
    /// <param name="invocationIndex">1-based, in the framework's invocation order.</param>
    /// <param name="invocationLabel">The invocation's display name, used only for readability.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="invocationIndex"/> is less than 1.</exception>
    public static ArtifactIdentity OfInvocation(
        string testClassName, string methodName, int invocationIndex, string? invocationLabel)
    {
        if (invocationIndex < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(invocationIndex), invocationIndex, "invocationIndex is 1-based");
        }

        return new ArtifactIdentity(testClassName, methodName, invocationIndex, invocationLabel);
    }

    /// <summary>Whether this identity names one invocation of a repeated method rather than a whole method.</summary>
    public bool IsInvocation => InvocationIndex > 0;

    /// <summary>The artifact base name, without any format suffix: the scheme documented on this record.</summary>
    public string FileSlug()
    {
        return OutputDirectoryResolver.ToFileSlug(MethodName, InvocationIndex, InvocationLabel);
    }

    /// <summary>
    /// The scenario title the value-free artifacts carry — a title no runtime value can reach.
    /// </summary>
    /// <remarks>
    /// <para>
    /// INTENT: The structural <c>.nt</c> artifact promises names, call
    /// hierarchy and outcome kinds and nothing else, and its header used to be
    /// the display name. A parameterized test's display name can interpolate
    /// <em>arguments</em> into itself, so one invocation's value-free artifact
    /// would open with a runtime value — in the one artifact whose whole point
    /// is carrying none. An invocation is therefore titled by what the
    /// developer wrote (the method) and by which run it was (the index),
    /// never by what it ran with.
    /// </para>
    /// <para>
    /// <b>Cross-runtime contract</b>, and the rule is exactly two lines:
    /// </para>
    /// <list type="bullet">
    /// <item>An invocation is <c>&lt;humanized method&gt; #&lt;index&gt;</c> —
    /// <c>Equipment can be found #2</c>.</item>
    /// <item>A method that runs once keeps its display name, so every
    /// committed baseline stays byte-identical. When the caller has no
    /// display name of its own — it passed the method name — a label a test
    /// runner appended to that method name (<c>EquipmentCanBeFound[KAYAK]</c>)
    /// is dropped, since a .NET method name can never contain a bracket and
    /// the text after one is the runner's, not the developer's.</item>
    /// </list>
    /// <para>
    /// The <em>file</em> name is a separate question and deliberately keeps
    /// the label: it is what tells two invocations apart on disk. See the
    /// record's own naming rule above.
    /// </para>
    /// </remarks>
    /// <param name="displayName">The runner's display name for this test; <see langword="null"/> means there is none.</param>
    public string StructuralScenario(string? displayName)
    {
        if (IsInvocation)
        {
            return ScenarioFramer.Humanize(BareMethodName()) + " #" + InvocationIndex;
        }

        return ScenarioFramer.Humanize(
            displayName is null || displayName == MethodName ? BareMethodName() : displayName);
    }

    /// <summary>
    /// The method's own name, without a label a test runner appended to it: a
    /// runner spells one invocation <c>EquipmentCanBeFound[KAYAK]</c>, and a
    /// .NET method name can never contain a bracket, so everything from the
    /// first one belongs to the runner.
    /// </summary>
    private string BareMethodName()
    {
        var bracket = MethodName.IndexOf('[');
        return bracket < 0 ? MethodName : MethodName[..bracket];
    }
}
