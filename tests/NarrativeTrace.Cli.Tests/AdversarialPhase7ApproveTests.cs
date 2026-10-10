// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Cli;
using Xunit;

namespace NarrativeTrace.Cli.Tests;

/// <summary>
/// Adversarial probes of <c>approve [--dir]</c>: file names at the edges of the suffix rule, skipped
/// build folders, symlinks, and a flag given without its value.
/// </summary>
public sealed class AdversarialPhase7ApproveTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "nt-approve7-" + Guid.NewGuid().ToString("N"));
    private readonly string _outside = Path.Combine(Path.GetTempPath(), "nt-approve7-out-" + Guid.NewGuid().ToString("N"));
    private readonly List<string> _links = [];

    public AdversarialPhase7ApproveTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        // Unlink every symlink before any recursive delete, so cleanup never follows one.
        foreach (var link in _links)
        {
            if (Directory.Exists(link) || File.Exists(link))
            {
                Directory.Delete(link);
            }
        }

        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }

        if (Directory.Exists(_outside))
        {
            Directory.Delete(_outside, recursive: true);
        }
    }

    private string Write(string relative, string content, string? under = null)
    {
        var path = Path.Combine(under ?? _root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    private (int Exit, string Out, string Err) Run(params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var exit = CliRunner.Run(["approve", .. args], output, error);
        return (exit, output.ToString(), error.ToString());
    }

    [Fact]
    public void A_review_copy_named_exactly_dot_received_nt_is_promoted_to_dot_approved_nt()
    {
        var received = Write(".received.nt", "scenario: root\n");

        var (exit, _, _) = Run("--dir", _root);

        Assert.Equal(0, exit);
        Assert.True(File.Exists(Path.Combine(_root, ".approved.nt")));
        Assert.False(File.Exists(received));
    }

    [Fact]
    public void A_double_suffixed_name_keeps_its_inner_suffix_when_promoted()
    {
        Write("a.received.nt.received.nt", "scenario: a\n");

        Run("--dir", _root);

        Assert.True(File.Exists(Path.Combine(_root, "a.received.nt.approved.nt")));
    }

    [Fact]
    public void Upper_case_received_suffix_is_not_a_review_copy()
    {
        var upper = Write("Order.RECEIVED.NT", "scenario: x\n");

        var (_, output, _) = Run("--dir", _root);

        Assert.True(File.Exists(upper));
        Assert.Contains("nothing to approve", output);
    }

    [Fact]
    public void A_directory_named_like_a_review_copy_is_not_promoted()
    {
        Write("weird.received.nt/inner.nt", "scenario: x\n");

        var (exit, output, _) = Run("--dir", _root);

        Assert.Equal(0, exit);
        Assert.Contains("nothing to approve", output);
        Assert.False(File.Exists(Path.Combine(_root, "weird.approved.nt")));
    }

    [Fact]
    public void Review_copies_under_nested_node_modules_are_never_promoted()
    {
        var copy = Write("site/node_modules/pkg/lib/x.received.nt", "scenario: x\n");

        Run("--dir", _root);

        Assert.True(File.Exists(copy));
    }

    [Fact]
    public void Review_copies_under_a_subproject_bin_and_obj_are_never_promoted()
    {
        var bin = Write("Sub/bin/Debug/x.received.nt", "scenario: x\n");
        var obj = Write("Sub/obj/y.received.nt", "scenario: y\n");

        Run("--dir", _root);

        Assert.True(File.Exists(bin));
        Assert.True(File.Exists(obj));
    }

    [Fact]
    public void A_hidden_folder_that_is_not_git_is_searched_and_promoted()
    {
        Write(".vscode/x.received.nt", "scenario: x\n");

        var (_, output, _) = Run("--dir", _root);

        Assert.Contains("approved: .vscode", output);
        Assert.True(File.Exists(Path.Combine(_root, ".vscode", "x.approved.nt")));
    }

    [Fact]
    public void Each_promotion_is_named_in_ordinal_order_regardless_of_creation_order()
    {
        Write("b/z.received.nt", "scenario: z\n");
        Write("a/y.received.nt", "scenario: y\n");

        var (_, output, _) = Run("--dir", _root);

        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(["approved: a/y.approved.nt", "approved: b/z.approved.nt"], lines);
    }

    [Fact]
    public void A_unicode_and_spaced_name_is_promoted()
    {
        Write("ünï cödé.received.nt", "scenario: u\n");

        Run("--dir", _root);

        Assert.True(File.Exists(Path.Combine(_root, "ünï cödé.approved.nt")));
    }

    [Fact]
    public void Promotion_replaces_an_existing_approved_baseline_with_the_reviewed_copy()
    {
        Write("x.approved.nt", "scenario: old\n");
        Write("x.received.nt", "scenario: new\n");

        Run("--dir", _root);

        Assert.Equal("scenario: new\n", File.ReadAllText(Path.Combine(_root, "x.approved.nt")));
    }

    [Fact]
    public void A_dir_argument_that_names_a_file_is_refused_with_exit_two()
    {
        var file = Write("plain.txt", "not a directory");

        var (exit, _, error) = Run("--dir", file);

        Assert.Equal(2, exit);
        Assert.Contains("no such directory", error);
    }

    [Fact]
    public void Dir_flag_given_without_a_value_is_refused_rather_than_approving_the_working_directory()
    {
        var (exit, _, _) = Run("--dir");

        Assert.NotEqual(0, exit);
    }

    [Fact]
    public void A_symlink_loop_back_to_the_root_is_not_followed_forever()
    {
        Write("x.received.nt", "scenario: x\n");
        CreateLink(Path.Combine(_root, "loop"), _root);

        var (exit, output, _) = Run("--dir", _root);

        Assert.Equal(0, exit);
        Assert.Single(output.Split('\n', StringSplitOptions.RemoveEmptyEntries));
    }

    [Fact]
    public void A_symlinked_directory_pointing_outside_the_root_is_not_promoted_through()
    {
        Directory.CreateDirectory(_outside);
        var outsideCopy = Write("elsewhere.received.nt", "scenario: out\n", under: _outside);
        CreateLink(Path.Combine(_root, "link"), _outside);

        Run("--dir", _root);

        Assert.True(File.Exists(outsideCopy));
    }

    private void CreateLink(string path, string target)
    {
        Directory.CreateSymbolicLink(path, target);
        _links.Add(path);
    }
}
