// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace Checkout;

public sealed class InMemoryInventory(string stockedSku, int units) : IInventoryService
{
    private int _available = units;

    public int AvailableUnits(string sku) => sku == stockedSku ? _available : 0;

    public void Reserve(string sku, int quantity)
    {
        if (sku != stockedSku || quantity > _available)
        {
            throw new InvalidOperationException($"not enough stock of {sku}");
        }

        _available -= quantity;
    }

    public void Release(string sku, int quantity)
    {
        if (sku == stockedSku)
        {
            _available += quantity;
        }
    }
}

public sealed class InMemoryOrderRepository : IOrderRepository
{
    private readonly List<Order> _saved = [];

    public IReadOnlyList<Order> Saved => _saved;

    public void Save(Order order) => _saved.Add(order);
}

public sealed class CardPaymentService : IPaymentService
{
    /// <summary>A Luhn-valid number the fake gateway always declines.</summary>
    public const string DecliningCard = "4000000000000002";

    public string Charge(string customerId, int amountCents, string card) =>
        card == DecliningCard
            ? throw new PaymentDeclinedException("card declined by the issuer")
            : $"PAY-{customerId}-{amountCents}";
}
