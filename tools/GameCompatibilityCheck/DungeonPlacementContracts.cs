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

// Run the shipped location/placement/capsule decisions, replacing only scene queries and
// native Unity properties. This does not simulate Unity physics or a network session.
internal static class DungeonPlacementContracts
{
    private const BindingFlags All = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static Type Karma = null!;
    private static readonly Dictionary<MethodInfo, MethodInfo> Copies = new();
    private static readonly GameObject Prefab = New<GameObject>();
    private static readonly CapsuleCollider Capsule = New<CapsuleCollider>();
    private static readonly Transform Transform = New<Transform>();
    private static readonly ZoneSystem Zone = New<ZoneSystem>();
    private static readonly Pathfinding Navigation = New<Pathfinding>();
    private static readonly List<Vector3> DungeonSpawnPath = new();
    private static int DungeonSpawnStaticMask = 1, DungeonSpawnCollisionMask = 2;
    private static Vector3 Scale, Center, Origin, Candidate, PathEnd, PathTarget, FloorProbe;
    private static Vector3 LastCapsule0, LastCapsule1;
    private static float Radius, Height, FloorY, LastRadius;
    private static int Axis, Queries, Assertions, NameLookups;
    private static bool HasFloor, FullPath, Blocked, LineBlocked, MissingCapsule;

    internal static void Run(Assembly plugin)
    {
        Karma = plugin.GetType("CreatureManager.CreatureKarmaManager", true)!;
        CheckLocations();
        CheckPlacement();
        CheckDiagnostics(plugin);
        System.Console.WriteLine($"Dungeon Enforcer placement: {Assertions} checks passed. Interior registry/radius, original realm/zone rules, floor/pivot and path corrections, scaled/rotated capsules, reserved positions, bounded/off diagnostics. Native geometry substituted; no actual dungeon or multiplayer session.");
    }

    private static void CheckLocations()
    {
        FieldInfo registryField = typeof(ZoneSystem).GetField("m_locationInstances", All)!;
        var registry = (IDictionary)Activator.CreateInstance(registryField.FieldType)!;
        registryField.SetValue(Zone, registry);
        AddLocation(0, "SunkenCrypt4"); AddLocation(1, "Crypt2"); AddLocation(2, "");
        Require(Location(new Vector3(0, 5000, 0)) == "SunkenCrypt4", "center resolves");
        foreach (float x in new[] { -31f, 0f, 31f })
        foreach (float z in new[] { -31f, 0f, 31f })
            Require(Location(new Vector3(x, 5000, z)) == "SunkenCrypt4", "outer room resolves independently of entrance radius");
        Require(Location(new Vector3(64, 5000, 0)) == "Crypt2", "adjacent zone uses its own location");
        Require(Location(new Vector3(0, 5000, 64)) == null && Location(new Vector3(128, 5000, 0)) == null, "missing/empty location rejected");
        Require(Location(new Vector3(1, 30, 0)) == "SunkenCrypt4" && Location(new Vector3(31, 30, 0)) == null, "outdoor radius unchanged");
        Require(Location(new Vector3(float.NaN, 5000, 0)) == null, "invalid coordinates rejected before registry access");
        Require(Location(new Vector3(31, 3000.5f, 0)) != null && Location(new Vector3(31, 3000, 0)) == null,
            "native interior boundary retained, not a 3500m cutoff");

        void AddLocation(int x, string name)
        {
            Type locationType = typeof(ZoneSystem).GetNestedType("ZoneLocation", All)!;
            Type instanceType = typeof(ZoneSystem).GetNestedType("LocationInstance", All)!;
            object location = FormatterServices.GetUninitializedObject(locationType), instance = Activator.CreateInstance(instanceType)!;
            locationType.GetField("m_prefabName", All)!.SetValue(location, name);
            locationType.GetField("m_exteriorRadius", All)!.SetValue(location, 2f);
            locationType.GetField("m_interiorRadius", All)!.SetValue(location, 3f);
            instanceType.GetField("m_location", All)!.SetValue(instance, location);
            instanceType.GetField("m_position", All)!.SetValue(instance, new Vector3(x * 64, 30, 0));
            registry.Add(new Vector2s(x, 0), instance);
        }
    }

