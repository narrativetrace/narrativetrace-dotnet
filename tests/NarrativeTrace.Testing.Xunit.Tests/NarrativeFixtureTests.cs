// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using NarrativeTrace.TestingXunit;
using Xunit;

namespace NarrativeTrace.Testing.Xunit.Tests;

public class NarrativeFixtureTests
{
    [Fact]
    public void Fixture_creates_active_context()
    {
        using var fixture = new NarrativeFixture();

        Assert.True(fixture.Context.IsActive);
    }

    [Fact]
    public void Create_builds_a_fixture_pinned_to_an_explicit_config()
    {
        using var fixture = NarrativeFixture.Create(
            new NarrativeTraceConfig(TracingLevel.Off));

        Assert.False(fixture.Context.IsActive);
    }

    [Fact]
    public void Create_captures_a_trace_like_any_other_fixture()
    {
        using var fixture = NarrativeFixture.Create(new NarrativeTraceConfig());
        fixture.Context.EnterMethod("Svc", "run", []);
        fixture.Context.ExitMethodWithReturn(null);

        Assert.Single(fixture.CaptureTrace().Roots);
    }

    [Fact]
    public void Default_context_honors_off_level_from_environment()
    {
        using var fixture = new NarrativeFixture(
            key => key == ConfigResolver.LevelKey ? "OFF" : null);

        fixture.Context.EnterMethod("Svc", "run", []);
        fixture.Context.ExitMethodWithReturn(null);

        Assert.Empty(fixture.CaptureTrace().Roots);
    }

    [Fact]
    public void Default_context_degrades_garbage_level_to_detail()
    {
        using var fixture = new NarrativeFixture(
            key => key == ConfigResolver.LevelKey ? "garbage" : null);

        fixture.Context.EnterMethod("Svc", "run", []);
        fixture.Context.ExitMethodWithReturn(null);

        Assert.Single(fixture.CaptureTrace().Roots);
    }

    [Fact]
    public void Fixture_captures_trace()
    {
        using var fixture = new NarrativeFixture();
        fixture.Context.EnterMethod(
            "Svc", "run", []);
        fixture.Context.ExitMethodWithReturn(null);

        var tree = fixture.CaptureTrace();

        Assert.Single(tree.Roots);
        Assert.Equal("run",
            tree.Roots[0].Signature.MethodName);
    }

    [Fact]
    public void Writes_markdown_companions_when_output_is_enabled()
    {
        var dir = TempDir();
        try
        {
            using var fixture = new NarrativeFixture(EnvOutput(dir, format: null));
            RecordOneCall(fixture);

            fixture.WriteArtifacts("OrderTests", "PlacesOrder", failed: false);

            Assert.True(File.Exists(Trace(dir, "places_order.md")));
            Assert.True(File.Exists(Trace(dir, "places_order.json")));
            Assert.True(File.Exists(Path.Combine(
                dir, "diagrams", "OrderTests", "places_order.mmd")));
        }
        finally
        {
            DeleteDir(dir);
        }
    }

    [Fact]
    public void Writes_only_the_primary_file_for_a_non_markdown_format()
    {
        var dir = TempDir();
        try
        {
            using var fixture = new NarrativeFixture(EnvOutput(dir, "mermaid"));
            RecordOneCall(fixture);

            fixture.WriteArtifacts("OrderTests", "PlacesOrder", failed: false);

            Assert.True(File.Exists(Trace(dir, "places_order.mmd")));
            Assert.False(File.Exists(Trace(dir, "places_order.json")));
        }
        finally
        {
            DeleteDir(dir);
        }
    }

    [Fact]
    public void Writes_the_entry_arrays_when_their_switches_are_set()
    {
        var dir = TempDir();
        try
        {
            using var fixture = new NarrativeFixture(key => key switch
            {
                ConfigResolver.OutputKey => "true",
                ConfigResolver.OutputDirKey => dir,
                ConfigResolver.CanonicalJsonKey => "true",
                ConfigResolver.StructuralJsonKey => "true",
                _ => null,
            });
            RecordOneCall(fixture);

            fixture.WriteArtifacts("OrderTests", "PlacesOrder", failed: false);

            Assert.StartsWith(
                "[",
                File.ReadAllText(Trace(dir, "places_order.canonical.json")),
                StringComparison.Ordinal);
            Assert.StartsWith(
                "[",
                File.ReadAllText(Trace(dir, "places_order.structural.json")),
                StringComparison.Ordinal);
        }
        finally
        {
            DeleteDir(dir);
        }
    }

