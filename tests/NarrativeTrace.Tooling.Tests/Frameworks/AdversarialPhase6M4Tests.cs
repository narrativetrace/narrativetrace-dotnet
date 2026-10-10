// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Diagnostics;
using NarrativeTrace.Tooling.Doctor;
using NarrativeTrace.Tooling.Doctor.Checks;
using NarrativeTrace.Tooling.Frameworks;
using NarrativeTrace.Tooling.Tests.Doctor;
using Xunit;

namespace NarrativeTrace.Tooling.Tests.Frameworks;

/// <summary>
/// Adversarial probes of the Phase 6 M4 framework table, wiring check, trap check, renderer, snippet
/// parser and snapshot builder: manifest shapes people really write, near-miss ids, comments and
/// strings as false evidence, hostile sizes, and null arguments.
/// </summary>
public sealed class AdversarialPhase6M4Tests
{
    private const string WebManifest =
        "<Project Sdk=\"Microsoft.NET.Sdk.Web\"><ItemGroup><PackageReference Include=\"NarrativeTrace.AspNetCore\" /></ItemGroup></Project>";

    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(2);

    private static FrameworkRow Row(string id) => FrameworkTable.Row(id)!;

    private static DoctorSnapshot Snapshot(string manifest, params (string Path, string Content)[] sources) =>
        DoctorSnapshotFixtures.WithProject(manifest) with
        {
            SourceFiles = sources.ToDictionary(source => source.Path, source => source.Content),
        };

    private static DoctorFinding RunWiring(string rowId, string manifest, params (string Path, string Content)[] sources) =>
        new FrameworkWiringCheck(Row(rowId)).Run(Snapshot(manifest, sources));

    private static string Repeat(string unit, int count) => string.Concat(Enumerable.Repeat(unit, count));

    private static string Normalise(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal);

    private static void AssertWithinBudget(Action action)
    {
        var clock = Stopwatch.StartNew();
        action();
        clock.Stop();
        Assert.True(clock.Elapsed < Budget, $"took {clock.Elapsed.TotalSeconds:F2} s, budget is {Budget.TotalSeconds} s");
    }

    [Fact]
    public void Xunit_is_detected_when_a_semicolon_list_has_spaces_around_the_id()
    {
        Assert.True(FrameworkTable.Detected(Row("xunit"), "<PackageReference Include=\" Moq ; xunit ; Foo \" />"));
    }

    [Theory]
    [InlineData("Foo.xunit")]
    [InlineData("xunitx")]
    [InlineData("my-xunit")]
    [InlineData("xunit-core")]
    public void Xunit_is_not_detected_by_an_id_that_merely_contains_xunit(string id)
    {
        Assert.False(FrameworkTable.Detected(Row("xunit"), $"<PackageReference Include=\"{id}\" />"));
    }

    [Theory]
    [InlineData("<PACKAGEREFERENCE INCLUDE=\"xunit\" />")]
    [InlineData("<packagereference include='xunit'></packagereference>")]
    public void Xunit_is_detected_for_upper_and_lower_case_element_and_attribute_names(string reference)
    {
        Assert.True(FrameworkTable.Detected(Row("xunit"), reference));
    }

    [Fact]
    public void Xunit_is_detected_in_a_manifest_with_a_byte_order_mark_and_crlf_line_endings()
    {
        const string manifest =
            "﻿<Project>\r\n  <ItemGroup>\r\n    <PackageReference Include=\"xunit\" Version=\"2.9.2\" />\r\n  </ItemGroup>\r\n</Project>";

        Assert.True(FrameworkTable.Detected(Row("xunit"), manifest));
    }

    [Fact]
    public void Package_reference_whose_condition_contains_a_greater_than_sign_is_still_detected()
    {
        const string manifest =
            "<Project><ItemGroup><PackageReference Condition=\"'$(TargetFramework)' > 'net8.0'\" Include=\"xunit\" /></ItemGroup></Project>";

        Assert.True(FrameworkTable.Detected(Row("xunit"), manifest));
    }

