// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Core;

/// <summary>
/// Immutable service metadata stamped onto every span context.
/// </summary>
public sealed record ServiceIdentity(
    string? ServiceName = null,
    string? ServiceVersion = null,
    string? Environment = null);
