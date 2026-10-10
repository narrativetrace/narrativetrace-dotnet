// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Tooling.Doctor;
using NarrativeTrace.Tooling.Feedback;

namespace NarrativeTrace.Cli.Feedback;

/// <summary>
/// <c>narrativetrace feedback</c> — drafts a problem report about NarrativeTrace itself, checks it
/// carries no values, and shows the person how to file it.
/// </summary>
/// <remarks>
/// <para>
/// INTENT: the whole decision is the user's, and this verb is built so that it cannot be anybody
/// else's. It writes two files and prints text. It opens no browser, runs no <c>gh</c>, and makes no
/// request of its own — the only outward-facing thing it does is ask an already-installed <c>gh</c>
/// whether it is signed in, and only on the channel that would need it.
/// </para>
/// <para>
/// <b>@llmNote</b> Every channel re-drafts from the flags it was given rather than reading back a
/// draft from disk. That is deliberate: there is no hidden state between invocations, so a report can
/// never be filed under a draft that was edited after it was shown.
/// </para>
/// <para>
/// <b>@sideEffects</b> Writes the draft and body files under the project, and only when the gate
/// cleared the report.
/// </para>
/// </remarks>
internal static class FeedbackCommand
{
    /// <summary>Where the two files land, relative to the project.</summary>
    internal const string OutputDirectory = "TestResults/narrativetrace/feedback";

    internal const string DraftFile = OutputDirectory + "/feedback-draft.md";
    internal const string BodyFile = OutputDirectory + "/feedback-body.md";

    internal const string Usage =
        "dotnet-narrativetrace feedback <draft|url|gh> [options]\n\n"
        + "Drafts a problem report about NarrativeTrace, checks it carries no values from your data,\n"
        + "and shows you how to file it. Nothing is sent until you choose to.\n\n"
        + "  --category <c>    prompt | skill | doctor | library  (required)\n"
        + "  --step <s>        the skill step, prompt step or doctor check id  (required)\n"
        + "  --did <s>         what you did  (required)\n"
        + "  --happened <s>    what happened  (required)\n"
        + "  --expected <s>    what you expected  (required)\n"
        + "  --language <tag>  the language the report is written in  (default en)\n"
        + "  --agent-product <s> / --agent-model <s>   who drafted it, as reported\n"
        + "  --trace <suffix>  a path suffix naming the .nt to attach; omitted = let the verb choose\n"
        + "  --json            machine-readable output instead of human text\n\n"
        + "Exit 0 = drafted / ready, 1 = the channel is not available or a file could not be written,\n"
        + "2 = a value-free rule refused the report (and nothing was written).\n";

    /// <summary>What to do instead when <c>gh</c> cannot be used.</summary>
    private const string NoGh =
        "gh is not installed or not signed in. Use `narrativetrace feedback url` instead: it needs no"
        + " tool and no credential beyond the browser you are already signed in to.";

    /// <summary>Runs the verb against the process's working directory and the real seams.</summary>
    internal static int Run(IReadOnlyList<string> args, TextWriter output, TextWriter error) =>
        Run(
            args,
            Directory.GetCurrentDirectory(),
            cwd => DoctorSnapshotBuilder.Build(cwd, Environment.GetEnvironmentVariable),
            GhAuthProbe.Authenticated,
            output,
            error);

    internal static int Run(
        IReadOnlyList<string> args,
        string cwd,
        Func<string, DoctorSnapshot> buildSnapshot,
        Func<bool> ghAuthenticated,
        TextWriter output,
        TextWriter error)
    {
        var parsed = FeedbackArguments.Parse(args);
        if (parsed.Help)
        {
            output.Write(Usage);
            return 0;
        }

        if (parsed.Error is not null)
        {
            error.WriteLine(parsed.Error + "\n\n" + Usage);
            return 2;
        }

        return Guarded(parsed, cwd, buildSnapshot, ghAuthenticated, output, error);
    }

    private static int Guarded(
        FeedbackArguments parsed, string cwd, Func<string, DoctorSnapshot> buildSnapshot,
        Func<bool> ghAuthenticated, TextWriter output, TextWriter error)
    {
        try
        {
            return DraftThen(parsed, cwd, buildSnapshot, ghAuthenticated, output, error);
        }
        catch (ArgumentException exception)
        {
            error.WriteLine(exception.Message);
            return 2;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            error.WriteLine("could not write the draft: " + exception.Message);
            return 1;
        }
    }

