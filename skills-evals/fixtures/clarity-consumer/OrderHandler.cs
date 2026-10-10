// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace Orders;

public interface IOrderHandler
{
    string Handle(string data);
}

public sealed class OrderHandler : IOrderHandler
{
    public string Handle(string data) => $"order:{data.Trim()}";
}
