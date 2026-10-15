// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Tooling.Feedback;
using Xunit;

namespace NarrativeTrace.Tooling.Tests.Feedback;

/// <summary>
/// A refusal quotes its rule's id and reason to a person who must fix the text, so each is pinned whole.
/// </summary>
public sealed class ValueFreeRuleReasonTests
{
    public static IEnumerable<object[]> Rules()
    {
        yield return [ValueFreeRule.RenderedCall, "vf.rendered-call",
            "a call line carries a parameter's VALUE — attach the structural trace (.nt) instead, which"
        + " carries the same shape without any value"];
        yield return [ValueFreeRule.RenderedOutcome, "vf.rendered-outcome",
            "an outcome arrow carries a RETURNED VALUE — the structural trace writes a bare"
        + " → value, an exception TYPE after !!, or ?? incomplete, and never a value"];
        yield return [ValueFreeRule.Duration, "vf.duration",
            "an elapsed time is a runtime measurement, which means this text came from a rendered"
        + " narrative rather than from the structural trace"];
        yield return [ValueFreeRule.Marker, "vf.marker",
            "the redaction marker only appears where a value was redacted, so this text is a rendered"
        + " narrative — the structural trace has nothing to redact and never carries it"];
        yield return [ValueFreeRule.NamedSecret, "vf.named-secret",
            "a field whose NAME says it holds a credential is shown with a value beside it — remove"
        + " the value; the name alone is shape and may stay"];
        yield return [ValueFreeRule.ValueShape, "vf.value-shape",
            "a value here is shaped like a credential, a key, a national id or a card number —"
        + " remove it; a report never needs the value, only what happened"];
        yield return [ValueFreeRule.Entropy, "vf.entropy",
            "a long encoded run here looks like a key, a hash or an opaque identifier — remove it;"
        + " if it is genuinely not a secret, describe it in words instead of pasting it"];
        yield return [ValueFreeRule.Email, "vf.email",
            "an email address is personal data and a public issue is public forever — remove it;"
        + " the report does not need to say who"];
        yield return [ValueFreeRule.HomePath, "vf.home-path",
            "an absolute home directory names the account it belongs to — the draft rewrites these"
        + " to ~ on its own, so one here means the text was edited by hand afterwards"];
        yield return [ValueFreeRule.Control, "vf.control",
            "a control character is not text a reader needs and is how a payload hides in one —"
        + " only a line feed and a tab belong in a report"];
    }

    [Theory]
    [MemberData(nameof(Rules))]
    public void Each_rule_names_itself_and_says_why_in_full(ValueFreeRule rule, string id, string reason)
    {
        Assert.Equal(id, rule.Id);
        Assert.Equal(reason, rule.Reason);
    }
}
