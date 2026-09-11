// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using Xunit;

namespace NarrativeTrace.Core.Tests;

/// <summary>
/// The .NET mirror of java's <c>RedactionDefaultsGapTest.NationalIdShapesTest</c>.
/// National identity numbers join the value axis, checksum-verified only —
/// never a length-and-digits guess — and
/// language-neutral by construction: a checksum does not care what language
/// the field name is in, which is exactly why this backstops the name-axis
/// deny-list.
/// </summary>
public class NationalIdShapesTests
{
    // Three of the CPFs below exist for the "remainder < 2" rule rather than
    // for Brazil: a check digit computed from remainder 0, 1 and 2 pins each
    // side of it, and without them the rule could be "<= 2", or absent, and
    // every case here would still pass.
    [Theory]
    [InlineData("12.345.678-5")] // Chile, RUT, dotted
    [InlineData("12345678-5")] // Chile, RUT, plain
    [InlineData("1234567-4")] // Chile, seven-digit body
    [InlineData("10000013-K")] // Chile, verifier 10 is written K
    [InlineData("10000004-0")] // Chile, verifier 11 is written 0
    [InlineData("529.982.247-25")] // Brazil, CPF, formatted
    [InlineData("52998224725")] // Brazil, CPF, bare
    [InlineData("11144477735")] // Brazil, CPF, second example
    [InlineData("75749118606")] // Brazil, CPF whose first check digit comes from remainder 1
    [InlineData("99603082430")] // Brazil, CPF whose second check digit comes from remainder 0
    [InlineData("99351819019")] // Brazil, CPF whose second check digit comes from remainder 2
    [InlineData("11.222.333/0001-81")] // Brazil, CNPJ, formatted
    [InlineData("11222333000181")] // Brazil, CNPJ, bare
    [InlineData("12345678Z")] // Spain, DNI
    [InlineData("12345678-Z")] // Spain, DNI, hyphenated
    [InlineData("X1234567L")] // Spain, NIE, X prefix
    [InlineData("Y1234567X")] // Spain, NIE, Y prefix
    [InlineData("Z1234567R")] // Spain, NIE, Z prefix
    [InlineData("184127645108946")] // France, NIR
    [InlineData("1 84 12 76 451 089 46")] // France, NIR, spaced as it is printed
    [InlineData("175032A12345606")] // France, NIR, Corsica 2A in the department position
    [InlineData("180022B75123469")] // France, NIR, Corsica 2B in the department position
    [InlineData("11010519491231002X")] // China, resident id, X check character
    [InlineData("440301199001010012")] // China, resident id, numeric check character
    [InlineData("11010521001231003X")] // China, birth year exactly at the upper bound of the range
    [InlineData("123-45-6789")] // US, SSN, ordinary
    [InlineData("001-01-0001")] // US, SSN, lowest area/group/serial that are each still valid
    [InlineData("899-99-9999")] // US, SSN, highest area below the 900-999 exclusion
    public void A_national_identity_number_is_masked_whatever_the_field_is_called(string value)
    {
        Assert.True(RedactionPolicy.Default.ShouldRedactValue(value));
    }

    [Theory]
    [InlineData("52998224726")] // CPF with the last check digit wrong
    [InlineData("11222333000182")] // CNPJ with the last check digit wrong
    [InlineData("12345678-6")] // RUT with the wrong verifier
    [InlineData("12345678A")] // DNI with the wrong check letter
    [InlineData("X1234567A")] // NIE with the wrong check letter
    [InlineData("184127645108947")] // NIR with the wrong key
    [InlineData("110105194912310021")] // Chinese id with the wrong check character
    [InlineData("110105194913320019")] // Chinese id, valid checksum, thirteenth month
    [InlineData("123456785")] // a nine-digit order number: a RUT without its verifier separator
    [InlineData("12345678901")] // an eleven-digit reference that is not a CPF
    [InlineData("987654321")] // an ordinary invoice number
    [InlineData("2026-09-02")] // a date, which is digits and a separator too
    [InlineData("123456789")] // an SSN's nine digits, bare — no dash, no evidence
    [InlineData("12-345-6789")] // SSN digits, wrongly grouped 2-3-4
    [InlineData("123-456-789")] // SSN digits, wrongly grouped 3-3-3
    [InlineData("123-45-678")] // SSN shape, serial one digit short
    [InlineData("123-45-67890")] // SSN shape, serial one digit long
    public void A_lookalike_that_fails_its_checksum_stays_visible(string value)
    {
        Assert.False(RedactionPolicy.Default.ShouldRedactValue(value));
    }

