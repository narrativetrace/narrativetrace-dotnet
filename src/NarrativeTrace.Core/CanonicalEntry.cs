// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Core;

/// <summary>
/// Canonical flat representation of a single trace entry matching
/// <c>entry.schema.json</c>.
/// </summary>
/// <remarks>
/// <para>
/// This is the interoperability boundary between the NarrativeTrace library
/// and any downstream consumer (NarrativeTrace backend, Datadog, Sumo Logic,
/// …). Layer 1 (universal) + Layer 2 (OTel semantic conventions) + Layer 3
/// (NarrativeTrace extensions) fields are all present as flat components.
/// The canonical entry serializer maps these PascalCase names to the dotted
/// schema keys (e.g. <c>CodeNamespace</c> → <c>code.namespace</c>,
/// <c>NtEventType</c> → <c>nt.eventType</c>).
/// </para>
/// <para>
/// Schema 1.1 added <c>NtNarrationTemplate</c>; 1.2 added the additive
/// identity fields — <c>NtPackage</c>, <c>NtReturnType</c>,
/// <c>NtExceptionPackage</c>, <c>NtInstanceId</c>, the source location, thread
/// identity and process resource identity, plus
/// <see cref="ParameterCapture.DeclaredType"/> on each parameter. Every one is
/// nullable and omitted when null, so an entry that supplies none of them is
/// still a valid 1.2 entry.
/// </para>
/// <para>
/// This port populates the ones it can derive deterministically from
/// reflection: the narration template, the namespace, the declared return and
/// parameter types, and the exception's namespace. The rest are <b>reserved,
/// not yet produced</b> — the environment-identity fields (instance id, thread,
/// host, process) vary between runs and need the capture switch Java gates them
/// behind before they can enter an artifact that must stay byte-identical, and
/// <c>code.filepath</c> / <c>code.lineno</c> have no source in a
/// <c>DispatchProxy</c> call, which sees neither declaration site nor call
/// site. <c>NtThreadVirtual</c> has no .NET counterpart at all.
/// </para>
/// </remarks>
public sealed record CanonicalEntry(
    string Timestamp,
    string Level,
    string Message,
    string? Service,
    string? Environment,
    string? TraceId,
    string? SpanId,
    string? ParentSpanId,
    string? CodeNamespace,
    string? CodeFunction,
    string NtEventType,
    string? NtTraceName = null,
    string? NtStoryId = null,
    string? NtChapterId = null,
    string? NtOutcome = null,
    string? NtForkId = null,
    int? NtBranchIndex = null,
    long? DurationMs = null,
    IReadOnlyList<ParameterCapture>? NtParameters = null,
    string? NtReturnValue = null,
    string? ExceptionType = null,
    string? ExceptionMessage = null,
    string? NtNarrationTemplate = null,
    string? NtPackage = null,
    string? NtReturnType = null,
    string? NtExceptionPackage = null,
    string? NtInstanceId = null,
    string? CodeFilepath = null,
    int? CodeLineno = null,
    string? ThreadName = null,
    long? ThreadId = null,
    bool? NtThreadVirtual = null,
    string? HostName = null,
    int? ProcessPid = null,
    string? ProcessRuntimeVersion = null)
{
    /// <summary>Discriminator: always <c>"entry"</c> for atomic events.</summary>
    public string NtEntryType { get; } = "entry";

    /// <summary>Schema version this entry conforms to.</summary>
    public string NtSchemaVersion { get; } = CanonicalSchema.Version;

    /// <summary>
    /// Causal chain identifier. Deliberately never populated (the Java
    /// reference dropped it); reserved as a nullable schema field.
    /// </summary>
    public string? NtCausalId { get; }
}
