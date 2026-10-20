// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Reflection;
using System.Runtime.CompilerServices;

namespace NarrativeTrace.DependencyInjection;

/// <summary>
/// List 1 of the instrumentation exclusions: types that are structurally never a business boundary
/// because a compiler or runtime emitted them. Built in and not configurable — membership is a
/// structural property, never a vendor name, and no option re-enables it.
/// </summary>
/// <remarks>
/// A type is generated when any of these holds: it (or an enclosing type) carries
/// <see cref="CompilerGeneratedAttribute"/> (closures, display classes, async and iterator state
/// machines, anonymous types); its name contains <c>&lt;</c>, which no source identifier can (covers
/// compilers that omit the attribute); its assembly is dynamic (<see cref="DispatchProxy"/>'s own
/// emitted proxy, Castle DynamicProxy mocks); or it is a <see cref="DispatchProxy"/> itself (an
/// already-proxied instance, which would be traced twice).
/// </remarks>
internal static class GeneratedTypes
{
    public static bool IsGenerated(Type type)
    {
        return type.Assembly.IsDynamic
            || typeof(DispatchProxy).IsAssignableFrom(type)
            || HasGeneratedName(type)
            || HasGeneratedEnclosure(type);
    }

    private static bool HasGeneratedName(Type type)
    {
        return type.Name.IndexOf('<') >= 0;
    }

    private static bool HasGeneratedEnclosure(Type type)
    {
        for (var current = type; current is not null; current = current.DeclaringType)
        {
            if (current.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false))
            {
                return true;
            }
        }

        return false;
    }
}
