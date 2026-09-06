// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Build;
using Xunit;

namespace Build.Tests;

/// <summary>
/// Covers <see cref="I18nManifestSupport"/> — parsing
/// <c>documentation/i18n/manifest.json</c> into the language and document
/// declarations every other translation check reads.
/// </summary>
public sealed class I18nManifestSupportTests : IDisposable
{
    private readonly string _repo = Directory.CreateTempSubdirectory("nt-i18n-manifest").FullName;

    public void Dispose() => Directory.Delete(_repo, recursive: true);

    private void WriteManifest(string json)
    {
        var file = Path.Combine(_repo, "documentation", "i18n", "manifest.json");
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, json);
    }

    [Fact]
    public void Missing_manifest_yields_null()
    {
        Assert.Null(I18nManifestSupport.LoadOrNull(_repo));
    }

    [Fact]
    public void A_well_formed_manifest_parses_every_field()
    {
        WriteManifest("""
            {
              "sourceLanguage": "en",
              "languages": [
                { "code": "es", "displayName": "Español", "directory": "documentation/guides/es",
                  "index": "documentation/guides/es/guias-de-usuario.md", "rootReadme": "LEAME.md",
                  "status": "in-progress" }
              ],
              "documents": [
                { "source": "documentation/guides/installation.md",
                  "translations": { "es": "guides/es/guia-de-instalacion.md" } }
              ]
            }
            """);

        var manifest = I18nManifestSupport.LoadOrNull(_repo);

        Assert.NotNull(manifest);
        Assert.Equal("en", manifest.SourceLanguage);
        var language = Assert.Single(manifest.Languages);
        Assert.Equal("es", language.Code);
        Assert.Equal("Español", language.DisplayName);
        Assert.Equal("documentation/guides/es", language.Directory);
        Assert.Equal("documentation/guides/es/guias-de-usuario.md", language.Index);
        Assert.Equal("LEAME.md", language.RootReadme);
        Assert.Equal(I18nStatus.InProgress, language.Status);
        var document = Assert.Single(manifest.Documents);
        Assert.Equal("documentation/guides/installation.md", document.Source);
        Assert.Equal("guides/es/guia-de-instalacion.md", document.Translations["es"]);
    }

    [Fact]
    public void Language_looks_up_by_code_and_is_null_for_an_undeclared_one()
    {
        WriteManifest("""
            {
              "languages": [
                { "code": "es", "displayName": "Español", "directory": "d", "index": "i", "rootReadme": "r",
                  "status": "complete" }
              ],
              "documents": []
            }
            """);

        var manifest = I18nManifestSupport.LoadOrNull(_repo)!;

        Assert.NotNull(manifest.Language("es"));
        Assert.Null(manifest.Language("fr"));
    }

    [Fact]
    public void A_document_with_no_translations_key_yields_an_empty_map()
    {
        WriteManifest("""
            { "languages": [], "documents": [ { "source": "documentation/x.md" } ] }
            """);

        var document = Assert.Single(I18nManifestSupport.LoadOrNull(_repo)!.Documents);

        Assert.Empty(document.Translations);
    }

    [Fact]
    public void An_unknown_status_throws()
    {
        WriteManifest("""
            {
              "languages": [
                { "code": "es", "displayName": "Español", "directory": "d", "index": "i", "rootReadme": "r",
                  "status": "done" }
              ],
              "documents": []
            }
            """);

        var ex = Assert.Throws<InvalidOperationException>(() => I18nManifestSupport.LoadOrNull(_repo));
        Assert.Contains("done", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Missing_sourceLanguage_defaults_to_en()
    {
        WriteManifest("""{ "languages": [], "documents": [] }""");

        Assert.Equal("en", I18nManifestSupport.LoadOrNull(_repo)!.SourceLanguage);
    }
}
