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
