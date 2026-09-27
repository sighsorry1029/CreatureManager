using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using BepInEx.Configuration;
using Mono.Cecil;
using MonoMod.Utils;
using UnityEngine;

// Execute the production reservation dispatcher, guards, geometry decision and spawn/cost
// ordering. World queries, native instantiation and economy storage are fixture boundaries.
internal static class EnforcerDelayContracts
{
    private const BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static Type Karma = null!;
    private static readonly Dictionary<MethodInfo, MethodInfo> Copies = new();
    private static IList Pending = null!, Players = null!;
    private static object State = null!;
    private static object GlobalSettings = null!, SelectedCandidate = null!, SelectedSettings = null!, BiomeDefinition = null!;
    private static readonly object Sync = new();
    private static readonly string[] EnforcerSpawnQuotes = { "fixture" };
    private static readonly ZoneSystem Zone = Uninitialized<ZoneSystem>();
    private static readonly GameObject Prefab = Uninitialized<GameObject>();
    private static readonly Character Creature = Uninitialized<Character>();
    private static readonly ZRoutedRpc RoutedRpc = Uninitialized<ZRoutedRpc>();
    private static ConfigEntryBase Mode = null!;
    private static ConfigEntry<int> Cap = null!;
    private static ConfigEntry<int> Delay = null!;
    private static bool Boss, Loaded, Clearance, Anchor, Capacity, PrefabAvailable, SpawnSucceeds;
    private static float Cooldown;
    private static int Active, Spawns, Costs, Ensures, Messages, GeometryChecks;
    private static int PositionSelections, Warnings;
    private static Vector3 LastSpawn, LastGeometry, AnchorOffset;
    private static ZDOID Excluded;
    private static readonly List<string> Events = new();

