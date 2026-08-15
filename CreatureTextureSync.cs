using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using HarmonyLib;

namespace CreatureManager;

/// <summary>
/// Transfers server-owned PNG files independently from the synchronized YAML transaction.
/// The YAML bundle owns the small <see cref="ManifestData"/> value; this service owns only
/// immutable, content-addressed bytes and the client cache used by the texture registry.
/// </summary>
internal static class CreatureTextureSync
{
    internal const int ProtocolVersion = 1;
    internal const int MaxFileCount = 128;
    internal const int MaxFileBytes = 4 * 1024 * 1024;
    internal const int MaxTotalBytes = 32 * 1024 * 1024;
    internal const int MaxDimension = 4096;
    internal const long MaxTotalPixels = 64L * 1024L * 1024L;
    internal const int ChunkBytes = 64 * 1024;

    private const long MaxPersistentCacheBytes = 256L * 1024L * 1024L;
    private const int MaxPersistentCacheFiles = 2048;
    private const int MaxQueuedChunks = 4096;
    private const int MaxChunksPerUpdate = 2;
    private const int HashTextLength = 64;
    private const int MaxLogicalNameLength = 255;
    private static readonly TimeSpan RequestRetryDelay = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan DuplicateServeDelay = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan ServeHistoryLifetime = TimeSpan.FromMinutes(2);
    private static readonly byte[] PngSignature = { 137, 80, 78, 71, 13, 10, 26, 10 };
    private static readonly char[] PortableInvalidFileNameChars = { '<', '>', ':', '"', '/', '\\', '|', '?', '*' };
    private static readonly uint[] Crc32Table = BuildCrc32Table();
    private static readonly object Sync = new();
    private static readonly Queue<OutgoingChunk> OutgoingChunks = new();
    private static readonly HashSet<string> QueuedTransfers = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, int> RemainingTransferChunks = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, DateTime> RecentlyServed = new(StringComparer.Ordinal);

    private static readonly string RequestRpc = CreatureManagerPlugin.ModGUID + ".TextureSync.Request.v1";
    private static readonly string ChunkRpc = CreatureManagerPlugin.ModGUID + ".TextureSync.Chunk.v1";

    private static ServerSnapshot? ActiveServerSnapshot;
    private static ClientTextureSet ActiveClientSet = ClientTextureSet.Empty;
    private static ClientStage? PendingClientStage;

    internal static event Action<string>? ClientManifestReady;

    internal sealed class ManifestData
    {
        public int Version { get; set; } = ProtocolVersion;
        public string RootHash { get; set; } = "";
        public List<ManifestEntryData> Files { get; set; } = new();
    }

    internal sealed class ManifestEntryData
    {
        public string Name { get; set; } = "";
        public string Sha256 { get; set; } = "";
        public int Length { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
    }

    /// <summary>
    /// Immutable candidate built before the YAML transaction is published. DomainManager can
    /// install it, retain the returned previous snapshot as its checkpoint, and restore that
    /// checkpoint if the surrounding definition transaction is rejected.
    /// </summary>
    internal sealed class ServerSnapshot
    {
        private readonly Dictionary<string, byte[]> _blobs;
        private readonly ManifestData _manifest;

        internal ServerSnapshot(ManifestData manifest, Dictionary<string, byte[]> blobs)
        {
            _manifest = CloneManifest(manifest);
            _blobs = blobs;
        }

        internal string RootHash => _manifest.RootHash;
        internal ManifestData GetManifestCopy()
        {
            return CloneManifest(_manifest);
        }

        internal bool TryGetBlob(string hash, out byte[] bytes)
        {
            return _blobs.TryGetValue(hash, out bytes!);
        }

        internal bool ContainsHash(string hash)
        {
            return _blobs.ContainsKey(hash);
        }
    }

    private sealed class ClientTextureSet
    {
        internal static readonly ClientTextureSet Empty = new("", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

        internal ClientTextureSet(string rootHash, Dictionary<string, string> pathsByName)
        {
            RootHash = rootHash;
            PathsByName = pathsByName;
        }

        internal string RootHash { get; }
        internal Dictionary<string, string> PathsByName { get; }
    }

    private sealed class ClientStage
    {
        internal ClientStage(ManifestData manifest)
        {
            Manifest = manifest;
        }

        internal ManifestData Manifest { get; }
        internal readonly Dictionary<string, string> PathsByHash = new(StringComparer.Ordinal);
        internal readonly Dictionary<string, byte[]> ValidatedBytesByHash = new(StringComparer.Ordinal);
        internal readonly HashSet<string> MissingHashes = new(StringComparer.Ordinal);
        internal readonly Dictionary<string, ReceiveState> Receivers = new(StringComparer.Ordinal);
        internal DateTime NextRequestUtc = DateTime.MinValue;
    }

    private sealed class ReceiveState
    {
        internal ReceiveState(int length)
        {
            Bytes = new byte[length];
            ReceivedChunks = new bool[(length + ChunkBytes - 1) / ChunkBytes];
        }

        internal byte[] Bytes { get; }
        internal bool[] ReceivedChunks { get; }
        internal int ReceivedCount;
    }

    private sealed class OutgoingChunk
    {
        internal long PeerId;
        internal ZRpc PeerRpc = null!;
        internal string RootHash = "";
        internal string ContentHash = "";
        internal int Offset;
    }

    [HarmonyPatch(typeof(ZNet), nameof(ZNet.OnNewConnection))]
    private static class RegisterPeerRpcsPatch
    {
        [HarmonyPostfix]
        private static void Postfix(ZNetPeer peer)
        {
            RegisterPeerRpcs(peer);
        }
    }

    [HarmonyPatch(typeof(ZNet), nameof(ZNet.Disconnect), typeof(ZNetPeer))]
    private static class ForgetDisconnectedPeerPatch
    {
        [HarmonyPrefix]
        private static void Prefix(ZNetPeer peer)
        {
            if (peer == null)
            {
                return;
            }

            lock (Sync)
            {
                RemovePeerTransfersLocked(peer.m_uid, peer.m_rpc);
            }
        }
    }

    private static void RegisterPeerRpcs(ZNetPeer? peer)
    {
        if (peer?.m_rpc == null)
        {
            return;
        }

        peer.m_rpc.Register<ZPackage>(RequestRpc, RPC_TextureRequest);
        peer.m_rpc.Register<ZPackage>(ChunkRpc, RPC_TextureChunk);
    }

