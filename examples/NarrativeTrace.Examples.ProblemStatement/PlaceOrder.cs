// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using Microsoft.Extensions.Logging;

namespace NarrativeTrace.Examples.ProblemStatement;

/// <summary>Collaborators <c>PlaceOrder</c> calls — minimal shapes, just enough to compile the
/// README "problem" section's before/after method (owner's example, Pro ledger §129).</summary>
public sealed record OrderRequest(string Id, string Sku, int Qty);

public sealed record Customer(string Id);

public sealed record Payment(string Id, decimal Amount);

public sealed record Order(string Id, Customer Customer, Payment Payment);

public interface ICustomerRepository
{
    Customer Find(string id);
}

public interface ICatalog
{
    decimal Price(string sku);
}

public interface IInventory
{
    void Reserve(string sku, int qty);
}

public interface IPaymentGateway
{
    Payment Charge(decimal amount);
}

public interface IOrderRepository
{
    Order Save(Customer customer, Payment payment);
}

/// <summary>
/// README "The problem" — "before": five collaborators, two success log lines that say nothing
/// about the four calls between them, one catch that logs and rethrows. The
/// <c>beforeExample</c> region is embedded verbatim into <c>README.md</c> (rule 8) —
/// <see cref="OrderService.PlaceOrder"/> below is the exact same method with the log lines
/// deleted, the "after" beside it.
/// </summary>
public sealed class OrderServiceWithLogging
{
    private readonly ILogger _log;
    private readonly ICustomerRepository _customers;
    private readonly ICatalog _catalog;
    private readonly IInventory _inventory;
    private readonly IPaymentGateway _payments;
    private readonly IOrderRepository _orders;

    public OrderServiceWithLogging(
        ILogger log,
        ICustomerRepository customers,
        ICatalog catalog,
        IInventory inventory,
        IPaymentGateway payments,
        IOrderRepository orders)
    {
        _log = log;
        _customers = customers;
        _catalog = catalog;
        _inventory = inventory;
        _payments = payments;
        _orders = orders;
    }

    // snippet:begin beforeExample
    public Order PlaceOrder(OrderRequest req)
    {
        _log.LogInformation("Placing order {OrderId}", req.Id);
        try
        {
            var customer = _customers.Find(req.Id);
            var price = _catalog.Price(req.Sku);
            _inventory.Reserve(req.Sku, req.Qty);
            var payment = _payments.Charge(price);
            var order = _orders.Save(customer, payment);
            _log.LogInformation("Order succeeded {OrderId}", order.Id);
            return order;
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Placing order failed {OrderId}", req.Id);
            throw;
        }
    }
    // snippet:end beforeExample
}

/// <summary>
/// README "The problem" — "after": the same method under NarrativeTrace with the log lines
/// deleted. <c>NarrativeTraceProxy.Create&lt;IOrderService&gt;</c> wrapping an instance of this
/// class produces the trace from the method name, parameter names, and return value alone —
/// the information that was already there.
/// </summary>
public sealed class OrderService
{
    private readonly ICustomerRepository _customers;
    private readonly ICatalog _catalog;
    private readonly IInventory _inventory;
    private readonly IPaymentGateway _payments;
    private readonly IOrderRepository _orders;

    public OrderService(
        ICustomerRepository customers,
        ICatalog catalog,
        IInventory inventory,
        IPaymentGateway payments,
        IOrderRepository orders)
    {
        _customers = customers;
        _catalog = catalog;
        _inventory = inventory;
        _payments = payments;
        _orders = orders;
    }

    // snippet:begin afterExample
    public Order PlaceOrder(OrderRequest req)
    {
        var customer = _customers.Find(req.Id);
        var price = _catalog.Price(req.Sku);
        _inventory.Reserve(req.Sku, req.Qty);
        var payment = _payments.Charge(price);
        var order = _orders.Save(customer, payment);
        return order;
    }
    // snippet:end afterExample
}
