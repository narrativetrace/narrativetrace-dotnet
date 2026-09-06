// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Examples.Minecraft;

/// <summary>Same behavior as <see cref="IWorldGenerator" />, generic name.</summary>
public interface IDataProcessor
{
    string Process(int a, int b);
}

/// <summary>Same behavior as <see cref="IPlayerInventory" />, generic name.</summary>
public interface IStateManager
{
    bool Update(string key, int value);
}

/// <summary>Same behavior as <see cref="ICraftingTable" />, generic name.</summary>
public interface IThingFactory
{
    string Create(string spec);
}

/// <summary>Same behavior as <see cref="ICreatureSpawner" />, generic name.</summary>
public interface IEntityHandler
{
    string Execute(string type, int a, int b, int c);
}

/// <summary>Same behavior as <see cref="IWorldServer" />, generic name.</summary>
public interface IGameManager
{
    string Handle(string input);
}

/// <summary>Generic-name implementation — the trace reveals nothing.</summary>
public sealed class DefaultDataProcessor : IDataProcessor
{
    public string Process(int a, int b)
    {
        return $"{a},{b}";
    }
}

/// <inheritdoc cref="IStateManager" />
public sealed class DefaultStateManager : IStateManager
{
    public bool Update(string key, int value)
    {
        return value > 0 && key.Length > 0;
    }
}

/// <inheritdoc cref="IThingFactory" />
public sealed class DefaultThingFactory : IThingFactory
{
    public string Create(string spec)
    {
        return spec;
    }
}

/// <inheritdoc cref="IEntityHandler" />
public sealed class DefaultEntityHandler : IEntityHandler
{
    public string Execute(string type, int a, int b, int c)
    {
        return type;
    }
}

/// <inheritdoc cref="IGameManager" />
public sealed class DefaultGameManager(
    IDataProcessor dataProcessor,
    IStateManager stateManager,
    IThingFactory thingFactory,
    IEntityHandler entityHandler) : IGameManager
{
    public string Handle(string input)
    {
        dataProcessor.Process(0, 0);
        stateManager.Update("wooden_pickaxe", 1);
        thingFactory.Create("wooden_pickaxe");
        entityHandler.Execute("zombie", 10, 64, 20);
        return $"{input} joined the world";
    }
}
