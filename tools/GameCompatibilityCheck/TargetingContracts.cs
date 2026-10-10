using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using BepInEx.Configuration;
using HarmonyLib;
using Mono.Cecil;
using MonoMod.Utils;
using UnityEngine;

// Run the compiled selection/hold policy. Native sensing, navigation, clock and random
// boundaries are substituted; this does not simulate combat, animation or multiplayer.
internal static class TargetingContracts
{
    private const BindingFlags All = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static Type Targeting = null!;
    private static readonly Dictionary<MethodInfo, MethodInfo> Copies = new();
    private static readonly MonsterAI Ai = New<MonsterAI>();
    private static readonly Character Creature = New<Character>(), Animal = New<Character>();
    private static readonly Player A = New<Player>(), B = New<Player>(), C = New<Player>();
    private static readonly ZNetView View = New<ZNetView>();
    private static readonly List<Player> Players = new();
    private static readonly HashSet<Character> Dead = new(new IdentityComparer<Character>()), Friendly = new(new IdentityComparer<Character>()),
        Unseen = new(new IdentityComparer<Character>()), Lava = new(new IdentityComparer<Character>());
    private static readonly HashSet<Player> Ghost = new(new IdentityComparer<Player>()), DebugFly = new(new IdentityComparer<Player>());
    private static ConfigEntryBase Mode = null!;
    private static ConfigEntry<float> Interval = null!, HoldDuration = null!;
    private static ConfigEntry<int> Chance = null!;
    private static Character? Current;
    private static Player? Local;
    private static ZDO Zdo = null!;
    private static bool Owner, Valid, Alerted, Tamed, Path, Cinematic;
    private static long OwnerId;
    private static float Now, Roll;
    private static int Pick, Enumerations, Senses, Paths, Rolls, Checks;