    private static string? Location(Vector3 point)
    {
        object?[] args = { point, null };
        return (bool)Call("TryGetZoneLocationPrefabName", args)! ? (string)args[1]! : null;
    }

    private static void CheckPlacement()
    {
        Reset(); Require(Place(out Vector3 point) && Near(point, Candidate), "normal upright pivot unchanged");
        Require(Near(PathTarget, Candidate), "navigation targets supporting floor");
        Reset(); Radius = 1.2f; Height = 1; // Original 1.0.17 BlobElite shape: effective height = 2 * radius.
        Require(Place(out point) && Near(point, Candidate + Vector3.up * 0.2f), "BlobElite-like pivot lifted 0.2m");
        Require(Math.Min(LastCapsule0.y, LastCapsule1.y) - LastRadius >= FloorY, "supporting floor is not an obstacle");
        Reset(); Center = Vector3.zero; Height = 6; Radius = 1;
        PathEnd = Candidate + new Vector3(0.25f, 0.25f, 0);
        Require(Place(out point) && Near(point, PathEnd + Vector3.up * 3), "large pivot lifted again after navmesh correction");
        Require(Near(PathTarget, Candidate), "large offset does not alter path endpoint eligibility");
        FloorY = PathEnd.y;
        Require((bool)Call("IsReservedDungeonPositionValid", Prefab, point, Origin)!, "large lift survives reserved-position validation");
        Require(Near(FloorProbe, PathEnd), "reserved floor probe uses supporting floor, not elevated pivot");
        Blocked = true;
        Require(!(bool)Call("IsReservedDungeonPositionValid", Prefab, point, Origin)!, "new obstruction cancels without moving reserved position");

        foreach (int axis in new[] { 0, 1, 2 })
        {
            Reset(); Axis = axis; Scale = new Vector3(-2, 3, 4); Center = Vector3.zero; Height = 4;
            float half = (float)Math.Sqrt(0.5);
            Quaternion yaw = new(0, half, 0, half);
            float offset = (float)Call("GetDungeonSpawnFloorOffset", Prefab, yaw)!;
            Require(Math.Abs(offset - (axis == 1 ? 6 : axis == 0 ? 2 : 1.5f)) < 0.0001f, "axis and absolute scale determine bottom");
            Require((bool)Call("HasDungeonSpawnClearance", Prefab, new Vector3(0, FloorY + offset, 0), yaw)!, "horizontal/vertical capsule clearance supported");
            Require(Math.Min(LastCapsule0.y, LastCapsule1.y) - LastRadius >= FloorY, "rotated/scaled shape clears floor");
        }
        Reset(); Center.y = 2;
        Require((float)Call("GetDungeonSpawnFloorOffset", Prefab, Quaternion.identity)! == 0, "already elevated pivot never lowered");
        Reset(); MissingCapsule = true;
        Require(Place(out point) && Near(point, Candidate) && LastRadius == 0.35f, "missing capsule retains existing generic clearance");

        Reset(); Candidate.x = 33;
        Require(!Place(out _) && Queries == 0, "adjacent-zone candidate rejected before physics");
        Reset(); Candidate.x = float.PositiveInfinity;
        Require(!Place(out _) && Queries == 0, "nonfinite candidate rejected before physics");
        Reset(); Candidate.y = 20;
        Require(!Place(out _), "ground world not accepted as dungeon");
        Reset(); Origin.x = 29; Candidate.x = 31f; PathEnd = new Vector3(32.1f, FloorY, 0);
        bool edgeFound = Place(out point);
        Require(edgeFound && Near(point, Candidate), "invalid cross-zone path endpoint never replaces valid direct candidate");
        LineBlocked = true; Require(!Place(out _), "invalid path plus blocked line remains rejected");
        Reset(); HasFloor = false; Require(!Place(out _), "missing floor rejected");
        Reset(); FloorY -= 5; Require(!Place(out _), "distant floor rejected");
        Reset(); Blocked = true; Require(!Place(out _), "wall or ceiling overlap remains rejected");
        Reset(); FullPath = false; LineBlocked = true; Require(!Place(out _), "unreachable candidate remains rejected");
        Require(!(bool)Call("IsSameDungeonZone", Origin, new Vector3(64, 5000, 0))!, "anchor guard rejects adjacent dungeon");

        using var module = ModuleDefinition.ReadModule(Karma.Assembly.Location);
        var manager = module.GetType(Karma.FullName);
        Require(manager.Methods.Single(m => m.Name == "TryFindDungeonZdoAnchorPosition").Body.Instructions.Any(i =>
            i.Operand is MethodReference m && m.Name == "IsSameDungeonZone"), "unloaded anchors use the same physical-zone guard");
    }

