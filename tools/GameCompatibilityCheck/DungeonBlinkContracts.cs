using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using Mono.Cecil;
using MonoMod.Utils;
using UnityEngine;

// Execute the production Blink decisions and capsule geometry. Only native scene queries,
// Unity object properties and the random sample are substituted; no physics scene is simulated.
internal static class DungeonBlinkContracts
{
    private const BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private static Type Modifiers = null!;
    private static readonly Dictionary<MethodInfo, MethodInfo> Copies = new();
    private static readonly Character Creature = New<Character>();
    private static readonly Player Target = New<Player>();
    private static readonly CapsuleCollider Capsule = New<CapsuleCollider>();
    private static readonly Transform CreatureTransform = New<Transform>(), TargetTransform = New<Transform>();
    private static readonly Queue<float?> CastResults = new();
    private static readonly Queue<bool> Overlaps = new();
    private static Vector3 Origin, TargetPosition, Scale, Center, FloorNormal;
    private static Vector2 RandomOffset;
    private static float Radius, Height, FloorY;
    private static int Axis, Mask, MaskLookups, ColliderLookups, Casts, Checks, Rays, Assertions;
    private static bool MissingCollider, Enabled, Trigger, Flying, HasFloor;
    private static Vector3 LastStart0, LastStart1, LastEnd0, LastEnd1;
    private static float LastRadius;

    internal static void Run(Assembly plugin)
    {
        Modifiers = plugin.GetType("CreatureManager.CreatureModifierManager", true)!;
        CheckOutdoorAndRealm();
        CheckObstaclesAndFloors();
        CheckCapsuleGeometry();
        CheckIntegration(plugin);
        System.Console.WriteLine($"Dungeon Blink contracts: {Assertions} checks passed. Outdoor/realm fast paths, wall clipping, starting/final overlap, floor bounds/slope/corrected path, flying, scaled/rotated capsules and effect ordering. Native queries substituted; real dungeon physics and multiplayer remain unverified.");
    }

    private static void CheckOutdoorAndRealm()
    {
        Reset(); Origin.y = TargetPosition.y = 20f; RandomOffset = new Vector2(0.25f, -0.5f);
        Require(Destination(out Vector3 point) && Near(point, new Vector3(10.5f, 20f, -1f)), "outdoor keeps the existing random destination");
        Require(ColliderLookups + MaskLookups + Casts + Checks + Rays == 0, "outdoor performs no collision/shape/mask queries");
        foreach (bool creatureInside in new[] { true, false })
        {
            Reset();
            if (creatureInside) TargetPosition.y = 0f; else Origin.y = 0f;
            Require(!Destination(out _) && Casts + Checks + Rays == 0, "cross-realm Blink rejected without scene queries");
        }

        Reset(); Require(Destination(out _) && Destination(out _), "repeated valid Blink");
        Require(MaskLookups == 1, "only fixed layer mask is cached");
        Overlaps.Enqueue(true);
        Require(!Destination(out _), "scene obstacles are checked again, never cached");
    }

