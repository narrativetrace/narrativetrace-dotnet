// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Text;

namespace NarrativeTrace.Tooling.Frameworks;

/// <summary>Whether one manifest element is the shape a marker or a package reference takes.</summary>
/// <param name="element">A start tag read by <see cref="ManifestElements"/>.</param>
public delegate bool ManifestPattern(ManifestElement element);

/// <summary>
/// The shapes a framework marker or a package reference takes in an MSBuild project file, as
/// predicates over the start tags <see cref="ManifestElements"/> reads.
/// </summary>
/// <remarks>
/// Every comparison is case-insensitive (NuGet ids and MSBuild names are) and exact per <c>;</c>-list
/// item, so a longer id (<c>xunit.runner.visualstudio</c>) or a bare mention in a description never
/// counts as the one asked about. <c>PackageVersion</c> (a central version pin) and <c>Update</c> (a
/// metadata edit) are not references and do not match.
/// </remarks>
public static class ManifestPatterns
{
    private const StringComparison Ignore = StringComparison.OrdinalIgnoreCase;

    /// <summary>A <c>&lt;PackageReference Include="id"&gt;</c>.</summary>
    public static ManifestPattern Package(string packageId) =>
        element => element.Is("PackageReference") && element.Items("Include").Any(id => id.Equals(packageId, Ignore));

    /// <summary>A package reference to <paramref name="packageId"/> or to any id under it (<c>id.*</c>).</summary>
    public static ManifestPattern PackageFamily(string packageId) =>
        element => element.Is("PackageReference") && element.Items("Include")
            .Any(id => id.Equals(packageId, Ignore) || id.StartsWith(packageId + ".", Ignore));

    /// <summary>A <c>&lt;FrameworkReference Include="name"&gt;</c>.</summary>
    public static ManifestPattern FrameworkReference(string name) =>
        element => element.Is("FrameworkReference") && element.Items("Include").Any(item => item.Equals(name, Ignore));

    /// <summary>
    /// A project SDK: <c>&lt;Project Sdk="name"&gt;</c> or <c>&lt;Import Sdk="name"&gt;</c> (each
    /// <c>;</c>-listed SDK, optionally <c>name/version</c>), or an <c>&lt;Sdk Name="name"/&gt;</c> element.
    /// </summary>
    public static ManifestPattern Sdk(string name) =>
        element => (element.Is("Project") || element.Is("Import"))
                && element.Items("Sdk").Any(sdk => sdk.Split('/')[0].Trim().Equals(name, Ignore))
            || (element.Is("Sdk") && element.Items("Name").Any(sdk => sdk.Equals(name, Ignore)));

    /// <summary>
    /// A <c>&lt;ProjectReference&gt;</c> to <c>&lt;projectName&gt;.csproj</c>: the file name after the last
    /// path separator, so <c>MyNarrativeTrace.X.csproj</c> is a different project.
    /// </summary>
    public static ManifestPattern Project(string projectName) =>
        element => element.Is("ProjectReference") && element.Items("Include")
            .Any(path => path[(path.LastIndexOfAny(['/', '\\']) + 1)..].Equals(projectName + ".csproj", Ignore));

    /// <summary>
    /// <paramref name="manifest"/> with every XML comment and CDATA section removed — both are text,
    /// never markup. An unterminated one runs to the end of the text, as an XML parser would refuse it.
    /// A scan, not a pattern.
    /// </summary>
    public static string WithoutCommentsAndCData(string manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        var result = new StringBuilder(manifest.Length);
        var comment = new Cursor(manifest, "<!--", "-->");
        var cdata = new Cursor(manifest, "<![CDATA[", "]]>");
        var at = 0;
        while (at < manifest.Length)
        {
            var nextComment = comment.NextFrom(at);
            var nextCData = cdata.NextFrom(at);
            if (nextComment < 0 && nextCData < 0)
            {
                result.Append(manifest, at, manifest.Length - at);
                break;
            }

            var commentFirst = nextCData < 0 || (nextComment >= 0 && nextComment < nextCData);
            var open = commentFirst ? nextComment : nextCData;
            result.Append(manifest, at, open - at);
            at = (commentFirst ? comment : cdata).EndAfter(open);
        }

        return result.ToString();
    }

    /// <summary>
    /// The next occurrence of one opener at or after a position, remembered so that repeated asks never
    /// search the same text twice — what keeps the stripping linear.
    /// </summary>
    private sealed class Cursor(string text, string open, string close)
    {
        private const int NotSearched = -2;
        private int _found = NotSearched;

        /// <summary>The next opener at or after <paramref name="at"/>, or -1 when none is left.</summary>
        public int NextFrom(int at)
        {
            if (_found == NotSearched || (_found >= 0 && _found < at))
            {
                _found = text.IndexOf(open, at, StringComparison.Ordinal);
            }

            return _found;
        }

        public int EndAfter(int opened)
        {
            var end = text.IndexOf(close, opened + open.Length, StringComparison.Ordinal);
            return end < 0 ? text.Length : end + close.Length;
        }
    }
}
