// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Globalization;
using NarrativeTrace.ContractProbe;
using NarrativeTrace.ContractProbe.Probes;

var options = Args.Parse(args);
var entries = ContractYaml.Read(options.ContractPath);

var holds = 0;
var notApplicable = 0;
var fails = 0;
var jsonEntries = new List<string>();

foreach (var entry in entries)
{
    var (verdict, detail) = Evaluate(entry, options);
    switch (verdict)
    {
        case "not-applicable-before-since": notApplicable++; break;
        case "holds": holds++; break;
        default: fails++; break;
    }

    Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "{0,-45} {1,-30} {2}", entry.Id, verdict, detail));
    jsonEntries.Add(JsonEntry(entry, verdict, detail));
}

var summary = $"{holds} holds, {notApplicable} not-applicable-before-since, {fails} fails "
    + $"(installed version {options.Version})";
Console.WriteLine();
Console.WriteLine(summary);

if (options.OutPath is not null)
{
    var json = "{\"version\":\"" + Escape(options.Version) + "\",\"entries\":["
        + string.Join(",", jsonEntries) + "],\"summary\":{\"holds\":" + holds
        + ",\"notApplicableBeforeSince\":" + notApplicable + ",\"fails\":" + fails + "}}";
    File.WriteAllText(options.OutPath, json);
}

return fails > 0 ? 1 : 0;

(string Verdict, string Detail) Evaluate(ContractEntry entry, Args opts)
{
    if (!Versions.IsApplicable(entry.Since, opts.Version))
        return ("not-applicable-before-since", $"since {entry.Since} is later than installed {opts.Version}");

    var observed = Observe(entry, opts);
    return observed == entry.Expect
        ? ("holds", $"observed \"{observed}\"")
        : ("fails", FailureMessage(entry, opts.Version, observed));
}

string FailureMessage(ContractEntry entry, string installedVersion, string observed)
{
    var coordinate = entry.Coordinate ?? entry.Id;
    return "documentation/contract.yaml: " + entry.Id + " documented default \"" + entry.Expect
        + "\" (since " + entry.Since + ") but " + coordinate + " " + installedVersion
        + " (published) reads \"" + observed + "\"";
}

string Observe(ContractEntry entry, Args opts) => entry.Id switch
{
    "entry-point-core" or "entry-point-runtime" or "entry-point-proxy" or "entry-point-logging" =>
        EntryPointProbe.Observe(entry.Coordinate!, opts.RegistryBase, opts.Version),
    "reflectable-output-default" => OutputDefaultProbe.Observe(),
    "reflectable-approval-default" => ApprovalDefaultProbe.Observe(),
    "reflectable-per-invocation-identity" => ReflectablePerInvocationIdentityProbe.Observe(),
    "probed-proxy-options-redaction" => ProxyOptionsRedactionProbe.Observe(),
    "probed-not-traced-rejected-on-method" => NotTracedOnMethodRejectedProbe.Observe(),
    "probed-templates-honour-redaction" => TemplatesHonourRedactionProbe.Observe(),
    "probed-typed-error-marker" => TypedErrorMarkerProbe.Observe(),
    "probed-native-stringification-not-trusted" => NativeStringificationNotTrustedProbe.Observe(),
    "probed-platform-type-carveout" => PlatformTypeCarveoutProbe.Observe(),
    "config-shape-one-package-install" => OnePackageInstallProbe.Observe(opts.RegistryBase, opts.Version),
    "config-shape-tracelogexporter-export-to-logger" => TraceLogExporterProbe.Observe(),
    "probed-run-name-console-footer" => RunNameConsoleFooterProbe.Observe(),
    "probed-run-name-manifest-field" => RunNameManifestFieldProbe.Observe(),
    _ => throw new InvalidOperationException(
        $"no probe dispatch registered for entry \"{entry.Id}\" — add one in Program.cs's Observe"),
};

string JsonEntry(ContractEntry entry, string verdict, string detail) =>
    "{\"id\":\"" + Escape(entry.Id) + "\",\"kind\":\"" + Escape(entry.Kind) + "\",\"since\":\""
        + Escape(entry.Since) + "\",\"verdict\":\"" + verdict + "\",\"detail\":\"" + Escape(detail) + "\"}";

string Escape(string value) => value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