    private static void CheckObstaclesAndFloors()
    {
        Reset();
        Require(Destination(out Vector3 point) && Near(point, TargetPosition), "open corridor keeps intended destination");
        Require(Casts == 1 && Checks == 2 && Rays == 1, "flat corridor has bounded queries and no retry loop");

        Reset(); CastResults.Enqueue(4.5f);
        Require(Destination(out point) && point.x > 4f && point.x < 4.5f, "wall clips movement before impact instead of canceling the whole Blink");
        Require(point.x + Radius < 5f, "full body remains in front of the wall at x=5");

        Reset(); CastResults.Enqueue(0.3f);
        Require(!Destination(out _) && Rays == 0, "insignificant clipped movement cancels");
        Reset(); Overlaps.Enqueue(true);
        Require(!Destination(out _) && Casts == 0, "starting overlap cannot slip through CapsuleCast");
        Reset(); Overlaps.Enqueue(false); Overlaps.Enqueue(true);
        Require(!Destination(out _), "final wall/ceiling overlap cancels");
        Reset(); HasFloor = false;
        Require(!Destination(out _), "ground creature cannot land over a void");
        Reset(); FloorY = 100f;
        Require(!Destination(out _), "outdoor terrain below dungeon is not a landing floor");
        Reset(); FloorY = 5002f;
        Require(!Destination(out _), "floor above the short probe is rejected");
        Reset(); FloorNormal = new Vector3(0.9f, 0.3f, 0f);
        Require(!Destination(out _), "steep wall face is not a floor");

        Reset(); FloorY = 4999f;
        Require(Destination(out point) && Near(point, new Vector3(10f, 4999f, 0f)) && Casts == 2,
            "nearby lower floor is reachable only after checking corrected path");
        Reset(); FloorY = 4999f; CastResults.Enqueue(null); CastResults.Enqueue(2f);
        Require(!Destination(out _), "floor adjustment cannot cross an intervening wall/slab");

        Reset(); Flying = true; HasFloor = false; CastResults.Enqueue(4.5f);
        Require(Destination(out point) && point.x < 4.5f && Rays == 0 && Checks == 2,
            "flying creatures keep altitude and still respect walls/clearance");
        foreach (string state in new[] { "missing", "disabled", "trigger", "invalid scale" })
        {
            Reset();
            if (state == "missing") MissingCollider = true;
            if (state == "disabled") Enabled = false;
            if (state == "trigger") Trigger = true;
            if (state == "invalid scale") Scale.x = float.NaN;
            Require(!Destination(out _) && Casts + Checks + Rays == 0, "unsafe capsule cancels: " + state);
        }
    }

    private static void CheckCapsuleGeometry()
    {
        Reset(); Scale = new Vector3(2f, 2f, 2f);
        Require(Destination(out _), "scaled creature in clear corridor");
        Require(LastRadius > 0.95f && LastRadius < 1f && Near(LastStart0, Origin + Vector3.up) &&
                Near(LastStart1, Origin + Vector3.up * 3f), "cast uses scaled live collider, not prefab size");

        Reset(); Axis = 0; Height = 4f; Center = new Vector3(1f, 1f, 1f); Scale = new Vector3(2f, 3f, 4f);
        float half = (float)Math.Sqrt(0.5);
        object?[] args = { Capsule, Vector3.zero, new Quaternion(0f, half, 0f, half), null, null, null };
        Require((bool)Call("TryGetBlinkCapsule", args)!, "horizontal capsule geometry is supported");
        Require(Near((Vector3)args[3]!, new Vector3(4f, 3f, 0f)) &&
                Near((Vector3)args[4]!, new Vector3(4f, 3f, -4f)) && (float)args[5]! == 2f,
            "direction, nonuniform scale, center and rotation are included");

        Reset(); Axis = 0; Height = 4f; Center = new Vector3(0f, 0.5f, 0f); RandomOffset = new Vector2(-0.5f, 0f);
        Require(Destination(out _), "horizontal capsule can face target at arrival");
        Require(Math.Abs(LastStart1.x - LastStart0.x) > 2.9f && Math.Abs(LastEnd1.z - LastEnd0.z) > 2.9f,
            "arrival clearance uses final facing rather than start orientation");
    }

    private static void CheckIntegration(Assembly plugin)
    {
        using var module = ModuleDefinition.ReadModule(plugin.Location);
        var modifier = module.GetType(Modifiers.FullName);
        var instructions = modifier.Methods.Single(m => m.Name == "TryBlinkOnAttackStart").Body.Instructions.ToList();
        int CallAt(string name) => instructions.FindIndex(i => i.Operand is MethodReference m && m.Name == name);
        int destination = CallAt("TryGetBlinkDestination");
        int cooldown = instructions.FindIndex(i => i.Operand is string s && s == "CreatureManager_BlinkNextTime" &&
            instructions.IndexOf(i) > destination);
        Require(CallAt("IsOwner") >= 0 && CallAt("IsOwner") < destination && cooldown > destination &&
                CallAt("PlayBlinkStartEffect") > cooldown && CallAt("TeleportCharacter") > cooldown,
            "owner guard and destination validation precede cooldown, effect and movement");
        Require(typeof(Character).GetMethod("GetCollider")!.IsPublic && typeof(Character).GetMethod("IsFlying")!.IsPublic,
            "new game calls use original public APIs");
    }

