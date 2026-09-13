// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Cli.Doctor;
using Xunit;

namespace NarrativeTrace.Cli.Tests.Doctor;

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
            Path.Combine(_root, "missing"), _ => null, json: false, _out, _err);

        Assert.Equal(2, exit);
        Assert.Contains("could not run", _err.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Returns_two_when_no_csproj_is_found()
    {
        var exit = DoctorCommand.Run(_root, _ => null, json: false, _out, _err);

        Assert.Equal(2, exit);
        Assert.Contains(".csproj", _err.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Renders_human_text_and_returns_one_when_a_check_fails()
    {
        File.WriteAllText(Path.Combine(_root, "app.csproj"), "<Project />");

        var exit = DoctorCommand.Run(_root, _ => null, json: false, _out, _err);

        Assert.Equal(1, exit);
        Assert.Contains("[FAIL]", _out.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Renders_json_when_requested()
    {
        File.WriteAllText(Path.Combine(_root, "app.csproj"), "<Project />");

        var exit = DoctorCommand.Run(_root, _ => null, json: true, _out, _err);

        Assert.Equal(1, exit);
        Assert.Contains("\"findings\"", _out.ToString(), StringComparison.Ordinal);
    }
}
