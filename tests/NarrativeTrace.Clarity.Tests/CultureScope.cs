// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Globalization;

namespace NarrativeTrace.Clarity.Tests;

/// <summary>
/// Forces a specific <see cref="CultureInfo.CurrentCulture"/> for the duration
/// of a test block, restoring the original culture on dispose. Lets culture
/// bugs surface on every machine instead of only on non-invariant locales.
/// </summary>
public sealed class CultureScope : IDisposable
{
    private readonly CultureInfo _original;

    public CultureScope(string cultureName)
    {
        _original = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo(cultureName);
    }

    public void Dispose()
    {
        CultureInfo.CurrentCulture = _original;
    }
}
