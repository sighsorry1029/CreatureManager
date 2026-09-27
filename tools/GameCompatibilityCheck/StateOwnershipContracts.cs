using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using Mono.Cecil;
using MonoMod.Utils;

// Characterize published state and queue transitions on the production bodies. The request
// fixture substitutes authenticated peer lookup only; it does not open a socket or Unity world.
internal static class StateOwnershipContracts
{
    private const BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static Type Texture = null!;
    private static ZNetPeer Peer = null!;
    private static bool Authenticated;
    private static MethodInfo Request = null!;
    private static readonly string Root = new('a', 64), Hash = new('b', 64);

    internal static void Run(Assembly plugin)
    {
        Texture = plugin.GetType("CreatureManager.CreatureTextureSync", true)!;
        CheckTextureQueue();
        System.Console.WriteLine("State ownership contracts passed: authenticated texture requests, duplicate/served retry suppression, partial/failed/stale completion, peer isolation, snapshot rollback and queue cleanup. No socket or Unity execution.");
        CheckFactionPublication(plugin);
    }

    private static void CheckFactionPublication(Assembly plugin)
    {
        Type manager = plugin.GetType("CreatureManager.CreatureFactionManager", true)!;
        Type api = plugin.GetType("CreatureManager.CreatureManagerFactionApi", true)!;
        Type definition = plugin.GetType("CreatureManager.FactionDefinition", true)!;
        // Preserve state in both the pre-refactor four-map implementation and its snapshot successor.
        var saved = manager.GetFields(Static).Where(field => !field.IsInitOnly && !field.IsLiteral)
            .ToDictionary(field => field, field => field.GetValue(null));
        PropertyInfo logProperty = plugin.GetType("CreatureManager.CreatureManagerPlugin", true)!.GetProperty("Log", Static)!;
        object previousLog = logProperty.GetValue(null)!;
        using var expectedRejectionLog = new BepInEx.Logging.ManualLogSource("Faction validation fixture");
        int warnings = 0, errors = 0;
        expectedRejectionLog.LogEvent += (_, e) =>
        {
            if (e.Level == BepInEx.Logging.LogLevel.Warning) warnings++;
            if (e.Level == BepInEx.Logging.LogLevel.Error) errors++;
        };
        string[] Names() => (string[])api.GetMethod("GetNames", Static)!.Invoke(null, null)!;
        bool Resolve(string method, Type owner, string? name, int expected)
        {
            object?[] args = { name, default(Character.Faction) };
            bool resolved = (bool)owner.GetMethod(method, Static)!.Invoke(null, args)!;
            Require(!resolved || (int)(Character.Faction)args[1]! == expected, "resolved faction has expected ID");
            return resolved;
        }
        bool Load(params (string Name, int Id)[] entries)
        {
            var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(definition))!;
            foreach (var item in entries)
            {
                object value = Activator.CreateInstance(definition)!;
                definition.GetProperty("Faction")!.SetValue(value, item.Name);
                definition.GetProperty("Id")!.SetValue(value, item.Id);
                list.Add(value);
            }
            return (bool)manager.GetMethod("Load", Static)!.Invoke(null, new object[] { list })!;
        }
        try
        {
            // Original BaseAI.GetAllInstances returns its managed list; no live AI means Load can run unmodified.
            Require(BaseAI.GetAllInstances().Count == 0, "faction fixture has no Unity instances");
            Require(Names().Length == 0 && !Resolve("TryGetFaction", manager, "Players", 0), "initial faction state remains empty");
            logProperty.SetValue(null, expectedRejectionLog);
            Require(Load(("Second", 202), ("First", 201)), "valid faction configuration published");
            Require(Names().SequenceEqual(new[] { "First", "Second" }), "public names sorted by stable ID");
            Require(Resolve("TryResolve", api, " first ", 201) && Resolve("TryResolve", api, "202", 202), "public name normalization and numeric lookup");
            Require(Resolve("TryGetFaction", manager, "Players", 0) && Resolve("TryGetFaction", manager, "0", 0) &&
                !Resolve("TryResolve", api, "Players", 0) && !Resolve("TryResolve", api, "0", 0), "runtime factions stay distinct from public registered factions");
            object?[] canonical = { " FIRST ", default(Character.Faction), null };
            Require((bool)manager.GetMethod("TryGetRegisteredFaction", Static)!.Invoke(null, canonical)! &&
                (string)canonical[2]! == "First", "canonical persisted name retained");
            Require(!Load(("Duplicate", 201), ("duplicate", 202)) && !Load(("First", 201), ("Second", 201)), "duplicate name and ID rejected");
            Require(Names().SequenceEqual(new[] { "First", "Second" }) && Resolve("TryResolve", api, "First", 201), "rejected reload retains published rules");
            Require(warnings == 2 && errors >= 2, "only expected rejection diagnostics emitted");
            Require(Load(("Replacement", 203)) && Names().SequenceEqual(new[] { "Replacement" }) &&
                !Resolve("TryResolve", api, "First", 201), "valid reload replaces old registered names");
            Require(Load() && Names().Contains("Players") && Names().Contains("Boss") &&
                !Resolve("TryResolve", api, "Replacement", 203), "empty configuration publishes defaults only when loaded");
            Require(warnings == 2, "valid reload did not warn while refreshing empty AI list");
        }
        finally
        {
            foreach (var item in saved) item.Key.SetValue(null, item.Value);
            logProperty.SetValue(null, previousLog);
        }
        System.Console.WriteLine("Faction publication contracts passed: empty startup, normalized/canonical/numeric names, stable IDs, registered/runtime API boundary, rejected reload preservation and default/replacement reload. No live AI or ZDO execution.");
    }

    private static void CheckTextureQueue()
    {
        Type manifestType = Texture.GetNestedType("ManifestData", BindingFlags.NonPublic)!;
        object manifest = Activator.CreateInstance(manifestType)!;
        manifestType.GetProperty("RootHash")!.SetValue(manifest, Root);
        object snapshot = Activator.CreateInstance(Texture.GetNestedType("ServerSnapshot", BindingFlags.NonPublic)!, Instance, null,
            new object[] { manifest, new Dictionary<string, byte[]> { [Hash] = new byte[65536 * 2 + 1] } }, null)!;
        object? previous = Invoke("InstallServerSnapshot", snapshot);
        using (var copy = new DynamicMethodDefinition(Texture.GetMethod("RPC_TextureRequest", Static)!))
        {
            typeof(DynamicMethodDefinition).GetProperty(nameof(DynamicMethodDefinition.OriginalMethod))!.SetValue(copy, null);
            copy.OwnerType = typeof(StateOwnershipContracts);
            foreach (var instruction in copy.Definition.Body.Instructions)
                if (instruction.Operand is MethodReference method && method.DeclaringType.FullName == Texture.FullName && method.Name == "TryGetAuthenticatedClientPeer")
                    instruction.Operand = copy.Module.ImportReference(typeof(StateOwnershipContracts).GetMethod(nameof(GetPeer), Static)!);
            Request = copy.Generate();
        }
        try
        {
            Peer = New<ZNetPeer>(); Peer.m_uid = 11; Peer.m_rpc = New<ZRpc>(); Authenticated = false;
            SendRequest(); Require(QueueCount == 0, "unauthenticated request has no effects");
            Authenticated = true; SendRequest();
            string first = Key(11);
            Require(QueueCount == 3 && Counts[first] == 3, "three-chunk request accepted");
            SendRequest(); Require(QueueCount == 3, "pending duplicate suppressed");
            CompleteNext(true); Require(Counts[first] == 2 && !History.ContainsKey(first), "partial transfer remains pending");
            SendRequest(); Require(QueueCount == 2, "duplicate during partial transfer suppressed");
            CompleteNext(true); CompleteNext(true);
            Require(!Counts.ContainsKey(first) && History.ContainsKey(first), "final success records serve history");
            SendRequest(); Require(QueueCount == 0, "recent successful duplicate suppressed");
            History[first] = DateTime.UtcNow.AddSeconds(-11);
            SendRequest(); Require(QueueCount == 3, "retry allowed after existing delay");
            CompleteNext(false); CompleteNext(false); CompleteNext(false);
            Require(!Counts.ContainsKey(first), "discarded transfer completes bookkeeping");
            Require(History[first] < DateTime.UtcNow.AddSeconds(-10), "discard does not refresh serve history");
            SendRequest(); Require(QueueCount == 3, "discarded content can be requested again");
            Invoke("CompleteQueuedChunkLocked", "stale", true);
            Require(Counts[first] == 3 && !History.ContainsKey("stale"), "unknown completion is inert");

            ZRpc firstRpc = Peer.m_rpc;
            Peer = New<ZNetPeer>(); Peer.m_uid = 112; Peer.m_rpc = New<ZRpc>(); SendRequest();
            Require(QueueCount == 6 && Counts.Count == 2, "another peer gets independent transfer");
            Invoke("RemovePeerTransfersLocked", 11L, firstRpc);
            Require(QueueCount == 3 && Counts.Count == 1 && Counts.ContainsKey(Key(112)), "disconnect removes only matching peer prefix");
            Invoke("RemovePeerTransfersLocked", 0L, null);
            Require(QueueCount == 3, "unknown disconnect is inert");
            Invoke("RemovePeerTransfersLocked", 112L, null);
            Require(QueueCount == 0 && Counts.Count == 0, "peer-wide removal clears queued transfer");

            SendRequest(); Invoke("InstallServerSnapshot", snapshot);
            RequireEmpty("install invalidates old requests");
            SendRequest(); Invoke("RestoreServerSnapshot", previous);
            RequireEmpty("rollback invalidates requests");
            SendRequest(); Require(QueueCount == 0, "request for inactive root is rejected");
            Invoke("InstallServerSnapshot", snapshot); SendRequest(); Invoke("ClearOutgoingTransfersLocked");
            RequireEmpty("session clear resets queue/count/history");
        }
        finally { Invoke("RestoreServerSnapshot", previous); }
    }

    private static Dictionary<string, int> Counts => (Dictionary<string, int>)Texture.GetField("RemainingTransferChunks", Static)!.GetValue(null)!;
    private static Dictionary<string, DateTime> History => (Dictionary<string, DateTime>)Texture.GetField("RecentlyServed", Static)!.GetValue(null)!;
    private static object Queue => Texture.GetField("OutgoingChunks", Static)!.GetValue(null)!;
    private static int QueueCount => (int)Queue.GetType().GetProperty("Count")!.GetValue(Queue)!;
    private static string Key(long peer) => (string)Invoke("BuildTransferKey", peer, Root, Hash)!;
    private static object? Invoke(string name, params object?[] args) => Texture.GetMethod(name, Static)!.Invoke(null, args);
    private static bool GetPeer(ZRpc rpc, out ZNetPeer peer) { peer = Peer; return Authenticated; }
    private static T New<T>() => (T)FormatterServices.GetUninitializedObject(typeof(T));
    private static void SendRequest()
    {
        var package = new ZPackage(); package.Write(1); package.Write(Root); package.Write(1); package.Write(Hash); package.SetPos(0);
        Request.Invoke(null, new object[] { Peer.m_rpc, package });
    }
    private static void CompleteNext(bool served)
    {
        object chunk = Queue.GetType().GetMethod("Dequeue")!.Invoke(Queue, null)!;
        long peer = (long)chunk.GetType().GetField("PeerId", Instance)!.GetValue(chunk)!;
        Invoke("CompleteQueuedChunkLocked", Key(peer), served);
    }
    private static void RequireEmpty(string message)
    {
        Require(QueueCount == 0 && Counts.Count == 0 && History.Count == 0 &&
            (DateTime)Texture.GetField("NextServeHistoryExpiryUtc", Static)!.GetValue(null)! == DateTime.MaxValue, message);
    }
    private static void Require(bool result, string message)
    { if (!result) throw new InvalidOperationException("State ownership contract failed: " + message); }
}
