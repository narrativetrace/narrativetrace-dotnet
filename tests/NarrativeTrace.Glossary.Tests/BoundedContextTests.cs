// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Glossary;
using Xunit;

namespace NarrativeTrace.Glossary.Tests;

public class BoundedContextTests
{
    [Fact]
    public void Holds_name_packages_and_description()
    {
        var context = new BoundedContext(
            "billing", ["Acme.Billing"], "Charging, invoicing, funds");

        Assert.Equal("billing", context.Name);
        Assert.Equal(["Acme.Billing"], context.Packages);
        Assert.Equal("Charging, invoicing, funds", context.Description);
    }

    [Fact]
    public void Empty_package_list_and_null_description_are_valid()
    {
        var context = new BoundedContext("_unassigned", []);

        Assert.Empty(context.Packages);
        Assert.Null(context.Description);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Blank_name_is_rejected(string? name)
    {
        Assert.Throws<ArgumentException>(() => new BoundedContext(name!, []));
    }

    [Fact]
    public void Null_package_list_is_rejected()
    {
        Assert.Throws<ArgumentNullException>(() => new BoundedContext("billing", null!));
    }

    [Fact]
    public void Packages_are_copied_defensively()
    {
        var packages = new List<string> { "Acme.Billing" };
        var context = new BoundedContext("billing", packages);

        packages.Add("Acme.Sales");

        Assert.Equal(["Acme.Billing"], context.Packages);
    }
}
