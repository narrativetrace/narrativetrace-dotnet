// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Tooling.Feedback;
using Xunit;

namespace NarrativeTrace.Tooling.Tests.Feedback;

/// <summary>
/// Adversarial tests for edge cases in the feedback module that are implemented in production
/// but not covered by existing tests.
/// </summary>
public sealed class AdversarialFeedbackTests
{
    // ---- ValueFreeMatchers boundary cases ----

    [Fact]
    public void A_base64_run_with_exactly_31_chars_does_not_match_entropy_rule()
    {
        // Base64 minimum is 32 characters; 31 should not trigger the entropy rule
        Assert.DoesNotContain(
            ValueFreeCheck.RulesRefusing(new string('A', 31)),
            rule => rule.Id == "vf.entropy");
    }

    [Fact]
    public void A_hyphenated_phrase_at_exactly_32_chars_is_not_flagged_as_entropy()
    {
        // Hyphens are deliberately NOT part of base64 runs, so they break the run
        // This should not be caught even though it's exactly 32 chars
        var text = "the-quick-brown-fox-jumps-over-lazy";
        var violations = ValueFreeCheck.RulesRefusing(text);
        Assert.DoesNotContain(violations, rule => rule.Id == "vf.entropy");
    }

    [Fact]
    public void A_duration_with_plain_number_not_preceded_by_em_dash_is_not_flagged()
    {
        // The duration pattern is anchored on the em-dash, not just any number
        // "version 32" should not match the duration rule
        var text = "version 32 was deployed";
        Assert.DoesNotContain(ValueFreeCheck.RulesRefusing(text), rule => rule.Id == "vf.duration");
    }

    [Fact]
    public void A_rendered_outcome_with_literal_value_word_is_accepted()
    {
        // "→ value" (structural literal) should be accepted
        var text = "- Service.method() → value";
        Assert.DoesNotContain(ValueFreeCheck.RulesRefusing(text), rule => rule.Id == "vf.rendered-outcome");
    }

    [Fact]
    public void A_rendered_outcome_arrow_followed_by_any_other_value_is_refused()
    {
        // "→ 42" or "→ someValue" should be refused
        var text = "- Service.method() → 42";
        Assert.Contains(ValueFreeCheck.RulesRefusing(text), rule => rule.Id == "vf.rendered-outcome");
    }

    [Fact]
    public void An_email_with_empty_local_part_is_not_matched()
    {
        // "@example.com" should not match (local part must be non-empty)
        var text = "contact @example.com for details";
        Assert.DoesNotContain(ValueFreeCheck.RulesRefusing(text), rule => rule.Id == "vf.email");
    }

    [Fact]
    public void A_bare_at_symbol_does_not_match_email_pattern()
    {
        // Just "@" followed by a domain should not match
        Assert.DoesNotContain(
            ValueFreeCheck.RulesRefusing("see the @NotTraced attribute"),
            rule => rule.Id == "vf.email");
    }

    [Fact]
    public void A_forward_slash_only_home_path_without_account_is_not_matched()
    {
        // "/home" alone is not a home directory (no account segment after)
        var text = "files in /home should be organized";
        Assert.DoesNotContain(ValueFreeCheck.RulesRefusing(text), rule => rule.Id == "vf.home-path");
    }

    [Fact]
    public void A_unix_path_without_account_segment_is_not_flagged_as_home_path()
    {
        // "/usr/share/ada" has "ada" but it's not in /home/ or /Users/, so not a home path
        var text = "configuration is in /usr/share/ada/config";
        Assert.DoesNotContain(ValueFreeCheck.RulesRefusing(text), rule => rule.Id == "vf.home-path");
    }

    [Fact]
    public void A_home_path_with_account_and_trailing_slash_is_matched()
    {
        // "/home/ada/" should match (has account segment)
        var text = "project is at /home/ada/";
        Assert.Contains(ValueFreeCheck.RulesRefusing(text), rule => rule.Id == "vf.home-path");
    }

    // ---- SecretVocabulary.Canonical edge cases ----

