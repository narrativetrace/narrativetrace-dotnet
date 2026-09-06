// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Core;

/// <summary>
/// The lifetime of an activated trace context; disposing it restores what was
/// in effect before.
/// </summary>
/// <remarks>
/// Always consume it with <c>using</c>. Returned by
/// <see cref="IContextSnapshot.Activate"/>, and meaningful only on the thread
/// that activated it — disposing from a different thread than the one that
/// activated restores the wrong state. Scopes nest, so activating inside an
/// active scope is fine as long as they unwind in order, which <c>using</c>
/// guarantees. Disposing twice is harmless.
/// </remarks>
public interface IContextScope : IDisposable;
