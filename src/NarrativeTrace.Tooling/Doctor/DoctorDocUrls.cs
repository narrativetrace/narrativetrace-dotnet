// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Tooling.Doctor;

/// <summary>
/// Public documentation anchors doctor findings point at. Every URL resolves
/// against the published GitHub snapshot, never the private origin.
/// </summary>
public static class DoctorDocUrls
{
    private const string Base =
        "https://github.com/narrativetrace/narrativetrace-dotnet/blob/main/";

    /// <summary>
    /// The published URL of a repository-relative documentation page (and anchor) — how a
    /// framework-table row's <c>Guide</c> becomes the link its finding carries.
    /// </summary>
    public static string ForPage(string repositoryRelativePage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRelativePage);
        return Base + repositoryRelativePage;
    }

    /// <summary>Package choices and the Legacy (<c>net48</c>) target.</summary>
    public const string InstallationPackages = Base + "documentation/guides/installation.md#packages";

    /// <summary>Option A — <c>DispatchProxy</c>, and why it requires an interface.</summary>
    public const string InstallationProxyOption =
        Base + "documentation/guides/installation.md#option-a--dispatchproxy-works-in-any-net-app";

    /// <summary>Configuring where trace output is written.</summary>
    public const string InstallationConfigureOutput =
        Base + "documentation/guides/installation.md#configure-trace-output";

    /// <summary>The <c>NARRATIVETRACE_*</c> environment variables.</summary>
    public const string ConfigurationEnvVars =
        Base + "documentation/guides/configuration.md#2-environment-variables-configresolver";

    /// <summary>Registering and wiring the ASP.NET Core middleware.</summary>
    public const string AspNetCoreRegistration =
        Base + "documentation/guides/aspnetcore.md#1-register-and-wire-the-middleware";

    /// <summary>Redaction: the always-on, name-based deny-list.</summary>
    public const string PrivacyRedaction =
        Base + "documentation/privacy-and-redaction.md#redaction-surface-by-surface";

    /// <summary>The <c>.nt</c> structural approval-trace format.</summary>
    public const string StructuralTraceFormat = Base + "documentation/structural-trace-format.md";

    /// <summary>The quickstart's "before you start" version-skew notes.</summary>
    public const string LlmsBeforeYouStart = Base + "documentation/guides/llms.txt#before-you-start";

    /// <summary>Confirming the install, including the agent skills the CLI carries.</summary>
    public const string ValidateInstallation =
        Base + "documentation/guides/installation.md#validate-installation";
}