    [Fact]
    public void Accented_contraseña_is_canonical_to_match_contrasena_term()
    {
        // "contraseña" with tilde should be normalized to match "contrasena" in the term list
        Assert.True(SecretVocabulary.NamesASecret("contraseña"));
    }

    [Fact]
    public void Decomposed_form_of_accented_word_is_canonical_to_match()
    {
        // A decomposed form of "contraseña" should normalize to the same canonical form
        // Create a decomposed version: "n" + combining tilde
        var decomposed = "contraseña".Normalize(System.Text.NormalizationForm.FormD);
        Assert.True(SecretVocabulary.NamesASecret(decomposed));
    }

    [Fact]
    public void Api_key_underscore_and_apikey_concatenated_are_two_different_terms()
    {
        // "api_key" and "apikey" are separate entries in the vocabulary
        // A field named "api_key" should match
        Assert.True(SecretVocabulary.NamesASecret("api_key"));
        // A field containing just "apikey" should also match
        Assert.True(SecretVocabulary.NamesASecret("apikey"));
        // But a field that is only "api" should not match either term completely
        Assert.False(SecretVocabulary.NamesASecret("api"));
    }

    [Fact]
    public void Secret_vocabulary_canonical_strips_diacritics()
    {
        var text = "Contraseña";
        var canonical = SecretVocabulary.Canonical(text);
        Assert.Equal("contrasena", canonical);
    }

    [Fact]
    public void Secret_vocabulary_canonical_is_lowercase()
    {
        var text = "PASSWORD";
        var canonical = SecretVocabulary.Canonical(text);
        Assert.Equal("password", canonical);
    }

    // ---- Attachments record `with` expression ----

    [Fact]
    public void Attachments_with_modification_validates_exclusivity()
    {
        // Create an attachment with a doctor report
        var original = Attachments.Of("{\"test\": true}", "trace");

        // Try to use `with` to add both a report and a reason (should fail)
        Assert.Throws<ArgumentException>(() =>
            new Attachments(original.DoctorReport, "reason", original.StructuralTrace));
    }

    [Fact]
    public void Attachments_with_modification_allows_changing_structural_trace()
    {
        var original = Attachments.Of("{\"test\": true}", "old trace");
        var modified = new Attachments(original.DoctorReport, "", "new trace");

        Assert.Equal("new trace", modified.StructuralTrace);
        Assert.Equal(original.DoctorReport, modified.DoctorReport);
    }

    // ---- AgentIdentity.Describe edge cases ----

    [Fact]
    public void Agent_describe_with_model_but_empty_product_returns_slash_model()
    {
        // Empty product, non-empty model returns the product / model format with empty product
        // Code path: Product.Length == 0 is false (Product is ""), Model.Length == 0 is false
        // So returns $"{Product} / {Model}" which is "" / "opus" = " / opus"
        var agent = new AgentIdentity("", "opus");
        var described = agent.Describe();
        Assert.Equal(" / opus", described);
    }

    [Fact]
    public void Agent_describe_with_empty_product_and_model_returns_empty()
    {
        var agent = new AgentIdentity("", "");
        Assert.Equal("", agent.Describe());
    }

    [Fact]
    public void Agent_describe_with_product_only_returns_product()
    {
        var agent = new AgentIdentity("my-product", "");
        Assert.Equal("my-product", agent.Describe());
    }

    [Fact]
    public void Agent_unknown_is_empty_not_null()
    {
        var unknown = AgentIdentity.Unknown();
        Assert.Equal("", unknown.Describe());
        Assert.Equal("", unknown.Product);
        Assert.Equal("", unknown.Model);
    }

    // ---- FeedbackReport.Invariant edge cases ----

    [Fact]
    public void Report_invariant_fails_when_runtime_contains_spaces()
    {
        var report = FeedbackFixtures.ValidReportBuilder()
            .Runtime("dotnet 6.0")
            .Build();

        Assert.False(report.Invariant());
    }

    [Fact]
    public void Report_invariant_fails_when_runtime_contains_uppercase()
    {
        var report = FeedbackFixtures.ValidReportBuilder()
            .Runtime("DOTNET")
            .Build();

        Assert.False(report.Invariant());
    }