    /// <summary>
    /// Pumps client requests and a bounded number of server chunks. Call from Plugin.Update.
    /// </summary>
    internal static void Update()
    {
        if (ZNet.instance == null)
        {
            return;
        }

        if (ZNet.instance.IsServer())
        {
            SendQueuedChunks();
            PruneServeHistory();
        }
        else
        {
            SendMissingRequestIfDue();
        }
    }

    /// <summary>
    /// Builds a deterministic snapshot from referenced, top-level PNG names. Missing names are
    /// deliberately omitted because they can refer to Unity resource textures instead of files.
    /// </summary>
    internal static bool TryBuildServerSnapshot(
        IEnumerable<string> referencedTextureNames,
        out ServerSnapshot snapshot,
        out string error)
    {
        snapshot = null!;
        error = "";
        if (referencedTextureNames == null)
        {
            error = "The referenced texture name collection is null.";
            return false;
        }

        try
        {
            string textureRoot = Path.GetFullPath(CreatureDomainManager.TextureDirectoryPath);
            Directory.CreateDirectory(textureRoot);

            SortedDictionary<string, string> requested = new(StringComparer.OrdinalIgnoreCase);
            foreach (string rawName in referencedTextureNames)
            {
                if (IsExplicitNonPngTextureReference(rawName))
                {
                    // Unity resource textures can contain a dot or a non-PNG extension. They
                    // remain name-only references and are deliberately outside byte sync.
                    continue;
                }

                if (!TryNormalizeLogicalName(
                        rawName,
                        requirePngExtension: false,
                        out string logicalName,
                        out _))
                {
                    // Unsafe path-shaped identifiers are never read as files, but they can still
                    // be valid Unity resource names and therefore remain outside byte sync.
                    continue;
                }

                requested[logicalName] = logicalName;
            }

            List<ManifestEntryData> entries = new();
            Dictionary<string, byte[]> blobs = new(StringComparer.Ordinal);
            long totalBytes = 0;
            long totalPixels = 0;
            foreach (string logicalName in requested.Values)
            {
                if (!CreatureTextureRegistry.TryResolveLocalPngFile(
                        logicalName,
                        out _,
                        out string path,
                        out string pathError))
                {
                    if (pathError.Length > 0)
                    {
                        error = $"Referenced texture '{logicalName}' is unsafe: {pathError}";
                        return false;
                    }

                    continue;
                }

                if (entries.Count >= MaxFileCount)
                {
                    error = $"Referenced file textures exceed the {MaxFileCount}-file safety limit.";
                    return false;
                }

                if (!TryReadStablePng(path, out byte[] bytes, out int width, out int height, out error))
                {
                    return false;
                }

                totalBytes += bytes.Length;
                totalPixels += (long)width * height;
                if (totalBytes > MaxTotalBytes)
                {
                    error = $"Referenced file textures exceed the {MaxTotalBytes}-byte aggregate safety limit.";
                    return false;
                }

                if (totalPixels > MaxTotalPixels)
                {
                    error = $"Referenced file textures exceed the {MaxTotalPixels}-pixel aggregate safety limit.";
                    return false;
                }

                string hash = ComputeSha256(bytes);
                entries.Add(new ManifestEntryData
                {
                    Name = logicalName,
                    Sha256 = hash,
                    Length = bytes.Length,
                    Width = width,
                    Height = height
                });
                if (!blobs.ContainsKey(hash))
                {
                    blobs.Add(hash, bytes);
                }
            }

            ManifestData manifest = CreateManifest(entries);
            snapshot = new ServerSnapshot(manifest, blobs);
            return true;
        }
        catch (Exception ex)
        {
            error = $"Failed to build the synchronized texture snapshot: {ex.Message}";
            return false;
        }
    }

    /// <summary>
    /// Installs a server candidate and returns the exact previous snapshot for rollback.
    /// </summary>
    internal static ServerSnapshot? InstallServerSnapshot(ServerSnapshot snapshot)
    {
        if (snapshot == null)
        {
            throw new ArgumentNullException(nameof(snapshot));
        }

        lock (Sync)
        {
            ServerSnapshot? previous = ActiveServerSnapshot;
            ActiveServerSnapshot = snapshot;
            ClearOutgoingTransfersLocked();
            return previous;
        }
    }

    internal static void RestoreServerSnapshot(ServerSnapshot? snapshot)
    {
        lock (Sync)
        {
            ActiveServerSnapshot = snapshot;
            ClearOutgoingTransfersLocked();
        }
    }

    internal static ManifestData CreateEmptyManifest()
    {
        return CreateManifest(Array.Empty<ManifestEntryData>());
    }

    /// <summary>
    /// Accepts the manifest embedded in a server YAML bundle. The previous client set remains
    /// active until every distinct hash in this generation is present and validated.
    /// </summary>
    internal static bool AcceptServerManifest(ManifestData manifest, out string error)
    {
        error = "";
        if (!TryValidateManifest(manifest, out ManifestData normalized, out error))
        {
            return false;
        }

        lock (Sync)
        {
            if (ActiveClientSet.RootHash.Equals(normalized.RootHash, StringComparison.Ordinal) &&
                PendingClientStage == null)
            {
                return true;
            }

            if (PendingClientStage != null &&
                PendingClientStage.Manifest.RootHash.Equals(normalized.RootHash, StringComparison.Ordinal))
            {
                return true;
            }
        }

        ClientStage stage = new(normalized);
        foreach (IGrouping<string, ManifestEntryData> hashGroup in normalized.Files.GroupBy(file => file.Sha256, StringComparer.Ordinal))
        {
            ManifestEntryData expected = hashGroup.First();
            if (TryFindOrCreateCachedContent(
                    hashGroup,
                    expected,
                    out string cachePath,
                    out byte[] validatedBytes))
            {
                stage.PathsByHash[expected.Sha256] = cachePath;
                stage.ValidatedBytesByHash[expected.Sha256] = validatedBytes;
            }
            else
            {
                stage.MissingHashes.Add(expected.Sha256);
            }
        }

        string? completedRoot = null;
        string? commitError = null;
        lock (Sync)
        {
            PendingClientStage = stage;
            if (stage.MissingHashes.Count == 0)
            {
                if (!TryCommitClientStageLocked(stage, out completedRoot, out commitError))
                {
                    PendingClientStage = null;
                }
            }
        }

        if (commitError != null)
        {
            error = commitError;
            return false;
        }

        if (completedRoot != null)
        {
            OnClientManifestReady(completedRoot);
        }

        return true;
    }

