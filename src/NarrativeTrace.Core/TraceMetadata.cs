// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Core;

/// <summary>
/// The provenance stamped onto a rendered trace: what scenario it came from and,
/// for a test-produced trace, which test produced it.
/// </summary>
/// <remarks>
/// Descriptive only — nothing here affects capture or filtering. It exists so an
/// artifact found later on disk or in CI can be traced back to the run that
/// created it. Every field but <see cref="Scenario"/> is optional, because a
/// trace captured outside a test framework has no test identity to record.
/// </remarks>
/// <param name="Scenario">
/// Human-readable name of what was being exercised — the heading the trace
/// renders under. The one required field.
/// </param>
/// <param name="TestClass">The declaring test class, or <see langword="null"/> outside a test run.</param>
/// <param name="TestMethod">The test method, or <see langword="null"/> outside a test run.</param>
/// <param name="Framework">The test framework that produced the trace (e.g. "xunit", "nunit"), or <see langword="null"/>.</param>
/// <param name="Timestamp">
/// When the trace was captured, pre-formatted as a string rather than a
/// <see cref="DateTime"/>: the value is written verbatim into artifacts that
/// must stay byte-identical across machine locales, so formatting is the
/// producer's decision and is not re-done here. <b>Only a timestamp</b> — the
/// scenario outcome belongs in <paramref name="Result"/>.
/// </param>
/// <param name="Result">
/// The scenario's overall outcome, as the producer knows it — typically the test
/// framework's verdict. Required, and written verbatim: exporters do not
/// cross-check it against the trace, because "did the traced code throw?" is a
/// different question from "did the scenario pass?". A test can fail an
/// assertion over a trace in which nothing threw, and can pass over one that
/// swallowed an exception.
/// </param>
/// <remarks>
/// When no framework verdict exists — an HTTP request trace, say — derive one
/// explicitly at the call site with
/// <c>ScenarioResultExtensions.Of(TraceNode.HasAnyError(tree.Roots))</c> rather
/// than expecting an exporter to guess.
/// </remarks>
public sealed record TraceMetadata(
    string Scenario,
    ScenarioResult Result,
    string? TestClass = null,
    string? TestMethod = null,
    string? Framework = null,
    string? Timestamp = null);
