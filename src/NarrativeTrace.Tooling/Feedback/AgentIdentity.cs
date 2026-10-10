// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Tooling.Feedback;

/// <summary>
/// Which agent product and model drafted the report, as the agent itself reports them.
/// </summary>
/// <remarks>
/// INTENT: triage needs to know whether a wording problem is one model's reading or everybody's.
/// Neither field is verified and neither is required — an agent that will not name itself still gets
/// to file — so "unknown" is <c>""</c> on both, never null and never a guess.
/// </remarks>
/// <param name="Product">The agent product, or <c>""</c>.</param>
/// <param name="Model">The model, or <c>""</c>.</param>
public sealed record AgentIdentity(string Product, string Model)
{
    /// <summary>The agent product, or <c>""</c>.</summary>
    public string Product { get; } = Product
        ?? throw new ArgumentNullException(nameof(Product), "an unknown agent product is \"\", never null — see Unknown()");

    /// <summary>The model, or <c>""</c>.</summary>
    public string Model { get; } = Model
        ?? throw new ArgumentNullException(nameof(Model), "an unknown agent model is \"\", never null — see Unknown()");

    /// <summary>An agent that did not name itself.</summary>
    public static AgentIdentity Unknown() => new("", "");

    /// <summary>The one line the report prints, or <c>""</c> when nothing is known.</summary>
    public string Describe()
    {
        if (Product.Length == 0 && Model.Length == 0)
        {
            return "";
        }

        return Model.Length == 0 ? Product : $"{Product} / {Model}";
    }
}
