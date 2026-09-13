// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Core;

/// <summary>
/// Thrown by <see cref="NarrativeApproval"/> when a scenario has no committed
/// baseline yet, or when its traced structure differs from one.
/// </summary>
/// <remarks>
/// A plain <see cref="Exception"/> subtype rather than any test framework's
/// assertion type: <c>NarrativeTrace.Core</c> takes no dependency on xUnit or
/// NUnit, and both report an unhandled exception from a test as a failure, so
/// this reaches the same outcome — a failed test with a readable message —
/// without Core knowing which framework is listening.
/// </remarks>
public sealed class NarrativeApprovalException : Exception
{
    /// <summary>Creates the exception with the message a reviewer needs to act on.</summary>
    public NarrativeApprovalException(string message)
        : base(message)
    {
    }
}