    [Fact]
    public void Project_reference_whose_condition_contains_a_greater_than_sign_still_counts_as_referenced()
    {
        const string manifest =
            "<Project><ItemGroup><ProjectReference Condition=\"$(Version) > 1\" " +
            "Include=\"..\\src\\NarrativeTrace.AspNetCore\\NarrativeTrace.AspNetCore.csproj\" /></ItemGroup></Project>";

        Assert.True(Row("aspnetcore").Package.ReferencedIn(manifest));
    }

    [Fact]
    public void A_cdata_section_is_text_not_markup_so_a_reference_inside_it_does_not_count()
    {
        const string manifest =
            "<Project Sdk=\"Microsoft.NET.Sdk.Web\"><![CDATA[<PackageReference Include=\"NarrativeTrace.AspNetCore\" />]]></Project>";

        Assert.False(Row("aspnetcore").Package.ReferencedIn(manifest));
    }

    [Fact]
    public void A_reference_after_an_unterminated_comment_is_inside_the_comment_and_does_not_count()
    {
        const string manifest = "<Project><!-- oops <PackageReference Include=\"NarrativeTrace.AspNetCore\" /></Project>";

        Assert.False(Row("aspnetcore").Package.ReferencedIn(manifest));
    }

    [Fact]
    public void A_reference_after_the_first_comment_close_is_live_markup_even_when_the_comment_looked_nested()
    {
        const string manifest =
            "<Project><!-- <!-- note --> <PackageReference Include=\"NarrativeTrace.AspNetCore\" /> --></Project>";

        Assert.True(Row("aspnetcore").Package.ReferencedIn(manifest));
    }

    [Fact]
    public void An_include_on_a_neighbouring_element_is_not_taken_for_a_package_reference()
    {
        const string manifest =
            "<Project><ItemGroup><PackageReference Version=\"1.0\" /><Compile Include=\"xunit\" /></ItemGroup></Project>";

        Assert.False(FrameworkTable.Detected(Row("xunit"), manifest));
    }