    [Fact]
    public void Writes_no_entry_arrays_by_default()
    {
        var dir = TempDir();
        try
        {
            using var fixture = new NarrativeFixture(EnvOutput(dir, format: null));
            RecordOneCall(fixture);

            fixture.WriteArtifacts("OrderTests", "PlacesOrder", failed: false);

            Assert.False(File.Exists(Trace(dir, "places_order.canonical.json")));
            Assert.False(File.Exists(Trace(dir, "places_order.structural.json")));
        }
        finally
        {
            DeleteDir(dir);
        }
    }

    [Fact]
    public void Writes_artifacts_by_default_with_no_env_var_set()
    {
        var dir = TempDir();
        try
        {
            using var fixture = new NarrativeFixture(
                key => key == ConfigResolver.OutputDirKey ? dir : null);
            RecordOneCall(fixture);

            fixture.WriteArtifacts("OrderTests", "PlacesOrder", failed: false);

            Assert.True(File.Exists(Trace(dir, "places_order.md")));
        }
        finally
        {
            DeleteDir(dir);
        }
    }

    [Fact]
    public void Writes_nothing_when_output_is_explicitly_disabled()
    {
        var dir = TempDir();
        try
        {
            using var fixture = new NarrativeFixture(key => key switch
            {
                ConfigResolver.OutputKey => "false",
                ConfigResolver.OutputDirKey => dir,
                _ => null,
            });
            RecordOneCall(fixture);

            fixture.WriteArtifacts("OrderTests", "PlacesOrder", failed: false);

            Assert.False(Directory.Exists(dir));
        }
        finally
        {
            DeleteDir(dir);
        }
    }

    private static void RecordOneCall(NarrativeFixture fixture)
    {
        fixture.Context.EnterMethod("Svc", "PlacesOrder", []);
        fixture.Context.ExitMethodWithReturn(null);
    }

    private static Func<string, string?> EnvOutput(string dir, string? format)
    {
        return key => key switch
        {
            ConfigResolver.OutputKey => "true",
            ConfigResolver.OutputDirKey => dir,
            ConfigResolver.FormatKey => format,
            _ => null,
        };
    }

    private static string Trace(string dir, string file)
    {
        return Path.Combine(dir, "traces", "OrderTests", file);
    }

    private static string TempDir()
    {
        return Path.Combine(
            Path.GetTempPath(), "nt-fx-" + Guid.NewGuid().ToString("N"));
    }

    private static void DeleteDir(string dir)
    {
        if (Directory.Exists(dir))
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Run_prints_the_narrative_and_rethrows_when_the_body_throws()
    {
        using var fixture = new NarrativeFixture();
        var writer = new StringWriter();

        var ex = Record.Exception(() => fixture.Run("Places_an_order", ctx =>
        {
            ctx.EnterMethod("OrderService", "PlaceOrder", []);
            ctx.ExitMethodWithException(new InvalidOperationException("boom"));
            throw new InvalidOperationException("boom");
        }, writer));

        Assert.IsType<InvalidOperationException>(ex);
        Assert.Contains("OrderService", writer.ToString());
    }

    [Fact]
    public void Run_prints_nothing_when_the_body_succeeds()
    {
        using var fixture = new NarrativeFixture();
        var writer = new StringWriter();

        fixture.Run("Places_an_order", ctx =>
        {
            ctx.EnterMethod("OrderService", "PlaceOrder", []);
            ctx.ExitMethodWithReturn(null);
        }, writer);

        Assert.Equal(string.Empty, writer.ToString());
    }

    [Fact]
    public void Run_warns_about_unresolved_template_placeholders()
    {
        using var fixture = new NarrativeFixture();
        var writer = new StringWriter();

        fixture.Run("Scenario", ctx =>
        {
            var handle = ctx.EnterMethod("Svc", "Do", [],
                new MethodOptions("hello {typo}"));
            ctx.ExitMethodWithReturn(null, handle);
        }, writer);

        Assert.Contains("Svc.Do: {typo} in narration", writer.ToString());
    }
}