    private static void Reset()
    {
        Origin = new Vector3(0f, 5000f, 0f); TargetPosition = new Vector3(10f, 5000f, 0f);
        Scale = Vector3.one; Center = Vector3.up; FloorNormal = Vector3.up; RandomOffset = Vector2.zero;
        Radius = 0.5f; Height = 2f; FloorY = 5000f; Axis = 1;
        Mask = MaskLookups = ColliderLookups = Casts = Checks = Rays = 0;
        MissingCollider = Trigger = Flying = false; Enabled = HasFloor = true;
        CastResults.Clear(); Overlaps.Clear();
    }

    private static bool Destination(out Vector3 point)
    {
        object?[] args = { Creature, Target, null };
        bool result = (bool)Call("TryGetBlinkDestination", args)!;
        point = (Vector3)args[2]!;
        return result;
    }

    private static object? Call(string name, params object?[] args) => Copy(Modifiers.GetMethod(name, Static)!).Invoke(null, args);

    private static MethodInfo Copy(MethodInfo source)
    {
        if (Copies.TryGetValue(source, out MethodInfo result)) return result;
        using var copy = new DynamicMethodDefinition(source);
        typeof(DynamicMethodDefinition).GetProperty(nameof(DynamicMethodDefinition.OriginalMethod))!.SetValue(copy, null);
        copy.OwnerType = typeof(DungeonBlinkContracts);
        foreach (var instruction in copy.Definition.Body.Instructions.ToArray())
        {
            if (instruction.Operand is FieldReference field && field.DeclaringType.FullName == Modifiers.FullName)
            {
                if (field.Name != "DungeonBlinkSolidMask") throw new InvalidOperationException("Unexpected Blink field: " + field.Name);
                instruction.Operand = copy.Module.ImportReference(typeof(DungeonBlinkContracts).GetField(nameof(Mask), Static)!);
                continue;
            }
            if (!(instruction.Operand is MethodReference method)) continue;
            string type = method.DeclaringType.FullName;
            string? boundary = type switch
            {
                "UnityEngine.Object" => method.Name switch { "op_Equality" => nameof(Equal), _ => null },
                "UnityEngine.Component" => method.Name == "get_transform" ? nameof(TransformOf) : null,
                "UnityEngine.Transform" => method.Name switch { "get_position" => nameof(PositionOf), "get_rotation" => nameof(RotationOf), "get_lossyScale" => nameof(ScaleOf), _ => null },
                "UnityEngine.Random" => method.Name == "get_insideUnitCircle" ? nameof(Sample) : null,
                "UnityEngine.LayerMask" => method.Name == "GetMask" ? nameof(Layers) : null,
                "UnityEngine.Collider" => method.Name switch { "get_enabled" => nameof(IsEnabled), "get_isTrigger" => nameof(IsTrigger), _ => null },
                "UnityEngine.CapsuleCollider" => method.Name switch { "get_radius" => nameof(GetRadius), "get_height" => nameof(GetHeight), "get_direction" => nameof(GetAxis), "get_center" => nameof(GetCenter), _ => null },
                "UnityEngine.Quaternion" => method.Name == "LookRotation" ? nameof(Look) : null,
                "UnityEngine.Physics" => method.Name switch { "CapsuleCast" => nameof(Cast), "CheckCapsule" => nameof(Overlap), "Raycast" => nameof(Floor), _ => null },
                "Character" => method.Name switch { "GetCollider" => nameof(GetCapsule), "IsFlying" => nameof(IsFlying), _ => null },
                _ => null
            };
            MethodInfo? target = boundary == null ? null : typeof(DungeonBlinkContracts).GetMethod(boundary, Static);
            if (target == null && (type == Modifiers.FullName || type == "Character" && method.Name == "InInterior" ||
                type == "Utils" && method.Name == "DistanceXZ"))
                target = Copy((MethodInfo)method.ResolveReflection());
            if (target == null) continue;
            instruction.OpCode = Mono.Cecil.Cil.OpCodes.Call;
            instruction.Operand = copy.Module.ImportReference(target);
        }
        return Copies[source] = copy.Generate();
    }

