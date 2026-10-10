// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Tooling.Doctor;

namespace NarrativeTrace.Tooling.Feedback;

/// <summary>
/// What the verb can learn about a project without being told: which NarrativeTrace it has installed,
/// and which structural trace is safe to attach.
/// </summary>
/// <remarks>
/// <para>
/// INTENT: the two facts triage needs most are the two a reporter is least likely to get right by
/// hand. The install coordinate decides whether a defect is already fixed; the structural trace shows
/// the shape of the call that misbehaved. Both are read from the doctor's own snapshot, so the report
/// and the doctor cannot disagree about the project they are describing.
/// </para>
/// <para>
/// <b>@llmNote</b> A trace is chosen only if it is BOTH a structural trace by grammar and clean by
/// every value-free rule, and when none is, the choice says why rather than attaching nothing
/// silently. A report that quietly lost its attachment is a report whose author thinks they filed
/// more than they did.
/// </para>
/// <para>
/// <b>@sideEffects</b> None: a pure function over a snapshot. Reading the doctor's JSON from disk is
/// the entry point's job, and it is passed in here.
/// </para>
/// </remarks>
public static class FeedbackGatherer
{
    /// <summary>What the install field says when the build declares none of our coordinates.</summary>
    public const string InstallUnknown = "not declared in this project's build file";

    /// <summary>Where the doctor leaves the report this verb attaches, relative to the project.</summary>
    public const string DoctorReportPath = "TestResults/narrativetrace/doctor-report.json";

    private const string CoordinatePrefix = "NarrativeTrace.";

    /// <summary>
    /// Which structural trace was chosen, or why none was.
    /// </summary>
    /// <param name="Content">The trace's content, or <c>""</c> when none was chosen.</param>
    /// <param name="Reason">Why nothing was chosen, or <c>""</c> when something was.</param>
    public sealed record TraceChoice(string Content, string Reason)
    {
        /// <summary>The trace's content, or <c>""</c> when none was chosen.</summary>
        public string Content { get; } = RequireExclusive(Content, Reason);

        /// <summary>Why nothing was chosen, or <c>""</c> when something was.</summary>
        public string Reason { get; } = Reason ?? throw new ArgumentException("a trace choice is content or a reason, never null");

        private static string RequireExclusive(string content, string reason)
        {
            if (content is null || reason is null)
            {
                throw new ArgumentException("a trace choice is content or a reason, never null");
            }

            if (string.IsNullOrWhiteSpace(content) == string.IsNullOrWhiteSpace(reason))
            {
                throw new ArgumentException(
                    "a choice attached a trace or says why it did not, never both and never neither");
            }

            return content;
        }
    }

    /// <summary>Every NarrativeTrace coordinate the build declares, or a stated unknown.</summary>
    public static string InstallCoordinate(DoctorSnapshot snapshot)
    {
        var ours = snapshot.InstalledPackages
            .Where(package => package.Key.StartsWith(CoordinatePrefix, StringComparison.Ordinal))
            .OrderBy(package => package.Key, StringComparer.Ordinal)
            .Select(package => $"{package.Key} {package.Value}")
            .ToList();
        return ours.Count == 0 ? InstallUnknown : string.Join(", ", ours);
    }

    /// <summary>
    /// The structural trace to attach.
    /// </summary>
    /// <param name="snapshot">The doctor's snapshot of the project.</param>
    /// <param name="preferredPath">A path suffix the user named, or <c>""</c> to let the verb choose.</param>
    public static TraceChoice ChooseTrace(DoctorSnapshot snapshot, string preferredPath)
    {
        var candidates = StructuralCandidates(snapshot);
        if (!string.IsNullOrWhiteSpace(preferredPath))
        {
            return Named(candidates, preferredPath);
        }

        var attachable = candidates.FirstOrDefault(candidate => IsAttachable(candidate.Value));
        return attachable.Key is null
            ? new TraceChoice("", RefusalFor(candidates))
            : new TraceChoice(attachable.Value, "");
    }

    /// <summary>The attachment set: the doctor's JSON when there is one, and at most one structural trace.</summary>
    public static Attachments Attachments(DoctorSnapshot snapshot, string doctorReportJson, string preferredTracePath)
    {
        var trace = ChooseTrace(snapshot, preferredTracePath).Content;
        if (string.IsNullOrWhiteSpace(doctorReportJson))
        {
            return Feedback.Attachments.WithoutDoctorReport(
                $"no report at {DoctorReportPath} — run the doctor first", trace);
        }

        return Feedback.Attachments.Of(doctorReportJson, trace);
    }

    /// <summary>Every <c>.nt</c> the snapshot saw, in path order so two runs choose the same one.</summary>
    private static SortedDictionary<string, string> StructuralCandidates(DoctorSnapshot snapshot)
    {
        var candidates = new SortedDictionary<string, string>(StringComparer.Ordinal);
        PutStructuralNames(candidates, snapshot.OutputFiles);
        PutStructuralNames(candidates, snapshot.ApprovedDirFiles);
        return candidates;
    }

    private static void PutStructuralNames(
        SortedDictionary<string, string> candidates, IReadOnlyDictionary<string, string> files)
    {
        foreach (var (path, content) in files.Where(file => file.Key.EndsWith(".nt", StringComparison.Ordinal)))
        {
            candidates[path] = content;
        }
    }

    private static TraceChoice Named(SortedDictionary<string, string> candidates, string preferredPath)
    {
        var match = candidates.FirstOrDefault(candidate => candidate.Key.EndsWith(preferredPath, StringComparison.Ordinal));
        if (match.Key is null)
        {
            return new TraceChoice("", $"{preferredPath} was not found under this project's output");
        }

        return IsAttachable(match.Value)
            ? new TraceChoice(match.Value, "")
            : new TraceChoice("", $"{preferredPath} {WhyNot(match.Value)}");
    }

    private static bool IsAttachable(string content) =>
        StructuralTrace.LooksStructural(content) && ValueFreeCheck.RulesRefusing(content).Count == 0;

    private static string RefusalFor(SortedDictionary<string, string> candidates)
    {
        if (candidates.Count == 0)
        {
            return "no structural trace was found under this project's output";
        }

        var first = candidates.First();
        return $"no attachable structural trace: {first.Key} {WhyNot(first.Value)}";
    }

    /// <summary>Why one candidate was not attachable, in the words the verb prints.</summary>
    private static string WhyNot(string content)
    {
        if (!StructuralTrace.LooksStructural(content))
        {
            return "is not a structural trace";
        }

        var rules = string.Join(", ", ValueFreeCheck.RulesRefusing(content).Select(rule => rule.Id));
        return "breaks " + (rules.Length == 0 ? "no rule" : rules);
    }
}
