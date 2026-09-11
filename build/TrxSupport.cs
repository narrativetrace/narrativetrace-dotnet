// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace NarrativeTrace.Build;

/// <summary>One <c>&lt;UnitTestResult&gt;</c> from a VSTest <c>.trx</c> file, joined against its
/// <c>&lt;TestDefinitions&gt;</c> entry so the test's declaring class travels with its outcome —
/// a <c>.trx</c>'s <c>UnitTestResult</c> element alone carries only the free-text <c>testName</c>
/// (method name plus any theory-data suffix), never the class.</summary>
public sealed record TrxTestResult(string ClassName, string TestName, string Outcome, double DurationSeconds)
{
    public bool Passed => Outcome == "Passed";

    /// <summary>Everything VSTest reports that is not a clean pass or a hard failure — "NotExecuted",
    /// "Inconclusive", "Timeout", etc. — collapses to the schema's <c>tests_skipped</c> bucket.</summary>
    public bool Failed => Outcome == "Failed";
}

/// <summary>
/// Reads VSTest <c>.trx</c> files — the one aggregate <c>verifyAll</c> needs to slice a single
/// <c>dotnet test</c> sweep into the <c>unit-tests</c>/<c>property</c>/<c>fuzz-tier-a</c>/
/// <c>architecture</c>/<c>conformance</c>/<c>stress-short</c> rows without a second invocation.
/// Mirrors the role Java's buildSrc <c>JUnitAggregateSupport</c> plays for JUnit's own XML reports.
/// </summary>
public static class TrxSupport
{
    private static readonly XNamespace Ns = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";

    /// <summary>
    /// Every <c>&lt;UnitTestResult&gt;</c> in <paramref name="trxPath"/>, with its declaring class
    /// resolved from <c>&lt;TestDefinitions&gt;</c> by <c>testId</c>. A result whose id has no
    /// matching definition (should not happen for a well-formed VSTest run) falls back to parsing
    /// the class out of the free-text <c>testName</c> up to its last '.', so a malformed file still
    /// degrades to a best-effort read rather than losing the row outright.
    /// </summary>
    public static IReadOnlyList<TrxTestResult> ReadResults(string trxPath)
    {
        var doc = XDocument.Load(trxPath);
        var root = doc.Root;
        if (root is null)
            return [];

        var classById = root.Descendants(Ns + "UnitTest")
            .Select(unitTest => (
                Id: (string?)unitTest.Attribute("id"),
                ClassName: (string?)unitTest.Element(Ns + "TestMethod")?.Attribute("className")))
            .Where(x => x.Id is not null && x.ClassName is not null)
            .ToDictionary(x => x.Id!, x => x.ClassName!);

        return root.Descendants(Ns + "UnitTestResult")
            .Select(result => ToTrxTestResult(result, classById))
            .ToList();
    }

    private static TrxTestResult ToTrxTestResult(XElement result, IReadOnlyDictionary<string, string> classById)
    {
        var testId = (string?)result.Attribute("testId");
        var testName = (string?)result.Attribute("testName") ?? "";
        var outcome = (string?)result.Attribute("outcome") ?? "NotExecuted";
        var duration = ParseDuration((string?)result.Attribute("duration"));
        var className = testId is not null && classById.TryGetValue(testId, out var found)
            ? found
            : FallbackClassName(testName);
        return new TrxTestResult(className, testName, outcome, duration);
    }

    /// <summary>VSTest writes <c>duration</c> as <c>hh:mm:ss.fffffff</c>; a missing or malformed
    /// value reads as <c>0</c> rather than throwing — a timing gap must never abort the aggregate.</summary>
    private static double ParseDuration(string? text) =>
        text is not null && TimeSpan.TryParse(text, System.Globalization.CultureInfo.InvariantCulture, out var span)
            ? span.TotalSeconds
            : 0.0;

    private static string FallbackClassName(string testName)
    {
        var lastDot = testName.LastIndexOf('.');
        return lastDot < 0 ? testName : testName[..lastDot];
    }

    /// <summary>Reads every <paramref name="trxPaths"/> file, tolerating one that no longer exists
    /// (a project whose Test invocation itself failed before producing a report) by skipping it
    /// rather than throwing — the aggregate must still report every OTHER project's real results.</summary>
    public static IReadOnlyList<TrxTestResult> ReadAll(IEnumerable<string> trxPaths) =>
        trxPaths.Where(File.Exists).SelectMany(ReadResults).ToList();

    /// <summary><c>tests_passed</c>/<c>tests_failed</c>/<c>tests_skipped</c>/<c>test_classes</c> — the
    /// schema's standard test-run metric keys, over whatever slice of results is handed in.</summary>
    public static IReadOnlyDictionary<string, object> Summarize(IReadOnlyList<TrxTestResult> results) =>
        new Dictionary<string, object>
        {
            ["tests_passed"] = results.Count(r => r.Passed),
            ["tests_failed"] = results.Count(r => r.Failed),
            ["tests_skipped"] = results.Count(r => !r.Passed && !r.Failed),
            ["test_classes"] = results.Select(r => r.ClassName).Distinct().Count(),
        };

    /// <summary>Whether every result in <paramref name="results"/> passed — <c>true</c> (vacuously)
    /// for an empty slice, matching Java's <c>JUnitAggregateSupport.allGreen</c>.</summary>
    public static bool AllGreen(IReadOnlyList<TrxTestResult> results) => results.All(r => r.Passed);

    public static double TotalSeconds(IReadOnlyList<TrxTestResult> results) => results.Sum(r => r.DurationSeconds);

    /// <summary>
    /// Reads <c>&lt;testResultsDir&gt;/&lt;project&gt;{suffix}</c> for every name in
    /// <paramref name="projectNames"/>, keyed by project name — the shape
    /// <see cref="VerifyAllTestSlices"/>'s project-based slicing rules read directly. A project
    /// with no file at that path (its own <c>dotnet test</c> invocation crashed before writing a
    /// report) contributes an empty list rather than being absent from the dictionary, so a caller
    /// enumerating projects never needs a null-check.
    /// </summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<TrxTestResult>> ReadByProject(
        string testResultsDir, IEnumerable<string> projectNames, string suffix = ".trx") =>
        projectNames.ToDictionary(
            name => name,
            name =>
            {
                var path = Path.Combine(testResultsDir, name + suffix);
                return File.Exists(path) ? ReadResults(path) : (IReadOnlyList<TrxTestResult>)[];
            });
}