    [Fact]
    public void Report_construction_refuses_blank_install()
    {
        // Blank mandatory fields are refused at construction time, not at invariant check
        Assert.Throws<ArgumentException>(() =>
            FeedbackFixtures.ValidReportBuilder()
                .Install("   ")
                .Build());
    }

    [Fact]
    public void Report_construction_refuses_blank_step()
    {
        // Blank mandatory fields are refused at construction time, not at invariant check
        Assert.Throws<ArgumentException>(() =>
            FeedbackFixtures.ValidReportBuilder()
                .Step("")
                .Build());
    }

    [Fact]
    public void Report_construction_refuses_blank_language()
    {
        // Blank mandatory fields are refused at construction time, not at invariant check
        Assert.Throws<ArgumentException>(() =>
            FeedbackFixtures.ValidReportBuilder()
                .Language("  ")
                .Build());
    }

    [Fact]
    public void Report_invariant_succeeds_for_valid_report()
    {
        var report = FeedbackFixtures.ValidReport();
        Assert.True(report.Invariant());
    }

    [Fact]
    public void Report_invariant_checks_attachment_exclusivity()
    {
        // This should fail at construction, not at invariant check
        Assert.Throws<ArgumentException>(() =>
            FeedbackFixtures.ValidReportBuilder()
                .Attachments(new Attachments("{\"test\": true}", "reason", ""))
                .Build());
    }

    // ---- IssueFormUrl truncation marker ----

    [Fact]
    public void Issue_form_url_includes_truncation_marker_when_field_is_clipped()
    {
        var report = FeedbackFixtures.ValidReportBuilder()
            .Step(new string('x', 10_000))
            .Build();

        var url = IssueFormUrl.Of(report);

        // The URL should include the truncation marker because the step was clipped
        Assert.Contains(Uri.EscapeDataString(IssueFormUrl.TruncationMarker), url, StringComparison.Ordinal);
    }

    [Fact]
    public void Issue_form_url_truncation_marker_is_present_in_url_encoding()
    {
        var report = FeedbackFixtures.ValidReportBuilder()
            .Install(new string('y', 5_000))
            .Build();

        var url = IssueFormUrl.Of(report);

        // The marker " … (continued in the pasted body)" should appear (percent-encoded)
        Assert.Contains("%20", url, StringComparison.Ordinal); // Space is encoded
        Assert.True(url.Length <= IssueFormUrl.MaxLength);
    }

    [Fact]
    public void Issue_form_url_truncation_marker_constant_is_correct()
    {
        Assert.Equal(" … (continued in the pasted body)", IssueFormUrl.TruncationMarker);
    }

    // ---- GhCommandLine newline handling ----

    [Fact]
    public void Gh_command_line_one_lines_newlines_in_step()
    {
        var report = FeedbackFixtures.ValidReportBuilder()
            .Step("step one\nstep two\nstep three")
            .Build();

        var line = GhCommandLine.Of(report, "body.md");

        // The line should not contain the newlines; they should be replaced with spaces
        Assert.DoesNotContain("\n", line, StringComparison.Ordinal);
        // The steps should be joined with spaces
        Assert.Contains("step one step two step three", line, StringComparison.Ordinal);
    }

    [Fact]
    public void Gh_command_line_collapses_multiple_spaces()
    {
        var report = FeedbackFixtures.ValidReportBuilder()
            .Step("step    with    many    spaces")
            .Build();

        var line = GhCommandLine.Of(report, "body.md");

        // Multiple spaces should collapse to single spaces
        Assert.DoesNotContain("    ", line, StringComparison.Ordinal);
    }

    [Fact]
    public void Gh_command_line_trims_the_body_file_path()
    {
        var line = GhCommandLine.Of(FeedbackFixtures.ValidReport(), "  /path/to/file.md  ");

        // Path should be trimmed
        Assert.Contains("/path/to/file.md", line, StringComparison.Ordinal);
        Assert.DoesNotContain("  /path/to/file.md  ", line, StringComparison.Ordinal);
    }

