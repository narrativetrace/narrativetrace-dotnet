// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using FsCheck.Xunit;
using NarrativeTrace.Core;
using NarrativeTrace.SecurityTests.Corpus;
using NarrativeTrace.SecurityTests.Oracle;
using Xunit;

namespace NarrativeTrace.SecurityTests;

/// <summary>
/// Target 6 of the parity document's fuzzing list: configuration loading from hostile values.
/// </summary>
/// <remarks>
/// <para>
/// INTENT: Configuration is the input a tracing library reads earliest and trusts most. It arrives
/// from an environment variable no deployment script validates, and every knob has to survive a
/// typo without failing the process it is meant to observe.
/// </para>
/// <para>
/// @edgeCase This port's <see cref="ConfigResolver"/> is not the same shape as Java's: it reads
/// <c>NARRATIVETRACE_*</c> environment variables through an injectable <c>Func&lt;string, string?&gt;</c>
/// seam rather than JVM system properties, every value degrades to a default rather than throwing
/// (documented on the class itself), and there is no buffer-capacity or pipeline-strategy key at
/// all — pipelines are constructed by type, not selected by a config-driven strategy name, so
/// Java's "known strategy or refused by name" target has no .NET counterpart to fuzz. Confirmed
/// absent by reading <c>ConfigResolver.cs</c>/<c>ResolvedConfig</c> in full; ported instead as one
/// property covering every key this port actually has.
/// </para>
/// <para>
/// @edgeCase <c>resolveCaptureFlags</c> does not exist in this port either — the environment-identity
/// capture switches are a documented, not-yet-ported gap (see the repository backlog's item 2 /
/// the progress ledger's canonical-schema-1.2 section), so that Java target is also N/A here.
/// </para>
/// </remarks>
public class ConfigurationPropertyTests
{
    public static IEnumerable<object[]> Strings() =>
        HostileCorpus.Strings().Select(c => new object[] { c });

    /// <summary>
    /// Every config key this port has, set to the same hostile value at once: the resolver must
    /// degrade every field to a default rather than throwing, whatever the string.
    /// </summary>
    [Theory]
    [MemberData(nameof(Strings))]
    public void Every_corpus_string_is_safe_as_any_known_config_value(CorpusCase hostile)
    {
        var exception = Record.Exception(() => ConfigResolver.Resolve(Env(hostile.Value), TracingLevel.Detail));
        Assert.True(exception is null, $"{hostile.Id}: {hostile.Description} — {exception}");
    }

    [Theory]
    [MemberData(nameof(Strings))]
    public void Every_corpus_string_is_safe_as_a_tracing_level(CorpusCase hostile)
    {
        var level = TracingLevelExtensions.FromName(hostile.Value, TracingLevel.Narrative);
        Assert.True(Enum.IsDefined(level), $"{hostile.Id}: {hostile.Description}");
    }

    /// <summary>
    /// A test class or method name reaches the artifact path from the runner, not from the library.
    /// The oracle is containment: whatever the name, the resolved file stays under the base
    /// directory. Regression for the path-traversal gap this fuzz property found and closed — see
    /// <c>OutputDirectoryResolverTests</c>.
    /// </summary>
    [Theory]
    [MemberData(nameof(Strings))]
    public void No_corpus_string_escapes_the_output_directory(CorpusCase hostile)
    {
        var baseDir = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "narrativetrace-security-base"));
        var resolver = new OutputDirectoryResolver(baseDir);

        var file = resolver.TraceFile("com.example." + hostile.Value, hostile.Value);

        var normalized = Path.GetFullPath(file);
        Assert.StartsWith(
            Path.Combine(baseDir, "traces") + Path.DirectorySeparatorChar,
            normalized, StringComparison.Ordinal);
    }

    [Fact]
    public void Resolving_configuration_starts_no_background_thread()
    {
        ConfigResolver.Resolve(Env("abc"), TracingLevel.Detail);
        ConfigResolver.Resolve(Env("not-a-level"), TracingLevel.Detail);

        Assert.True(Oracles.NoLibraryThreadLeft(), "resolving configuration must start no background thread");
    }

    [Property(MaxTest = 100, Arbitrary = [typeof(SecurityArbitraries.PropertyValueArbitraries)])]
    public void Any_property_value_leaves_config_resolution_usable(string value)
    {
        var exception = Record.Exception(() => ConfigResolver.Resolve(Env(value), TracingLevel.Detail));
        Assert.Null(exception);
    }

    [Property(MaxTest = 100, Arbitrary = [typeof(SecurityArbitraries.PropertyValueArbitraries)])]
    public void Any_property_value_resolves_to_a_known_level(string value)
    {
        var level = TracingLevelExtensions.FromName(value, TracingLevel.Detail);
        Assert.True(Enum.IsDefined(level));
    }

    [Property(MaxTest = 100, Arbitrary = [typeof(SecurityArbitraries.PropertyValueArbitraries)])]
    public void Any_property_value_escapes_no_output_directory(string value)
    {
        var baseDir = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "narrativetrace-security-base"));
        var resolver = new OutputDirectoryResolver(baseDir);

        var file = resolver.TraceFile("com.example." + value, value);

        Assert.StartsWith(
            Path.Combine(baseDir, "traces") + Path.DirectorySeparatorChar,
            Path.GetFullPath(file), StringComparison.Ordinal);
    }

    /// <summary>Sets every known config key to the same value — the seam this port's tests already use.</summary>
    private static Func<string, string?> Env(string value)
    {
        var keys = new[]
        {
            ConfigResolver.LevelKey, ConfigResolver.OutputKey, ConfigResolver.OutputDirKey,
            ConfigResolver.FormatKey, ConfigResolver.CanonicalJsonKey, ConfigResolver.StructuralJsonKey,
            ConfigResolver.NarrationKey,
        };
        var map = keys.ToDictionary(k => k, _ => value, StringComparer.Ordinal);
        return k => map.TryGetValue(k, out var v) ? v : null;
    }
}
