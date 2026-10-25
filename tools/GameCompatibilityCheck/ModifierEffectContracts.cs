using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using Mono.Cecil;
using MonoMod.Utils;
using UnityEngine;

// Execute the compiled spawn body and original game lifecycle guards, substituting native
// Unity boundaries only. This is not a particle/audio, scene-unload or multiplayer test.
internal static class ModifierEffectContracts
{
    private const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    private static readonly GameObject Prefab = New<GameObject>(), Spawned = New<GameObject>();
    private static readonly Vector3 Position = new(1f, 2f, 3f);
    private static readonly Quaternion Rotation = new(0f, 1f, 0f, 0f);
    private static readonly Dictionary<MethodInfo, MethodInfo> Copies = new();
    private static TimedDestruction? Timer;
    private static bool ThrowOnSpawn, ThrowOnTimer, ExpectedPrior, SpawnFlag;
    private static int Spawns, Triggers, DisabledViews, DestroyedEffects, Checks;
    private static bool Server;
    private static int Broadcasts;
    private static readonly List<bool> LocalRoutes = new();
    private static readonly ZNet Net = New<ZNet>();
    private static readonly ZRoutedRpc Router = New<ZRoutedRpc>();
    private static readonly Transform Transform = New<Transform>();
    private static readonly ZNetScene Scene = New<ZNetScene>();
    private static string? RequestedPrefab, PlayedPrefab;
    private static bool PlayedNetworked;

    internal static void Run(Assembly plugin)
    {
        Type modifiers = plugin.GetType("CreatureManager.CreatureModifierManager", true)!;
        MethodInfo spawn = Copy(modifiers.GetMethod("InstantiateModifierEffect", All)!);
        bool saved = ZNetView.m_forceDisableInit;
        try
        {
            Require(typeof(ZNetView).GetField("m_forceDisableInit")?.IsPublic == true &&
                    typeof(TimedDestruction).GetMethod("Trigger", Type.EmptyTypes)?.IsPublic == true,
                "new runtime accesses are public on the original game DLL");
            foreach (string name in new[] { "fx_StaffShield_Hit", "fx_StaffShield_Break", "fx_ShieldCharge_5",
                         "sfx_metal_shield_blocked_overlay", "fx_tentaroot_death", "fx_Adrenaline1" })
            foreach (bool prior in new[] { false, true })
            foreach (bool networked in new[] { false, true })
            {
                Reset(prior);
                Timer = New<TimedDestruction>();
                Timer.m_timeout = 3f;
                Timer.m_triggerOnAwake = name != "fx_StaffShield_Hit" && name != "fx_StaffShield_Break";
                Require(ReferenceEquals(Invoke(spawn, name, networked), Spawned), "returns the spawned effect for parenting");
                bool suppressed = prior || !networked;
                Require(Spawns == 1 && SpawnFlag == suppressed && DisabledViews == (suppressed ? 1 : 0),
                    "RPC copies stay local; direct owner effects retain network initialization: " + name);
                Require(ZNetView.m_forceDisableInit == prior, "restores previous scope: " + name);
                Require(Triggers == (Timer.m_triggerOnAwake ? 0 : 1) && Timer.m_timeout == 3f,
                    "starts only the unarmed timer and preserves its timeout: " + name);
            }

            foreach (bool prior in new[] { false, true })
            foreach (bool timerFailure in new[] { false, true })
            {
                Reset(prior);
                Timer = New<TimedDestruction>();
                ThrowOnSpawn = !timerFailure;
                ThrowOnTimer = timerFailure;
                bool failed = false;
                try { Invoke(spawn, "fx_StaffShield_Hit"); }
                catch (TargetInvocationException e) when (e.InnerException is InvalidOperationException exception && exception.Message == "fixture failure")
                { failed = true; }
                Require(failed && ZNetView.m_forceDisableInit == prior, "exception cannot leak the global suppression flag");
            }

            Reset(false);
            Invoke(spawn, "fx_StaffShield_Hit");
            Require(Spawns == 1 && Triggers == 0 && !ZNetView.m_forceDisableInit, "missing timer does not leak the scope");

            foreach (string name in new[] { "blob_aoe", "fx_custom_gameplay", "custom_projectile" })
            foreach (bool prior in new[] { false, true })
            {
                Reset(prior);
                Timer = New<TimedDestruction>();
                Invoke(spawn, name);
                Require(Spawns == 1 && SpawnFlag == prior && ZNetView.m_forceDisableInit == prior && Triggers == 0,
                    "unknown/AoE prefab retains its initialization and timer policy: " + name);
            }

            Reset(false);
            Timer = New<TimedDestruction>(); // suppressed view has been removed by Unity before timeout
            Copy(typeof(TimedDestruction).GetMethod("DestroyNow", All)!).Invoke(null, new object[] { Timer });
            Require(DestroyedEffects == 1, "original timer cleans up locally without a ZNetView");
            CheckDeliveryRoutes(modifiers);
            CheckFixedBlinkEffect(modifiers);

            using var module = ModuleDefinition.ReadModule(plugin.Location);
            var type = module.GetType(modifiers.FullName);
            foreach (string caller in new[] { "PlayVortexHitEffectsLocal", "PlayReflectionEffectsLocal",
                         "PlayBlinkStartEffectLocal", "PlayConfiguredEffectsLocal" })
            {
                var calls = type.Methods.Single(m => m.Name == caller).Body.Instructions
                    .Select(i => i.Operand).OfType<MethodReference>().ToArray();
                Require(calls.Any(m => m.Name == "InstantiateModifierEffect") &&
                        !calls.Any(m => m.DeclaringType.FullName == "UnityEngine.Object" && m.Name == "Instantiate"),
                    "local playback cannot bypass effect classification: " + caller);
            }
        }
        finally { ZNetView.m_forceDisableInit = saved; }
        System.Console.WriteLine($"Modifier FX lifecycle: {Checks} checks passed. Cosmetic network-init suppression, original Awake guard, success/exception scope restoration, manual/automatic timers and AoE/custom preservation. Native Unity boundaries substituted; no scene or multiplayer session.");
    }

