// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Glossary;

/// <summary>
/// File-writing sink of the live translation stream: one
/// <c>&lt;traceId&gt;.md</c> per trace.
/// </summary>
/// <remarks>
/// <para>
/// The persistence half of <see cref="TranslationSubscriber"/>'s
/// file-writing variant. Each rendered line appends immediately (with a
/// trailing newline), so a translated trace file is tail-able while its trace
/// is still running and is complete once the trace's gaps footer lands.
/// </para>
/// <para>
/// The output directory must be creatable at construction — a configured
/// destination that cannot exist is a configuration error and fails fast.
/// Per-line IO is best-effort: the first failure is reported once, later ones
/// are dropped silently (a persistently unwritable directory must not turn
/// the diagnostics into a failure storm), and no failure ever reaches the
/// pipeline. File stems are trace ids, which <c>TraceId</c> validates to
/// exactly 32 lowercase hex characters at construction — filesystem-safe by
/// type, no sanitization needed here.
/// </para>
/// </remarks>
internal sealed class TranslationFileSink : TranslationSubscriber.ITraceLineSink
{
    private readonly string outputDir;
    private readonly TextWriter diagnostics;
    private int failureReported;

    /// <param name="outputDir">
    /// Directory receiving one <c>&lt;traceId&gt;.md</c> per trace; created if
    /// absent.
    /// </param>
    /// <param name="diagnostics">Receives the one-time write-failure report.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="outputDir"/> is null, blank, or cannot be created.
    /// </exception>
    internal TranslationFileSink(string outputDir, TextWriter diagnostics)
    {
        if (string.IsNullOrWhiteSpace(outputDir))
        {
            throw new ArgumentException("outputDir must not be blank", nameof(outputDir));
        }

        try
        {
            Directory.CreateDirectory(outputDir);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException
            or NotSupportedException or ArgumentException)
        {
            throw new ArgumentException(
                $"cannot create translation output dir {outputDir}", nameof(outputDir), e);
        }

        this.outputDir = outputDir;
        this.diagnostics = diagnostics
            ?? throw new ArgumentNullException(nameof(diagnostics));
    }

    public void Emit(string traceId, string line)
    {
        var file = Path.Combine(outputDir, traceId + ".md");
        try
        {
            File.AppendAllText(file, line + "\n");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException
            or NotSupportedException)
        {
            if (Interlocked.CompareExchange(ref failureReported, 1, 0) == 0)
            {
                diagnostics.WriteLine(
                    $"dropping translated trace lines; cannot write {file}: {e.Message}");
            }
        }
    }
}
