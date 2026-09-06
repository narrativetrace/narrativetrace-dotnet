// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using NarrativeTrace.Glossary;
using Xunit;

namespace NarrativeTrace.Glossary.Tests;

/// <summary>
/// Mirrors Java's <c>ClassPackageIndexTest</c>. Java writes empty
/// <c>.class</c> files into a temp directory to control the package layout;
/// .NET has no per-type file, so the fixtures are emitted into a dynamic
/// assembly instead — the same control over simple name and namespace, by the
/// platform's own means.
/// </summary>
public sealed class ClassPackageIndexTests
{
    private readonly ModuleBuilder module = DynamicModule();

    [Fact]
    public void Resolves_simple_type_name_to_its_namespace()
    {
        DefineType("Acme.Billing.OverdraftService");

        var index = ClassPackageIndex.FromAssemblies([module.Assembly]);

        Assert.Equal("Acme.Billing", index.NamespaceOf("OverdraftService"));
    }

    [Fact]
    public void Ambiguous_simple_name_resolves_to_unknown()
    {
        DefineType("Acme.Billing.Account");
        DefineType("Acme.Inventory.Account");

        var index = ClassPackageIndex.FromAssemblies([module.Assembly]);

        Assert.Null(index.NamespaceOf("Account"));
    }

    [Fact]
    public void Skips_module_and_compiler_generated_types()
    {
        DefineType("Acme.Billing.<>c__DisplayClass0_0");
        DefineType("Acme.Billing.Closure", compilerGenerated: true);

        var index = ClassPackageIndex.FromAssemblies([module.Assembly]);

        Assert.Null(index.NamespaceOf("<>c__DisplayClass0_0"));
        Assert.Null(index.NamespaceOf("<Module>"));
        Assert.Null(index.NamespaceOf("Closure"));
    }

    [Fact]
    public void Nested_type_indexes_under_its_inner_simple_name()
    {
        DefineType("Acme.Billing.OverdraftService").DefineNestedType(
            "Builder", TypeAttributes.NestedPublic).CreateTypeInfo();

        var index = ClassPackageIndex.FromAssemblies([module.Assembly]);

        Assert.Equal("Acme.Billing", index.NamespaceOf("Builder"));
    }

    /// <summary>
    /// Mirror of Java's <c>fromClasspathKeepsOnlyExistingDirectories</c>: the
    /// discovery factory indexes the application's own output and nothing
    /// else, so a framework type's simple name stays unknown.
    /// </summary>
    [Fact]
    public void From_loaded_assemblies_indexes_the_applications_own_output_only()
    {
        var index = ClassPackageIndex.FromLoadedAssemblies();

        Assert.Equal(
            "NarrativeTrace.Glossary.Tests",
            index.NamespaceOf(nameof(ClassPackageIndexTests)));
        Assert.Equal("NarrativeTrace.Glossary", index.NamespaceOf("ContextResolver"));
        Assert.Null(index.NamespaceOf("StringBuilder"));
    }

    [Fact]
    public void Global_namespace_type_resolves_to_the_empty_namespace()
    {
        DefineType("Standalone");

        var index = ClassPackageIndex.FromAssemblies([module.Assembly]);

        Assert.Equal(string.Empty, index.NamespaceOf("Standalone"));
    }

    [Fact]
    public void Unknown_name_resolves_to_null()
    {
        var index = ClassPackageIndex.FromAssemblies([module.Assembly]);

        Assert.Null(index.NamespaceOf("NeverSeen"));
    }

    [Fact]
    public void Guards_reject_null_arguments()
    {
        var index = ClassPackageIndex.FromAssemblies([module.Assembly]);

        Assert.Throws<ArgumentNullException>(
            () => ClassPackageIndex.FromAssemblies(null!));
        Assert.Throws<ArgumentNullException>(() => { index.NamespaceOf(null!); });
    }

    [Fact]
    public void Invariant_holds_for_constructed_index()
    {
        DefineType("Acme.Billing.OverdraftService");
        DefineType("Acme.Billing.Account");
        DefineType("Acme.Inventory.Account");

        var index = ClassPackageIndex.FromAssemblies([module.Assembly]);

        Assert.True(index.Invariant());
    }

    /// <summary>A type whose namespace and simple name the fixture chooses.</summary>
    private TypeBuilder DefineType(string fullName, bool compilerGenerated = false)
    {
        var type = module.DefineType(fullName, TypeAttributes.Public);
        if (compilerGenerated)
        {
            type.SetCustomAttribute(new CustomAttributeBuilder(
                typeof(CompilerGeneratedAttribute).GetConstructor(Type.EmptyTypes)!,
                []));
        }

        type.CreateTypeInfo();
        return type;
    }

    private static ModuleBuilder DynamicModule()
    {
        return AssemblyBuilder
            .DefineDynamicAssembly(
                new AssemblyName($"ClassPackageIndexFixtures{Guid.NewGuid():N}"),
                AssemblyBuilderAccess.Run)
            .DefineDynamicModule("fixtures");
    }
}
