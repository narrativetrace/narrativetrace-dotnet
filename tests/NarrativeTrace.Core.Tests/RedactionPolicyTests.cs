// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using Xunit;

namespace NarrativeTrace.Core.Tests;

public class RedactionPolicyTests
{
    [Fact]
    public void Default_redacts_exact_sensitive_names()
    {
        Assert.True(RedactionPolicy.Default.ShouldRedact("password"));
        Assert.True(RedactionPolicy.Default.ShouldRedact("cvv"));
        Assert.True(RedactionPolicy.Default.ShouldRedact("authorization"));
    }

    [Fact]
    public void Default_redacts_sensitive_names_embedded_as_substrings()
    {
        Assert.True(RedactionPolicy.Default.ShouldRedact("userPassword"));
        Assert.True(RedactionPolicy.Default.ShouldRedact("cardCvv"));
        Assert.True(
            RedactionPolicy.Default.ShouldRedact("accessTokenExpiry"));
    }

    [Fact]
    public void Default_matching_is_case_insensitive()
    {
        Assert.True(RedactionPolicy.Default.ShouldRedact("PASSWORD"));
        Assert.True(RedactionPolicy.Default.ShouldRedact("Cvv"));
    }

    [Fact]
    public void Default_does_not_redact_benign_names_sharing_a_prefix()
    {
        // "author" must not match "authorization"; "username" matches none.
        Assert.False(RedactionPolicy.Default.ShouldRedact("author"));
        Assert.False(RedactionPolicy.Default.ShouldRedact("username"));
        Assert.False(RedactionPolicy.Default.ShouldRedact("broken"));
    }

    [Fact]
    public void Null_field_name_is_not_redacted()
    {
        Assert.False(RedactionPolicy.Default.ShouldRedact(null));
    }

    [Fact]
    public void Disabled_policy_redacts_nothing()
    {
        Assert.False(RedactionPolicy.Disabled.ShouldRedact("password"));
        Assert.False(RedactionPolicy.Disabled.ShouldRedact("cvv"));
    }

    [Fact]
    public void Custom_patterns_replace_the_defaults_entirely()
    {
        var policy = RedactionPolicy.OfPatterns(["ssn"]);

        Assert.True(policy.ShouldRedact("taxSsn"));
        Assert.False(policy.ShouldRedact("password"));
    }

    [Fact]
    public void Custom_patterns_are_matched_case_insensitively()
    {
        var policy = RedactionPolicy.OfPatterns(["PIN"]);

        Assert.True(policy.ShouldRedact("cardpin"));
    }

    [Fact]
    public void IsRedacted_is_true_when_annotated_even_if_the_name_matches_nothing()
    {
        Assert.True(RedactionPolicy.Default.IsRedacted("balance", annotated: true));
    }

    [Fact]
    public void IsRedacted_is_true_when_the_name_matches_even_if_not_annotated()
    {
        Assert.True(RedactionPolicy.Default.IsRedacted("cvv", annotated: false));
    }

    [Fact]
    public void IsRedacted_is_false_when_neither_annotated_nor_name_matched()
    {
        Assert.False(RedactionPolicy.Default.IsRedacted("balance", annotated: false));
    }

    [Fact]
    public void IsRedacted_honors_annotation_even_under_a_disabled_name_policy()
    {
        Assert.True(RedactionPolicy.Disabled.IsRedacted("balance", annotated: true));
    }

    [Theory]
    [InlineData("cardNumber")]
    [InlineData("card_number")]
    [InlineData("jwt")]
    [InlineData("cookie")]
    [InlineData("setCookie")]
    [InlineData("set_cookie")]
    [InlineData("sessionId")]
    [InlineData("session_id")]
    [InlineData("accountNumber")]
    [InlineData("account_number")]
    [InlineData("routingNumber")]
    [InlineData("routing_number")]
    public void Default_redacts_the_widened_name_list(string fieldName)
    {
        Assert.True(RedactionPolicy.Default.ShouldRedact(fieldName));
    }

    [Theory]
    [InlineData("pan")]
    [InlineData("PAN")]
    [InlineData("cardPan")]
    [InlineData("card_pan")]
    [InlineData("iban")]
    [InlineData("IBAN")]
    [InlineData("ibanNumber")]
    [InlineData("IbAn")]
    public void Default_redacts_pan_and_iban_on_token_boundaries(string fieldName)
    {
        Assert.True(RedactionPolicy.Default.ShouldRedact(fieldName));
    }

    [Theory]
    [InlineData("companyName")]
    [InlineData("expansionRatio")]
    [InlineData("panelId")]
    [InlineData("spanCount")]
    [InlineData("planId")]
    [InlineData("japaneseAddress")]
    public void Default_does_not_redact_pan_or_iban_as_a_bare_substring(string fieldName)
    {
        Assert.False(RedactionPolicy.Default.ShouldRedact(fieldName));
    }