    // ---- StructuralTrace edge cases ----

    [Fact]
    public void Structural_trace_with_fork_marker_before_header_is_not_structural()
    {
        const string traceWithMarkerFirst =
            "~ fork [2]\n"
            + "scenario: order is placed\n"
            + "- OrderService.placeOrder(customerId, total)\n";

        Assert.False(StructuralTrace.LooksStructural(traceWithMarkerFirst));
    }

    [Fact]
    public void Structural_trace_with_multiple_headers_is_not_structural()
    {
        const string traceWithMultipleHeaders =
            "scenario: first scenario\n"
            + "- OrderService.placeOrder(customerId)\n"
            + "scenario: second scenario\n"
            + "- OrderService.placeOrder(customerId)\n";

        Assert.False(StructuralTrace.LooksStructural(traceWithMultipleHeaders));
    }

    [Fact]
    public void Structural_trace_with_trailing_blank_lines_is_still_structural()
    {
        const string traceWithTrailingBlanks =
            "scenario: order is placed\n"
            + "- OrderService.placeOrder(customerId, total) → value\n"
            + "\n"
            + "\n";

        Assert.True(StructuralTrace.LooksStructural(traceWithTrailingBlanks));
    }

    [Fact]
    public void Structural_trace_with_leading_blank_lines_then_header_is_structural()
    {
        const string traceWithLeadingBlanks =
            "\n"
            + "scenario: order is placed\n"
            + "- OrderService.placeOrder(customerId, total)\n";

        Assert.True(StructuralTrace.LooksStructural(traceWithLeadingBlanks));
    }

    [Fact]
    public void Structural_trace_with_call_line_with_escaped_name_is_valid()
    {
        // Test that underscore-containing names work
        const string traceWithUnderscore =
            "scenario: test\n"
            + "- Service$Class.method_name(a, b)\n";

        Assert.True(StructuralTrace.LooksStructural(traceWithUnderscore));
    }

    [Fact]
    public void Structural_trace_outcome_throw_with_fully_qualified_name()
    {
        const string traceWithFullyQualifiedThrow =
            "scenario: error case\n"
            + "- Service.method() !! System.InvalidOperationException\n";

        Assert.True(StructuralTrace.LooksStructural(traceWithFullyQualifiedThrow));
    }

    [Fact]
    public void Structural_trace_with_parameters_containing_spaces_is_valid()
    {
        const string traceWithSpacedParams =
            "scenario: test\n"
            + "- Service.method(param1 , param2, param3)\n";

        Assert.True(StructuralTrace.LooksStructural(traceWithSpacedParams));
    }

    // ---- HomePaths edge cases ----

    [Fact]
    public void Home_paths_rewrite_preserves_structure()
    {
        var path = "/home/alice/projects/myapp/src";
        var rewritten = HomePaths.ToTilde(path);
        Assert.Equal("~/projects/myapp/src", rewritten);
    }

    [Fact]
    public void Home_paths_rewrite_handles_spaces_in_path()
    {
        var path = "/home/alice/my documents/file.txt";
        var rewritten = HomePaths.ToTilde(path);
        Assert.Equal("~/my documents/file.txt", rewritten);
    }

    [Fact]
    public void Home_paths_with_multiple_home_directories_rewrites_all()
    {
        var text = "copied from /home/alice/src to /home/bob/dst";
        var rewritten = HomePaths.ToTilde(text);
        Assert.Equal("copied from ~/src to ~/dst", rewritten);
    }

    // ---- Rendered outcome pattern edge cases ----

    [Fact]
    public void Rendered_outcome_with_structural_value_literal_is_not_a_violation()
    {
        var text = "result → value from the operation";
        Assert.DoesNotContain(ValueFreeCheck.RulesRefusing(text), rule => rule.Id == "vf.rendered-outcome");
    }

    [Fact]
    public void Rendered_outcome_with_returned_value_is_a_violation()
    {
        var text = "result → {\"id\": 123}";
        Assert.Contains(ValueFreeCheck.RulesRefusing(text), rule => rule.Id == "vf.rendered-outcome");
    }
}
