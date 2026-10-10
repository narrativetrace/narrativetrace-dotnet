// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Tooling.Feedback;

/// <summary>
/// Where a .NET problem report is filed, and the one form it is filed through.
/// </summary>
/// <remarks>
/// <para>
/// INTENT: one place, so the issue-form URL and the <c>gh</c> line cannot name different repositories
/// or different templates. Every runtime in the family has its own repository and the SAME form file
/// name — the form is a cross-runtime artifact, the repository is not.
/// </para>
/// <para>
/// <b>@llmNote</b> <see cref="RequireDotNetRuntime"/> exists because a wrong answer here is unfixable
/// in public: a report filed into this tracker from another runtime's project is a public issue in
/// the wrong repository, and deleting it does not un-publish it. The report's own runtime field is
/// checked rather than assumed.
/// </para>
/// </remarks>
public static class PublicRepository
{
    /// <summary>The public .NET repository, <c>owner/name</c>.</summary>
    public const string Slug = "narrativetrace/narrativetrace-dotnet";

    /// <summary>The issue form every runtime's repository carries under <c>.github/ISSUE_TEMPLATE/</c>.</summary>
    public const string Form = "narrativetrace-report.yml";

    /// <summary>The runtime this library files for.</summary>
    public const string Runtime = "dotnet";

    /// <summary>
    /// The longest a GitHub label name may be. A label built past this is one GitHub will refuse, so
    /// building it is never the right answer — and an unbounded label is also how a 10,000-character
    /// language tag got into a URL that then blew its own length budget.
    /// </summary>
    private const int LabelLimit = 50;

    /// <summary>
    /// Ensures the report belongs to this runtime's tracker.
    /// </summary>
    /// <exception cref="ArgumentException">when the report is not a .NET report.</exception>
    internal static void RequireDotNetRuntime(FeedbackReport report)
    {
        if (!Runtime.Equals(report.Runtime, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"this library files into {Slug}, and the report's runtime is \"{report.Runtime}\""
                + " — file it through that runtime's own tooling");
        }
    }

    /// <summary>
    /// The labels triage sorts on, in a stable order, each inside the platform's own length limit.
    /// </summary>
    /// <remarks>
    /// <b>@llmNote</b> No <c>agent:&lt;product&gt;</c> label, deliberately. The agent product is free
    /// text as the agent reported it, so a label built from it is a label set strangers extend — and
    /// labels from a reporter without push access are silently dropped anyway. The agent line travels
    /// as a FIELD, where it is searchable and harmless.
    /// </remarks>
    internal static IReadOnlyList<string> LabelsFor(FeedbackReport report) =>
    [
        ClipLabel("from-agent"),
        ClipLabel("runtime:" + report.Runtime),
        ClipLabel("category:" + report.Category.Id),
        ClipLabel("lang:" + report.Language),
    ];

    /// <summary>The issue title: the category, then the step it happened at.</summary>
    internal static string TitleFor(FeedbackReport report) => $"{report.Category.Id}: {report.Step}";

    private static string ClipLabel(string label) =>
        label.Length <= LabelLimit ? label : label[..LabelLimit];
}