    private static void CheckDiagnostics(Assembly plugin)
    {
        Type entry = plugin.GetType("CreatureManager.CreatureManagerPlugin", true)!;
        Type diag = Karma.GetNestedType("DungeonDiagnostics", All)!, reasons = Karma.GetNestedType("DungeonSpawnReason", All)!;
        FieldInfo configField = entry.GetField("DungeonEnforcerDiagnostics", All)!;
        object? saved = configField.GetValue(null);
        Type toggle = configField.FieldType.GetGenericArguments()[0];
        var config = new ConfigFile(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "cm-dungeon-diag-" + Guid.NewGuid() + ".cfg"), false) { SaveOnConfigSet = false };
        MethodInfo bind = typeof(ConfigFile).GetMethods().Single(m => m.Name == "Bind" && m.IsGenericMethodDefinition &&
            m.GetParameters().Length == 4 && m.GetParameters()[3].ParameterType == typeof(ConfigDescription));
        var setting = (ConfigEntryBase)bind.MakeGenericMethod(toggle).Invoke(config, new[] { "fixture", "diag", Enum.Parse(toggle, "Off"), new ConfigDescription("") })!;
        configField.SetValue(null, setting);
        var entries = (IDictionary)diag.GetField("Entries", All)!.GetValue(null)!;
        void Invoke(string method, params object?[] args)
        {
            MethodInfo body = diag.GetMethod(method, All)!;
            (method == "Record" ? Copy(body) : body).Invoke(null, args);
        }
        void Record(object reason, string detail) => Invoke("Record", reason, new Vector3(1, 5000, 2), null, detail);
        try
        {
            Invoke("Reset");
            NameLookups = 0;
            Invoke("Record", Enum.GetValues(reasons).GetValue(0), Vector3.zero, Prefab, "ignored");
            Require(entries.Count == 0 && NameLookups == 0, "Off retains no observations and never looks up prefab names");
            setting.SetSerializedValue("On"); Invoke("Update", 100f);
            foreach (object reason in Enum.GetValues(reasons))
                for (int i = 0; i < 100; i++) Record(reason, "prefab\r\n|" + new string('x', 200));
            Require(entries.Count == Enum.GetValues(reasons).Length, "buckets bounded by fixed reasons, not prefab/player count");
            foreach (DictionaryEntry bucket in entries)
            {
                var samples = (IList)bucket.Value!.GetType().GetField("Samples", All)!.GetValue(bucket.Value)!;
                Require((int)bucket.Value.GetType().GetField("Count", All)!.GetValue(bucket.Value)! == 100 && samples.Count == 3,
                    "all attempts counted but only three samples retained per reason");
                Require(samples.Cast<string>().All(s => s.Length < 160 && !s.Contains('\n') && !s.Contains('\r') && !s.Contains('|')),
                    "sample text is bounded and cannot inject log lines");
            }
            Invoke("Update", 129.99f); Require(entries.Count > 0, "summary waits full window");
            Invoke("Update", 130f); Require(entries.Count == 0, "summary consumes observations at deadline");
            Invoke("Record", Enum.GetValues(reasons).GetValue(0), Vector3.zero, Prefab, "ignored");
            Require(NameLookups == 1, "On queries a prefab name only while collecting a bounded sample");
            var logger = (BepInEx.Logging.ManualLogSource)entry.GetProperty("Log", All)!.GetValue(null)!;
            EventHandler<BepInEx.Logging.LogEventArgs> failingListener = (_, _) => throw new InvalidOperationException("fixture listener");
            logger.LogEvent += failingListener;
            try { Invoke("Update", 160f); Require(entries.Count == 0, "failed log listener cannot escape or retain stale observations"); }
            finally { logger.LogEvent -= failingListener; }
            Record(Enum.GetValues(reasons).GetValue(0)!, "next"); setting.SetSerializedValue("Off"); Invoke("Update", 131f);
            Require(entries.Count == 0 && (float)diag.GetField("NextReport", All)!.GetValue(null)! == 0, "Off clears stale window");
            setting.SetSerializedValue("On"); Record(Enum.GetValues(reasons).GetValue(0)!, "reset"); Invoke("Reset");
            Require(entries.Count == 0, "world/config reset clears observations");
        }
        finally { Invoke("Reset"); configField.SetValue(null, saved); }
    }

    private static void Reset()
    {
        Origin = new Vector3(0, 5000, 0); Candidate = new Vector3(8, 5000, 0); PathEnd = Candidate;
        Scale = Vector3.one; Center = Vector3.up; Axis = 1; Radius = 0.5f; Height = 2; FloorY = 5000;
        HasFloor = FullPath = true; Blocked = LineBlocked = MissingCapsule = false; Queries = 0;
    }
    private static bool Place(out Vector3 point)
    {
        object?[] args = { Prefab, Origin, Origin, Candidate, null };
        bool found = (bool)Call("TryValidateDungeonSpawnPosition", args)!; point = (Vector3)args[4]!; return found;
    }
    private static object? Call(string name, params object?[] args) => Copy(Karma.GetMethod(name, All)!).Invoke(null, args);
    private static MethodInfo Copy(MethodInfo source)
    {
        if (Copies.TryGetValue(source, out MethodInfo result)) return result;
        using var copy = new DynamicMethodDefinition(source);
        typeof(DynamicMethodDefinition).GetProperty(nameof(DynamicMethodDefinition.OriginalMethod))!.SetValue(copy, null);
        copy.OwnerType = typeof(DungeonPlacementContracts);
        foreach (var instruction in copy.Definition.Body.Instructions.ToArray())
        {
            if (instruction.Operand is FieldReference field && field.DeclaringType.FullName == Karma.FullName)
            {
                instruction.Operand = copy.Module.ImportReference(typeof(DungeonPlacementContracts).GetField(field.Name, All) ??
                    throw new InvalidOperationException("Unexpected placement field: " + field.Name));
                continue;
            }
            if (!(instruction.Operand is MethodReference method)) continue;
            string type = method.DeclaringType.FullName;
            string? boundary = type switch
            {
                "UnityEngine.Object" => method.Name switch { "op_Equality" => nameof(Equal), "op_Inequality" => nameof(Different), "get_name" => nameof(Name), _ => null },
                "UnityEngine.GameObject" => method.Name switch { "GetComponent" => nameof(ComponentOf), "get_transform" => nameof(TransformOf), _ => null },
                "UnityEngine.Transform" => method.Name == "get_lossyScale" ? nameof(ScaleOf) : null,
                "UnityEngine.CapsuleCollider" => method.Name switch { "get_radius" => nameof(GetRadius), "get_height" => nameof(GetHeight), "get_center" => nameof(GetCenter), "get_direction" => nameof(GetAxis), _ => null },
                "UnityEngine.Quaternion" => method.Name == "LookRotation" ? nameof(Look) : null,
                "UnityEngine.Physics" => method.Name switch { "CheckCapsule" => nameof(Overlap), "Linecast" => nameof(Line), _ => null },
                "ZoneSystem" => method.Name switch { "get_instance" => nameof(GetZone), "IsZoneLoaded" => nameof(Loaded), "GetSolidHeight" => nameof(Floor), _ => null },
                "Pathfinding" => method.Name switch { "get_instance" => nameof(GetNavigation), "GetPath" => nameof(Path), _ => null },
                _ => type == Karma.FullName ? method.Name switch
                {
                    "TryGetExpandWorldDataCurrentLocationPrefabName" => nameof(NoEwd),
                    "TryFindDungeonZdoAnchorPosition" => nameof(UnloadedAnchor), _ => null
                } : null
            };
            MethodInfo? target = boundary == null ? null : typeof(DungeonPlacementContracts).GetMethod(boundary, All);
            if (boundary == nameof(ComponentOf)) target = target!.MakeGenericMethod(((GenericInstanceMethod)method).GenericArguments[0].ResolveReflection());
            if (target == null && (type == Karma.FullName || type == "ZoneSystem" && method.Name == "GetZone" ||
                type == "Character" && method.Name == "InInterior" || type == "Utils" && method.Name is "FloorToInt" or "DistanceXZ"))
                target = Copy((MethodInfo)method.ResolveReflection());
            if (target == null) continue;
            instruction.OpCode = Mono.Cecil.Cil.OpCodes.Call; instruction.Operand = copy.Module.ImportReference(target);
        }
        return Copies[source] = copy.Generate();
    }
    private static T New<T>() => (T)FormatterServices.GetUninitializedObject(typeof(T));
    private static bool Equal(UnityEngine.Object a, UnityEngine.Object b) => ReferenceEquals(a, b);
    private static bool Different(UnityEngine.Object a, UnityEngine.Object b) => !ReferenceEquals(a, b);
    private static string Name(UnityEngine.Object _) { NameLookups++; return "fixture"; }
    private static T? ComponentOf<T>(GameObject _) where T : class => typeof(T) == typeof(CapsuleCollider) && !MissingCapsule ? Capsule as T : null;
    private static Transform TransformOf(GameObject _) => Transform;
    private static Vector3 ScaleOf(Transform _) => Scale;
    private static float GetRadius(CapsuleCollider _) => Radius;
    private static float GetHeight(CapsuleCollider _) => Height;
    private static Vector3 GetCenter(CapsuleCollider _) => Center;
    private static int GetAxis(CapsuleCollider _) => Axis;
    private static Quaternion Look(Vector3 direction) // Native LookRotation is a fixture boundary, not the capsule math.
    {
        double half = Math.Atan2(direction.x, direction.z) * 0.5;
        return new Quaternion(0, (float)Math.Sin(half), 0, (float)Math.Cos(half));
    }
    private static ZoneSystem GetZone() => Zone;
    private static bool Loaded(ZoneSystem _, Vector3 p) => true;
    private static bool UnloadedAnchor(Vector3 p, float radius, out Vector3 result)
    { throw new InvalidOperationException("Loaded geometry fixture must not use anchor fallback"); }
    private static bool NoEwd(out string name) { name = ""; return false; }
    private static Pathfinding GetNavigation() => Navigation;
    private static bool Floor(ZoneSystem _, Vector3 p, out float height, int margin)
    { Queries++; FloorProbe = p; height = FloorY; return HasFloor; }
    private static bool Overlap(Vector3 a, Vector3 b, float radius, int mask, QueryTriggerInteraction triggers)
    {
        Queries++; LastCapsule0 = a; LastCapsule1 = b; LastRadius = radius;
        Require(mask == DungeonSpawnCollisionMask && triggers == QueryTriggerInteraction.Ignore, "existing collision mask retained");
        return Blocked || Math.Min(a.y, b.y) - radius < FloorY - 0.001f;
    }
    private static bool Line(Vector3 a, Vector3 b, int mask, QueryTriggerInteraction triggers)
    { Require(mask == DungeonSpawnStaticMask, "existing route mask retained"); return LineBlocked; }
    private static bool Path(Pathfinding _, Vector3 start, Vector3 end, List<Vector3> path, Pathfinding.AgentType agent,
        bool requireFullPath, bool cleanup, bool havePath)
    { PathTarget = end; path.Add(start); path.Add(PathEnd); return FullPath; }
    private static bool Near(Vector3 a, Vector3 b) => (a - b).sqrMagnitude < 0.00001f;
    private static void Require(bool condition, string label)
    { Assertions++; if (!condition) throw new InvalidOperationException("Dungeon Enforcer placement: " + label); }
}
