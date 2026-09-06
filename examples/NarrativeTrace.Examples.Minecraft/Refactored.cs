// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Examples.Minecraft;

/// <summary>A generated slice of the world at a coordinate.</summary>
public sealed record Chunk(int X, int Z, string Biome);

/// <summary>Generates world terrain.</summary>
public interface IWorldGenerator
{
    Chunk GenerateChunk(int x, int z);
}

/// <summary>Holds a player's items.</summary>
public interface IPlayerInventory
{
    bool AddItem(string item, int count);
}

/// <summary>Turns recipes into items.</summary>
public interface ICraftingTable
{
    string Craft(string recipe);
}

/// <summary>Spawns hostile creatures into the world.</summary>
public interface ICreatureSpawner
{
    string SpawnHostile(string creatureType, int x, int y, int z);
}

/// <summary>Coordinates everything that happens when a player joins.</summary>
public interface IWorldServer
{
    string PlayerJoined(string playerName);
}

/// <summary>Descriptive-name implementation — the trace reads like the domain.</summary>
public sealed class DefaultWorldGenerator : IWorldGenerator
{
    public Chunk GenerateChunk(int x, int z)
    {
        return new Chunk(x, z, "plains");
    }
}

/// <inheritdoc cref="IPlayerInventory" />
public sealed class DefaultPlayerInventory : IPlayerInventory
{
    public bool AddItem(string item, int count)
    {
        return count > 0 && item.Length > 0;
    }
}

/// <inheritdoc cref="ICraftingTable" />
public sealed class DefaultCraftingTable : ICraftingTable
{
    public string Craft(string recipe)
    {
        return recipe;
    }
}

/// <inheritdoc cref="ICreatureSpawner" />
public sealed class DefaultCreatureSpawner : ICreatureSpawner
{
    public string SpawnHostile(string creatureType, int x, int y, int z)
    {
        return creatureType;
    }
}

/// <inheritdoc cref="IWorldServer" />
public sealed class DefaultWorldServer(
    IWorldGenerator worldGenerator,
    IPlayerInventory inventory,
    ICraftingTable craftingTable,
    ICreatureSpawner spawner) : IWorldServer
{
    public string PlayerJoined(string playerName)
    {
        worldGenerator.GenerateChunk(0, 0);
        inventory.AddItem("wooden_pickaxe", 1);
        craftingTable.Craft("wooden_pickaxe");
        spawner.SpawnHostile("zombie", 10, 64, 20);
        return $"{playerName} joined the world";
    }
}
