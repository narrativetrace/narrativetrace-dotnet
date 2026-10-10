// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Diagnostics;
using System.Text;

namespace NarrativeTrace.Tooling.Feedback;

/// <summary>
/// The pre-filled issue-form URL — the default way a report is filed, and the only one that needs no
/// tool installed and no credential held by anybody but the person clicking it.
/// </summary>
/// <remarks>
/// <para>
/// INTENT: the user opens this in their own browser, where they are already signed in, and submits it
/// themselves. That click is the approval and the authentication at once, which is why there is no
/// endpoint, no token and nothing for an agent to hold. The agent prints the URL; it never opens it.
/// </para>
/// <para>
/// <b>@llmNote</b> The BODY is deliberately not in the URL. A doctor report and a structural trace are
/// kilobytes, a URL that is too long answers 414, and the limit is undocumented — so the long fields
/// live in the body file and the user pastes them. What travels in the URL is the short, searchable
/// half: the category, the install coordinate, the step, the language and the agent line.
/// </para>
/// <para>
/// <b>@llmNote</b> <c>labels</c> only takes effect for someone with push access ("labels are silently
/// dropped otherwise"), so for an outside reporter the parameter is harmless and inert; it is here
/// because the same label set is what the <c>gh</c> path applies, and two label lists that could
/// disagree would be two triage queues.
/// </para>
/// </remarks>
public static class IssueFormUrl
{
    /// <summary>
    /// The length this URL is kept under. GitHub answers <c>414 URI Too Long</c> past an undocumented
    /// limit, so the budget is a conservative one we set ourselves and measure, rather than one we
    /// discover from a user's failure. <see cref="StaysUnderBudget"/> is the postcondition.
    /// </summary>
    public const int MaxLength = 4_000;

    /// <summary>What a field that did not fit ends with, so a reader knows the rest is in the body file.</summary>
    public const string TruncationMarker = " … (continued in the pasted body)";

    private static readonly string Base = "https://github.com/" + PublicRepository.Slug + "/issues/new";

    /// <summary>Per-field ceiling before the whole-URL budget is enforced.</summary>
    private const int FieldLimit = 400;

    /// <summary>
    /// The URL for this report.
    /// </summary>
    /// <exception cref="ArgumentException">when the report is not a .NET report.</exception>
    public static string Of(FeedbackReport report)
    {
        PublicRepository.RequireDotNetRuntime(report);
        var url = Build(report, FieldLimit);
        for (var limit = FieldLimit; url.Length > MaxLength && limit > 16; limit /= 2)
        {
            url = Build(report, limit);
        }

        Debug.Assert(StaysUnderBudget(url), "the issue-form URL must stay inside its own budget");
        return url;
    }

    /// <summary>Whether a built URL is inside the budget — the postcondition <see cref="Of"/> asserts.</summary>
    internal static bool StaysUnderBudget(string url) => url.Length <= MaxLength;

    private static string Build(FeedbackReport report, int fieldLimit)
    {
        var parameters = new (string Key, string Value)[]
        {
            ("template", PublicRepository.Form),
            ("title", Clip(PublicRepository.TitleFor(report), fieldLimit)),
            ("labels", string.Join(",", PublicRepository.LabelsFor(report))),
            ("runtime", Clip(report.Runtime, fieldLimit)),
            ("category", report.Category.Id),
            ("install", Clip(report.Install, fieldLimit)),
            ("step", Clip(report.Step, fieldLimit)),
            ("language", Clip(report.Language, fieldLimit)),
            ("agent", Clip(report.Agent.Describe(), fieldLimit)),
        };
        return Base + "?" + Query(parameters);
    }

    private static string Query(IReadOnlyList<(string Key, string Value)> parameters)
    {
        var builder = new StringBuilder();
        foreach (var (key, value) in parameters)
        {
            if (builder.Length > 0)
            {
                builder.Append('&');
            }

            builder.Append(key).Append('=').Append(Encode(value));
        }

        return builder.ToString();
    }

    /// <summary>
    /// Percent-encoding, with the space written <c>%20</c> rather than <c>+</c>.
    /// <c>Uri.EscapeDataString</c> already writes <c>%20</c>, which is what a browser hands GitHub's
    /// prefill reader expects — an HTML-form <c>+</c> would show as a literal plus.
    /// </summary>
    private static string Encode(string value) => Uri.EscapeDataString(value);

    private static string Clip(string value, int limit) =>
        value.Length <= limit ? value : value[..limit] + TruncationMarker;
}
