// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Core;

/// <summary>
/// Optional capability for contexts that can lose part of a trace and are
/// willing to say so.
/// </summary>
/// <remarks>
/// <para>
/// Separate from <see cref="INarrativeContext"/> on purpose: capture is the
/// context's job, while <em>accounting for what capture could not keep</em> is
/// an optional one — the no-op context and a purely synchronous capture have
/// nothing to report and should not have to implement a member that always
/// answers zero. Test the context for this interface and fall back to
/// <see cref="TraceLoss.None"/>:
/// </para>
/// <code>
/// var loss = context as ITraceLossSource is { } source
///     ? source.TraceLoss
///     : TraceLoss.None;
/// </code>
/// <para>
/// A wrapper context (the <c>ILogger</c> bridge, the scoped async context)
/// should forward the inner context's value rather than answering zero, or the
/// loss disappears at the layer that was supposed to report it.
/// </para>
/// </remarks>
public interface ITraceLossSource
{
    /// <summary>
    /// What this context has lost so far: buffered events shed under load, plus
    /// worker scopes refused by the adoption ceiling.
    /// </summary>
    /// <remarks>
    /// A live reading, not a snapshot of a finished run — read it after the
    /// work is done and before reporting. Never <see langword="null"/>;
    /// a lossless run reports <see cref="TraceLoss.None"/>.
    /// </remarks>
    TraceLoss TraceLoss { get; }
}
