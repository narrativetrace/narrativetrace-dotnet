// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Text;

namespace NarrativeTrace.Core;

internal static class CamelCaseSplitter
{
    // Split only at a lowercase→uppercase boundary, mirroring Java's
    // (?<=[a-z])(?=[A-Z]) so acronyms stay grouped (HTTPServer → httpserver).
    public static string ToPhrase(string identifier)
    {
        if (string.IsNullOrEmpty(identifier))
        {
            return identifier;
        }

        var sb = new StringBuilder(identifier.Length + 4);
        for (var i = 0; i < identifier.Length; i++)
        {
            if (i > 0
                && char.IsLower(identifier[i - 1])
                && char.IsUpper(identifier[i]))
            {
                sb.Append(' ');
            }

            sb.Append(char.ToLowerInvariant(identifier[i]));
        }

        return sb.ToString();
    }
}