    private static T New<T>() => (T)FormatterServices.GetUninitializedObject(typeof(T));
    private static bool Equal(UnityEngine.Object a, UnityEngine.Object b) => ReferenceEquals(a, b);
    private static Transform TransformOf(Component c) => ReferenceEquals(c, Target) ? TargetTransform : CreatureTransform;
    private static Vector3 PositionOf(Transform t) => ReferenceEquals(t, TargetTransform) ? TargetPosition : Origin;
    private static Quaternion RotationOf(Transform _) => Quaternion.identity;
    private static Vector3 ScaleOf(Transform _) => Scale;
    private static Vector2 Sample() => RandomOffset;
    private static CapsuleCollider? GetCapsule(Character _) { ColliderLookups++; return MissingCollider ? null : Capsule; }
    private static bool IsFlying(Character _) => Flying;
    private static bool IsEnabled(Collider _) => Enabled;
    private static bool IsTrigger(Collider _) => Trigger;
    private static float GetRadius(CapsuleCollider _) => Radius;
    private static float GetHeight(CapsuleCollider _) => Height;
    private static int GetAxis(CapsuleCollider _) => Axis;
    private static Vector3 GetCenter(CapsuleCollider _) => Center;
    private static Quaternion Look(Vector3 direction, Vector3 up)
    {
        double halfYaw = Math.Atan2(direction.x, direction.z) * 0.5;
        return new Quaternion(0f, (float)Math.Sin(halfYaw), 0f, (float)Math.Cos(halfYaw));
    }
    private static int Layers(string[] names)
    {
        MaskLookups++;
        Require(names.Contains("static_solid") && names.Contains("piece") && names.Contains("terrain") &&
                !names.Any(n => n.StartsWith("character", StringComparison.Ordinal)), "geometry mask excludes self/other characters");
        return 42;
    }
    private static bool Cast(Vector3 a, Vector3 b, float radius, Vector3 direction, out RaycastHit hit, float distance, int mask, QueryTriggerInteraction triggers)
    {
        Casts++; LastStart0 = a; LastStart1 = b; LastRadius = radius;
        Require(mask == 42 && triggers == QueryTriggerInteraction.Ignore && distance > 0f, "bounded solid-only sweep");
        float? result = CastResults.Count == 0 ? null : CastResults.Dequeue();
        hit = new RaycastHit { distance = result ?? 0f };
        return result.HasValue;
    }
    private static bool Overlap(Vector3 a, Vector3 b, float radius, int mask, QueryTriggerInteraction triggers)
    {
        Checks++; LastEnd0 = a; LastEnd1 = b;
        Require(mask == 42 && triggers == QueryTriggerInteraction.Ignore, "solid-only overlap");
        return Overlaps.Count > 0 && Overlaps.Dequeue();
    }
    private static bool Floor(Vector3 origin, Vector3 direction, out RaycastHit hit, float distance, int mask, QueryTriggerInteraction triggers)
    {
        Rays++;
        Require(direction == Vector3.down && distance <= 3f && mask == 42 && triggers == QueryTriggerInteraction.Ignore, "floor probe stays local");
        hit = new RaycastHit { point = new Vector3(origin.x, FloorY, origin.z), normal = FloorNormal };
        float drop = origin.y - FloorY;
        return HasFloor && drop >= 0f && drop <= distance;
    }
    private static bool Near(Vector3 a, Vector3 b) => (a - b).sqrMagnitude < 0.00001f;
    private static void Require(bool condition, string description)
    {
        Assertions++;
        if (!condition) throw new InvalidOperationException("Dungeon Blink: " + description);
    }
}