    internal static void Run(Assembly plugin)
    {
        Targeting = plugin.GetType("CreatureManager.CreatureTargeting", true)!;
        Type entry = plugin.GetType("CreatureManager.CreatureManagerPlugin", true)!;
        string[] names = { "PlayerTargetSwitching", "PlayerTargetSwitchInterval", "PlayerTargetSwitchChance", "PlayerTargetHoldDuration" };
        FieldInfo[] fields = names.Select(n => entry.GetField(n, All)!).ToArray();
        object?[] previous = fields.Select(f => f.GetValue(null)).ToArray();
        FieldInfo statesField = Targeting.GetField("States", All)!;
        object previousStates = statesField.GetValue(null)!;
        var config = new ConfigFile(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "cm-targeting-" + Guid.NewGuid() + ".cfg"), false) { SaveOnConfigSet = false };
        Type toggle = fields[0].FieldType.GetGenericArguments()[0];
        MethodInfo bind = typeof(ConfigFile).GetMethods().Single(m => m.Name == "Bind" && m.IsGenericMethodDefinition &&
            m.GetParameters().Length == 4 && m.GetParameters()[3].ParameterType == typeof(ConfigDescription));
        Mode = (ConfigEntryBase)bind.MakeGenericMethod(toggle).Invoke(config,
            new object[] { "AI", "Mode", Enum.Parse(toggle, "On"), new ConfigDescription("") })!;
        Interval = config.Bind("AI", "Interval", 10f);
        Chance = config.Bind("AI", "Chance", 100);
        HoldDuration = config.Bind("AI", "Hold duration", 5f);
        object[] entries = { Mode, Interval, Chance, HoldDuration };
        EventHandler resetHandler = (EventHandler)Delegate.CreateDelegate(typeof(EventHandler), entry.GetMethod("ResetTargetingConfiguration", All)!);
        try
        {
            // Avoid Unity Object's native type initializer while retaining the real state values.
            statesField.SetValue(null, Activator.CreateInstance(statesField.FieldType, new object[] { new IdentityComparer<Character>() }));
            for (int i = 0; i < fields.Length; i++) fields[i].SetValue(null, entries[i]);
            foreach (object setting in entries) setting.GetType().GetEvent("SettingChanged")!.AddEventHandler(setting, resetHandler);
            CheckGates();
            CheckAttempts();
            CheckCandidates();
            CheckHoldingAndLifetime();
            CheckHoldDuration();
            CheckIntegration(plugin);
        }
        finally
        {
            Call("ResetRuntimeState");
            statesField.SetValue(null, previousStates);
            foreach (object setting in entries) setting.GetType().GetEvent("SettingChanged")!.RemoveEventHandler(setting, resetHandler);
            for (int i = 0; i < fields.Length; i++) fields[i].SetValue(null, previous[i]);
        }
        System.Console.WriteLine($"AI targeting contracts: {Checks} checks passed. Default-off policy, combat/owner gates, interval/chance, animal tank and player candidates, bosses, configurable hold duration/cancellation, ownership revisions, live settings and teardown. Sensing/path/random boundaries substituted; no game session.");
    }

    private static void CheckGates()
    {
        foreach (string gate in new[] { "off", "zero", "invalid", "remote", "tamed", "dead", "calm", "no target", "dead target", "friendly target", "phase two" })
        {
            Reset();
            switch (gate)
            {
                case "off": Mode.SetSerializedValue("Off"); break;
                case "zero": Chance.Value = 0; break;
                case "invalid": Valid = false; break;
                case "remote": Owner = false; break;
                case "tamed": Tamed = true; break;
                case "dead": Dead.Add(Creature); break;
                case "calm": Alerted = false; break;
                case "no target": Current = null; break;
                case "dead target": Dead.Add(Animal); break;
                case "friendly target": Friendly.Add(Animal); break;
                case "phase two": SetPrefab("FrozenKing_p2"); break;
            }
            Require(ReferenceEquals(Select(), Animal) && States.Count == 0 && Enumerations + Senses + Paths + Rolls == 0, "fast exit: " + gate);
        }
    }

    private static void CheckAttempts()
    {
        Reset();
        Require(ReferenceEquals(Select(), Animal), "first observation keeps vanilla target");
        Now = 9.9f;
        Require(ReferenceEquals(Select(), Animal) && Enumerations + Senses + Paths + Rolls == 0, "wait the full initial interval");
        Now = 10f;
        Require(ReferenceEquals(Select(), A) && Enumerations == 1 && Paths == 1, "animal tank plus a single player can switch");

        Reset(); Chance.Value = 25; Select(); Now = 10; Roll = 0.25f;
        Require(ReferenceEquals(Select(), Animal) && Rolls == 1 && Enumerations == 0, "25 percent boundary fails before enumeration");
        Roll = 0; Now = 19;
        Require(ReferenceEquals(Select(), Animal) && Rolls == 1, "failed roll consumes interval");
        Now = 20; Require(ReferenceEquals(Select(), A), "later successful roll");

        Reset(); Select(); Now = 10; Roll = 1f;
        Require(ReferenceEquals(Select(), A) && Rolls == 0, "100 percent cannot fail at Random.value's inclusive upper bound");

        Reset(); Players.Clear(); Select(); Now = 10; Select(); Players.Add(A); Now = 11;
        Require(ReferenceEquals(Select(), Animal) && Enumerations == 1, "empty candidate set consumes interval");
        Reset(); Path = false; Select(); Now = 10; Select(); Path = true; Now = 11;
        Require(ReferenceEquals(Select(), Animal) && Paths == 1, "failed path consumes interval");

        Reset(); Creature.m_boss = true; Select(); Now = 10;
        Require(ReferenceEquals(Select(), A), "ordinary bosses participate");
    }

    private static void CheckCandidates()
    {
        Reset(); Current = A; Select(); Now = 10;
        Require(ReferenceEquals(Select(A), A) && Paths == 0, "current player is never an alternative");
        Players.Add(B); Now = 20;
        Require(ReferenceEquals(Select(A), B), "second player replaces current player");

        foreach (int pick in new[] { 0, 1 })
        {
            Reset(); Current = A; Players.Add(B); Players.Add(C); Pick = pick; Select(A); Now = 10;
            Require(ReferenceEquals(Select(A), pick == 0 ? C : B) && Paths == 1, "random selection of either alternate without per-player paths");
        }

        foreach (string gate in new[] { "dead", "friendly", "unseen", "skip", "ghost", "debug", "cinematic", "lava", "path" })
        {
            Reset();
            switch (gate)
            {
                case "dead": Dead.Add(A); break;
                case "friendly": Friendly.Add(A); break;
                case "unseen": Unseen.Add(A); break;
                case "skip": A.m_aiSkipTarget = true; break;
                case "ghost": Ghost.Add(A); break;
                case "debug": DebugFly.Add(A); break;
                case "cinematic": Local = A; Cinematic = true; break;
                case "lava": Ai.m_skipLavaTargets = true; Lava.Add(A); break;
                case "path": Path = false; break;
            }
            Select(); Now = 10;
            Require(ReferenceEquals(Select(), Animal), "candidate rejected: " + gate);
        }
        Reset(); Lava.Add(A); Select(); Now = 10;
        Require(ReferenceEquals(Select(), A), "lava remains allowed when creature does not skip lava targets");
    }

    private static void CheckHoldingAndLifetime()
    {
        Hold(); Now = 14;
        Require(ReferenceEquals(Select(), A) && Enumerations == 1 && Rolls == 0, "holds selected player without resampling");
        Now = 16;
        Require(ReferenceEquals(Select(), Animal), "returns vanilla result at first search after hold expires");
        foreach (string reason in new[] { "lost target", "replaced target", "dead", "friendly", "unseen", "path", "tamed", "owner", "round trip", "new zdo" })
        {
            Hold(); Now = 12;
            switch (reason)
            {
                case "lost target": Current = null; break;
                case "replaced target": Current = B; break;
                case "dead": Dead.Add(A); break;
                case "friendly": Friendly.Add(A); break;
                case "unseen": Unseen.Add(A); break;
                case "path": Path = false; break;
                case "tamed": Tamed = true; break;
                case "owner": OwnerId++; break;
                case "round trip": Zdo.OwnerRevision += 2; break;
                case "new zdo": Zdo = New<ZDO>(); break;
            }
            Require(ReferenceEquals(Select(), Animal), "hold canceled: " + reason);
        }
        foreach (string change in new[] { "off", "chance", "interval", "hold" })
        {
            Hold();
            if (change == "off") Mode.SetSerializedValue("Off");
            if (change == "chance") Chance.Value = 25;
            if (change == "interval") Interval.Value = 20;
            if (change == "hold") HoldDuration.Value = 8;
            Require(States.Count == 0, "live setting clears state: " + change);
            Now = 12; Require(ReferenceEquals(Select(), Animal), "live setting releases hold: " + change);
        }
        Hold(); Call("ForgetCharacter", Creature); Require(States.Count == 0, "character destruction removes state");
        Hold(); Call("ResetRuntimeState"); Require(States.Count == 0, "world/plugin cleanup removes state");
    }

    private static void CheckHoldDuration()
    {
        foreach (float duration in new[] { 0f, 2.5f, 8f, 10f, 30f })
        {
            Reset(); Interval.Value = 120; HoldDuration.Value = duration;
            Select(); Now = 120; Current = Select();
            Require(ReferenceEquals(Current, A), "initial switch with hold duration " + duration);
            if (duration > 0)
            {
                Now = 120 + duration - 0.25f;
                Require(ReferenceEquals(Select(), A), "retain target before configured deadline: " + duration);
            }
            int senses = Senses, paths = Paths;
            Now = 120 + Math.Max(2f, duration);
            Require(ReferenceEquals(Select(), Animal) && Senses == senses && Paths == paths && Enumerations == 1,
                "expired/zero hold returns vanilla result without extra sensing/path work: " + duration);
        }

        Reset(); HoldDuration.Value = 20; Select(); Now = 10; Current = Select(); Players.Add(B);
        Now = 20;
        Require(ReferenceEquals(Select(), A) && Enumerations == 1, "long hold defers a due switching attempt");
        Now = 30;
        Require(ReferenceEquals(Select(), B) && Enumerations == 2, "deferred attempt can run as the hold expires");

        Reset(); Interval.Value = 2; HoldDuration.Value = 0; Select(); Now = 2; Current = Select(); Players.Add(B);
        Now = 4;
        Require(ReferenceEquals(Select(), B) && Enumerations == 2, "zero hold still permits a new attempt when its interval is due");

        Hold(); HoldDuration.Value = 0; Now = 12;
        Require(States.Count == 0 && ReferenceEquals(Select(), Animal), "live zero duration clears the hold and starts a fresh interval");
        Now = 20;
        Require(ReferenceEquals(Select(), Animal) && Enumerations == 1, "live duration change cancels the old attempt deadline");
        Now = 22; Players.Add(B);
        Require(ReferenceEquals(Select(), B) && Enumerations == 2, "new attempt runs after the restarted interval");
    }

    private static void CheckIntegration(Assembly plugin)
    {
        Type patch = plugin.GetType("CreatureManager.CreatureManagerMonsterAITargetPatch", true)!;
        MethodInfo original = AccessTools.DeclaredMethod(typeof(MonsterAI), "UpdateTarget",
            new[] { typeof(Humanoid), typeof(float), typeof(bool).MakeByRefType(), typeof(bool).MakeByRefType() });
        MethodInfo find = AccessTools.DeclaredMethod(typeof(BaseAI), "FindEnemy", Type.EmptyTypes);
        var before = PatchProcessor.GetOriginalInstructions(original).ToList();
        var after = ((IEnumerable<CodeInstruction>)patch.GetMethod("Transpiler", All)!.Invoke(null, new object[] { before })!).ToList();
        int index = before.FindIndex(i => i.Calls(find));
        Require(after.Count == before.Count + 6 && after[index + 6].Calls(Targeting.GetMethod("SelectTarget", All)!), "wrap exactly one FindEnemy result");
        after.RemoveRange(index + 1, 6);
        Require(after.SequenceEqual(before), "all original instructions/labels and attack/abandonment branches retained");
        Require(original.IsPrivate && find.IsFamily && AccessTools.DeclaredMethod(typeof(BaseAI), "HavePath").IsFamily &&
                AccessTools.DeclaredField(typeof(BaseAI), "m_character").IsFamily && AccessTools.DeclaredField(typeof(BaseAI), "m_nview").IsFamily,
            "original private/protected access metadata verified");
        var originalPath = (Func<BaseAI, Vector3, bool>)Targeting.GetField("HavePath", All)!.GetValue(null)!;
        Require(originalPath.Method == AccessTools.DeclaredMethod(typeof(BaseAI), "HavePath"),
            "cached delegate binds to original protected HavePath");
        // Calling even its flying branch initializes Character's animation hashes through
        // native Unity. Binding is checked here; actual navigation needs an in-game test.
        using var module = ModuleDefinition.ReadModule(plugin.Location);
        bool Calls(string type, string method, string name) => module.GetType("CreatureManager." + type).Methods.Single(m => m.Name == method)
            .Body.Instructions.Any(i => i.Operand is MethodReference m && m.DeclaringType.FullName == Targeting.FullName && m.Name == name);
        Require(Calls("CreatureManagerCharacterOnDestroyPatch", "Prefix", "ForgetCharacter") &&
                Calls("CreatureManagerZNetSceneOnDestroyPatch", "Prefix", "ResetRuntimeState"), "teardown hooks connected");
        var entry = module.GetType("CreatureManager.CreatureManagerPlugin");
        int Handlers(string name) => entry.Methods.Single(m => m.Name == name).Body.Instructions.Count(i =>
            i.Operand is MethodReference m && m.Name == "ResetTargetingConfiguration");
        Require(Handlers("Awake") == 4 && Handlers("UnsubscribeConfigHandlers") == 4, "all four config events subscribe and unsubscribe");
    }

    private static void Hold()
    {
        Reset(); Select(); Now = 10; Current = Select();
        Require(ReferenceEquals(Current, A), "hold setup selects player");
    }

    private static void Reset()
    {
        Mode.SetSerializedValue("On"); Interval.Value = 10; Chance.Value = 100; HoldDuration.Value = 5;
        Call("ResetRuntimeState");
        Current = Animal; Now = Roll = 0; Pick = 0; OwnerId = 41;
        Owner = Valid = Alerted = Path = true; Tamed = Cinematic = false;
        Players.Clear(); Players.Add(A); Dead.Clear(); Friendly.Clear(); Unseen.Clear(); Lava.Clear(); Ghost.Clear(); DebugFly.Clear();
        A.m_aiSkipTarget = B.m_aiSkipTarget = C.m_aiSkipTarget = false;
        Ai.m_skipLavaTargets = Creature.m_boss = false; Local = null;
        Zdo = New<ZDO>(); Zdo.m_uid = new ZDOID(99001, 951); Zdo.OwnerRevision = 1;
        SetPrefab("Greydwarf");
        Enumerations = Senses = Paths = Rolls = 0;
    }

    private static IDictionary States => (IDictionary)Targeting.GetField("States", All)!.GetValue(null)!;
    private static void SetPrefab(string name) => typeof(ZDO).GetField("m_prefab", All)!.SetValue(Zdo, name.GetStableHashCode());
    private static Character? Select(Character? vanilla = null) => (Character?)Call("SelectTarget", vanilla ?? Animal, Ai, Creature, View);
    private static object? Call(string name, params object?[] args) => Copy(Targeting.GetMethod(name, All)!).Invoke(null, args);
    private static MethodInfo Copy(MethodInfo source)
    {
        if (Copies.TryGetValue(source, out MethodInfo copyMethod)) return copyMethod;
        using var copy = new DynamicMethodDefinition(source);
        typeof(DynamicMethodDefinition).GetProperty(nameof(DynamicMethodDefinition.OriginalMethod))!.SetValue(copy, null);
        copy.OwnerType = typeof(TargetingContracts);
        foreach (var instruction in copy.Definition.Body.Instructions.ToArray())
        {
            if (instruction.Operand is FieldReference field && field.DeclaringType.FullName == "Player" && field.Name == "m_localPlayer")
            {
                instruction.Operand = copy.Module.ImportReference(typeof(TargetingContracts).GetField(nameof(Local), All)!);
                continue;
            }
            if (!(instruction.Operand is MethodReference method)) continue;
            string type = method.DeclaringType.FullName;
            string? boundary = type switch
            {
                "UnityEngine.Object" => method.Name switch { "op_Equality" => nameof(Equal), "op_Inequality" => nameof(NotEqual), _ => null },
                "UnityEngine.Time" => method.Name == "get_time" ? nameof(TimeNow) : null,
                "UnityEngine.Random" => method.Name switch { "get_value" => nameof(RandomRoll), "Range" => nameof(RandomPick), _ => null },
                "ZNetView" => method.Name switch { "IsValid" => nameof(IsValid), "IsOwner" => nameof(IsOwner), "GetZDO" => nameof(GetZdo), _ => null },
                "ZDO" => method.Name == "GetOwner" ? nameof(GetOwner) : null,
                "Character" => method.Name switch { "IsDead" => nameof(IsDead), "IsTamed" => nameof(IsTamed), "InGhostMode" => nameof(InGhost), "AboveOrInLava" => nameof(InLava), _ => null },
                "Player" => method.Name switch { "GetAllPlayers" => nameof(GetPlayers), "IsDead" => nameof(IsDead), "InGhostMode" => nameof(InGhost), "InDebugFlyMode" => nameof(InDebug), _ => null },
                "BaseAI" => method.Name switch { "IsEnemy" => nameof(IsEnemy), "IsAlerted" => nameof(IsAlerted), "CanSenseTarget" => nameof(CanSense), "GetTargetCreature" => nameof(GetTarget), _ => null },
                "MonsterAI" => method.Name == "GetTargetCreature" ? nameof(GetTarget) : null,
                "CinematicsManager" => method.Name == "IsPlaying" ? nameof(IsPlaying) : null,
                _ => type == Targeting.FullName && method.Name == "HasPath" ? nameof(HasPath) : null
            };
            MethodInfo? target = boundary == null ? null : typeof(TargetingContracts).GetMethod(boundary, All);
            if (target == null && type == Targeting.FullName) target = Copy((MethodInfo)method.ResolveReflection());
            if (target == null) continue;
            instruction.OpCode = Mono.Cecil.Cil.OpCodes.Call;
            instruction.Operand = copy.Module.ImportReference(target);
        }
        return Copies[source] = DMDGenerator<DMDCecilGenerator>.Generate(copy);
    }

    private static T New<T>() => (T)FormatterServices.GetUninitializedObject(typeof(T));
    private sealed class IdentityComparer<T> : IEqualityComparer<T> where T : class
    {
        public bool Equals(T? x, T? y) => ReferenceEquals(x, y);
        public int GetHashCode(T value) => RuntimeHelpers.GetHashCode(value);
    }
    private static bool Equal(UnityEngine.Object a, UnityEngine.Object b) => ReferenceEquals(a, b);
    private static bool NotEqual(UnityEngine.Object a, UnityEngine.Object b) => !ReferenceEquals(a, b);
    private static float TimeNow() => Now;
    private static float RandomRoll() { Rolls++; return Roll; }
    private static int RandomPick(int min, int max) => max == 1 ? 0 : Pick;
    private static bool IsValid(ZNetView _) => Valid;
    private static bool IsOwner(ZNetView _) => Owner;
    private static ZDO GetZdo(ZNetView _) => Zdo;
    private static long GetOwner(ZDO _) => OwnerId;
    private static bool IsDead(Character c) => Dead.Contains(c);
    private static bool IsTamed(Character _) => Tamed;
    private static bool InLava(Character c) => Lava.Contains(c);
    private static bool InGhost(Character p) => p is Player player && Ghost.Contains(player);
    private static bool InDebug(Player p) => DebugFly.Contains(p);
    private static bool IsPlaying() => Cinematic;
    private static bool IsEnemy(Character _, Character target) => !Friendly.Contains(target);
    private static bool IsAlerted(BaseAI _) => Alerted;
    private static bool CanSense(BaseAI _, Character target) { Senses++; return !Unseen.Contains(target); }
    private static Character? GetTarget(BaseAI _) => Current;
    private static bool HasPath(MonsterAI _, Player target) { Paths++; return Path; }
    private static List<Player> GetPlayers() { Enumerations++; return Players; }
    private static void Require(bool condition, string description)
    {
        Checks++;
        if (!condition) throw new InvalidOperationException("AI targeting: " + description);
    }
}
