// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Skills.Catalogue;
using NarrativeTrace.Skills.Render;

namespace NarrativeTrace.SkillsDevTool;

/// <summary>
/// The OpenAI plugin directory's committed manifest, rendered from the catalogue's one directory
/// listing. It lives inside the catalogue package, not at the repository root, so the published root
/// never carries a second <c>plugin.json</c> a vendor could discover on its own.
/// </summary>
/// <remarks>
/// INTENT: <c>skills render</c> writes exactly this file and <c>skills lint</c> compares exactly this
/// file, so the path and the content are typed once, like <see cref="CursorFiles"/>.
/// </remarks>
internal static class OpenAiPluginFile
{
    /// <summary>The package root, repository-relative with <c>/</c> separators.</summary>
    internal const string PackageRoot = "src/NarrativeTrace.Skills.Catalogue/openai-plugin";

    /// <summary>The manifest's repository-relative path with <c>/</c> separators.</summary>
    internal const string ManifestPath = PackageRoot + "/plugin.json";

    /// <summary>The freshly rendered, versionless manifest.</summary>
    internal static string Render() => OpenAiPluginJsonRenderer.Render(SkillCatalogue.OpenAiDirectory);

    /// <summary>The package root's absolute path under <paramref name="repoRoot"/>.</summary>
    internal static string PackageRootPath(string repoRoot) =>
        System.IO.Path.Combine(repoRoot, PackageRoot.Replace('/', System.IO.Path.DirectorySeparatorChar));

    /// <summary>The manifest's absolute path under <paramref name="repoRoot"/>.</summary>
    internal static string Path(string repoRoot) =>
        System.IO.Path.Combine(repoRoot, ManifestPath.Replace('/', System.IO.Path.DirectorySeparatorChar));
}