    /// <summary>
    /// Clears transient transfer and in-memory generation state. Content-addressed disk files are
    /// intentionally retained for reconnects and future worlds.
    /// </summary>
    internal static void ResetRuntimeState()
    {
        lock (Sync)
        {
            ActiveServerSnapshot = null;
            ClearOutgoingTransfersLocked();
        }

        ResetClientState();
    }

    /// <summary>
    /// Starts a remote-authority session with an empty active generation. Local PNG files stay
    /// hidden until the server manifest is fully cached, preventing transient client-specific
    /// replacements while the authoritative generation is in flight.
    /// </summary>
    internal static void BeginRemoteSession()
    {
        ClearClientState(authoritative: true);
    }

    /// <summary>
    /// Clears only remote-client authority and staging. This is the source-of-truth transition
    /// boundary; it deliberately leaves a listen/dedicated server's active blob snapshot intact.
    /// </summary>
    internal static void ResetClientState()
    {
        ClearClientState(authoritative: false);
    }

    private static void ClearClientState(bool authoritative)
    {
        lock (Sync)
        {
            ActiveClientSet = ClientTextureSet.Empty;
            PendingClientStage = null;
        }

        if (!CreatureTextureRegistry.TrySetSynchronizedTextureFiles(
                new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase),
                authoritative,
                out string error))
        {
            CreatureManagerPlugin.Log.LogWarning($"Failed to clear synchronized textures: {error}");
        }
    }

    /// <summary>
    /// Validates and canonicalizes a deserialized manifest without mutating the supplied object.
    /// DomainManager can use this during bundle preflight before accepting it as active state.
    /// </summary>
    internal static bool TryValidateManifest(
        ManifestData? manifest,
        out ManifestData normalized,
        out string error)
    {
        return TryNormalizeAndValidateManifest(manifest, out normalized, out error);
    }

    private static void RPC_TextureRequest(ZRpc rpc, ZPackage package)
    {
        if (!TryGetAuthenticatedClientPeer(rpc, out ZNetPeer peer))
        {
            return;
        }

        long sender = peer.m_uid;

        try
        {
            int version = package.ReadInt();
            string rootHash = NormalizeHash(package.ReadString());
            int count = package.ReadInt();
            if (version != ProtocolVersion || !IsSha256(rootHash) || count < 1 || count > MaxFileCount)
            {
                return;
            }

            List<string> requestedHashes = new(count);
            HashSet<string> uniqueHashes = new(StringComparer.Ordinal);
            for (int index = 0; index < count; ++index)
            {
                string hash = NormalizeHash(package.ReadString());
                if (!IsSha256(hash) || !uniqueHashes.Add(hash))
                {
                    return;
                }

                requestedHashes.Add(hash);
            }

            lock (Sync)
            {
                ServerSnapshot? snapshot = ActiveServerSnapshot;
                if (snapshot == null || !snapshot.RootHash.Equals(rootHash, StringComparison.Ordinal))
                {
                    return;
                }

                DateTime now = DateTime.UtcNow;
                foreach (string hash in requestedHashes)
                {
                    if (!snapshot.ContainsHash(hash))
                    {
                        return;
                    }

                    string transferKey = BuildTransferKey(sender, rootHash, hash);
                    if (QueuedTransfers.Contains(transferKey) ||
                        RecentlyServed.TryGetValue(transferKey, out DateTime lastServed) && now - lastServed < DuplicateServeDelay)
                    {
                        continue;
                    }

                    if (!snapshot.TryGetBlob(hash, out byte[] bytes))
                    {
                        return;
                    }

                    int chunkCount = (bytes.Length + ChunkBytes - 1) / ChunkBytes;
                    if (OutgoingChunks.Count + chunkCount > MaxQueuedChunks)
                    {
                        CreatureManagerPlugin.Log.LogWarning(
                            $"Ignored a synchronized texture request from peer {sender} because the server texture queue is full.");
                        return;
                    }

                    for (int offset = 0; offset < bytes.Length; offset += ChunkBytes)
                    {
                        OutgoingChunks.Enqueue(new OutgoingChunk
                        {
                            PeerId = sender,
                            PeerRpc = rpc,
                            RootHash = rootHash,
                            ContentHash = hash,
                            Offset = offset
                        });
                    }

                    QueuedTransfers.Add(transferKey);
                    RemainingTransferChunks[transferKey] = chunkCount;
                }
            }
        }
        catch (Exception ex)
        {
            CreatureManagerPlugin.Log.LogWarning(
                $"Rejected an invalid synchronized texture request from peer {sender}: {ex.Message}");
        }
    }

