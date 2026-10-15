// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace Checkout.Tests;

/// <summary>
/// Saves a byte-stable captured-output file for a recipe page's output block to embed via
/// <c>snippet-check</c>: <c>artifacts/recipes/&lt;slug&gt;/&lt;name&gt;</c>, gitignored like every
/// other generated file under <c>artifacts/</c>.
/// </summary>
internal static class CapturedOutput
{
    // This assembly builds to recipes/checkout/Checkout.Tests/bin/<Config>/<TFM>/ — six levels
    // below the repository root.
    private static readonly string RepoRoot =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../../"));

    public static void Write(string slug, string name, string content)
    {
        var directory = Path.Combine(RepoRoot, "artifacts", "recipes", slug);
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, name), content);
    }
}
