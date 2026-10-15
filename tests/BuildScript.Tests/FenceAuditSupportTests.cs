// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Build;
using Xunit;

namespace Build.Tests;

/// <summary>Covers <see cref="FenceAuditSupport"/> — an untagged opening fence indexes as prose.</summary>
public sealed class FenceAuditSupportTests
{
    [Fact]
    public void A_tagged_fence_has_no_untagged_lines() =>
        Assert.Empty(FenceAuditSupport.UntaggedFenceLines("```bash\nls\n```\n"));

    [Fact]
    public void An_untagged_opening_fence_is_reported_by_its_line_number() =>
        Assert.Equal([3], FenceAuditSupport.UntaggedFenceLines("text\n\n```\nls\n```\n"));

    [Fact]
    public void The_closing_fence_is_not_mistaken_for_an_untagged_opener() =>
        Assert.Equal([1], FenceAuditSupport.UntaggedFenceLines("```\nx\n```\n```csharp\ny\n```\n"));

    [Fact]
    public void Tilde_fences_are_audited_too() =>
        Assert.Equal([1, 5], FenceAuditSupport.UntaggedFenceLines("~~~\na\n~~~\n\n~~~\nb\n~~~\n~~~sh\nc\n~~~\n"));

    [Fact]
    public void A_shorter_run_inside_a_longer_fence_is_content()
    {
        const string nested = "````markdown\n```\ninner\n```\n````\n";
        Assert.Empty(FenceAuditSupport.UntaggedFenceLines(nested));
    }

    [Fact]
    public void A_different_fence_character_inside_a_block_is_content() =>
        Assert.Empty(FenceAuditSupport.UntaggedFenceLines("```text\n~~~\n```\n"));

    [Fact]
    public void An_indented_untagged_fence_in_a_list_is_reported() =>
        Assert.Equal([2], FenceAuditSupport.UntaggedFenceLines("1. step\n   ```\n   ls\n   ```\n"));

    [Fact]
    public void Crlf_line_endings_do_not_hide_an_untagged_fence() =>
        Assert.Equal([1], FenceAuditSupport.UntaggedFenceLines("```\r\nls\r\n```\r\n"));

    [Fact]
    public void An_info_string_of_only_whitespace_counts_as_untagged() =>
        Assert.Equal([1], FenceAuditSupport.UntaggedFenceLines("```   \nls\n```\n"));

    [Fact]
    public void Empty_text_has_no_fences() =>
        Assert.Empty(FenceAuditSupport.UntaggedFenceLines(""));

    [Fact]
    public void Null_text_is_rejected() =>
        Assert.Throws<ArgumentNullException>(() => FenceAuditSupport.UntaggedFenceLines(null!));
}
