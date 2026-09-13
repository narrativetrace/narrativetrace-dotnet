// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using YamlDotNet.Serialization;

namespace NarrativeTrace.ContractProbe;

/// <summary>
/// One <c>documentation/contract.yaml</c> entry, mirroring <c>build/ContractLintSupport.cs</c>'s
/// own <c>ContractEntry</c> (the two projects cannot share code: this one consumes only registry
/// artifacts and never depends on the root build, which is not published). <see cref="Expect"/> is
/// the single observed string the named probe must produce for the claim to hold.
/// </summary>
internal sealed record ContractEntry(
    string Id, string Kind, string Page, string Claim, string Since, string Expect,
    string Probe, string? Coordinate, string? Registry);

/// <summary>
/// Reads <c>documentation/contract.yaml</c> — the schema itself is validated per commit by the
/// root build's <c>ContractLint</c> target; this reader trusts that and only extracts what
/// <see cref="ContractRunner"/> needs to run.
/// </summary>
internal static class ContractYaml
{
    public static IReadOnlyList<ContractEntry> Read(string file)
    {
        using var reader = new StreamReader(file);
        var root = new DeserializerBuilder().Build().Deserialize<Dictionary<object, object>>(reader)
            ?? throw new InvalidOperationException($"{file}: empty document");
        var rawEntries = (List<object>)root["entries"];
        return rawEntries.Select(raw => ToEntry((Dictionary<object, object>)raw)).ToList();
    }

    private static ContractEntry ToEntry(Dictionary<object, object> raw)
    {
        string? Field(string name) => raw.TryGetValue(name, out var value) ? (string?)value : null;
        var expect = Field("documented_default") ?? Field("expected_effect")
            ?? throw new InvalidOperationException($"entry \"{Field("id")}\": no documented_default/expected_effect");

        return new ContractEntry(
            Field("id")!, Field("kind")!, Field("page")!, Field("claim")!, Field("since")!, expect,
            Field("probe")!, Field("coordinate"), Field("registry"));
    }
}
