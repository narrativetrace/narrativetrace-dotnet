// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Clarity;
using NarrativeTrace.Core;
using NarrativeTrace.Proxy;

namespace NarrativeTrace.Examples.Minecraft;

/// <summary>
/// The flagship "your code should tell its own story" demonstration: one
/// call graph traced twice — once through descriptively-named services, once
/// through generically-named ones. The structure is identical; only the clean
/// version's names let a reader follow the story, and only it scores well on
/// clarity.
/// </summary>
public static class MinecraftNamingDemo
{
    /// <summary>Traces the descriptively-named "player joins world" flow.</summary>
    public static TraceTree TraceRefactored(INarrativeContext context)
    {
        var worldGenerator = Trace<IWorldGenerator>(new DefaultWorldGenerator(), context);
        var inventory = Trace<IPlayerInventory>(new DefaultPlayerInventory(), context);
        var craftingTable = Trace<ICraftingTable>(new DefaultCraftingTable(), context);
        var spawner = Trace<ICreatureSpawner>(new DefaultCreatureSpawner(), context);
        var server = Trace<IWorldServer>(
            new DefaultWorldServer(worldGenerator, inventory, craftingTable, spawner), context);

        server.PlayerJoined("Steve");
        return context.CaptureTrace();
    }

    /// <summary>Traces the same flow through generically-named services.</summary>
    public static TraceTree TraceUnrefactored(INarrativeContext context)
    {
        var dataProcessor = Trace<IDataProcessor>(new DefaultDataProcessor(), context);
        var stateManager = Trace<IStateManager>(new DefaultStateManager(), context);
        var thingFactory = Trace<IThingFactory>(new DefaultThingFactory(), context);
        var entityHandler = Trace<IEntityHandler>(new DefaultEntityHandler(), context);
        var manager = Trace<IGameManager>(
            new DefaultGameManager(dataProcessor, stateManager, thingFactory, entityHandler),
            context);

        manager.Handle("Steve");
        return context.CaptureTrace();
    }

    /// <summary>Overall clarity of the clean service set versus the cryptic one.</summary>
    public static (double Clean, double Cryptic) CompareClarity()
    {
        var clean = ScanOverall(
            typeof(DefaultWorldServer), typeof(DefaultWorldGenerator),
            typeof(DefaultPlayerInventory), typeof(DefaultCraftingTable),
            typeof(DefaultCreatureSpawner));
        var cryptic = ScanOverall(
            typeof(DefaultGameManager), typeof(DefaultDataProcessor),
            typeof(DefaultStateManager), typeof(DefaultThingFactory),
            typeof(DefaultEntityHandler));
        return (clean, cryptic);
    }

    private static double ScanOverall(params Type[] types)
    {
        var results = ClarityScanner.Scan(types);
        return results.Values.Average(r => r.Overall);
    }

    private static T Trace<T>(T target, INarrativeContext context)
        where T : class
    {
        return NarrativeTraceProxy.Create(target, context);
    }
}
