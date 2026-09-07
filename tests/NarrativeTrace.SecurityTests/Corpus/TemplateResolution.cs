// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Reflection;
using NarrativeTrace.Proxy;

namespace NarrativeTrace.SecurityTests.Corpus;

/// <summary>
/// Drives <see cref="NarrationResolver"/> with a runtime template string, the way the hostile
/// corpus needs and a compile-time <c>[Narrated("...")]</c> attribute constant cannot.
/// </summary>
/// <remarks>
/// <para>
/// INTENT: <see cref="NarrationResolver.Resolve"/> takes a real <see cref="ParameterInfo"/> array,
/// not a name/value map — it reads a parameter's own <c>[NotTraced]</c> attribute as part of the
/// redaction decision. <see cref="IFixtureShapes"/> exists purely so reflection can hand it one: a
/// private interface with one method per <see cref="HostileGraphs.TemplateValues"/> fixture name,
/// its single parameter named to match the corpus's root segment (<c>card</c>, <c>user</c>, …).
/// </para>
/// <para>
/// @edgeCase Every corpus template path is written in Java's javaBean casing (<c>card.cvv</c>,
/// <c>user.password</c>) because the corpus is copied verbatim. This runtime's placeholder grammar
/// (<c>NarrationResolver</c>) matches a property name by exact case, and every fixture record here
/// (<see cref="HostileGraphs.Card"/>, <see cref="HostileGraphs.Credentials"/>, …) uses ordinary C#
/// PascalCase — <c>Cvv</c>, not <c>cvv</c>. So none of the corpus's lowercase paths resolve against
/// these fixtures at all: the placeholder survives literally, which trivially satisfies "no leak"
/// without exercising redaction. This is the same class of structural consequence as this runtime's
/// documented one-level-path restriction, not a defect — a .NET author naturally writes
/// <c>{card.Cvv}</c>, matching their own property, not a JavaBean getter name. See
/// <c>TemplateRedactionPropertyTests</c> for how the property tests separate "never leaks" (true of
/// every corpus case, resolved or not) from "must show the marker" (asserted only where this runtime's
/// actual case-sensitive, one-level grammar resolves the path).
/// </para>
/// </remarks>
internal static class TemplateResolution
{
    private static readonly IReadOnlyDictionary<string, ParameterInfo[]> ParametersByFixture =
        new Dictionary<string, ParameterInfo[]>
        {
            ["card"] = ParametersOf(nameof(IFixtureShapes.Card)),
            ["user"] = ParametersOf(nameof(IFixtureShapes.User)),
            ["order"] = ParametersOf(nameof(IFixtureShapes.Order)),
            ["deep"] = ParametersOf(nameof(IFixtureShapes.Deep)),
            ["unicode"] = ParametersOf(nameof(IFixtureShapes.Unicode)),
            ["wide"] = ParametersOf(nameof(IFixtureShapes.Wide)),
            ["chain"] = ParametersOf(nameof(IFixtureShapes.Chain)),
            ["password-scalar"] = ParametersOf(nameof(IFixtureShapes.PasswordScalar)),
            ["jwt-scalar"] = ParametersOf(nameof(IFixtureShapes.JwtScalar)),
            ["newline-scalar"] = ParametersOf(nameof(IFixtureShapes.NewlineScalar)),
        };

    private static readonly ParameterInfo[] ValueParameter = ParametersOf(nameof(IFixtureShapes.Value));

    /// <summary>Resolves <paramref name="template"/> against the named fixture, planting <paramref name="sentinel"/>.</summary>
    /// <param name="fixtureName">One of <see cref="HostileGraphs.TemplateValues"/>'s keys, or <see langword="null"/> for <c>card</c>.</param>
    public static string? Resolve(string template, string? fixtureName, string sentinel)
    {
        var key = fixtureName ?? "card";
        var values = HostileGraphs.TemplateValues(key, sentinel);
        var args = new object?[] { values.Values.Single() };
        return NarrationResolver.Resolve(template, args, ParametersByFixture[key]);
    }

    /// <summary>
    /// Resolves a bare <c>{value}</c> placeholder against an arbitrary value — for pinning what a
    /// placeholder does with a value that carries nothing redacted, outside the five corpus fixtures.
    /// </summary>
    public static string? ResolveValue(object value)
    {
        return NarrationResolver.Resolve("{value}", [value], ValueParameter);
    }

    private static ParameterInfo[] ParametersOf(string methodName) =>
        typeof(IFixtureShapes).GetMethod(methodName)!.GetParameters();

    /// <summary>Reflection-only: never implemented, never called. See the class remarks.</summary>
    private interface IFixtureShapes
    {
        void Card(object card);

        void User(object user);

        void Order(object order);

        void Deep(object a);

        void Unicode(object café);

        void Wide(object wide);

        void Chain(object chain);

        void Value(object value);

        /// <summary>The bare-scalar-placeholder fixture — the key IS the parameter name.</summary>
        void PasswordScalar(object password);

        /// <summary>A credential-shaped scalar under a name no deny-list knows.</summary>
        void JwtScalar(object value);

        /// <summary>A scalar carrying a raw newline instead of a secret.</summary>
        void NewlineScalar(object comment);
    }
}
