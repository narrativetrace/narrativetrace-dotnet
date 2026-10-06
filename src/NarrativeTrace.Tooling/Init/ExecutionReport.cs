// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Collections.ObjectModel;

namespace NarrativeTrace.Tooling.Init;

/// <summary>
/// What actually happened when a plan was applied, action by action.
/// </summary>
/// <remarks>
/// INTENT: the answer a caller reports and exits on. A refusal — planned, or discovered when the
/// filesystem would not cooperate — is DATA here, never an exception, so one impossible action never
/// costs a run the actions that did work.
/// </remarks>
public sealed record ExecutionReport
{
    /// <param name="carrier">The coordinate the plan was stamped with.</param>
    /// <param name="results">One entry per action, in plan order.</param>
    public ExecutionReport(string carrier, IReadOnlyList<Applied> results)
    {
        if (string.IsNullOrWhiteSpace(carrier))
        {
            throw new ArgumentException("a report names the carrier the plan came from");
        }

        ArgumentNullException.ThrowIfNull(results);
        Carrier = carrier;
        Results = new ReadOnlyCollection<Applied>([.. results]);
    }

    /// <summary>Whether an action happened.</summary>
    public enum Status
    {
        /// <summary>The filesystem now says what the action promised.</summary>
        Applied,

        /// <summary>Nothing was done, and <see cref="Applied.Detail"/> says why.</summary>
        Refused,
    }

    /// <summary>The coordinate the plan was stamped with.</summary>
    public string Carrier { get; }

    /// <summary>One entry per action, in plan order.</summary>
    public IReadOnlyList<Applied> Results { get; }

    /// <summary>True when anything was refused.</summary>
    public bool HasRefusals => Results.Any(result => result.ActionStatus == Status.Refused);

    /// <summary>1 when anything was refused, 0 otherwise — what an entry point returns.</summary>
    public int ExitCode => HasRefusals ? 1 : 0;

    /// <summary>
    /// One action's outcome. A <see cref="Status.Refused"/> result must carry a reason — the same
    /// contract <see cref="Refuse"/> holds on the planning side. A refusal nobody can act on is worse
    /// than no refusal at all: it reaches a person as an empty parenthesis in a warning.
    /// </summary>
    public sealed record Applied
    {
        /// <param name="action">The action as planned.</param>
        /// <param name="status">Whether it happened.</param>
        /// <param name="detail">Why it did not, or <c>""</c> when it did.</param>
        public Applied(Action action, Status status, string detail)
        {
            ArgumentNullException.ThrowIfNull(action);
            ArgumentNullException.ThrowIfNull(detail);
            if (status == Status.Refused && string.IsNullOrWhiteSpace(detail))
            {
                throw new ArgumentException(
                    "a refusal carries the reason it refused — " + action.Path + " gives none");
            }

            Action = action;
            ActionStatus = status;
            Detail = detail;
        }

        /// <summary>The action as planned.</summary>
        public Action Action { get; }

        /// <summary>Whether it happened.</summary>
        public Status ActionStatus { get; }

        /// <summary>Why it did not, or <c>""</c> when it did.</summary>
        public string Detail { get; }

        /// <summary>An action that happened, with no detail to explain.</summary>
        internal static Applied Ran(Action action) => new(action, Status.Applied, "");

        /// <summary>An action that did not happen, with the reason it did not.</summary>
        internal static Applied Refused(Action action, string detail) =>
            new(action, Status.Refused, detail);
    }
}