    private static object Invoke(MethodInfo method, string name, bool networked = false) =>
        method.Invoke(null, new object[] { Prefab, name, Position, Rotation, networked })!;

    private static void CheckDeliveryRoutes(Type modifiers)
    {
        foreach (bool server in new[] { false, true })
        foreach (string name in new[] { "PlayDeathwardTriggerEffects", "PlayBlinkStartEffect", "BroadcastReapingFeedback" })
        {
            Server = server;
            Broadcasts = 0;
            LocalRoutes.Clear();
            object[] args = name == "PlayDeathwardTriggerEffects" || name == "PlayBlinkStartEffect" ? new object[] { Position }
                : new object[] { New<Character>(), false };
            Copy(modifiers.GetMethod(name, All)!).Invoke(null, args);
            Require(server ? Broadcasts == 1 && LocalRoutes.Count == 0
                    : Broadcasts == 0 && LocalRoutes.Count == 1 && LocalRoutes[0],
                "server broadcasts / direct client owner retains network effect: " + name);
        }

        foreach (string name in new[] { "RPC_DeathwardEffect", "RPC_BlinkEffect", "RPC_ReapingFeedback" })
        {
            LocalRoutes.Clear();
            ZPackage package = new();
            package.Write(Position);
            if (name == "RPC_BlinkEffect") package.Write("blob_aoe");
            if (name == "RPC_ReapingFeedback")
            {
                package.Write(ZDOID.None);
                package.Write(false);
                package.Write(Vector3.one);
            }
            package.SetPos(0);
            Copy(modifiers.GetMethod(name, All)!).Invoke(null, new object[] { 42L, package });
            Require(LocalRoutes.Count == 1 && !LocalRoutes[0], "RPC recipient does not re-network its effect: " + name);
            Require(package.GetPos() == package.Size(), "RPC consumes the unchanged payload layout: " + name);
        }
    }

