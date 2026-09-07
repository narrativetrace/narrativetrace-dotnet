// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Clarity;
using Xunit;

namespace NarrativeTrace.Clarity.Tests;

// Sample members below are reachable only via reflection under scan, so the
// compiler-visible "unused private member" rule does not apply to them.
#pragma warning disable S1144

public sealed class ClarityScannerTests
{
    [Fact]
    public void Scan_produces_a_result_keyed_by_type_name()
    {
        var results = ClarityScanner.Scan([typeof(ReservationService)]);

        Assert.True(results.ContainsKey(nameof(ReservationService)));
    }

    [Fact]
    public void Type_whose_only_surface_is_a_property_is_still_scored()
    {
        // Flipped 2026-08-25 (owner decision): clarity scores the domain
        // vocabulary the author wrote, and a property-only DTO is exactly
        // where bad domain names hide. This previously asserted absence.
        var results = ClarityScanner.Scan([typeof(OpaqueValue)]);

        Assert.True(results.ContainsKey(nameof(OpaqueValue)));
    }

    [Fact]
    public void A_weak_property_name_is_reported_as_a_property_issue()
    {
        var results = ClarityScanner.Scan([typeof(OpaqueData)]);

        var result = results[nameof(OpaqueData)];
        Assert.Contains(result.Issues, i => i.Category == "property-name");
    }

    [Fact]
    public void Indexers_are_not_scored_as_properties()
    {
        var results = ClarityScanner.Scan([typeof(Basket)]);

        Assert.DoesNotContain(
            results.TryGetValue(nameof(Basket), out var r)
                ? r.Issues
                : [],
            i => i.Element.EndsWith(".Item", StringComparison.Ordinal));
    }

    [Fact]
    public void Parameter_names_flow_into_scoring()
    {
        var clear = ClarityScanner.Scan([typeof(ClearParams)])[nameof(ClearParams)];
        var cryptic = ClarityScanner.Scan([typeof(CrypticParams)])[nameof(CrypticParams)];

        Assert.True(
            clear.Parameter > cryptic.Parameter,
            $"clear={clear.Parameter} should beat cryptic={cryptic.Parameter}");
    }

    [Fact]
    public void Record_is_scored_on_its_components_not_its_generated_members()
    {
        // Flipped 2026-08-25 (owner decision). This previously asserted the
        // record was skipped entirely: correct while properties were not
        // narrative surface, since every remaining member was synthesized.
        // Components now resurface as noun-scored properties, which is also
        // what Java does — closing a divergence between the two runtimes.
        var results = ClarityScanner.Scan([typeof(TravelExpense)]);

        Assert.True(results.ContainsKey(nameof(TravelExpense)));
        Assert.DoesNotContain(
            results[nameof(TravelExpense)].Issues,
            i => i.Element.EndsWith(".Equals", StringComparison.Ordinal)
                || i.Element.EndsWith(".Deconstruct", StringComparison.Ordinal));
    }

    private sealed record TravelExpense(string Description, string Payer);

    [Fact]
    public void Record_is_scored_only_on_the_methods_its_author_wrote()
    {
        var issues = ClarityScanner.Scan([typeof(LedgerEntry)])[nameof(LedgerEntry)].Issues;

        // Process is a real action and keeps the verb+noun standard; the
        // generated Equals(LedgerEntry) must raise nothing at all.
        Assert.Contains(issues, issue => issue.Element == "LedgerEntry.Process");
        Assert.DoesNotContain(issues, issue => issue.Element == "LedgerEntry.Equals");
    }

    private sealed record LedgerEntry(string Payer)
    {
        public void Process()
        {
            // Method intentionally left empty.
        }
    }

    [Fact]
    public void Record_struct_generated_members_are_never_scored()
    {
        // A record struct gets Deconstruct and Equals(T) but no <Clone>$, so
        // sniffing for the clone member would miss it; the generated-member
        // check has to be per-method, not per-type. Flipped 2026-08-25: the
        // type is now scored on its components rather than skipped, but the
        // generated members must still contribute nothing.
        var results = ClarityScanner.Scan([typeof(Money)]);

        Assert.DoesNotContain(
            results[nameof(Money)].Issues,
            i => i.Element.EndsWith(".Equals", StringComparison.Ordinal)
                || i.Element.EndsWith(".Deconstruct", StringComparison.Ordinal));
    }

    private readonly record struct Money(decimal Amount, string Currency);

    [Fact]
    public void Hand_written_equality_and_deconstruct_members_are_still_scored()
    {
        var results = ClarityScanner.Scan([typeof(ManualComparableValue)]);

        // Only the compiler's own members are exempt. A type that writes these
        // by hand chose those names and is still answerable for them.
        Assert.True(results.ContainsKey(nameof(ManualComparableValue)));
    }

    private sealed class ManualComparableValue : IEquatable<ManualComparableValue>
    {
        private readonly string _payer = "";

        public bool Equals(ManualComparableValue? other)
        {
            return other is not null && other._payer == _payer;
        }

        public override bool Equals(object? obj)
        {
            return Equals(obj as ManualComparableValue);
        }

        public override int GetHashCode()
        {
            return _payer.GetHashCode(StringComparison.Ordinal);
        }

        public void Deconstruct(out string payer)
        {
            payer = _payer;
        }
    }

    // Sample types under scan — public methods drive the synthetic trace.
    private sealed class ReservationService
    {
        public void ReserveRoom(string guestName, int roomNumber)
        {
            // Method intentionally left empty.
        }
    }

    private sealed class ClearParams
    {
        public void CalculateInvoiceTotal(decimal unitPrice, int quantity)
        {
            // Method intentionally left empty.
        }
    }

    private sealed class CrypticParams
    {
        public void CalculateInvoiceTotal(decimal a, int b)
        {
            // Method intentionally left empty.
        }
    }

    private sealed class OpaqueData
    {
        public string Data { get; set; } = "";
    }

    private sealed class Basket
    {
        public int this[int index] => index;
    }

    private sealed class OpaqueValue
    {
        public int Amount { get; set; }

        public override string ToString()
        {
            return "";
        }

        public override bool Equals(object? obj)
        {
            return false;
        }

        public override int GetHashCode()
        {
            return 0;
        }

        private void Helper()
        {
            // Method intentionally left empty.
        }
    }
}
