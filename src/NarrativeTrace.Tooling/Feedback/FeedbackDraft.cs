// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Text;

namespace NarrativeTrace.Tooling.Feedback;

/// <summary>
/// What drafting produced: either a report the gate cleared, with the two texts rendered from it, or
/// a refusal naming every rule that stood in the way.
/// </summary>
/// <remarks>
/// INTENT: a closed pair rather than a draft carrying a list of problems, so no caller can print a
/// URL or write a body file while a violation stands. A <see cref="Refused"/> has no body to file, by
/// construction — the type system is what enforces the gate's ordering here.
/// </remarks>
public abstract record FeedbackDraft
{
    private FeedbackDraft()
    {
    }

    /// <summary>
    /// The gate refused. Nothing was rendered and nothing may be written.
    /// </summary>
    public sealed record Refused : FeedbackDraft
    {
        /// <summary>The gate refused, naming every rule in the way.</summary>
        /// <param name="violations">Every rule that refused, field order then rule order; never empty.</param>
        public Refused(IReadOnlyList<ValueFreeViolation> violations)
        {
            ArgumentNullException.ThrowIfNull(violations);
            if (violations.Count == 0)
            {
                throw new ArgumentException(
                    "a refusal names what refused it — an empty violation list is a Drafted", nameof(violations));
            }

            Violations = [.. violations];
        }

        /// <summary>Every rule that refused, field order then rule order; never empty.</summary>
        public IReadOnlyList<ValueFreeViolation> Violations { get; }

        /// <summary>The refusal, one line per violation, in the words the verb prints before exiting 2.</summary>
        public string Describe()
        {
            var builder = new StringBuilder();
            builder.Append("This report cannot be filed. ").Append(Violations.Count).Append(" rule(s) refused it:\n");
            foreach (var violation in Violations)
            {
                builder.Append("  - ").Append(violation.Describe()).Append('\n');
            }

            return builder.ToString();
        }
    }

    /// <summary>
    /// The gate cleared the report.
    /// </summary>
    public sealed record Drafted : FeedbackDraft
    {
        /// <summary>The gate cleared the report; the two texts were rendered from it.</summary>
        /// <param name="report">The report AFTER home-path rewriting.</param>
        /// <param name="draft">The whole report plus the privacy note and the two choices: what the agent SHOWS.</param>
        /// <param name="body">The report alone, in Markdown: what gets filed, with nothing about the process.</param>
        public Drafted(FeedbackReport report, string draft, string body)
        {
            ArgumentNullException.ThrowIfNull(report);
            ArgumentNullException.ThrowIfNull(draft);
            ArgumentNullException.ThrowIfNull(body);
            if (!draft.Contains(body, StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "the draft must contain the body verbatim — a person who approves the draft is approving"
                    + " what gets filed, byte for byte");
            }

            Report = report;
            Draft = draft;
            Body = body;
        }

        /// <summary>The report after home-path rewriting — what the two texts were rendered from.</summary>
        public FeedbackReport Report { get; }

        /// <summary>The whole report plus the privacy note: what the agent shows.</summary>
        public string Draft { get; }

        /// <summary>The report alone, in Markdown: what gets filed.</summary>
        public string Body { get; }
    }
}