    private static void CheckFixedBlinkEffect(Type modifiers)
    {
        foreach (bool networked in new[] { false, true })
        {
            RequestedPrefab = PlayedPrefab = null;
            Copy(modifiers.GetMethod("PlayBlinkStartEffectLocal", All)!).Invoke(null, new object[] { Position, networked });
            Require(RequestedPrefab == "fx_Adrenaline1" && PlayedPrefab == "fx_Adrenaline1" && PlayedNetworked == networked,
                "Blink always looks up and plays the fixed effect while retaining its delivery mode");
        }
        foreach (string oldEffect in new[] { "blob_aoe", "custom_projectile", "none", "" })
        {
            var package = new ZPackage();
            package.Write(Position);
            package.Write(oldEffect);
            package.SetPos(0);
            LocalRoutes.Clear();
            Copy(modifiers.GetMethod("RPC_BlinkEffect", All)!).Invoke(null, new object[] { 42L, package });
            Require(LocalRoutes.Count == 1 && !LocalRoutes[0] && package.GetPos() == package.Size(),
                "old RPC effect name cannot select or suppress Blink playback: " + oldEffect);
        }
        // A saved v2 snapshot must still load even when it contains the now-inert key.
        var snapshot = new ZPackage();
        snapshot.Write(2);
        for (int section = 0; section < 3; section++) snapshot.Write(0);
        snapshot.Write(1);
        snapshot.Write("CreatureManager_ModifiersApplied".GetStableHashCode());
        snapshot.Write(1);
        snapshot.Write(0);
        snapshot.Write(1);
        snapshot.Write("CreatureManager_BlinkStartEffect".GetStableHashCode());
        snapshot.Write("blob_aoe");
        snapshot.Write(0);
        object?[] args = { Convert.ToBase64String(snapshot.GetArray()), null, null };
        Require((bool)modifiers.GetMethod("TryDeserializeModifierState", All)!.Invoke(null, args)!,
            "legacy Blink effect does not invalidate the saved modifier snapshot: " + args[2]);

        using var module = ModuleDefinition.ReadModule(modifiers.Assembly.Location);
        var type = module.GetType(modifiers.FullName);
        Require(!type.Methods.Where(m => m.HasBody && !m.IsConstructor).SelectMany(m => m.Body.Instructions)
                .Any(i => i.Operand is MethodReference m && m.Name == "get_StartEffect" ||
                          i.Operand is string text && text == "CreatureManager_BlinkStartEffect"),
            "runtime never consumes old DTO or stored Blink effect names");
    }
    private static T New<T>() => (T)FormatterServices.GetUninitializedObject(typeof(T));
    private static void Require(bool value, string message)
    {
        Checks++;
        if (!value) throw new InvalidOperationException("Modifier FX: " + message);
    }
    private static void Reset(bool prior)
    {
        ExpectedPrior = prior;
        ZNetView.m_forceDisableInit = prior;
        Timer = null;
        ThrowOnSpawn = ThrowOnTimer = SpawnFlag = false;
        Spawns = Triggers = DisabledViews = DestroyedEffects = 0;
    }
    private static MethodInfo Copy(MethodInfo source)
    {
        if (Copies.TryGetValue(source, out MethodInfo found)) return found;
        using var copy = new DynamicMethodDefinition(source);
        typeof(DynamicMethodDefinition).GetProperty(nameof(DynamicMethodDefinition.OriginalMethod))!.SetValue(copy, null);
        copy.OwnerType = typeof(ModifierEffectContracts);
        foreach (var instruction in copy.Definition.Body.Instructions)
        {
            if (instruction.Operand is not MethodReference method) continue;
            string? boundary = method.DeclaringType.FullName switch
            {
                "UnityEngine.Object" => method.Name switch
                {
                    "Instantiate" => nameof(Instantiate), "op_Inequality" => nameof(Different),
                    "op_Equality" => nameof(Equal), "op_Implicit" => nameof(Alive), "Destroy" => nameof(Destroy), _ => null
                },
                "UnityEngine.GameObject" when method.Name == "GetComponent" => nameof(GetTimer),
                "UnityEngine.Component" when method.Name == "get_gameObject" => nameof(GameObjectOf),
                "UnityEngine.Component" when method.Name == "get_transform" => nameof(TransformOf),
                "UnityEngine.Transform" when method.Name == "get_localScale" => nameof(Scale),
                "Character" when method.Name == "GetCenterPoint" => nameof(Center),
                "Character" when method.Name == "GetZDOID" => nameof(Id),
                "ZNet" when method.Name == "get_instance" => nameof(GetNet),
                "ZNet" when method.Name == "IsServer" => nameof(IsServer),
                "ZRoutedRpc" when method.Name == "get_instance" => nameof(GetRouter),
                "ZRoutedRpc" when method.Name == "InvokeRoutedRPC" => nameof(Broadcast),
                "ZNetScene" when method.Name == "get_instance" => nameof(GetScene),
                "ZNetScene" when method.Name == "GetPrefab" => nameof(GetPrefab),
                "CreatureManager.CreatureModifierManager" => method.Name switch
                {
                    "IsTrustedServerRpc" => nameof(Trusted),
                    "PlayDeathwardTriggerEffectsLocal" => nameof(DeathwardLocal),
                    "PlayBlinkStartEffectLocal" => nameof(BlinkLocal),
                    "IsEffectWithinPlaybackRange" => nameof(InRange),
                    "InstantiateModifierEffect" => nameof(BlinkSpawn),
                    "ApplyReapingFeedbackLocal" => nameof(ReapingLocal), _ => null
                },
                "TimedDestruction" when method.Name == "Trigger" => nameof(Trigger),
                _ => null
            };
            if (boundary == null) continue;
            instruction.OpCode = Mono.Cecil.Cil.OpCodes.Call;
            instruction.Operand = copy.Module.ImportReference(typeof(ModifierEffectContracts).GetMethod(boundary, All)!);
        }
        return Copies[source] = DMDGenerator<DMDCecilGenerator>.Generate(copy);
    }
    private static GameObject Instantiate(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        Spawns++;
        SpawnFlag = ZNetView.m_forceDisableInit;
        Require(ReferenceEquals(prefab, Prefab) && position == Position && rotation == Rotation, "preserves prefab/position/rotation");
        if (ThrowOnSpawn) throw new InvalidOperationException("fixture failure");
        if (SpawnFlag)
        {
            ZNetView view = New<ZNetView>();
            // The original Awake must return before native component access or ZDO registration.
            Copy(typeof(ZNetView).GetMethod("Awake", All)!).Invoke(null, new object[] { view });
            Require(view.GetZDO() == null, "suppressed original Awake creates no ZDO");
        }
        return Spawned;
    }
    private static TimedDestruction? GetTimer(GameObject _) => Timer;
    private static bool Different(UnityEngine.Object a, UnityEngine.Object b) => !ReferenceEquals(a, b);
    private static bool Equal(UnityEngine.Object a, UnityEngine.Object b) => ReferenceEquals(a, b);
    private static bool Alive(UnityEngine.Object value) => !ReferenceEquals(value, null);
    private static GameObject GameObjectOf(Component _) => Spawned;
    private static Transform TransformOf(Component _) => Transform;
    private static Vector3 Scale(Transform _) => Vector3.one;
    private static Vector3 Center(Character _) => Position;
    private static ZDOID Id(Character _) => ZDOID.None;
    private static ZNet GetNet() => Net;
    private static bool IsServer(ZNet _) => Server;
    private static ZRoutedRpc GetRouter() => Router;
    private static void Broadcast(ZRoutedRpc _, long receiver, string name, params object[] args)
    {
        Broadcasts++;
        if (name != "CreatureManager_BlinkEffect") return;
        var package = new ZPackage(((ZPackage)args[0]).GetArray());
        Require(package.ReadVector3() == Position && package.ReadString() == "fx_Adrenaline1" && package.GetPos() == package.Size(),
            "server sends the fixed Blink effect in the existing RPC format");
    }
    private static bool Trusted(long _) => true;
    private static void DeathwardLocal(Vector3 _, bool networked) => LocalRoutes.Add(networked);
    private static void BlinkLocal(Vector3 _, bool networked) => LocalRoutes.Add(networked);
    private static ZNetScene GetScene() => Scene;
    private static GameObject GetPrefab(ZNetScene _, string name) { RequestedPrefab = name; return Prefab; }
    private static bool InRange(Vector3 _) => true;
    private static GameObject BlinkSpawn(GameObject _, string name, Vector3 position, Quaternion rotation, bool networked)
    {
        PlayedPrefab = name;
        PlayedNetworked = networked;
        return Spawned;
    }
    private static void ReapingLocal(Vector3 _, ZDOID id, bool changed, Vector3 scale, bool networked) => LocalRoutes.Add(networked);
    private static void Trigger(TimedDestruction timer)
    {
        Require(ReferenceEquals(timer, Timer) && ZNetView.m_forceDisableInit == ExpectedPrior, "timer starts outside suppression scope");
        Triggers++;
        if (ThrowOnTimer) throw new InvalidOperationException("fixture failure");
    }
    private static void Destroy(UnityEngine.Object value)
    {
        if (value is ZNetView) DisabledViews++;
        else if (ReferenceEquals(value, Spawned)) DestroyedEffects++;
        else throw new InvalidOperationException("Unexpected lifecycle destruction");
    }
}
