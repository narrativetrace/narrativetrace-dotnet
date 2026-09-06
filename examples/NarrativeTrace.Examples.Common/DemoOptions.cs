// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Examples.Common;

/// <summary>
/// The command-line switches an example understands. Pacing and colour are
/// the launcher's business (<c>demo.sh</c>); the example itself only decides
/// how its log lines are formatted.
/// </summary>
/// <param name="Classic">
/// <c>true</c> when <c>--classic</c> was passed: every line carries the
/// traditional timestamp / level / thread / logger prefix instead of the
/// bare message.
/// </param>
public sealed record DemoOptions(bool Classic)
{
    private const string ClassicSwitch = "--classic";

    /// <summary>
    /// Parses the example's arguments. Anything other than
    /// <c>--classic</c> is an error — the examples are reference code, so an
    /// unknown switch is a typo to report, not a hint to ignore.
    /// </summary>
    /// <exception cref="ArgumentException">An argument is not a known switch.</exception>
    public static DemoOptions Parse(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        var unknown = Array.Find(args, a => a != ClassicSwitch);
        if (unknown is not null)
        {
            throw new ArgumentException(
                $"unknown argument: {unknown} (the only switch is {ClassicSwitch})",
                nameof(args));
        }

        return new DemoOptions(Classic: args.Contains(ClassicSwitch));
    }
}