    internal static void Run(Assembly plugin)
    {
        Karma = plugin.GetType("CreatureManager.CreatureKarmaManager", true)!;
        Pending = (IList)Activator.CreateInstance(Karma.GetField("PendingEnforcerSummons", Static)!.FieldType)!;
        Players = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(Nested("ConnectedPlayerContext")))!;
        State = Activator.CreateInstance(Nested("SectorState"), true)!;
        GlobalSettings = Activator.CreateInstance(Nested("KarmaSettings"), true)!;
        Type entry = plugin.GetType("CreatureManager.CreatureManagerPlugin", true)!;
        var config = new ConfigFile(Path.Combine(Path.GetTempPath(), "CreatureManager-delay-" + Guid.NewGuid() + ".cfg"), false) { SaveOnConfigSet = false };
        var saved = new Dictionary<FieldInfo, object?>();
        foreach (string name in new[] { "KarmaMode", "MaximumEnforcersPerSector", "BlockEnforcerWhileBossActive", "DungeonEnforcerSpawnDelay" })
        {
            FieldInfo field = entry.GetField(name, Static)!;
            saved[field] = field.GetValue(null);
            Type valueType = field.FieldType.GetGenericArguments()[0];
            object initial = valueType == typeof(int) ? 1 : Enum.ToObject(valueType, 1);
            MethodInfo bind = typeof(ConfigFile).GetMethods().Single(m => m.Name == "Bind" && m.IsGenericMethodDefinition &&
                m.GetParameters().Length == 4 && m.GetParameters()[3].ParameterType == typeof(ConfigDescription));
            var setting = (ConfigEntryBase)bind.MakeGenericMethod(valueType).Invoke(config,
                new[] { "fixture", name, initial, new ConfigDescription("") })!;
            field.SetValue(null, setting);
            if (name == "KarmaMode") Mode = setting;
            if (name == "MaximumEnforcersPerSector") Cap = (ConfigEntry<int>)setting;
            if (name == "DungeonEnforcerSpawnDelay") Delay = (ConfigEntry<int>)setting;
        }
        try
        {
            CheckAdmission();
            CheckTimingAndPosition();
            CheckCancellation();
            CheckLimitsAndOmen();
            CheckGeometry();
            CheckWarningProtocol();
            CheckIntegration(plugin);
        }
        finally
        {
            foreach (var item in saved) item.Key.SetValue(null, item.Value);
            Pending.Clear(); Players.Clear();
        }
        System.Console.WriteLine("Enforcer delay contracts passed: dungeon admission/warning, zero/outdoor immediate, exact deadline/fixed position, no pre-spawn charge, duplicate ticks, pending+active caps, sequential cap reduction, player departure/realm, boss/Off/cooldown/Karma cancellation, Omen bypass/dead-ID, sector re-ensure/failures, loaded clearance/unloaded anchor, trusted warning payload and unchanged quotes, lifecycle/call-order inspection. Native/world/storage boundaries substituted; no actual encounter or network session.");
    }

    private static void CheckAdmission()
    {
        foreach (bool dungeon in new[] { true, false })
        foreach (int delay in new[] { 0, 5, 10 })
        {
            Reset(); Delay.Value = delay;
            object seed = Plan(new Vector3(8, 5000, 8)); Pending.Clear();
            SelectedCandidate = Get(seed, "Candidate"); SelectedSettings = Get(seed, "Settings");
            Set(SelectedSettings, "Chance", 100f);
            BiomeDefinition = Activator.CreateInstance(Nested("EnforcerBiomeDefinition"), true)!;
            ((IList)Get(BiomeDefinition, "Dungeon")).Add(SelectedCandidate);
            ((IList)Get(BiomeDefinition, "Outdoor")).Add(SelectedCandidate);
            if (!dungeon) { Players.Clear(); Player(Vector3.zero); }
            object?[] args = { Players[0], 0f, null, false, false, false, null, null, ZDOID.None };
            Require((bool)Call("TrySummonForPlayer", args)!, "admission accepted");
            bool deferred = dungeon && delay > 0;
            Require(Pending.Count == (deferred ? 1 : 0) && Spawns == (deferred ? 0 : 1) &&
                    Costs == (deferred ? 0 : 1) && Warnings == (deferred ? 1 : 0), "only delayed dungeon encounters reserve and warn");
            Require(PositionSelections == 1, "position selected once at admission");
            if (deferred)
            {
                Require((float)Get(Pending[0]!, "SpawnAt") == delay, "deadline uses config");
                Require(!(bool)Call("TrySummonForPlayer", args)! && args[2]!.ToString() == "ActiveEnforcerCap", "repeat admission cannot stack reservations");
                Delay.Value = 0; Tick(delay - 1f);
                Require(Spawns == 0 && Pending.Count == 1, "live delay edit preserves existing deadline");
                Tick(delay);
                Require(Spawns == 1 && Costs == 1 && PositionSelections == 1, "due does not reselect encounter position");
            }
        }
    }

    private static void CheckTimingAndPosition()
    {
        Reset();
        object plan = Plan(new Vector3(8, 5000, 8));
        Tick(9.9f);
        Require(Pending.Count == 1 && Spawns == 0 && Costs == 0 && Ensures == 0 && GeometryChecks == 0, "waiting does not create or charge");
        Players.Clear(); Player(new Vector3(-20, 5000, -20));
        Tick(10f);
        Require(Pending.Count == 0 && Spawns == 1 && Costs == 1 && LastSpawn == new Vector3(8, 5000, 8), "moves target but never spawn position");
        Require(Events.SequenceEqual(new[] { "ensure", "spawn", "cost", "message" }), "capacity before spawn; cost only after creation");
        Tick(11f); Tick(30f);
        Require(Spawns == 1 && Costs == 1, "completed reservation is never replayed");
        Require((float)Get(plan, "SpawnAt") == 10f, "deadline remains selected deadline");

        Reset(); plan = Plan(new Vector3(8, 5000, 8));
        object minion = Activator.CreateInstance(Nested("EnforcerMinionDefinition"), true)!;
        Set(minion, "Prefab", "Seeker"); Set(minion, "Count", 1);
        ((IList)Get(Get(Get(plan, "Candidate"), "Summon"), "Minions")).Add(minion);
        Tick(9f); Require(Spawns == 0, "minions do not arrive during warning");
        Tick(10f); Require(Spawns == 2 && Active == 1 && Costs == 1, "boss and minion arrive together; one encounter cost");
    }

    private static void CheckCancellation()
    {
        foreach (string reason in new[] { "no player", "outdoor", "other dungeon", "boss", "Off", "cooldown", "Karma", "prefab", "capacity", "spawn" })
        {
            Reset(); Plan(new Vector3(8, 5000, 8));
            switch (reason)
            {
                case "no player": Players.Clear(); break;
                case "outdoor": Players.Clear(); Player(Vector3.zero); break;
                case "other dungeon": Players.Clear(); Player(new Vector3(640, 5000, 0)); break;
                case "boss": Boss = true; break;
                case "Off": Mode.SetSerializedValue("Off"); break;
                case "cooldown": Cooldown = 5f; break;
                case "Karma": Set(State, "Karma", 0f); break;
                case "prefab": PrefabAvailable = false; break;
                case "capacity": Capacity = false; break;
                case "spawn": SpawnSucceeds = false; break;
            }
            Tick(10f);
            Require(Pending.Count == 0 && Spawns == 0 && Costs == 0, "canceled without cost: " + reason);
            Tick(20f);
            Require(Costs == 0, "canceled plan is not retried: " + reason);
        }

        Reset(); Plan(new Vector3(8, 5000, 8)); Boss = true; Tick(1f); Boss = false; Tick(10f);
        Require(Pending.Count == 0 && Spawns == 0, "boss appearing during countdown cancels permanently");
        Reset(); Plan(new Vector3(8, 5000, 8)); Player(new Vector3(10, 5000, 0)); Players.RemoveAt(0); Tick(10f);
        Require(Spawns == 1, "another living player in the same anchor zone keeps reservation alive");
    }

    private static void CheckLimitsAndOmen()
    {
        Reset();
        Plan(new Vector3(8, 5000, 8));
        Require(Call("GetEnforcerBlockerFailure", new Vector3(0, 5000, 0), null, ZDOID.None, true)!.ToString() == "ActiveEnforcerCap", "reservation counts for admission");
        Require((int)Call("CountPendingEnforcers", Vector3.zero, null)! == 0, "outdoor region excludes dungeon reservation");
        Require((int)Call("CountPendingEnforcers", new Vector3(640, 5000, 0), null)! == 0, "distant region excludes reservation");
        var region = new HashSet<string> { (string)Call("GetSectorKey", new Vector3(8, 5000, 8))! };
        Require((int)Call("CountPendingEnforcers", new Vector3(640, 5000, 0), region)! == 1, "merged region uses reserved position");
        Cap.Value = 2; Active = 1;
        Require(Call("GetEnforcerBlockerFailure", new Vector3(0, 5000, 0), null, ZDOID.None, true)!.ToString() == "ActiveEnforcerCap", "active and pending combined");

        Reset();
        Plan(new Vector3(8, 5000, 8), ignoreCooldown: true, ignoreKarma: true);
        Plan(new Vector3(12, 5000, 8), ignoreCooldown: true, ignoreKarma: true);
        Cap.Value = 1; Tick(10f);
        Require(Spawns == 1 && Costs == 1 && Pending.Count == 0, "lowered cap executes one and cancels excess");

        Reset();
        object omen = Plan(new Vector3(8, 5000, 8), ignoreCooldown: true, ignoreKarma: true);
        ZDOID lastBoss = new(1234L, 77);
        Set(omen, "ExcludedCharacterId", lastBoss);
        Set(State, "Karma", 0f); Cooldown = 50f;
        Tick(10f);
        Require(Spawns == 1 && Costs == 1 && Ensures == 1 && Excluded == lastBoss, "Omen bypass, excluded death, and pruned zero-Karma state re-ensure");
        Reset(); Plan(new Vector3(8, 5000, 8), ignoreCooldown: false, ignoreKarma: true); Cooldown = 50f; Tick(10f);
        Require(Spawns == 0, "Omen configured to respect cooldown remains blocked");
    }

    private static void CheckGeometry()
    {
        foreach (bool loaded in new[] { true, false })
        foreach (bool valid in new[] { true, false })
        {
            Reset(); Plan(new Vector3(8, 5000, 8)); Loaded = loaded;
            Clearance = Anchor = valid;
            Tick(10f);
            Require(Spawns == (valid ? 1 : 0) && LastGeometry == new Vector3(8, 5000, 8), "fixed loaded/unloaded validation");
        }
        Reset(); Plan(new Vector3(8, 5000, 8)); Loaded = false; AnchorOffset = Vector3.right; Tick(10f);
        Require(Spawns == 0 && Costs == 0, "different nearby anchor cannot relocate spawn");
    }

    private static void CheckWarningProtocol()
    {
        Reset();
        var warning = new ZPackage(); warning.Write("$cm_message_enforcer_warning"); warning.Write(10);
        Call("RPC_CenterQuote", 7L, warning); Require(Messages == 0, "non-server warning rejected");
        warning.SetPos(0); Call("RPC_CenterQuote", 42L, warning);
        Require(Messages == 1 && Events.Last() == "$cm_message_enforcer_warning:10", "server warning duration received");
        var quote = new ZPackage(); quote.Write("$cm_message_enforcer_spawn_01"); quote.SetPos(0);
        Call("RPC_CenterQuote", 42L, quote);
        Require(Messages == 2 && Events.Last() == "$cm_message_enforcer_spawn_01:0", "old string-only quote still accepted");
        var truncated = new ZPackage(); truncated.Write("$cm_message_enforcer_warning"); truncated.SetPos(0);
        Call("RPC_CenterQuote", 42L, truncated); Require(Messages == 2, "truncated warning ignored");
    }

    private static void CheckIntegration(Assembly plugin)
    {
        using var module = ModuleDefinition.ReadModule(plugin.Location);
        var karma = module.GetType(Karma.FullName);
        foreach (string method in new[] { "ResetRuntimeState", "UpdateSummons" })
            Require(karma.Methods.Single(m => m.Name == method).Body.Instructions.Any(i => i.Operand is FieldReference f && f.Name == "PendingEnforcerSummons"), "lifecycle owns pending cleanup: " + method);
        Require(karma.NestedTypes.Single(t => t.Name == "ParsedConfiguration").Methods.Single(m => m.Name == "Commit").Body.Instructions
            .Any(i => i.Operand is FieldReference f && f.Name == "PendingEnforcerSummons"), "successful YAML commit clears reservations");
        var update = karma.Methods.Single(m => m.Name == "UpdateSummons").Body.Instructions;
        int pending = update.ToList().FindIndex(i => i.Operand is MethodReference m && m.Name == "UpdatePendingEnforcerSummons");
        int periodic = update.ToList().FindIndex(i => i.Operand is FieldReference f && f.Name == "NextSummonCheckTime");
        Require(pending >= 0 && periodic > pending, "deadline dispatch is independent of periodic interval");
        var admission = karma.Methods.Single(m => m.Name == "TrySummonForPlayer").Body.Instructions;
        Require(admission.Any(i => i.Operand is FieldReference f && f.Name == "DungeonEnforcerSpawnDelay") &&
                admission.Any(i => i.Operand is MethodReference m && m.Name == "TrySpawnEnforcerEncounter"), "configured delay has immediate spawn path");
        foreach (string resource in new[] { "translations.English.yml", "translations.Korean.yml" })
        {
            using var reader = new StreamReader(plugin.GetManifestResourceStream(resource)!);
            Require(reader.ReadToEnd().Contains("cm_message_enforcer_warning:"), "bundled warning localization: " + resource);
        }
    }

    private static void Reset()
    {
        Pending.Clear(); Players.Clear(); Events.Clear(); Player(new Vector3(0, 5000, 0));
        Boss = false; Loaded = Clearance = Anchor = Capacity = PrefabAvailable = SpawnSucceeds = true;
        Cooldown = 0; Active = Spawns = Costs = Ensures = Messages = GeometryChecks = 0;
        LastSpawn = LastGeometry = AnchorOffset = Vector3.zero; Excluded = ZDOID.None;
        PositionSelections = Warnings = 0; Delay.Value = 10;
        Set(State, "Karma", 100f); Mode.SetSerializedValue("KarmaLevelAndEnforcer"); Cap.Value = 1;
    }

    private static object Plan(Vector3 spawn, bool ignoreCooldown = false, bool ignoreKarma = false)
    {
        object plan = Activator.CreateInstance(Nested("EnforcerSummonPlan"), true)!;
        object candidate = Activator.CreateInstance(Nested("EnforcerCandidateDefinition"), true)!;
        Set(Get(candidate, "Summon"), "Boss", "Seeker");
        object settings = Activator.CreateInstance(Nested("ResolvedEnforcerSettings"), true)!;
        Set(settings, "RequiredKarma", 40f); Set(settings, "ConsumeKarma", 30f);
        Set(plan, "Candidate", candidate); Set(plan, "Settings", settings);
        Set(plan, "SpawnPosition", spawn); Set(plan, "PlayerPosition", new Vector3(0, 5000, 0));
        Set(plan, "StatePosition", new Vector3(0, 5000, 0)); Set(plan, "SpawnAt", 10f); Set(plan, "Dungeon", true);
        Set(plan, "IgnoreCooldown", ignoreCooldown); Set(plan, "IgnoreRequiredKarma", ignoreKarma);
        Pending.Add(plan); return plan;
    }
    private static void Player(Vector3 position) => Players.Add(Activator.CreateInstance(Nested("ConnectedPlayerContext"), Instance, null,
        new object[] { 1L, new ZDOID(1234L, (uint)Players.Count + 1), position }, null));
    private static void Tick(float now) => Call("UpdatePendingEnforcerSummons", now);
    private static Type Nested(string name) => Karma.GetNestedType(name, BindingFlags.NonPublic)!;
    private static object Get(object value, string name) => value.GetType().GetField(name, Instance)!.GetValue(value)!;
    private static void Set(object value, string name, object field) => value.GetType().GetField(name, Instance)!.SetValue(value, field);
    private static object? Call(string name, params object?[] args) => Copy(Karma.GetMethods(Static).Single(m =>
        m.Name == name && m.GetParameters().Length == args.Length && m.GetParameters().Select((p, i) =>
            p.ParameterType.IsByRef || args[i] == null || p.ParameterType.IsInstanceOfType(args[i])).All(matches => matches))).Invoke(null, args);

    private static MethodInfo Copy(MethodInfo source)
    {
        if (Copies.TryGetValue(source, out MethodInfo result)) return result;
        using var copy = new DynamicMethodDefinition(source);
        typeof(DynamicMethodDefinition).GetProperty(nameof(DynamicMethodDefinition.OriginalMethod))!.SetValue(copy, null);
        copy.OwnerType = typeof(EnforcerDelayContracts);
        foreach (var instruction in copy.Definition.Body.Instructions.ToArray())
        {
            if (instruction.Operand is FieldReference field && field.DeclaringType.FullName == Karma.FullName)
            {
                string fieldName = field.Name switch { "PendingEnforcerSummons" => nameof(Pending), "Settings" => nameof(GlobalSettings), _ => field.Name };
                FieldInfo replacement = typeof(EnforcerDelayContracts).GetField(fieldName, Static) ??
                    throw new InvalidOperationException("Unexpected delay fixture field: " + field.Name);
                instruction.Operand = copy.Module.ImportReference(replacement);
                if (field.Name is "PendingEnforcerSummons" or "Settings")
                    copy.Definition.Body.GetILProcessor().InsertAfter(instruction, Mono.Cecil.Cil.Instruction.Create(Mono.Cecil.Cil.OpCodes.Castclass, field.FieldType));
                continue;
            }
            if (!(instruction.Operand is MethodReference method)) continue;
            string type = method.DeclaringType.FullName;
            string? boundary = type == Karma.FullName ? method.Name switch
            {
                "GetConnectedAlivePlayerContexts" => nameof(GetPlayers), "GetEnforcerBlockerState" => nameof(Blockers),
                "GetBestStateUnsafe" => nameof(GetState), "GetRemainingEnforcerCooldownUnsafe" => nameof(GetCooldown),
                "GetSectorKeys" => nameof(SectorKeys),
                "GetBiome" => nameof(Biome), "TryGetEnforcerBiomeDefinition" => nameof(GetBiomeDefinition),
                "TryGetDungeonLocationPrefabName" => nameof(DungeonName), "TrySelectEnforcerCandidate" => nameof(SelectCandidate),
                "TryFindSummonPosition" => nameof(SelectPosition), "BroadcastRegionalCenterMessage" => nameof(Warning),
                "TryEnsureSectorStatesUnsafe" => nameof(Ensure), "ApplyEnforcerCostUnsafe" => nameof(Cost),
                "TryGetCreaturePrefab" => nameof(GetPrefab), "TrySpawnCreature" => nameof(Spawn),
                "BroadcastRegionalCenterQuote" => nameof(SpawnMessage), "GetPrefabName" => nameof(PrefabName),
                "TryFindDungeonMinionPosition" => nameof(MinionPosition), "TryFindDungeonZdoAnchorPosition" => nameof(FindAnchor),
                "HasDungeonSpawnClearance" => nameof(CheckClearance), "GetSpawnRotation" => nameof(Rotation),
                "ShowLocalCenterQuote" => nameof(ReceiveMessage), _ => null
            } : method.DeclaringType.Name == "ResolvedEnforcerSettings" && method.Name == "FromGlobal" ? nameof(FromGlobal)
                : type == "UnityEngine.Random" && method.Name == "Range" ? nameof(RandomRange)
                : type == "ZoneSystem" ? method.Name switch
            {
                "get_instance" => nameof(GetZone), "IsZoneLoaded" => nameof(IsLoaded), "GetSolidHeight" => nameof(Floor), _ => null
            } : type == "UnityEngine.Object" ? method.Name switch
            {
                "op_Equality" => nameof(SameObject), "op_Inequality" => nameof(DifferentObject), _ => null
            } : type == "ZRoutedRpc" ? method.Name switch
            {
                "get_instance" => nameof(GetRoutedRpc), "GetServerPeerID" => nameof(ServerPeer), _ => null
            } : null;
            MethodInfo? target = boundary == null ? null : typeof(EnforcerDelayContracts).GetMethod(boundary, Static);
            if (target?.IsGenericMethodDefinition == true)
            {
                MethodInfo original = (MethodInfo)method.ResolveReflection();
                Type[] parameters = original.GetParameters().Select(p => p.ParameterType).ToArray();
                Type[] arguments = boundary switch
                {
                    nameof(GetPlayers) => original.ReturnType.GetGenericArguments(),
                    nameof(GetState) => new[] { original.ReturnType },
                    nameof(GetCooldown) or nameof(Cost) => new[] { parameters[2] },
                    nameof(GetBiomeDefinition) => new[] { parameters[1].GetElementType()! },
                    nameof(FromGlobal) => new[] { parameters[0], original.ReturnType },
                    nameof(SelectPosition) => new[] { parameters[2] },
                    nameof(SelectCandidate) => new[] { parameters[0].GetGenericArguments()[0], parameters[1] },
                    nameof(Spawn) => new[] { parameters[6], parameters[7].GetGenericArguments()[0] },
                    _ => throw new InvalidOperationException(boundary)
                };
                target = target.MakeGenericMethod(arguments);
            }
            if (target == null && (type == Karma.FullName || type == "ZoneSystem" && method.Name == "GetZone" ||
                type == "Character" && method.Name == "InInterior" || type == "Utils" && method.Name == "FloorToInt"))
                target = Copy((MethodInfo)method.ResolveReflection());
            if (target == null) continue;
            instruction.OpCode = Mono.Cecil.Cil.OpCodes.Call;
            instruction.Operand = copy.Module.ImportReference(target);
        }
        return Copies[source] = copy.Generate();
    }

    private static T Uninitialized<T>() => (T)FormatterServices.GetUninitializedObject(typeof(T));
    private static bool SameObject(UnityEngine.Object a, UnityEngine.Object b) => ReferenceEquals(a, b);
    private static bool DifferentObject(UnityEngine.Object a, UnityEngine.Object b) => !ReferenceEquals(a, b);
    private static List<T> GetPlayers<T>() => (List<T>)Players;
    private static T GetState<T>(Vector3 position, out string key) { key = "fixture"; return (T)State; }
    private static float GetCooldown<T>(Vector3 position, float now, T settings, HashSet<string>? region) => Cooldown;
    private static void Blockers(Vector3 position, out int active, out bool boss, Character? excluded, HashSet<string>? region, ZDOID excludedId)
    { active = Active; boss = Boss; Excluded = excludedId; }
    private static bool Ensure(IReadOnlyCollection<string> keys) { Ensures++; Events.Add("ensure"); return Capacity; }
    private static IEnumerable<string> SectorKeys(Vector3 position) => new[] { "fixture" };
    private static Heightmap.Biome Biome(Vector3 position) => Heightmap.Biome.Meadows;
    private static bool GetBiomeDefinition<T>(Heightmap.Biome biome, out T definition) { definition = (T)BiomeDefinition; return true; }
    private static bool DungeonName(Vector3 position, out string name) { name = ""; return false; }
    private static TSettings FromGlobal<TGlobal, TSettings>(TGlobal settings) => (TSettings)SelectedSettings;
    private static bool SelectCandidate<TCandidate, TSettings>(List<TCandidate> candidates, TSettings baseline, float karma,
        bool ignoreRequired, out TCandidate candidate, out TSettings settings)
    { candidate = (TCandidate)SelectedCandidate; settings = (TSettings)SelectedSettings; return true; }
    private static bool SelectPosition<T>(GameObject prefab, Vector3 player, T settings, out Vector3 position)
    { PositionSelections++; position = player + new Vector3(8, 0, 8); return true; }
    private static void Warning(string message, Vector3 position, HashSet<string>? region, int seconds)
    { Require(message == "$cm_message_enforcer_warning" && seconds == Delay.Value, "admission warns with selected duration"); Warnings++; }
    private static float RandomRange(float min, float max) => min;
    private static float Cost<T>(Vector3 position, float now, T settings) { Costs++; Events.Add("cost"); return 70f; }
    private static bool GetPrefab(string name, out GameObject prefab) { prefab = Prefab; return PrefabAvailable; }
    private static bool Spawn<T, TLoot>(string name, GameObject prefab, Vector3 position, Vector3 target, bool enforcer,
        string suffix, T settings, IReadOnlyList<TLoot>? loot, out Character character)
    {
        character = Creature;
        if (!SpawnSucceeds) return false;
        Spawns++; if (enforcer) Active++; LastSpawn = position; Events.Add("spawn"); return true;
    }
    private static void SpawnMessage(IReadOnlyList<string> quotes, Vector3 position, HashSet<string>? region) { Messages++; Events.Add("message"); }
    private static string PrefabName(Character character) => "Seeker";
    private static bool MinionPosition(GameObject prefab, Character boss, Vector3 target, out Vector3 position) { position = LastSpawn; return true; }
    private static ZoneSystem GetZone() => Zone;
    private static bool IsLoaded(ZoneSystem zone, Vector3 position) => Loaded;
    private static bool Floor(ZoneSystem zone, Vector3 position, out float height, int margin) { height = position.y; return true; }
    private static bool FindAnchor(Vector3 position, float radius, out Vector3 anchor)
    { GeometryChecks++; LastGeometry = position; anchor = position + AnchorOffset; return Anchor; }
    private static bool CheckClearance(GameObject prefab, Vector3 position, Quaternion rotation)
    { GeometryChecks++; LastGeometry = position; return Clearance; }
    private static Quaternion Rotation(Vector3 position, Vector3 target) => Quaternion.identity;
    private static ZRoutedRpc GetRoutedRpc() => RoutedRpc;
    private static long ServerPeer(ZRoutedRpc rpc) => 42L;
    private static void ReceiveMessage(string message, int seconds) { Messages++; Events.Add(message + ":" + seconds); }
    private static void Require(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException("Enforcer delay contract failed: " + label);
    }
}
