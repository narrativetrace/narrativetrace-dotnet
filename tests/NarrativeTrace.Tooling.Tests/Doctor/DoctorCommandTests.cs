// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Tooling.Doctor;
using NarrativeTrace.Tooling.Init;
using Xunit;

namespace NarrativeTrace.Tooling.Tests.Doctor;

public sealed class DoctorCommandTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "nt-doctor-cmd-" + Guid.NewGuid().ToString("N"));

    private readonly StringWriter _out = new();
    private readonly StringWriter _err = new();

    public DoctorCommandTests()
    {
        Directory.CreateDirectory(_root);
    }

    /// <summary>A single-skill in-memory carrier, pure — no disk I/O.</summary>
    private static Carrier StubCarrier() => Carrier.FromSource("1.0.0", name => name switch
    {
        "catalogue.json" =>
            "{\"runtime\": \"dotnet\", \"skills\": [{\"name\": \"narrativetrace-doctor\", "
            + "\"description\": \"d\", \"agents\": \"agents/narrativetrace-doctor/SKILL.md\", "
            + "\"claude\": \"claude/narrativetrace-doctor/SKILL.md\"}]}",
        "agents/narrativetrace-doctor/SKILL.md" => "---\nname: narrativetrace-doctor\n---\n",
        "claude/narrativetrace-doctor/SKILL.md" => "---\nname: narrativetrace-doctor\n---\n",
        _ => null,
    });

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public void Returns_two_when_the_directory_does_not_exist()
    {
        var exit = DoctorCommand.Run(
            Path.Combine(_root, "missing"), _ => null, StubCarrier, json: false, _out, _err);

        Assert.Equal(2, exit);
        Assert.Contains("could not run", _err.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Returns_two_when_no_csproj_is_found()
    {
        var exit = DoctorCommand.Run(_root, _ => null, StubCarrier, json: false, _out, _err);

        Assert.Equal(2, exit);
        Assert.Contains(".csproj", _err.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Renders_human_text_and_returns_one_when_a_check_fails()
    {
        File.WriteAllText(Path.Combine(_root, "app.csproj"), "<Project />");

        var exit = DoctorCommand.Run(_root, _ => null, StubCarrier, json: false, _out, _err);

        Assert.Equal(1, exit);
        Assert.Contains("[FAIL]", _out.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Renders_json_when_requested()
    {
        File.WriteAllText(Path.Combine(_root, "app.csproj"), "<Project />");

        var exit = DoctorCommand.Run(_root, _ => null, StubCarrier, json: true, _out, _err);

        Assert.Equal(1, exit);
        Assert.Contains("\"findings\"", _out.ToString(), StringComparison.Ordinal);
    }

    /// <summary>
    /// Proves the carrier the caller supplies is what config.skills-installed reads its catalogue
    /// from: a project with no NarrativeTrace restore output resolves no family version, so the check
    /// passes "cannot tell" even though nothing is installed.
    /// </summary>
    [Fact]
    public void Wires_the_given_carrier_into_the_skills_installed_check()
    {
        File.WriteAllText(Path.Combine(_root, "app.csproj"), "<Project />");

        var exit = DoctorCommand.Run(_root, _ => null, StubCarrier, json: true, _out, _err);

        Assert.Equal(1, exit);
        var json = _out.ToString();
        Assert.Contains("\"config.skills-installed\"", json, StringComparison.Ordinal);
        Assert.Contains("cannot tell", json, StringComparison.Ordinal);
    }

    /// <summary>
    /// "Doctor green" is exit 0 and a failing finding is exit 1 — the verdict a script reads must not
    /// live only inside the report (item 165: two consumers read exit 0 as green on a [FAIL] run).
    /// </summary>
    [Fact]
    public void Returns_zero_when_every_check_passes_and_one_the_moment_one_does_not()
    {
        File.WriteAllText(Path.Combine(_root, "app.csproj"), "<Project />");
        File.WriteAllText(
            Path.Combine(_root, "OrderServiceTests.cs"), "Assert.Contains(\"[REDACTED]\", rendered);");

        var passing = DoctorCommand.Run(_root, _ => null, StubCarrier, json: false, _out, _err);
        File.Delete(Path.Combine(_root, "OrderServiceTests.cs"));
        var failing = DoctorCommand.Run(_root, _ => null, StubCarrier, json: false, _out, _err);

        Assert.Equal(0, passing);
        Assert.Equal(1, failing);
    }
}