    [Fact]
    public void Custom_pattern_of_pan_still_respects_token_boundaries()
    {
        var policy = RedactionPolicy.OfPatterns(["pan"]);

        Assert.True(policy.ShouldRedact("cardPan"));
        Assert.False(policy.ShouldRedact("companyName"));
    }

    [Fact]
    public void ShouldRedactValue_matches_a_jwt_shaped_string()
    {
        const string jwt =
            "eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxMjM0NTY3ODkwIn0.dGhpc2lzYXNpZ25hdHVyZQ";

        Assert.True(RedactionPolicy.Default.ShouldRedactValue(jwt));
    }

    [Fact]
    public void ShouldRedactValue_does_not_match_a_dotted_hostname_or_bare_dotted_value()
    {
        Assert.False(RedactionPolicy.Default.ShouldRedactValue("a.b.c"));
        Assert.False(RedactionPolicy.Default.ShouldRedactValue("api.example.com"));
    }

    [Fact]
    public void ShouldRedactValue_matches_a_luhn_valid_card_number()
    {
        Assert.True(RedactionPolicy.Default.ShouldRedactValue("4111111111111111"));
        Assert.True(RedactionPolicy.Default.ShouldRedactValue("4111 1111 1111 1111"));
        Assert.True(RedactionPolicy.Default.ShouldRedactValue("4111-1111-1111-1111"));
    }

    [Fact]
    public void ShouldRedactValue_leaves_a_luhn_invalid_number_visible()
    {
        // One digit off from the pinned-valid card above — deliberately not a
        // real PAN, and the required negative: an order number that happens
        // to be card-length but fails Luhn must not be masked.
        Assert.False(RedactionPolicy.Default.ShouldRedactValue("4111111111111112"));
    }

    [Theory]
    [InlineData("12345")]
    [InlineData("2026-09-02")]
    [InlineData("999999999999999999999")]
    public void ShouldRedactValue_leaves_non_card_length_numbers_visible(string value)
    {
        Assert.False(RedactionPolicy.Default.ShouldRedactValue(value));
    }

    [Fact]
    public void ShouldRedactValue_matches_a_set_cookie_header_value()
    {
        Assert.True(RedactionPolicy.Default.ShouldRedactValue(
            "sessionToken=abc123; Path=/; HttpOnly; Secure"));
    }

    [Fact]
    public void ShouldRedactValue_leaves_an_ordinary_key_value_pair_visible()
    {
        Assert.False(RedactionPolicy.Default.ShouldRedactValue("key=value"));
        Assert.False(RedactionPolicy.Default.ShouldRedactValue("name=Ada; age=36"));
    }

    [Fact]
    public void ShouldRedactValue_is_off_under_the_disabled_policy()
    {
        Assert.False(RedactionPolicy.Disabled.ShouldRedactValue("4111111111111111"));
    }

    [Fact]
    public void ShouldRedactValue_stays_on_under_a_custom_name_policy()
    {
        var policy = RedactionPolicy.OfPatterns(["ssn"]);

        Assert.True(policy.ShouldRedactValue("4111111111111111"));
    }

    [Fact]
    public void ShouldRedactValue_null_is_not_redacted()
    {
        Assert.False(RedactionPolicy.Default.ShouldRedactValue(null));
    }

    // Mirrors java's multilingual sensitive-field vocabulary and its
    // subsequent narrowing: es/pt/fr/zh field names, always on, no locale to
    // select. See java's RedactionPolicy.DEFAULT_PATTERNS/WORD_PATTERNS.
    [Theory]
    [InlineData("contrasena")]
    [InlineData("contraseña")]
    [InlineData("tarjeta")]
    [InlineData("cedula")]
    [InlineData("cédula")]
    [InlineData("claveAcceso")]
    [InlineData("clave_acceso")]
    [InlineData("claveSecreta")]
    [InlineData("clave_secreta")]
    [InlineData("cartao")]
    [InlineData("cartão")]
    [InlineData("motDePasse")]
    [InlineData("mot_de_passe")]
    [InlineData("carteBancaire")]
    [InlineData("carte_bancaire")]
    [InlineData("numeroCarte")]
    [InlineData("numero_carte")]
    [InlineData("shenfenzheng")]
    public void Default_redacts_the_multilingual_name_list(string fieldName)
    {
        Assert.True(RedactionPolicy.Default.ShouldRedact(fieldName));
    }

    [Fact]
    public void Default_redacts_the_chinese_word_patterns_verbatim()
    {
        Assert.True(RedactionPolicy.Default.ShouldRedact("密码"));
        Assert.True(RedactionPolicy.Default.ShouldRedact("身份证"));
    }

