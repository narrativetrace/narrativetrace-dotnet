// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Tooling.Frameworks;

/// <summary>Kebab-case: lower-case ASCII words of letters and digits, joined by single hyphens.</summary>
internal static class Kebab
{
    /// <summary>Whether <paramref name="text"/> is non-empty kebab-case (<c>default-logger</c>, <c>xunit</c>).</summary>
    public static bool IsKebab(string? text)
    {
        if (string.IsNullOrEmpty(text) || text[0] == '-' || text[^1] == '-' || text.Contains("--", StringComparison.Ordinal))
        {
            return false;
        }

        return text.All(c => c is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '-');
    }
}
