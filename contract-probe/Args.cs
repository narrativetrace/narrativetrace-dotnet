// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.ContractProbe;

/// <summary>Parsed <c>--key=value</c> command-line arguments for <see cref="ContractRunner"/>.</summary>
internal sealed record Args(string Version, string ContractPath, string? OutPath, string RegistryBase)
{
    private const string VersionPrefix = "--version=";
    private const string ContractPrefix = "--contract=";
    private const string OutPrefix = "--out=";
    private const string RegistryBasePrefix = "--registry-base=";

    public static Args Parse(string[] args)
    {
        string? version = null;
        var contract = "documentation/contract.yaml";
        string? outPath = null;
        var registryBase = "https://api.nuget.org/v3-flatcontainer";

        foreach (var arg in args)
            ParseOne(arg, ref version, ref contract, ref outPath, ref registryBase);

        if (version is null)
            throw new ArgumentException("--version=<published version> is required");

        return new Args(version, contract, outPath, registryBase);
    }

    private static void ParseOne(
        string arg, ref string? version, ref string contract, ref string? outPath, ref string registryBase)
    {
        if (arg.StartsWith(VersionPrefix, StringComparison.Ordinal))
            version = arg[VersionPrefix.Length..];
        else if (arg.StartsWith(ContractPrefix, StringComparison.Ordinal))
            contract = arg[ContractPrefix.Length..];
        else if (arg.StartsWith(OutPrefix, StringComparison.Ordinal))
            outPath = arg[OutPrefix.Length..];
        else if (arg.StartsWith(RegistryBasePrefix, StringComparison.Ordinal))
            registryBase = arg[RegistryBasePrefix.Length..];
    }
}