    // The SSA's own structural rules stand in for the SSN's missing check
    // digit: these combinations are never issued, so rejecting them costs
    // nothing real -- and rejecting them is what keeps 000-00-0000, the
    // placeholder filling test fixtures and redacted forms everywhere,
    // visible rather than blanked.
    [Theory]
    [InlineData("000-12-3456")] // area 000
    [InlineData("666-12-3456")] // area 666
    [InlineData("900-12-3456")] // area in the 900-999 exclusion, low end
    [InlineData("999-12-3456")] // area in the 900-999 exclusion, high end
    [InlineData("123-00-4567")] // group 00
    [InlineData("123-45-0000")] // serial 0000
    [InlineData("000-00-0000")] // the placeholder: every rule fires at once
    public void A_structurally_invalid_ssn_stays_visible(string value)
    {
        Assert.False(RedactionPolicy.Default.ShouldRedactValue(value));
    }

    // The entire point of the value axis: taxpayerRef matches no name-deny-list
    // pattern (unlike ssn/socialsecuritynumber), yet a real SSN inside it is
    // still masked -- caught by shape, not by name.
    [Fact]
    public void A_valid_ssn_in_an_innocuously_named_field_is_masked()
    {
        var record = new TaxRecord("123-45-6789");

        var output = ValueRenderer.Render(record);

        Assert.Contains("TaxpayerRef: [REDACTED]", output);
        Assert.DoesNotContain("123-45-6789", output);
    }

    // The over-redaction guard: an ordinary nine-digit order number, the exact
    // digit count an SSN carries, renders in full through the real renderer.
    [Fact]
    public void An_ordinary_nine_digit_order_number_renders_visible()
    {
        var record = new TaxRecord("123456789");

        var output = ValueRenderer.Render(record);

        Assert.Contains("TaxpayerRef: \"123456789\"", output);
    }

    private sealed record TaxRecord(string TaxpayerRef);

    // The birth date embedded at positions 7-14 is most of what separates a
    // Chinese resident id from any eighteen-digit number: the check character
    // alone lets one in eleven through.
    [Theory]
    [InlineData("110105189912310007")] // before the earliest plausible birth year
    [InlineData("110105210101010004")] // after the latest
    [InlineData("110105199000010019")] // month 00
    [InlineData("110105199001000007")] // day 00
    [InlineData("110105199001320000")] // day 32
    public void An_eighteen_digit_number_with_an_impossible_birth_date_is_not_a_resident_id(string value)
    {
        Assert.False(RedactionPolicy.Default.ShouldRedactValue(value));
    }

    // Every repeated-digit string satisfies both CPF check digits, and none
    // of them is a document -- they are what a form writes when it has none.
    [Theory]
    [InlineData("11111111111")]
    [InlineData("22222222222")]
    [InlineData("99999999999")]
    public void A_repeated_digit_placeholder_is_not_a_cpf(string value)
    {
        Assert.False(RedactionPolicy.Default.ShouldRedactValue(value));
    }

    [Fact]
    public void A_disabled_policy_masks_no_national_id()
    {
        Assert.False(RedactionPolicy.Disabled.ShouldRedactValue("52998224725"));
        Assert.False(RedactionPolicy.Disabled.ShouldRedactValue("11010519491231002X"));
    }
}