    // The narrowing above: bare "clave"/"carte" were narrowed away — they
    // collided with clavePrimaria/claveForanea and carteGraphique/
    // carteRoutiere, ordinary business fields, not credentials.
    [Theory]
    [InlineData("clavePrimaria")]
    [InlineData("claveForanea")]
    [InlineData("carteGraphique")]
    [InlineData("carteRoutiere")]
    public void Default_does_not_redact_narrowed_bare_clave_or_carte(string fieldName)
    {
        Assert.False(RedactionPolicy.Default.ShouldRedact(fieldName));
    }

    // The short non-English words are token-boundary-only, matching pan/iban's
    // existing treatment — a substring rule would blank ordinary words that
    // merely contain them.
    [Theory]
    [InlineData("rut")]
    [InlineData("RUT")]
    [InlineData("cuit")]
    [InlineData("dni")]
    [InlineData("senha")]
    [InlineData("cpf")]
    [InlineData("cnpj")]
    [InlineData("nir")]
    [InlineData("mima")]
    public void Default_redacts_the_new_word_patterns_on_token_boundaries(string fieldName)
    {
        Assert.True(RedactionPolicy.Default.ShouldRedact(fieldName));
    }

    [Theory]
    [InlineData("truth")]
    [InlineData("brute")]
    [InlineData("scrutiny")]
    [InlineData("circuit")]
    [InlineData("biscuit")]
    [InlineData("midnight")]
    [InlineData("nirvana")]
    [InlineData("semiMajorAxis")]
    [InlineData("chosenHash")]
    [InlineData("frozenHash")]
    public void Default_does_not_redact_ordinary_words_containing_a_new_word_pattern(string fieldName)
    {
        Assert.False(RedactionPolicy.Default.ShouldRedact(fieldName));
    }

    // SecretValueShapes.Canonical: NFD accent fold, applied to both the pattern
    // and the field name, so the accented and unaccented spellings are the same
    // pattern rather than two.
    [Fact]
    public void Accented_and_unaccented_spellings_of_a_name_match_alike()
    {
        Assert.True(RedactionPolicy.Default.ShouldRedact("contraseñaUsuario"));
        Assert.True(RedactionPolicy.Default.ShouldRedact("contrasenaUsuario"));
    }

    // The operator-widening knob: additive, never a replacement for the
    // defaults, sourced from the environment (the .NET-native override
    // channel; see AdditionalRedactionPatterns and ConfigResolver).
    [Fact]
    public void Additional_patterns_from_the_environment_extend_the_default_policy()
    {
        var policy = RedactionPolicy.DefaultsFor(["betalingskort"]);

        Assert.True(policy.ShouldRedact("betalingskort"));
        Assert.True(policy.ShouldRedact("password"), "the defaults must still apply");
    }

    [Fact]
    public void Additional_patterns_extend_a_custom_ofPatterns_policy_too()
    {
        var policy = RedactionPolicy.OfPatternsFor(["ssn"], ["betalingskort"]);

        Assert.True(policy.ShouldRedact("betalingskort"));
        Assert.True(policy.ShouldRedact("ssn"));
        Assert.False(policy.ShouldRedact("password"), "OfPatterns still replaces the built-in vocabulary");
    }

    [Fact]
    public void No_additional_patterns_leaves_the_default_policy_unchanged()
    {
        var policy = RedactionPolicy.DefaultsFor([]);

        Assert.True(policy.ShouldRedact("password"));
        Assert.False(policy.ShouldRedact("betalingskort"));
    }

    // Cross-runtime audit (2026-09): these terms were absent from all five
    // runtimes' deny-lists. Substring-matched like the rest of DefaultPatterns
    // (not token-boundary — that list is reserved for short words that
    // collide with ordinary business terms, see the class remarks).
    [Theory]
    [InlineData("passphrase")]
    [InlineData("otp")]
    [InlineData("bearer")]
    [InlineData("accesskey")]
    [InlineData("access_key")]
    [InlineData("socialsecurity")]
    [InlineData("social_security")]
    [InlineData("socialsecuritynumber")]
    [InlineData("taxid")]
    [InlineData("tax_id")]
    [InlineData("passwort")]
    [InlineData("kennwort")]
    public void Default_redacts_the_2026_09_widened_terms(string fieldName)
    {
        Assert.True(RedactionPolicy.Default.ShouldRedact(fieldName));
    }

    [Theory]
    [InlineData("userPassphrase")]
    [InlineData("otpCode")]
    [InlineData("bearerCode")]
    [InlineData("accessKeyId")]
    [InlineData("access_key_id")]
    [InlineData("mySocialSecurityNumber")]
    [InlineData("taxIdNumber")]
    [InlineData("userPasswort")]
    [InlineData("meinKennwort")]
    public void Default_redacts_the_2026_09_widened_terms_as_substrings(string fieldName)
    {
        Assert.True(RedactionPolicy.Default.ShouldRedact(fieldName));
    }
}