    private static void RPC_TextureChunk(ZRpc rpc, ZPackage package)
    {
        if (!IsAuthenticatedServerSender(rpc))
        {
            return;
        }

        try
        {
            int version = package.ReadInt();
            string rootHash = NormalizeHash(package.ReadString());
            string contentHash = NormalizeHash(package.ReadString());
            int totalLength = package.ReadInt();
            int offset = package.ReadInt();
            if (package.Size() - package.GetPos() < sizeof(int))
            {
                return;
            }

            int chunkLength = package.ReadInt();
            if (chunkLength < 1 ||
                chunkLength > ChunkBytes ||
                chunkLength > package.Size() - package.GetPos())
            {
                return;
            }

            byte[] chunk = package.ReadByteArray(chunkLength);
            if (version != ProtocolVersion ||
                !IsSha256(rootHash) ||
                !IsSha256(contentHash) ||
                totalLength < 1 ||
                totalLength > MaxFileBytes ||
                offset < 0 ||
                offset % ChunkBytes != 0 ||
                chunk == null ||
                chunk.Length < 1 ||
                chunk.Length > ChunkBytes ||
                (long)offset + chunk.Length > totalLength ||
                chunk.Length != Math.Min(ChunkBytes, totalLength - offset))
            {
                return;
            }

            string? completedRoot = null;
            lock (Sync)
            {
                ClientStage? stage = PendingClientStage;
                if (stage == null ||
                    !stage.Manifest.RootHash.Equals(rootHash, StringComparison.Ordinal) ||
                    !stage.MissingHashes.Contains(contentHash))
                {
                    return;
                }

                ManifestEntryData? expected = stage.Manifest.Files.FirstOrDefault(file => file.Sha256.Equals(contentHash, StringComparison.Ordinal));
                if (expected == null || expected.Length != totalLength)
                {
                    return;
                }

                if (!stage.Receivers.TryGetValue(contentHash, out ReceiveState receiver))
                {
                    receiver = new ReceiveState(totalLength);
                    stage.Receivers.Add(contentHash, receiver);
                }

                int chunkIndex = offset / ChunkBytes;
                if (!receiver.ReceivedChunks[chunkIndex])
                {
                    Buffer.BlockCopy(chunk, 0, receiver.Bytes, offset, chunk.Length);
                    receiver.ReceivedChunks[chunkIndex] = true;
                    ++receiver.ReceivedCount;
                }

                if (receiver.ReceivedCount != receiver.ReceivedChunks.Length)
                {
                    return;
                }

                if (!TryValidateReceivedContent(receiver.Bytes, expected, out string cachePath, out string validationError))
                {
                    stage.Receivers.Remove(contentHash);
                    stage.NextRequestUtc = DateTime.UtcNow.Add(RequestRetryDelay);
                    CreatureManagerPlugin.Log.LogWarning(
                        $"Rejected synchronized texture {contentHash} for manifest {rootHash}: {validationError}");
                    return;
                }

                stage.Receivers.Remove(contentHash);
                stage.MissingHashes.Remove(contentHash);
                stage.PathsByHash[contentHash] = cachePath;
                stage.ValidatedBytesByHash[contentHash] = receiver.Bytes;
                if (stage.MissingHashes.Count == 0)
                {
                    if (!TryCommitClientStageLocked(stage, out completedRoot, out string commitError))
                    {
                        PendingClientStage = null;
                        CreatureManagerPlugin.Log.LogWarning(
                            $"Kept the last-known-good synchronized textures because manifest {rootHash} could not be committed: {commitError}");
                    }
                }
            }

            if (completedRoot != null)
            {
                OnClientManifestReady(completedRoot);
            }
        }
        catch (Exception ex)
        {
            CreatureManagerPlugin.Log.LogWarning($"Rejected an invalid synchronized texture chunk: {ex.Message}");
        }
    }

    private static void SendQueuedChunks()
    {
        int attempts;
        lock (Sync)
        {
            attempts = OutgoingChunks.Count;
        }

        int sent = 0;
        while (sent < MaxChunksPerUpdate && attempts-- > 0)
        {
            OutgoingChunk? outgoing;
            byte[] sourceBytes;
            string transferKey;
            lock (Sync)
            {
                if (OutgoingChunks.Count == 0)
                {
                    return;
                }

                outgoing = OutgoingChunks.Dequeue();
                transferKey = BuildTransferKey(outgoing.PeerId, outgoing.RootHash, outgoing.ContentHash);
                ServerSnapshot? snapshot = ActiveServerSnapshot;
                if (snapshot == null ||
                    !snapshot.RootHash.Equals(outgoing.RootHash, StringComparison.Ordinal) ||
                    !snapshot.TryGetBlob(outgoing.ContentHash, out sourceBytes))
                {
                    CompleteQueuedChunkLocked(transferKey, served: false);
                    continue;
                }
            }

            if (!TryGetAuthenticatedClientPeer(outgoing.PeerRpc, out ZNetPeer peer) || peer.m_uid != outgoing.PeerId)
            {
                lock (Sync)
                {
                    RemovePeerTransfersLocked(outgoing.PeerId, outgoing.PeerRpc);
                }

                continue;
            }

            if (outgoing.PeerRpc.GetSocket().GetSendQueueSize() > 20_000)
            {
                lock (Sync)
                {
                    OutgoingChunks.Enqueue(outgoing);
                }

                continue;
            }

            int length = Math.Min(ChunkBytes, sourceBytes.Length - outgoing.Offset);
            byte[] chunk = new byte[length];
            Buffer.BlockCopy(sourceBytes, outgoing.Offset, chunk, 0, length);
            ZPackage package = new();
            package.Write(ProtocolVersion);
            package.Write(outgoing.RootHash);
            package.Write(outgoing.ContentHash);
            package.Write(sourceBytes.Length);
            package.Write(outgoing.Offset);
            package.Write(chunk);
            try
            {
                outgoing.PeerRpc.Invoke(ChunkRpc, package);
                lock (Sync)
                {
                    CompleteQueuedChunkLocked(transferKey, served: true);
                }

                ++sent;
            }
            catch (Exception ex)
            {
                lock (Sync)
                {
                    RemovePeerTransfersLocked(outgoing.PeerId, outgoing.PeerRpc);
                }

                CreatureManagerPlugin.Log.LogWarning(
                    $"Stopped synchronized texture transfer to peer {outgoing.PeerId}: {ex.Message}");
            }
        }
    }

    private static void SendMissingRequestIfDue()
    {
        ZNetPeer? serverPeer = ZNet.instance.GetServerPeer();
        if (serverPeer?.m_rpc == null || !serverPeer.IsReady() || !serverPeer.m_rpc.IsConnected())
        {
            return;
        }

        string rootHash;
        string[] missingHashes;
        lock (Sync)
        {
            ClientStage? stage = PendingClientStage;
            DateTime now = DateTime.UtcNow;
            if (stage == null || stage.MissingHashes.Count == 0 || now < stage.NextRequestUtc)
            {
                return;
            }

            rootHash = stage.Manifest.RootHash;
            missingHashes = stage.MissingHashes.OrderBy(hash => hash, StringComparer.Ordinal).ToArray();
            stage.NextRequestUtc = now.Add(RequestRetryDelay);
        }

        ZPackage request = new();
        request.Write(ProtocolVersion);
        request.Write(rootHash);
        request.Write(missingHashes.Length);
        foreach (string hash in missingHashes)
        {
            request.Write(hash);
        }

        try
        {
            serverPeer.m_rpc.Invoke(RequestRpc, request);
        }
        catch (Exception ex)
        {
            CreatureManagerPlugin.Log.LogWarning(
                $"Failed to request synchronized textures for manifest {rootHash}: {ex.Message}");
        }
    }

    private static bool TryGetAuthenticatedClientPeer(ZRpc? rpc, out ZNetPeer peer)
    {
        peer = null!;
        if (rpc == null || ZNet.instance == null || !ZNet.instance.IsServer())
        {
            return false;
        }

        peer = ZNet.instance.GetPeer(rpc);
        return peer != null &&
               peer.m_uid != 0L &&
               peer.IsReady() &&
               ReferenceEquals(peer.m_rpc, rpc) &&
               rpc.IsConnected();
    }