    private static int DraftThen(
        FeedbackArguments parsed, string cwd, Func<string, DoctorSnapshot> buildSnapshot,
        Func<bool> ghAuthenticated, TextWriter output, TextWriter error)
    {
        var snapshot = buildSnapshot(cwd);
        var report = parsed.Report(snapshot, ReadDoctorReport(cwd));
        var draft = FeedbackDrafter.Draft(report);
        if (draft is FeedbackDraft.Refused refused)
        {
            error.Write(parsed.Json ? FeedbackJson.Refused(parsed.Channel, refused) : refused.Describe());
            return 2;
        }

        var drafted = (FeedbackDraft.Drafted)draft;
        Write(cwd, drafted);
        var traceNote = FeedbackGatherer.ChooseTrace(snapshot, parsed.Trace).Reason;
        return Print(parsed, drafted, ghAuthenticated, output, error, traceNote);
    }

    private static int Print(
        FeedbackArguments parsed, FeedbackDraft.Drafted drafted, Func<bool> ghAuthenticated,
        TextWriter output, TextWriter error, string traceNote) => parsed.Channel switch
        {
            "url" => PrintUrl(parsed, drafted, output),
            "gh" => PrintGh(parsed, drafted, ghAuthenticated, output, error),
            _ => PrintDraft(parsed, drafted, output, traceNote),
        };

    private static int PrintDraft(FeedbackArguments parsed, FeedbackDraft.Drafted drafted, TextWriter output, string traceNote)
    {
        if (parsed.Json)
        {
            output.Write(FeedbackJson.Drafted(drafted.Report, DraftFile, BodyFile, traceNote));
            return 0;
        }

        output.WriteLine(drafted.Draft);
        output.WriteLine($"Written to {DraftFile} and {BodyFile}.");
        if (traceNote.Length > 0)
        {
            output.WriteLine("No structural trace attached: " + traceNote);
        }

        return 0;
    }

    private static int PrintUrl(FeedbackArguments parsed, FeedbackDraft.Drafted drafted, TextWriter output)
    {
        var url = IssueFormUrl.Of(drafted.Report);
        if (parsed.Json)
        {
            output.Write(FeedbackJson.Url(url, BodyFile));
            return 0;
        }

        output.WriteLine(url);
        output.WriteLine(
            "\nOpen that in your own browser, where you are already signed in, then paste the contents of "
            + BodyFile + " into the report's last box and submit it.\n\n" + FeedbackRender.PrivacyNote);
        return 0;
    }

    private static int PrintGh(
        FeedbackArguments parsed, FeedbackDraft.Drafted drafted, Func<bool> ghAuthenticated,
        TextWriter output, TextWriter error)
    {
        if (!ghAuthenticated())
        {
            // The only place in the verb the gh probe is consulted — exactly one entry point.
            return Unavailable(parsed, error);
        }

        var command = GhCommandLine.Of(drafted.Report, BodyFile);
        if (parsed.Json)
        {
            output.Write(FeedbackJson.Gh(command));
            return 0;
        }

        output.WriteLine(command);
        output.WriteLine(
            "\nThat line is printed, not run. Running it files the report under your own GitHub account.\n\n"
            + FeedbackRender.PrivacyNote);
        return 0;
    }

    private static int Unavailable(FeedbackArguments parsed, TextWriter error)
    {
        if (parsed.Json)
        {
            error.Write(FeedbackJson.GhUnavailable(NoGh));
        }
        else
        {
            error.WriteLine(NoGh);
        }

        return 1;
    }

    /// <summary>The doctor's JSON report, or <c>""</c> when this project has none.</summary>
    private static string ReadDoctorReport(string cwd)
    {
        var report = Path.Combine(cwd, FeedbackGatherer.DoctorReportPath.Replace('/', Path.DirectorySeparatorChar));
        return File.Exists(report) ? File.ReadAllText(report) : "";
    }

    private static void Write(string cwd, FeedbackDraft.Drafted drafted)
    {
        Directory.CreateDirectory(Path.Combine(cwd, OutputDirectory.Replace('/', Path.DirectorySeparatorChar)));
        File.WriteAllText(Path.Combine(cwd, DraftFile.Replace('/', Path.DirectorySeparatorChar)), drafted.Draft);
        File.WriteAllText(Path.Combine(cwd, BodyFile.Replace('/', Path.DirectorySeparatorChar)), drafted.Body);
    }
}
