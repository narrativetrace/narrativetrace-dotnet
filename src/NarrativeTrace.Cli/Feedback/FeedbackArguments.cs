// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Tooling.Doctor;
using NarrativeTrace.Tooling.Feedback;

namespace NarrativeTrace.Cli.Feedback;

/// <summary>
/// What <c>narrativetrace feedback</c> was asked to do, parsed out of a command line and nothing
/// else.
/// </summary>
/// <remarks>
/// <para>
/// INTENT: every flag decision stays a pure function, so the verb itself is a handful of lines over
/// the tooling library. A bad flag is DATA (<see cref="Error"/>), never an exception, and the FIRST
/// problem is the one reported.
/// </para>
/// <para>
/// <b>@llmNote</b> The five report fields are MANDATORY and that is checked here rather than left to
/// the library's constructor, so a person who forgets one is told which flag they forgot instead of
/// reading a constructor's message about a blank field.
/// </para>
/// </remarks>
internal sealed class FeedbackArguments
{
    /// <summary>The three channels, in the order the skill offers them.</summary>
    internal static readonly IReadOnlyList<string> Channels = ["draft", "url", "gh"];

    private static readonly HashSet<string> ValueFlags = new(StringComparer.Ordinal)
    {
        "--category", "--step", "--did", "--happened", "--expected",
        "--language", "--agent-product", "--agent-model", "--trace",
    };

    private FeedbackArguments(
        string channel, string category, string step, string did, string happened, string expected,
        string language, string agentProduct, string agentModel, string trace, bool json, bool help, string? error)
    {
        Channel = channel;
        Category = category;
        Step = step;
        Did = did;
        Happened = happened;
        Expected = expected;
        Language = language;
        AgentProduct = agentProduct;
        AgentModel = agentModel;
        Trace = trace;
        Json = json;
        Help = help;
        Error = error;
    }

    internal string Channel { get; }

    internal string Category { get; }

    internal string Step { get; }

    internal string Did { get; }

    internal string Happened { get; }

    internal string Expected { get; }

    internal string Language { get; }

    internal string AgentProduct { get; }

    internal string AgentModel { get; }

    /// <summary>A path suffix naming the structural trace to attach, or <c>""</c> to let the verb choose.</summary>
    internal string Trace { get; }

    internal bool Json { get; }

    internal bool Help { get; }

    /// <summary>Why the command line could not be read, or <see langword="null"/> when it could.</summary>
    internal string? Error { get; }

    /// <summary>Reads the arguments that follow the verb; the first of them is the channel.</summary>
    internal static FeedbackArguments Parse(IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        var reading = new Reading();
        var index = 0;
        while (index < arguments.Count)
        {
            index = reading.Read(arguments, index) + 1;
        }

        return reading.Result();
    }

    /// <summary>The report these arguments and this project describe.</summary>
    internal FeedbackReport Report(DoctorSnapshot snapshot, string doctorReportJson) =>
        FeedbackReport.NewBuilder()
            .Runtime(PublicRepository.Runtime)
            .Category(FeedbackCategory.OfId(Category))
            .Install(FeedbackGatherer.InstallCoordinate(snapshot))
            .Step(Step)
            .Narrative(new ProblemNarrative(Did, Happened, Expected))
            .Language(Language)
            .Agent(new AgentIdentity(AgentProduct, AgentModel))
            .Attachments(FeedbackGatherer.Attachments(snapshot, doctorReportJson, Trace))
            .Build();

    /// <summary>One pass over the arguments; mutable so each flag stays one readable line.</summary>
    private sealed class Reading
    {
        private string _channel = "";
        private string _category = "";
        private string _step = "";
        private string _did = "";
        private string _happened = "";
        private string _expected = "";
        private string _language = "en";
        private string _agentProduct = "";
        private string _agentModel = "";
        private string _trace = "";
        private bool _json;
        private bool _help;
        private string? _error;

        internal FeedbackArguments Result()
        {
            Validate();
            return new FeedbackArguments(
                _channel, _category, _step, _did, _happened, _expected,
                _language, _agentProduct, _agentModel, _trace, _json, _help, _error);
        }

        /// <summary>Reads one argument and returns the index of the last one it consumed.</summary>
        internal int Read(IReadOnlyList<string> arguments, int index)
        {
            var argument = arguments[index];
            if (!argument.StartsWith('-') && _channel.Length == 0)
            {
                _channel = argument;
                return index;
            }

            var equals = argument.IndexOf('=');
            var name = equals < 0 ? argument : argument[..equals];
            if (Switched(name))
            {
                return index;
            }

            if (!ValueFlags.Contains(name))
            {
                Fail($"unknown option: \"{argument}\"");
                return index;
            }

            var valueIndex = equals < 0 ? index + 1 : index;
            var value = equals < 0 ? At(arguments, valueIndex) : argument[(equals + 1)..];
            return Valued(name, value) ? valueIndex : index;
        }

        private bool Switched(string name)
        {
            switch (name)
            {
                case "--json": _json = true; return true;
                case "--help" or "-h": _help = true; return true;
                default: return false;
            }
        }

        private bool Valued(string name, string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                Fail($"{name} needs a value");
                return false;
            }

            switch (name)
            {
                case "--category": _category = value; break;
                case "--step": _step = value; break;
                case "--did": _did = value; break;
                case "--happened": _happened = value; break;
                case "--expected": _expected = value; break;
                case "--language": _language = value; break;
                case "--agent-product": _agentProduct = value; break;
                case "--agent-model": _agentModel = value; break;
                default: _trace = value; break;
            }

            return true;
        }

        /// <summary>Everything a channel cannot run without, checked in the order a person types it.</summary>
        private void Validate()
        {
            if (_help)
            {
                return;
            }

            if (!Channels.Contains(_channel))
            {
                Fail(_channel.Length == 0
                    ? "feedback needs a channel: " + string.Join(", ", Channels)
                    : $"unknown feedback channel: \"{_channel}\"");
                return;
            }

            RequireCategory();
            Require(_step, "--step");
            Require(_did, "--did");
            Require(_happened, "--happened");
            Require(_expected, "--expected");
        }

        private void RequireCategory()
        {
            if (_category.Length == 0)
            {
                Fail("--category needs a value: prompt, skill, doctor or library");
                return;
            }

            try
            {
                FeedbackCategory.OfId(_category);
            }
            catch (ArgumentException exception)
            {
                Fail(exception.Message);
            }
        }

        private void Require(string value, string flag)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                Fail($"{flag} is required — a report without it says nothing");
            }
        }

        private void Fail(string message) => _error ??= message;

        private static string? At(IReadOnlyList<string> arguments, int index) =>
            index < arguments.Count ? arguments[index] : null;
    }
}