    private static bool IsAuthenticatedServerSender(ZRpc? rpc)
    {
        if (rpc == null ||
            ZNet.instance == null ||
            ZNet.instance.IsServer())
        {
            return false;
        }

        ZNetPeer? serverPeer = ZNet.instance.GetServerPeer();
        return serverPeer != null &&
               serverPeer.IsReady() &&
               rpc.IsConnected() &&
               ReferenceEquals(serverPeer.m_rpc, rpc) &&
               ReferenceEquals(ZNet.instance.GetServerRPC(), rpc);
    }

    private static bool TryReadStablePng(
        string path,
        out byte[] bytes,
        out int width,
        out int height,
        out string error)
    {
        bytes = Array.Empty<byte>();
        width = 0;
        height = 0;
        error = "";
        if (!IsSafeRegularPngFile(path))
        {
            error = $"Texture file '{path}' is no longer a regular file.";
            return false;
        }

        FileInfo before = new(path);
        if (!before.Exists)
        {
            error = $"Texture file '{path}' disappeared while its snapshot was being built.";
            return false;
        }

        if (before.Length < 1 || before.Length > MaxFileBytes)
        {
            error = $"Texture file '{path}' must contain from 1 to {MaxFileBytes} bytes.";
            return false;
        }

        using (FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            if (stream.Length != before.Length)
            {
                error = $"Texture file '{path}' changed while it was being opened.";
                return false;
            }

            bytes = new byte[checked((int)stream.Length)];
            int totalRead = 0;
            while (totalRead < bytes.Length)
            {
                int read = stream.Read(bytes, totalRead, bytes.Length - totalRead);
                if (read == 0)
                {
                    error = $"Texture file '{path}' ended before its declared length.";
                    bytes = Array.Empty<byte>();
                    return false;
                }

                totalRead += read;
            }
        }

        FileInfo after = new(path);
        if (!after.Exists ||
            !IsSafeRegularPngFile(path) ||
            before.Length != after.Length ||
            before.LastWriteTimeUtc != after.LastWriteTimeUtc ||
            bytes.LongLength != before.Length)
        {
            error = $"Texture file '{path}' changed while it was being read; save it again after the current write completes.";
            bytes = Array.Empty<byte>();
            return false;
        }

        return TryValidatePng(bytes, out width, out height, out error);
    }