    [Fact]
    public void An_unterminated_comment_in_one_manifest_does_not_hide_a_reference_in_another_manifest()
    {
        var snapshot = DoctorSnapshotFixtures.Empty() with
        {
            ProjectFiles = new Dictionary<string, string>
            {
                ["app.csproj"] = "<Project Sdk=\"Microsoft.NET.Sdk.Web\"><!-- todo",
                ["Directory.Build.props"] =
                    "<Project><ItemGroup><PackageReference Include=\"NarrativeTrace.AspNetCore\" /></ItemGroup></Project>",
            },
        };

        var finding = new FrameworkWiringCheck(Row("aspnetcore")).Run(snapshot);

        Assert.False(finding.Passed);
        Assert.Contains("never applied", finding.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("is not referenced", finding.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("NarrativeTrace.")]
    [InlineData("NarrativeTrace.Has Space")]
    [InlineData("NarrativeTrace.a;b")]
    [InlineData("NarrativeTrace.x\"")]
    public void A_package_id_that_no_nuget_id_can_be_is_refused_when_a_row_is_built(string id)
    {
        Assert.Throws<ArgumentException>(() => new IntegrationPackage(id));
    }

    [Theory]
    [InlineData("default-logger", true)]
    [InlineData("v2-api", true)]
    [InlineData("1", true)]
    [InlineData("", false)]
    [InlineData("-a", false)]
    [InlineData("a-", false)]
    [InlineData("a--b", false)]
    [InlineData("A", false)]
    [InlineData("a_b", false)]
    [InlineData("a b", false)]
    [InlineData("é", false)]
    public void Kebab_accepts_only_lower_case_letters_digits_and_single_hyphens(string text, bool expected)
    {
        Assert.Equal(expected, Kebab.IsKebab(text));
    }

    [Fact]
    public void Kebab_refuses_null()
    {
        Assert.False(Kebab.IsKebab(null));
    }

    [Fact]
    public void A_package_reference_tag_repeated_without_a_closing_angle_bracket_is_scanned_within_budget()
    {
        var text = Repeat("<PackageReference ", 12_000);

        AssertWithinBudget(() => Assert.False(FrameworkTable.Detected(Row("xunit"), text)));
    }

    [Fact]
    public void An_sdk_tag_repeated_without_a_closing_angle_bracket_is_scanned_within_budget()
    {
        var text = Repeat("<Sdk ", 40_000);

        AssertWithinBudget(() => Assert.False(FrameworkTable.Detected(Row("aspnetcore"), text)));
    }

    [Fact]
    public void A_framework_reference_tag_repeated_without_a_closing_angle_bracket_is_scanned_within_budget()
    {
        var text = Repeat("<FrameworkReference ", 12_000);

        AssertWithinBudget(() => Assert.False(FrameworkTable.Detected(Row("aspnetcore"), text)));
    }

    [Fact]
    public void A_project_reference_tag_repeated_without_a_closing_angle_bracket_is_scanned_within_budget()
    {
        var text = Repeat("<ProjectReference ", 12_000);

        AssertWithinBudget(() => Assert.False(Row("aspnetcore").Package.ReferencedIn(text)));
    }

    [Fact]
    public void An_include_attribute_repeated_without_a_closing_tag_is_scanned_within_budget()
    {
        var text = Repeat("<PackageReference Include=\"", 8_000);

        AssertWithinBudget(() => Assert.False(FrameworkTable.Detected(Row("xunit"), text)));
    }

    [Fact]
    public void An_opentelemetry_addsource_call_repeated_without_a_closing_paren_is_checked_within_budget()
    {
        var source = Repeat(".AddSource(", 18_000);

        AssertWithinBudget(() =>
        {
            var finding = RunWiring(
                "opentelemetry",
                "<Project><ItemGroup><PackageReference Include=\"OpenTelemetry.Extensions.Hosting\" /></ItemGroup></Project>",
                ("Program.cs", source));

            Assert.False(finding.Passed);
        });
    }

    [Fact]
    public void Removing_comments_from_a_repeated_comment_run_is_linear_and_leaves_no_text()
    {
        var text = Repeat("<!--x-->", 25_000);
        var unterminated = Repeat("<!--", 50_000);

        AssertWithinBudget(() =>
        {
            Assert.Equal(string.Empty, ManifestPatterns.WithoutCommentsAndCData(text));
            Assert.Equal(string.Empty, ManifestPatterns.WithoutCommentsAndCData(unterminated));
        });
    }

    [Theory]
    [InlineData("aspnetcore", WebManifest, "// services.AddNarrativeTrace(builder.Configuration);\n// app.UseNarrativeTrace();")]
    [InlineData(
        "xunit",
        "<Project><ItemGroup><PackageReference Include=\"NarrativeTrace.Testing.Xunit\" /></ItemGroup></Project>",
        "// TODO: wrap the traced calls in NarrativeFixture")]
    [InlineData(
        "nunit",
        "<Project><ItemGroup><PackageReference Include=\"NarrativeTrace.Testing.NUnit\" /></ItemGroup></Project>",
        "// class Tests : NarrativeTestBase")]
    public void Wiring_that_only_appears_in_a_c_sharp_comment_does_not_count_as_applied(
        string rowId, string manifest, string source)
    {
        var finding = RunWiring(rowId, manifest, ("Program.cs", source));

        Assert.False(finding.Passed);
        Assert.Contains("never applied", finding.Message, StringComparison.Ordinal);
    }

    public static TheoryData<string> WiringRowIds()
    {
        var ids = new TheoryData<string>();
        foreach (var row in FrameworkTable.RowsWithWiringChecks())
        {
            ids.Add(row.Id);
        }

        return ids;
    }

    [Theory]
    [MemberData(nameof(WiringRowIds))]
    public void Every_rows_own_snippet_wiring_counts_as_code_but_not_as_a_block_or_line_comment(string rowId)
    {
        var row = Row(rowId);
        var manifest = $"<Project Sdk=\"Microsoft.NET.Sdk.Web\"><ItemGroup><PackageReference Include=\"{row.Package.PackageId}\" /></ItemGroup></Project>";
        var snippet = WiringSnippets.Text(rowId);
        var blockCommented = "/*\n" + snippet.Replace("*/", "* /", StringComparison.Ordinal) + "\n*/";
        var lineCommented = string.Join('\n', snippet.Split('\n').Select(line => "// " + line));

        Assert.True(RunWiring(rowId, manifest, ("Program.cs", snippet)).Passed);
        Assert.False(RunWiring(rowId, manifest, ("Program.cs", blockCommented)).Passed);
        Assert.False(RunWiring(rowId, manifest, ("Program.cs", lineCommented)).Passed);
    }

    [Fact]
    public void Spaced_calls_to_the_aspnetcore_wiring_are_accepted_as_applied()
    {
        const string source = "services.AddNarrativeTrace (builder.Configuration);\napp.UseNarrativeTrace ();";

        Assert.True(RunWiring("aspnetcore", WebManifest, ("Program.cs", source)).Passed);
    }

    [Fact]
    public void A_middleware_whose_type_name_only_starts_with_the_middleware_name_is_not_the_wiring()
    {
        const string source = "builder.Services.AddNarrativeTrace(c);\napp.UseMiddleware<NarrativeTraceMiddlewareFactory>();";

        var finding = RunWiring("aspnetcore", WebManifest, ("Program.cs", source));

        Assert.False(finding.Passed);
        Assert.Contains("only partly applied", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_project_reference_alone_with_no_wiring_fails_as_never_applied()
    {
        const string manifest =
            "<Project Sdk=\"Microsoft.NET.Sdk.Web\"><ItemGroup><ProjectReference Include=\"..\\src\\NarrativeTrace.AspNetCore\\NarrativeTrace.AspNetCore.csproj\" /></ItemGroup></Project>";

        var finding = RunWiring("aspnetcore", manifest);

        Assert.False(finding.Passed);
        Assert.Contains("never applied", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void The_trap_passes_when_the_only_proxy_call_is_commented_out()
    {
        var snapshot = DoctorSnapshotFixtures.WithSource(
            ("Program.cs", "// var p = NarrativeTraceProxy.Create<IOrders>(orders, context);"));

        Assert.True(TrapSilentSinkCheck.Run(snapshot).Passed);
    }

    /// <summary>
    /// A spaced call is still a call: a proxy created as <c>NarrativeTraceProxy . Create</c> is wiring
    /// that needs a sink. (A spaced <c>AddNarrativeTrace (…)</c> is wiring AND its own sink — the
    /// ASP.NET Core registration brings <c>LoggerTraceExporter</c>.)
    /// </summary>
    [Fact]
    public void The_trap_treats_a_spaced_proxy_creation_as_wiring_that_needs_a_sink()
    {
        Assert.False(TrapSilentSinkCheck.Run(DoctorSnapshotFixtures.WithSource(
            ("Program.cs", "var p = NarrativeTraceProxy . Create<IOrders>(o, c);"))).Passed);
        Assert.True(TrapSilentSinkCheck.Run(DoctorSnapshotFixtures.WithSource(
            ("Program.cs", "services.AddNarrativeTrace (builder.Configuration);"))).Passed);
    }

    [Fact]
    public void A_sink_named_only_in_a_comment_does_not_attach_a_consumer()
    {
        var snapshot = DoctorSnapshotFixtures.WithSource(
            ("Program.cs", "var p = NarrativeTraceProxy.Create<IOrders>(orders, context);\n// TODO: CaptureTrace() and render it"));

        Assert.False(TrapSilentSinkCheck.Run(snapshot).Passed);
    }

    [Fact]
    public void A_near_miss_capture_method_name_is_not_the_capture_sink()
    {
        var snapshot = DoctorSnapshotFixtures.WithSource(
            ("Program.cs", "var p = NarrativeTraceProxy.Create<IOrders>(o, c);\nvar t = Recorder.CaptureTraces(x);"));

        Assert.False(TrapSilentSinkCheck.Run(snapshot).Passed);
    }

    [Fact]
    public void A_sink_in_a_different_source_file_from_the_wiring_satisfies_the_trap()
    {
        var snapshot = DoctorSnapshotFixtures.WithSource(
            ("Orders.cs", "var p = NarrativeTraceProxy.Create<IOrders>(o, c);"),
            ("Diagnostics.cs", "var t = trace.CaptureTrace();"));

        Assert.True(TrapSilentSinkCheck.Run(snapshot).Passed);
    }

    [Fact]
    public void A_multi_line_fix_indents_continuation_lines_under_the_label_and_keeps_blank_lines_empty()
    {
        var finding = new DoctorFinding("config.x-y", false, "msg", "line one\n\n  indented\n\n", "https://doc", null);

        var rendered = Normalise(DoctorRenderer.RenderHuman(new DoctorReport([finding], 1)));

        Assert.Equal(
            "[FAIL] config.x-y: msg\n" +
            "       fix: line one\n" +
            "\n" +
            "              indented\n" +
            "       see: https://doc\n" +
            "0/1 checks passed.",
            rendered);
    }

    [Fact]
    public void A_whitespace_only_continuation_line_is_rendered_without_trailing_spaces()
    {
        var finding = new DoctorFinding("config.x-y", false, "msg", "first\n   \nlast", "https://doc", null);

        var lines = Normalise(DoctorRenderer.RenderHuman(new DoctorReport([finding], 1))).Split('\n');

        Assert.All(lines, line => Assert.Equal(line.TrimEnd(), line));
    }

    [Fact]
    public void An_empty_report_says_zero_of_zero_checks_passed()
    {
        Assert.Equal("0/0 checks passed.", DoctorRenderer.RenderHuman(new DoctorReport([], 0)));
    }

    [Fact]
    public void A_passing_finding_renders_without_fix_skill_or_see_lines()
    {
        var finding = DoctorFinding.Pass("trap.silent-sink", "ok", "https://doc");

        Assert.Equal(
            "[PASS] trap.silent-sink: ok\n1/1 checks passed.",
            Normalise(DoctorRenderer.RenderHuman(new DoctorReport([finding], 0))));
    }

    [Fact]
    public void Splice_refuses_a_document_that_carries_the_same_marker_pair_twice()
    {
        const string document =
            "<!-- x:begin -->\nold\n<!-- x:end -->\n<!-- x:begin -->\nold2\n<!-- x:end -->\n";

        Assert.Throws<ArgumentException>(() => FrameworkTableDocs.Splice(document, "x", "new\n"));
    }

    [Fact]
    public void Splice_accepts_a_document_with_windows_line_endings()
    {
        const string document = "a\r\n<!-- x:begin -->\r\nold\r\n<!-- x:end -->\r\nb\r\n";

        var result = FrameworkTableDocs.Splice(document, "x", "new\n");

        Assert.Contains("new", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Splicing_the_same_content_twice_is_the_same_as_splicing_it_once()
    {
        const string document = "head\n<!-- x:begin -->\nold\n<!-- x:end -->\ntail\n";

        var once = FrameworkTableDocs.Splice(document, "x", "new\n");

        Assert.Equal(once, FrameworkTableDocs.Splice(once, "x", "new\n"));
    }

    [Fact]
    public void Splice_refuses_an_end_marker_that_comes_before_its_begin_marker()
    {
        const string document = "<!-- x:end -->\nmiddle\n<!-- x:begin -->\n";

        Assert.Throws<ArgumentException>(() => FrameworkTableDocs.Splice(document, "x", "new\n"));
    }

    [Fact]
    public void Splice_with_no_begin_marker_refuses_the_document_by_name()
    {
        var error = Assert.Throws<ArgumentException>(
            () => FrameworkTableDocs.Splice("<!-- x:end -->\n", "x", "new\n"));

        Assert.Contains("x:begin", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_llms_full_refuses_a_null_document_with_an_argument_null_exception()
    {
        Assert.Throws<ArgumentNullException>(() => FrameworkTableDocs.RenderLlmsFull(null!));
    }

    [Theory]
    [InlineData("<!-->")]
    [InlineData("<!--->")]
    public void A_short_html_comment_like_line_inside_a_section_does_not_crash_the_snippet_parser(string line)
    {
        var markdown = "## aspnetcore\n" + line + "\n<!-- snippet: a.cs region=r -->\n```csharp\nbody\n```\n";

        var parsed = WiringSnippets.Parse(markdown);

        Assert.True(parsed.ContainsKey("aspnetcore"));
    }

    [Fact]
    public void The_snippet_parser_refuses_a_null_document_with_an_argument_null_exception()
    {
        Assert.Throws<ArgumentNullException>(() => WiringSnippets.Parse(null!));
    }

    [Fact]
    public void A_marked_block_after_a_second_heading_is_not_attributed_to_the_earlier_section()
    {
        const string markdown = "## first\n\n## second\n<!-- snippet: a.cs region=r -->\n```csharp\nbody\n```\n";

        var parsed = WiringSnippets.Parse(markdown);

        Assert.False(parsed.ContainsKey("first"));
        Assert.Equal("a.cs", parsed["second"].Fixture);
    }

    [Fact]
    public void Builder_ignores_manifests_under_bin_obj_and_node_modules()
    {
        var root = Directory.CreateTempSubdirectory("nt-adv-m4-").FullName;
        try
        {
            File.WriteAllText(Path.Combine(root, "app.csproj"), "<Project />");
            foreach (var excluded in new[] { "bin/Debug", "obj", "node_modules/pkg" })
            {
                Directory.CreateDirectory(Path.Combine(root, excluded));
                File.WriteAllText(Path.Combine(root, excluded, "hidden.csproj"), "<Project />");
            }

            var snapshot = DoctorSnapshotBuilder.Build(root, _ => null);

            Assert.Equal(["app.csproj"], snapshot.ProjectFiles.Keys.Select(Path.GetFileName));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Builder_survives_a_symbolic_link_loop_inside_the_project()
    {
        var root = Directory.CreateTempSubdirectory("nt-adv-m4-loop-").FullName;
        try
        {
            File.WriteAllText(Path.Combine(root, "Program.cs"), "class Program { }");
            Directory.CreateSymbolicLink(Path.Combine(root, "loop"), root);

            var build = Task.Run(() => DoctorSnapshotBuilder.Build(root, _ => null));

            Assert.True(build.Wait(TimeSpan.FromSeconds(10)), "the builder did not finish walking a symbolic link loop");
            Assert.NotEmpty(build.Result.SourceFiles);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Builder_survives_a_subdirectory_the_user_cannot_read()
    {
        var root = Directory.CreateTempSubdirectory("nt-adv-m4-perm-").FullName;
        var locked = Path.Combine(root, "locked");
        try
        {
            Directory.CreateDirectory(locked);
            File.WriteAllText(Path.Combine(root, "app.csproj"), "<Project />");
            File.SetUnixFileMode(locked, UnixFileMode.None);

            var snapshot = DoctorSnapshotBuilder.Build(root, _ => null);

            Assert.Contains(Path.Combine(root, "app.csproj"), snapshot.ProjectFiles.Keys);
        }
        finally
        {
            File.SetUnixFileMode(locked, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Builder_survives_a_manifest_the_user_cannot_read()
    {
        var root = Directory.CreateTempSubdirectory("nt-adv-m4-file-").FullName;
        var manifest = Path.Combine(root, "app.csproj");
        try
        {
            File.WriteAllText(manifest, "<Project />");
            File.SetUnixFileMode(manifest, UnixFileMode.None);

            var snapshot = DoctorSnapshotBuilder.Build(root, _ => null);

            // Unreadable for an ordinary user (read as empty); root reads it anyway — either way no crash.
            Assert.Contains(manifest, snapshot.ProjectFiles.Keys);
        }
        finally
        {
            File.SetUnixFileMode(manifest, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Every_framework_wiring_check_id_maps_to_the_add_narrative_tracing_skill()
    {
        foreach (var id in FrameworkTable.WiringCheckIds())
        {
            Assert.Equal("add-narrative-tracing", DoctorFindingSkills.ForCheck(id));
        }
    }

    [Fact]
    public void Null_arguments_to_detection_and_the_wiring_check_are_refused_with_argument_null_exceptions()
    {
        Assert.Throws<ArgumentNullException>(() => FrameworkTable.Detected(null!, "<Project />"));
        Assert.Throws<ArgumentNullException>(() => FrameworkTable.Detected(Row("xunit"), null!));
        Assert.Throws<ArgumentNullException>(() => new FrameworkWiringCheck(Row("xunit")).Run(null!));
    }
}
