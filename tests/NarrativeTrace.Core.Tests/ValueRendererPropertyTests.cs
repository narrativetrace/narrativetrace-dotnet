// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using FsCheck;
using FsCheck.Xunit;
using NarrativeTrace.Core;

namespace NarrativeTrace.Core.Tests;

public class ValueRendererPropertyTests
{
    [Property]
    public bool Render_never_returns_null(int value)
    {
        return ValueRenderer.Render(value) is not null;
    }

    [Property]
    public bool Strings_are_always_quoted(NonNull<string> s)
    {
        var result = ValueRenderer.Render(s.Get);
        return result.StartsWith('"') && result.EndsWith('"');
    }

    [Property]
    public bool Booleans_render_as_true_or_false(bool value)
    {
        var result = ValueRenderer.Render(value);
        return result == "true" || result == "false";
    }

    [Property]
    public bool Integers_render_as_parseable_numbers(int value)
    {
        var result = ValueRenderer.Render(value);
        return int.TryParse(result, out var parsed) && parsed == value;
    }

    [Property]
    public bool Long_strings_are_truncated_to_max_length(NonNull<string> s)
    {
        var opts = new RenderOptions(MaxStringLength: 10);
        var result = ValueRenderer.Render(s.Get, opts);
        var unquoted = result[1..^1];
        var sanitized = ControlEscape.Sanitize(s.Get);

        if (sanitized.Length <= 10)
        {
            return unquoted == sanitized;
        }

        return unquoted.EndsWith("...", StringComparison.Ordinal)
            && unquoted.Length == 13;
    }

    [Property]
    public bool Chars_are_single_quoted(char c)
    {
        var result = ValueRenderer.Render(c);
        return result.StartsWith('\'') && result.EndsWith('\'');
    }
}