    private static bool IsSafeRegularPngFile(string path)
    {
        try
        {
            FileAttributes attributes = File.GetAttributes(path);
            return (attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint)) == 0;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryValidatePng(byte[] bytes, out int width, out int height, out string error)
    {
        width = 0;
        height = 0;
        error = "";
        if (bytes.Length < 45 || !PngSignature.SequenceEqual(bytes.Take(PngSignature.Length)))
        {
            error = "The file does not have a valid PNG signature and minimum chunk structure.";
            return false;
        }

        int offset = PngSignature.Length;
        bool sawHeader = false;
        bool sawImageData = false;
        bool sawEnd = false;
        while (offset <= bytes.Length - 12)
        {
            uint chunkLengthValue = ReadBigEndianUInt32(bytes, offset);
            if (chunkLengthValue > int.MaxValue)
            {
                error = "A PNG chunk length exceeds the supported range.";
                return false;
            }

            int chunkLength = (int)chunkLengthValue;
            long nextOffset = (long)offset + 12L + chunkLength;
            if (nextOffset > bytes.Length)
            {
                error = "A PNG chunk extends beyond the end of the file.";
                return false;
            }

            uint expectedCrc = ReadBigEndianUInt32(bytes, offset + 8 + chunkLength);
            uint actualCrc = ComputeCrc32(bytes, offset + 4, chunkLength + 4);
            if (expectedCrc != actualCrc)
            {
                error = "A PNG chunk has an invalid CRC.";
                return false;
            }

            bool isHeader = MatchesChunkType(bytes, offset + 4, 'I', 'H', 'D', 'R');
            bool isImageData = MatchesChunkType(bytes, offset + 4, 'I', 'D', 'A', 'T');
            bool isEnd = MatchesChunkType(bytes, offset + 4, 'I', 'E', 'N', 'D');
            if (!sawHeader)
            {
                if (!isHeader || chunkLength != 13)
                {
                    error = "The first PNG chunk must be a 13-byte IHDR chunk.";
                    return false;
                }

                uint widthValue = ReadBigEndianUInt32(bytes, offset + 8);
                uint heightValue = ReadBigEndianUInt32(bytes, offset + 12);
                if (widthValue < 1 || heightValue < 1 || widthValue > MaxDimension || heightValue > MaxDimension)
                {
                    error = $"PNG dimensions must be from 1 to {MaxDimension} pixels on each axis.";
                    return false;
                }

                if (bytes[offset + 18] != 0 || bytes[offset + 19] != 0 || bytes[offset + 20] > 1)
                {
                    error = "The PNG uses an unsupported compression, filter, or interlace method.";
                    return false;
                }

                width = (int)widthValue;
                height = (int)heightValue;
                if (!IsSupportedPngColorLayout(bytes[offset + 16], bytes[offset + 17]))
                {
                    error = "The PNG IHDR bit depth and color type combination is invalid.";
                    return false;
                }

                sawHeader = true;
            }
            else if (isHeader)
            {
                error = "The PNG contains more than one IHDR chunk.";
                return false;
            }

            if (isImageData)
            {
                // Empty IDAT chunks are legal separators; require at least one chunk that
                // actually contributes to the zlib stream before accepting IEND.
                sawImageData |= chunkLength > 0;
            }

            offset = (int)nextOffset;
            if (isEnd)
            {
                if (!sawImageData || chunkLength != 0 || offset != bytes.Length)
                {
                    error = "The PNG must contain image data followed by a valid final IEND chunk.";
                    return false;
                }

                sawEnd = true;
                break;
            }
        }

        if (!sawHeader || !sawEnd)
        {
            error = "The PNG is missing its IHDR or IEND chunk.";
            return false;
        }

        return true;
    }

    private static bool IsSupportedPngColorLayout(byte bitDepth, byte colorType)
    {
        return colorType switch
        {
            0 => bitDepth is 1 or 2 or 4 or 8 or 16,
            2 => bitDepth is 8 or 16,
            3 => bitDepth is 1 or 2 or 4 or 8,
            4 => bitDepth is 8 or 16,
            6 => bitDepth is 8 or 16,
            _ => false
        };
    }

    private static ManifestData CreateManifest(IEnumerable<ManifestEntryData> entries)
    {
        ManifestData manifest = new()
        {
            Version = ProtocolVersion,
            Files = entries
                .OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(entry => entry.Name, StringComparer.Ordinal)
                .Select(CloneEntry)
                .ToList()
        };
        manifest.RootHash = ComputeManifestRoot(manifest.Files);
        return manifest;
    }

    private static bool TryNormalizeAndValidateManifest(
        ManifestData? manifest,
        out ManifestData normalized,
        out string error)
    {
        normalized = CreateEmptyManifest();
        error = "";
        if (manifest == null || manifest.Files == null || manifest.Version != ProtocolVersion)
        {
            error = $"Texture manifest must use protocol version {ProtocolVersion} and contain a files list.";
            return false;
        }

        if (manifest.Files.Count > MaxFileCount)
        {
            error = $"Texture manifest exceeds the {MaxFileCount}-file safety limit.";
            return false;
        }

        List<ManifestEntryData> entries = new(manifest.Files.Count);
        HashSet<string> names = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, ManifestEntryData> entriesByHash = new(StringComparer.Ordinal);
        long totalBytes = 0;
        long totalPixels = 0;
        foreach (ManifestEntryData entry in manifest.Files)
        {
            if (entry == null ||
                !TryNormalizeLogicalName(entry.Name, requirePngExtension: true, out string logicalName, out error))
            {
                return false;
            }

            if (!logicalName.Equals(entry.Name, StringComparison.Ordinal) || !names.Add(logicalName))
            {
                error = $"Texture manifest name '{entry.Name}' is not canonical or is duplicated.";
                return false;
            }

            string hash = NormalizeHash(entry.Sha256);
            if (!IsSha256(hash) ||
                entry.Length < 1 ||
                entry.Length > MaxFileBytes ||
                entry.Width < 1 ||
                entry.Width > MaxDimension ||
                entry.Height < 1 ||
                entry.Height > MaxDimension)
            {
                error = $"Texture manifest entry '{logicalName}' has an invalid hash, size, or dimensions.";
                return false;
            }

            totalBytes += entry.Length;
            totalPixels += (long)entry.Width * entry.Height;
            if (totalBytes > MaxTotalBytes)
            {
                error = $"Texture manifest exceeds the {MaxTotalBytes}-byte aggregate safety limit.";
                return false;
            }


            if (totalPixels > MaxTotalPixels)
            {
                error = $"Texture manifest exceeds the {MaxTotalPixels}-pixel aggregate safety limit.";
                return false;
            }

            ManifestEntryData normalizedEntry = new()
            {
                Name = logicalName,
                Sha256 = hash,
                Length = entry.Length,
                Width = entry.Width,
                Height = entry.Height
            };
            if (entriesByHash.TryGetValue(hash, out ManifestEntryData existing) &&
                (existing.Length != normalizedEntry.Length || existing.Width != normalizedEntry.Width || existing.Height != normalizedEntry.Height))
            {
                error = $"Texture manifest hash '{hash}' has inconsistent metadata.";
                return false;
            }

            entriesByHash[hash] = normalizedEntry;
            entries.Add(normalizedEntry);
        }

        normalized = CreateManifest(entries);
        string suppliedRoot = NormalizeHash(manifest.RootHash);
        if (!IsSha256(suppliedRoot) || !normalized.RootHash.Equals(suppliedRoot, StringComparison.Ordinal))
        {
            error = "Texture manifest root hash does not match its canonical file entries.";
            return false;
        }

        return true;
    }

    private static string ComputeManifestRoot(IEnumerable<ManifestEntryData> entries)
    {
        StringBuilder canonical = new();
        canonical.Append("CreatureManager.TextureManifest\0");
        canonical.Append(ProtocolVersion.ToString(CultureInfo.InvariantCulture));
        canonical.Append('\n');
        foreach (ManifestEntryData entry in entries
                     .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(item => item.Name, StringComparer.Ordinal))
        {
            AppendCanonicalField(canonical, entry.Name);
            AppendCanonicalField(canonical, entry.Sha256);
            canonical.Append(entry.Length.ToString(CultureInfo.InvariantCulture));
            canonical.Append('\0');
            canonical.Append(entry.Width.ToString(CultureInfo.InvariantCulture));
            canonical.Append('\0');
            canonical.Append(entry.Height.ToString(CultureInfo.InvariantCulture));
            canonical.Append('\n');
        }

        return ComputeSha256(Encoding.UTF8.GetBytes(canonical.ToString()));
    }

    private static void AppendCanonicalField(StringBuilder builder, string value)
    {
        builder.Append(value.Length.ToString(CultureInfo.InvariantCulture));
        builder.Append(':');
        builder.Append(value);
        builder.Append('\0');
    }

    private static bool TryFindOrCreateCachedContent(
        IEnumerable<ManifestEntryData> sameHashEntries,
        ManifestEntryData expected,
        out string cachePath,
        out byte[] bytes)
    {
        bytes = Array.Empty<byte>();
        cachePath = GetCachePath(expected.Sha256);
        if (TryValidateContentFile(cachePath, expected, out byte[]? cachedBytes) && cachedBytes != null)
        {
            bytes = cachedBytes;
            TouchCacheFile(cachePath);
            return true;
        }

        TryDeleteInvalidCacheFile(cachePath);
        foreach (ManifestEntryData entry in sameHashEntries)
        {
            if (!CreatureTextureRegistry.TryResolveLocalPngFile(
                    entry.Name,
                    out _,
                    out string localPath,
                    out _))
            {
                continue;
            }

            if (!TryValidateContentFile(localPath, expected, out byte[]? localBytes) || localBytes == null)
            {
                continue;
            }

            if (TryWriteCacheFile(expected.Sha256, localBytes, out cachePath, out _))
            {
                bytes = localBytes;
                return true;
            }

            return false;
        }

        return false;
    }

    private static bool TryValidateReceivedContent(
        byte[] bytes,
        ManifestEntryData expected,
        out string cachePath,
        out string error)
    {
        cachePath = "";
        error = "";
        if (bytes.Length != expected.Length ||
            !ComputeSha256(bytes).Equals(expected.Sha256, StringComparison.Ordinal) ||
            !TryValidatePng(bytes, out int width, out int height, out error) ||
            width != expected.Width ||
            height != expected.Height)
        {
            if (error.Length == 0)
            {
                error = "The received size, SHA-256, or dimensions do not match the manifest.";
            }

            return false;
        }

        return TryWriteCacheFile(expected.Sha256, bytes, out cachePath, out error);
    }

    private static bool TryValidateContentFile(string path, ManifestEntryData expected, out byte[]? bytes)
    {
        bytes = null;
        try
        {
            FileInfo file = new(path);
            if (!file.Exists || file.Length != expected.Length || file.Length < 1 || file.Length > MaxFileBytes)
            {
                return false;
            }

            byte[] candidate = File.ReadAllBytes(path);
            if (candidate.Length != expected.Length ||
                !ComputeSha256(candidate).Equals(expected.Sha256, StringComparison.Ordinal) ||
                !TryValidatePng(candidate, out int width, out int height, out _) ||
                width != expected.Width ||
                height != expected.Height)
            {
                return false;
            }

            bytes = candidate;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryWriteCacheFile(string hash, byte[] bytes, out string path, out string error)
    {
        path = GetCachePath(hash);
        error = "";
        string? temporaryPath = null;
        try
        {
            Directory.CreateDirectory(GetCacheDirectory());
            temporaryPath = path + ".tmp." + Guid.NewGuid().ToString("N");
            File.WriteAllBytes(temporaryPath, bytes);
            if (File.Exists(path))
            {
                File.Replace(temporaryPath, path, null);
                temporaryPath = null;
            }
            else
            {
                File.Move(temporaryPath, path);
                temporaryPath = null;
            }
            TouchCacheFile(path);
            PrunePersistentCache();
            return true;
        }
        catch (Exception ex)
        {
            error = $"Failed to store synchronized texture cache '{path}': {ex.Message}";
            return false;
        }
        finally
        {
            if (temporaryPath != null)
            {
                try
                {
                    File.Delete(temporaryPath);
                }
                catch
                {
                    // Best-effort cleanup of a cache-local temporary file.
                }
            }
        }
    }

    private static bool TryCommitClientStageLocked(
        ClientStage stage,
        out string? completedRoot,
        out string error)
    {
        completedRoot = null;
        error = "";
        Dictionary<string, string> pathsByName = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, byte[]> bytesByName = new(StringComparer.OrdinalIgnoreCase);
        foreach (ManifestEntryData entry in stage.Manifest.Files)
        {
            if (!stage.PathsByHash.TryGetValue(entry.Sha256, out string path))
            {
                error = $"Synchronized texture hash {entry.Sha256} is not staged.";
                return false;
            }

            if (!stage.ValidatedBytesByHash.TryGetValue(entry.Sha256, out byte[]? bytes))
            {
                error = $"Synchronized texture bytes for '{entry.Name}' are not staged.";
                return false;
            }

            pathsByName.Add(entry.Name, path);
            bytesByName.Add(entry.Name, bytes!);
        }

        if (!CreatureTextureRegistry.TrySetSynchronizedTextureFiles(
                bytesByName,
                authoritative: true,
                out error))
        {
            return false;
        }

        ActiveClientSet = new ClientTextureSet(stage.Manifest.RootHash, pathsByName);
        if (ReferenceEquals(PendingClientStage, stage))
        {
            PendingClientStage = null;
        }

        completedRoot = stage.Manifest.RootHash;
        return true;
    }

    private static void OnClientManifestReady(string rootHash)
    {
        PrunePersistentCache();
        try
        {
            ClientManifestReady?.Invoke(rootHash);
        }
        catch (Exception ex)
        {
            CreatureManagerPlugin.Log.LogError(
                $"A synchronized texture-ready subscriber failed for manifest {rootHash}: {ex}");
        }
    }

    private static void PrunePersistentCache()
    {
        try
        {
            string directory = GetCacheDirectory();
            if (!Directory.Exists(directory))
            {
                return;
            }

            FileInfo[] files = new DirectoryInfo(directory)
                .EnumerateFiles("*.png", SearchOption.TopDirectoryOnly)
                .Where(file => IsSha256(Path.GetFileNameWithoutExtension(file.Name)))
                .OrderBy(file => file.LastAccessTimeUtc)
                .ToArray();
            long total = files.Sum(file => file.Length);
            if (total <= MaxPersistentCacheBytes && files.Length <= MaxPersistentCacheFiles)
            {
                return;
            }

            HashSet<string> protectedHashes = new(StringComparer.Ordinal);
            lock (Sync)
            {
                foreach (string path in ActiveClientSet.PathsByName.Values)
                {
                    protectedHashes.Add(Path.GetFileNameWithoutExtension(path));
                }

                if (PendingClientStage != null)
                {
                    foreach (ManifestEntryData entry in PendingClientStage.Manifest.Files)
                    {
                        protectedHashes.Add(entry.Sha256);
                    }
                }
            }

            int remainingFiles = files.Length;
            foreach (FileInfo file in files)
            {
                if (total <= MaxPersistentCacheBytes && remainingFiles <= MaxPersistentCacheFiles)
                {
                    break;
                }

                string hash = Path.GetFileNameWithoutExtension(file.Name);
                if (protectedHashes.Contains(hash))
                {
                    continue;
                }

                long length = file.Length;
                file.Delete();
                total -= length;
                --remainingFiles;
            }
        }
        catch (Exception ex)
        {
            CreatureManagerPlugin.Log.LogWarning($"Failed to prune the synchronized texture cache: {ex.Message}");
        }
    }

    private static void TouchCacheFile(string path)
    {
        try
        {
            File.SetLastAccessTimeUtc(path, DateTime.UtcNow);
        }
        catch
        {
            // Last-access timestamps are an optional eviction hint.
        }
    }

    private static void TryDeleteInvalidCacheFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // A locked stale cache entry simply remains unavailable for this stage.
        }
    }

    private static void PruneServeHistory()
    {
        DateTime cutoff = DateTime.UtcNow.Subtract(ServeHistoryLifetime);
        lock (Sync)
        {
            foreach (string key in RecentlyServed.Where(pair => pair.Value < cutoff).Select(pair => pair.Key).ToArray())
            {
                RecentlyServed.Remove(key);
            }
        }
    }

    private static void ClearOutgoingTransfersLocked()
    {
        OutgoingChunks.Clear();
        QueuedTransfers.Clear();
        RemainingTransferChunks.Clear();
        RecentlyServed.Clear();
    }

    private static void CompleteQueuedChunkLocked(string transferKey, bool served)
    {
        if (!RemainingTransferChunks.TryGetValue(transferKey, out int remaining))
        {
            QueuedTransfers.Remove(transferKey);
            return;
        }

        if (remaining > 1)
        {
            RemainingTransferChunks[transferKey] = remaining - 1;
            return;
        }

        RemainingTransferChunks.Remove(transferKey);
        QueuedTransfers.Remove(transferKey);
        if (served)
        {
            RecentlyServed[transferKey] = DateTime.UtcNow;
        }
    }

    private static void RemovePeerTransfersLocked(long peerId, ZRpc? peerRpc)
    {
        if (peerId == 0L && peerRpc == null)
        {
            return;
        }

        OutgoingChunk[] retained = OutgoingChunks
            .Where(chunk => chunk.PeerId != peerId || peerRpc != null && !ReferenceEquals(chunk.PeerRpc, peerRpc))
            .ToArray();
        OutgoingChunks.Clear();
        foreach (OutgoingChunk chunk in retained)
        {
            OutgoingChunks.Enqueue(chunk);
        }

        string prefix = peerId + "|";
        foreach (string key in QueuedTransfers.Where(key => key.StartsWith(prefix, StringComparison.Ordinal)).ToArray())
        {
            QueuedTransfers.Remove(key);
            RemainingTransferChunks.Remove(key);
            RecentlyServed.Remove(key);
        }
    }

    private static string GetCacheDirectory()
    {
        return Path.Combine(CreatureDomainManager.CacheDirectoryPath, "synced-textures");
    }

    private static string GetCachePath(string hash)
    {
        return Path.Combine(GetCacheDirectory(), hash + ".png");
    }

    private static bool TryNormalizeLogicalName(
        string? value,
        bool requirePngExtension,
        out string logicalName,
        out string error)
    {
        logicalName = "";
        error = "";
        string name = value?.Trim() ?? "";
        if (name.Length == 0 || name.Length > MaxLogicalNameLength)
        {
            error = $"Texture file names must contain from 1 to {MaxLogicalNameLength} characters.";
            return false;
        }

        if (Path.IsPathRooted(name) ||
            name.IndexOf(Path.DirectorySeparatorChar) >= 0 ||
            name.IndexOf(Path.AltDirectorySeparatorChar) >= 0 ||
            name.IndexOf(':') >= 0 ||
            name.IndexOf("..", StringComparison.Ordinal) >= 0 ||
            name.EndsWith(".", StringComparison.Ordinal) ||
            name.EndsWith(" ", StringComparison.Ordinal) ||
            name.Any(char.IsControl) ||
            name.IndexOfAny(PortableInvalidFileNameChars) >= 0 ||
            !Path.GetFileName(name).Equals(name, StringComparison.Ordinal) ||
            name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            error = $"Texture file name '{name}' must be a safe top-level file name, not a path.";
            return false;
        }

        string extension = Path.GetExtension(name);
        if (extension.Length == 0)
        {
            if (requirePngExtension)
            {
                error = $"Texture manifest name '{name}' must include the .png extension.";
                return false;
            }

            name += ".png";
        }
        else if (!extension.Equals(".png", StringComparison.OrdinalIgnoreCase))
        {
            error = $"Texture file name '{name}' must use the .png extension.";
            return false;
        }

        logicalName = name;
        return true;
    }

    private static bool IsExplicitNonPngTextureReference(string? value)
    {
        string name = value?.Trim() ?? "";
        if (name.Length == 0)
        {
            return false;
        }

        try
        {
            string extension = Path.GetExtension(name);
            return extension.Length > 0 && !extension.Equals(".png", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException || ex is PathTooLongException)
        {
            return false;
        }
    }

    private static string ComputeSha256(byte[] bytes)
    {
        using SHA256 sha = SHA256.Create();
        byte[] digest = sha.ComputeHash(bytes);
        StringBuilder text = new(digest.Length * 2);
        foreach (byte value in digest)
        {
            text.Append(value.ToString("x2"));
        }

        return text.ToString();
    }

    private static string NormalizeHash(string? value)
    {
        return value?.Trim().ToLowerInvariant() ?? "";
    }

    private static bool IsSha256(string? value)
    {
        if (value == null || value.Length != HashTextLength)
        {
            return false;
        }

        foreach (char character in value)
        {
            if (!((character >= '0' && character <= '9') || (character >= 'a' && character <= 'f')))
            {
                return false;
            }
        }

        return true;
    }

    private static uint ReadBigEndianUInt32(byte[] bytes, int offset)
    {
        return ((uint)bytes[offset] << 24) |
               ((uint)bytes[offset + 1] << 16) |
               ((uint)bytes[offset + 2] << 8) |
               bytes[offset + 3];
    }

    private static uint ComputeCrc32(byte[] bytes, int offset, int count)
    {
        uint crc = uint.MaxValue;
        int end = offset + count;
        for (int index = offset; index < end; ++index)
        {
            crc = Crc32Table[(int)((crc ^ bytes[index]) & 0xff)] ^ (crc >> 8);
        }

        return crc ^ uint.MaxValue;
    }

    private static uint[] BuildCrc32Table()
    {
        uint[] table = new uint[256];
        for (int value = 0; value < table.Length; ++value)
        {
            uint crc = (uint)value;
            for (int bit = 0; bit < 8; ++bit)
            {
                crc = (crc & 1) != 0 ? 0xedb88320U ^ (crc >> 1) : crc >> 1;
            }

            table[value] = crc;
        }

        return table;
    }

    private static bool MatchesChunkType(byte[] bytes, int offset, char a, char b, char c, char d)
    {
        return bytes[offset] == a && bytes[offset + 1] == b && bytes[offset + 2] == c && bytes[offset + 3] == d;
    }

    private static string BuildTransferKey(long peerId, string rootHash, string contentHash)
    {
        return peerId + "|" + rootHash + "|" + contentHash;
    }

    private static ManifestData CloneManifest(ManifestData source)
    {
        return new ManifestData
        {
            Version = source.Version,
            RootHash = source.RootHash,
            Files = source.Files.Select(CloneEntry).ToList()
        };
    }

    private static ManifestEntryData CloneEntry(ManifestEntryData source)
    {
        return new ManifestEntryData
        {
            Name = source.Name,
            Sha256 = source.Sha256,
            Length = source.Length,
            Width = source.Width,
            Height = source.Height
        };
    }
}
