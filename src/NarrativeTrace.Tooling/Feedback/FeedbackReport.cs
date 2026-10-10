// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Tooling.Feedback;

/// <summary>
/// One problem report, gathered: which runtime and install, which part of the product, which step,
/// the three sentences, the language it is written in, who drafted it, and at most two attachments.
/// </summary>
/// <remarks>
/// <para>
/// INTENT: the single object the gate, the draft, the issue-form URL and the <c>gh</c> line all read,
/// so none of them can disagree about what is being filed. Every runtime in the family gathers the
/// same fields under the same names — the issue form is shared, so the field set is a cross-runtime
/// contract, not a .NET shape.
/// </para>
/// <para>
/// <b>@llmNote</b> Built through <see cref="Builder"/> rather than positionally: eight components of
/// which six are strings is a call nobody can read at the point of use, and two of them swapped is a
/// report filed with the wrong step.
/// </para>
/// <para>
/// <b>@llmNote</b> <see cref="Fields"/> is what the gate reads, and it is deliberately EVERY field
/// that reaches a URL or a body file — including the install coordinate and the agent line, which look
/// harmless and are the two fields a wrapper script is most likely to interpolate something into.
/// </para>
/// </remarks>
public sealed class FeedbackReport
{
    private FeedbackReport(
        string runtime,
        FeedbackCategory category,
        string install,
        string step,
        ProblemNarrative narrative,
        string language,
        AgentIdentity agent,
        Attachments attachments)
    {
        RequireText(runtime, nameof(runtime));
        RequireText(install, nameof(install));
        RequireText(step, nameof(step));
        RequireText(language, nameof(language));
        RequireParts(category, narrative, agent, attachments);
        if (category.RequiresDoctorReport && !attachments.HasDoctorReport())
        {
            throw new ArgumentException(
                $"a \"{category.Id}\" report needs the doctor's JSON report — run the doctor, or file"
                + " this under prompt or library instead");
        }

        Runtime = runtime;
        Category = category;
        Install = install;
        Step = step;
        Narrative = narrative;
        Language = language;
        Agent = agent;
        Attachments = attachments;
    }

    /// <summary>The runtime this report is about — <c>dotnet</c> for this library.</summary>
    public string Runtime { get; }

    /// <summary>Which part of NarrativeTrace the report is about.</summary>
    public FeedbackCategory Category { get; }

    /// <summary>The install coordinate the project resolved.</summary>
    public string Install { get; }

    /// <summary>The skill step, prompt step or doctor check id the problem happened at.</summary>
    public string Step { get; }

    /// <summary>The three sentences.</summary>
    public ProblemNarrative Narrative { get; }

    /// <summary>The BCP-47 language the body is written in.</summary>
    public string Language { get; }

    /// <summary>Who drafted the report.</summary>
    public AgentIdentity Agent { get; }

    /// <summary>The doctor report and at most one structural trace.</summary>
    public Attachments Attachments { get; }

    /// <summary>
    /// Every field the gate inspects, in the order a refusal lists them. The insertion order is
    /// preserved, so the same report always refuses in the same words — which is what lets a test and
    /// a corpus row assert on them.
    /// </summary>
    public IReadOnlyDictionary<string, string> Fields() => new Dictionary<string, string>
    {
        ["install"] = Install,
        ["step"] = Step,
        ["did"] = Narrative.Did,
        ["happened"] = Narrative.Happened,
        ["expected"] = Narrative.Expected,
        ["agent"] = Agent.Describe(),
        [ValueFreeCheck.DoctorReportField] = Attachments.DoctorReport,
        ["trace"] = Attachments.StructuralTrace,
    };

    /// <summary>
    /// True when this report is in a state the rest of the verb may rely on: no blank mandatory text;
    /// the attachment set's own exclusivity holds; the category's doctor-report rule holds; the
    /// runtime is canonical (lower case, no spaces) because it becomes a <c>runtime:</c> label.
    /// </summary>
    internal bool Invariant() =>
        !string.IsNullOrWhiteSpace(Runtime)
        && Runtime.Equals(Runtime.ToLowerInvariant(), StringComparison.Ordinal)
        && !Runtime.Contains(' ')
        && !string.IsNullOrWhiteSpace(Install)
        && !string.IsNullOrWhiteSpace(Step)
        && !string.IsNullOrWhiteSpace(Language)
        && Attachments.HasDoctorReport() == string.IsNullOrWhiteSpace(Attachments.DoctorUnavailable)
        && (!Category.RequiresDoctorReport || Attachments.HasDoctorReport());

    private static void RequireText(string value, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"a report's \"{field}\" must not be blank", field);
        }
    }

    private static void RequireParts(
        FeedbackCategory category, ProblemNarrative narrative, AgentIdentity agent, Attachments attachments)
    {
        if (category is null || narrative is null || agent is null || attachments is null)
        {
            throw new ArgumentException(
                "a report needs a category, a narrative, an agent identity and an attachment set");
        }
    }

    /// <summary>Starts a report from the default values.</summary>
    public static Builder NewBuilder() => new();

    /// <summary>Starts a copy of this report, for a single-field override.</summary>
    public Builder ToBuilder() => new Builder()
        .Runtime(Runtime)
        .Category(Category)
        .Install(Install)
        .Step(Step)
        .Narrative(Narrative)
        .Language(Language)
        .Agent(Agent)
        .Attachments(Attachments);

    /// <summary>Builds a <see cref="FeedbackReport"/> one named field at a time.</summary>
    public sealed class Builder
    {
        private string _runtime = PublicRepository.Runtime;
        private FeedbackCategory? _category;
        private string _install = "";
        private string _step = "";
        private ProblemNarrative? _narrative;
        private string _language = "en";
        private AgentIdentity _agent = AgentIdentity.Unknown();
        private Attachments? _attachments;

        /// <summary>Sets the runtime.</summary>
        public Builder Runtime(string value)
        {
            _runtime = value;
            return this;
        }

        /// <summary>Sets the category.</summary>
        public Builder Category(FeedbackCategory value)
        {
            _category = value;
            return this;
        }

        /// <summary>Sets the install coordinate.</summary>
        public Builder Install(string value)
        {
            _install = value;
            return this;
        }

        /// <summary>Sets the step.</summary>
        public Builder Step(string value)
        {
            _step = value;
            return this;
        }

        /// <summary>Sets the narrative.</summary>
        public Builder Narrative(ProblemNarrative value)
        {
            _narrative = value;
            return this;
        }

        /// <summary>Sets the language.</summary>
        public Builder Language(string value)
        {
            _language = value;
            return this;
        }

        /// <summary>Sets the agent identity.</summary>
        public Builder Agent(AgentIdentity value)
        {
            _agent = value;
            return this;
        }

        /// <summary>Sets the attachment set.</summary>
        public Builder Attachments(Attachments value)
        {
            _attachments = value;
            return this;
        }

        /// <summary>Builds the report, validating every field.</summary>
        public FeedbackReport Build() => new(
            _runtime,
            _category ?? throw new ArgumentException("a report needs a category"),
            _install,
            _step,
            _narrative ?? throw new ArgumentException("a report needs a narrative"),
            _language,
            _agent,
            _attachments ?? throw new ArgumentException("a report needs an attachment set"));
    }
}
