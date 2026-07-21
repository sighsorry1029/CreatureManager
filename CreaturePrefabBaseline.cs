using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CreatureManager;

[Flags]
internal enum CreaturePrefabBaselineGroup : ulong
{
    None = 0,

    AttackDamage = 1UL << 0,
    AttackTuple = 1UL << 1,
    AttackStatusEffect = 1UL << 2,
    AttackProjectile = 1UL << 3,
    AttackAi = 1UL << 4,

    CharacterIdentity = 1UL << 5,
    CharacterBoss = 1UL << 6,
    CharacterGlobalKey = 1UL << 7,
    CharacterHealth = 1UL << 8,
    CharacterDamageModifiers = 1UL << 9,
    CharacterSpeed = 1UL << 10,
    CharacterJump = 1UL << 11,
    CharacterSwim = 1UL << 12,
    CharacterFlight = 1UL << 13,

    BaseAiSenses = 1UL << 14,
    BaseAiIdleSound = 1UL << 15,
    BaseAiMovement = 1UL << 16,
    BaseAiSerpent = 1UL << 17,
    BaseAiRandomMove = 1UL << 18,
    BaseAiFlight = 1UL << 19,
    BaseAiAvoid = 1UL << 20,
    BaseAiFlee = 1UL << 21,
    BaseAiAggressive = 1UL << 22,
    BaseAiMessages = 1UL << 23,

    MonsterAiAlertRange = 1UL << 24,
    MonsterAiHunt = 1UL << 25,
    MonsterAiChase = 1UL << 26,
    MonsterAiCircle = 1UL << 27,
    MonsterAiHurtFlee = 1UL << 28,
    MonsterAiCharge = 1UL << 29,
    MonsterAiSleep = 1UL << 30,
    MonsterAiAvoidLand = 1UL << 31,

    HumanoidDefaultItems = 1UL << 32,
    HumanoidRandomWeapon = 1UL << 33,
    HumanoidRandomArmor = 1UL << 34,
    HumanoidRandomShield = 1UL << 35,
    HumanoidRandomItems = 1UL << 36,
    HumanoidRandomSets = 1UL << 37,

    VisualScale = 1UL << 38,
    RagdollReferences = 1UL << 39,
    Appearance = 1UL << 40,

    ProjectileSpawnOnHit = 1UL << 41,
    SpawnAbilitySpawnPrefabs = 1UL << 42,

    AttackAll = AttackDamage | AttackTuple | AttackStatusEffect | AttackProjectile | AttackAi,
    CharacterAll = CharacterIdentity | CharacterBoss | CharacterGlobalKey | CharacterHealth |
                   CharacterDamageModifiers | CharacterSpeed | CharacterJump | CharacterSwim | CharacterFlight,
    BaseAiAll = BaseAiSenses | BaseAiIdleSound | BaseAiMovement | BaseAiSerpent | BaseAiRandomMove |
                BaseAiFlight | BaseAiAvoid | BaseAiFlee | BaseAiAggressive | BaseAiMessages,
    MonsterAiAll = MonsterAiAlertRange | MonsterAiHunt | MonsterAiChase | MonsterAiCircle |
                   MonsterAiHurtFlee | MonsterAiCharge | MonsterAiSleep | MonsterAiAvoidLand,
    HumanoidAll = HumanoidDefaultItems | HumanoidRandomWeapon | HumanoidRandomArmor |
                  HumanoidRandomShield | HumanoidRandomItems | HumanoidRandomSets,
    VisualAll = VisualScale | RagdollReferences | Appearance,
    ProjectileAll = ProjectileSpawnOnHit | SpawnAbilitySpawnPrefabs,
    All = AttackAll | CharacterAll | BaseAiAll | MonsterAiAll | HumanoidAll | VisualAll | ProjectileAll
}

/// <summary>
/// Keeps the values that existed immediately before CreatureManager first wrote each supported field group.
/// A new apply pass restores only groups written by the preceding pass; omitted YAML therefore returns to
/// the pre-CreatureManager value without replacing whole components or prefab objects.
/// </summary>
internal static class CreaturePrefabBaseline
{
    private static readonly CreaturePrefabBaselineGroup[] IndividualGroups =
    {
        CreaturePrefabBaselineGroup.AttackDamage,
        CreaturePrefabBaselineGroup.AttackTuple,
        CreaturePrefabBaselineGroup.AttackStatusEffect,
        CreaturePrefabBaselineGroup.AttackProjectile,
        CreaturePrefabBaselineGroup.AttackAi,
        CreaturePrefabBaselineGroup.ProjectileSpawnOnHit,
        CreaturePrefabBaselineGroup.SpawnAbilitySpawnPrefabs,
        CreaturePrefabBaselineGroup.CharacterIdentity,
        CreaturePrefabBaselineGroup.CharacterBoss,
        CreaturePrefabBaselineGroup.CharacterGlobalKey,
        CreaturePrefabBaselineGroup.CharacterHealth,
        CreaturePrefabBaselineGroup.CharacterDamageModifiers,
        CreaturePrefabBaselineGroup.CharacterSpeed,
        CreaturePrefabBaselineGroup.CharacterJump,
        CreaturePrefabBaselineGroup.CharacterSwim,
        CreaturePrefabBaselineGroup.CharacterFlight,
        CreaturePrefabBaselineGroup.BaseAiSenses,
        CreaturePrefabBaselineGroup.BaseAiIdleSound,
        CreaturePrefabBaselineGroup.BaseAiMovement,
        CreaturePrefabBaselineGroup.BaseAiSerpent,
        CreaturePrefabBaselineGroup.BaseAiRandomMove,
        CreaturePrefabBaselineGroup.BaseAiFlight,
        CreaturePrefabBaselineGroup.BaseAiAvoid,
        CreaturePrefabBaselineGroup.BaseAiFlee,
        CreaturePrefabBaselineGroup.BaseAiAggressive,
        CreaturePrefabBaselineGroup.BaseAiMessages,
        CreaturePrefabBaselineGroup.MonsterAiAlertRange,
        CreaturePrefabBaselineGroup.MonsterAiHunt,
        CreaturePrefabBaselineGroup.MonsterAiChase,
        CreaturePrefabBaselineGroup.MonsterAiCircle,
        CreaturePrefabBaselineGroup.MonsterAiHurtFlee,
        CreaturePrefabBaselineGroup.MonsterAiCharge,
        CreaturePrefabBaselineGroup.MonsterAiSleep,
        CreaturePrefabBaselineGroup.MonsterAiAvoidLand,
        CreaturePrefabBaselineGroup.HumanoidDefaultItems,
        CreaturePrefabBaselineGroup.HumanoidRandomWeapon,
        CreaturePrefabBaselineGroup.HumanoidRandomArmor,
        CreaturePrefabBaselineGroup.HumanoidRandomShield,
        CreaturePrefabBaselineGroup.HumanoidRandomItems,
        CreaturePrefabBaselineGroup.HumanoidRandomSets,
        CreaturePrefabBaselineGroup.VisualScale,
        CreaturePrefabBaselineGroup.RagdollReferences,
        CreaturePrefabBaselineGroup.Appearance
    };

    private static readonly Dictionary<int, Entry> Entries = new();

    internal static void BeginApplyPass()
    {
        foreach (KeyValuePair<int, Entry> pair in Entries.ToArray())
        {
            int instanceId = pair.Key;
            Entry entry = pair.Value;
            if (entry.Prefab == null)
            {
                Entries.Remove(instanceId);
                continue;
            }

            RestoreGroups(entry, entry.AppliedGroups);
            entry.AppliedGroups = CreaturePrefabBaselineGroup.None;
        }
    }

    internal static void Capture(GameObject prefab, CreaturePrefabBaselineGroup groups)
    {
        if (prefab == null || groups == CreaturePrefabBaselineGroup.None)
        {
            return;
        }

        Entry entry = GetOrCreateEntry(prefab);
        foreach (CreaturePrefabBaselineGroup group in IndividualGroups)
        {
            if ((groups & group) == 0)
            {
                continue;
            }

            if ((entry.CapturedGroups & group) != 0 || CaptureGroup(entry, group))
            {
                entry.CapturedGroups |= group;
                entry.AppliedGroups |= group;
            }
        }
    }

    internal static void Restore(GameObject prefab, CreaturePrefabBaselineGroup groups)
    {
        if (prefab == null || !Entries.TryGetValue(prefab.GetInstanceID(), out Entry entry) ||
            !ReferenceEquals(entry.Prefab, prefab))
        {
            return;
        }

        CreaturePrefabBaselineGroup restored = groups & entry.AppliedGroups;
        RestoreGroups(entry, restored);
        entry.AppliedGroups &= ~restored;
    }

    internal static void RestoreAndForget(GameObject prefab)
    {
        if (prefab == null || !Entries.TryGetValue(prefab.GetInstanceID(), out Entry entry) ||
            !ReferenceEquals(entry.Prefab, prefab))
        {
            return;
        }

        RestoreGroups(entry, entry.AppliedGroups);
        Entries.Remove(prefab.GetInstanceID());
    }

    internal static void Forget(GameObject prefab)
    {
        if (!ReferenceEquals(prefab, null))
        {
            Entries.Remove(prefab.GetInstanceID());
        }
    }

    internal static void RestoreAllAndClear()
    {
        foreach (Entry entry in Entries.Values)
        {
            if (entry.Prefab != null)
            {
                RestoreGroups(entry, entry.AppliedGroups);
            }
        }

        Entries.Clear();
    }

    private static Entry GetOrCreateEntry(GameObject prefab)
    {
        int instanceId = prefab.GetInstanceID();
        if (Entries.TryGetValue(instanceId, out Entry entry) && ReferenceEquals(entry.Prefab, prefab))
        {
            return entry;
        }

        entry = new Entry(prefab);
        Entries[instanceId] = entry;
        return entry;
    }

    private static bool CaptureGroup(Entry entry, CreaturePrefabBaselineGroup group)
    {
        GameObject prefab = entry.Prefab;
        if ((group & CreaturePrefabBaselineGroup.AttackAll) != 0)
        {
            return entry.Attack.Capture(prefab, group);
        }

        if ((group & CreaturePrefabBaselineGroup.CharacterAll) != 0)
        {
            return entry.Character.Capture(prefab, group);
        }

        if ((group & CreaturePrefabBaselineGroup.BaseAiAll) != 0)
        {
            return entry.BaseAi.Capture(prefab, group);
        }

        if ((group & CreaturePrefabBaselineGroup.MonsterAiAll) != 0)
        {
            return entry.MonsterAi.Capture(prefab, group);
        }

        if ((group & CreaturePrefabBaselineGroup.HumanoidAll) != 0)
        {
            return entry.Humanoid.Capture(prefab, group);
        }

        if ((group & CreaturePrefabBaselineGroup.ProjectileAll) != 0)
        {
            return entry.Projectile.Capture(prefab, group);
        }

        switch (group)
        {
            case CreaturePrefabBaselineGroup.VisualScale:
                entry.Scale = prefab.transform.localScale;
                return true;
            case CreaturePrefabBaselineGroup.RagdollReferences:
                return entry.Ragdoll.Capture(prefab);
            case CreaturePrefabBaselineGroup.Appearance:
                return entry.Appearance.Capture(prefab);
            default:
                return false;
        }
    }

    private static void RestoreGroups(Entry entry, CreaturePrefabBaselineGroup groups)
    {
        groups &= entry.CapturedGroups;
        if (groups == CreaturePrefabBaselineGroup.None || entry.Prefab == null)
        {
            return;
        }

        foreach (CreaturePrefabBaselineGroup group in IndividualGroups)
        {
            if ((groups & group) == 0)
            {
                continue;
            }

            if ((group & CreaturePrefabBaselineGroup.AttackAll) != 0)
            {
                entry.Attack.Restore(entry.Prefab, group);
            }
            else if ((group & CreaturePrefabBaselineGroup.CharacterAll) != 0)
            {
                entry.Character.Restore(entry.Prefab, group);
            }
            else if ((group & CreaturePrefabBaselineGroup.BaseAiAll) != 0)
            {
                entry.BaseAi.Restore(entry.Prefab, group);
            }
            else if ((group & CreaturePrefabBaselineGroup.MonsterAiAll) != 0)
            {
                entry.MonsterAi.Restore(entry.Prefab, group);
            }
            else if ((group & CreaturePrefabBaselineGroup.HumanoidAll) != 0)
            {
                entry.Humanoid.Restore(entry.Prefab, group);
            }
            else if ((group & CreaturePrefabBaselineGroup.ProjectileAll) != 0)
            {
                entry.Projectile.Restore(entry.Prefab, group);
            }
            else if (group == CreaturePrefabBaselineGroup.VisualScale)
            {
                entry.Prefab.transform.localScale = entry.Scale;
            }
            else if (group == CreaturePrefabBaselineGroup.RagdollReferences)
            {
                entry.Ragdoll.Restore();
            }
            else if (group == CreaturePrefabBaselineGroup.Appearance)
            {
                entry.Appearance.Restore(entry.Prefab);
            }
        }
    }

    private sealed class Entry
    {
        internal readonly GameObject Prefab;
        internal readonly AttackState Attack = new();
        internal readonly CharacterState Character = new();
        internal readonly BaseAiState BaseAi = new();
        internal readonly MonsterAiState MonsterAi = new();
        internal readonly HumanoidState Humanoid = new();
        internal readonly ProjectileState Projectile = new();
        internal readonly RagdollState Ragdoll = new();
        internal readonly AppearanceState Appearance = new();
        internal CreaturePrefabBaselineGroup CapturedGroups;
        internal CreaturePrefabBaselineGroup AppliedGroups;
        internal Vector3 Scale;

        internal Entry(GameObject prefab)
        {
            Prefab = prefab;
        }
    }

    private sealed class AttackState
    {
        private HitData.DamageTypes _damages;
        private float _attackForce;
        private int _toolTier;
        private Attack.AttackType _attackType;
        private string _attackAnimation = "";
        private StatusEffect? _statusEffect;
        private float _statusEffectChance;
        private GameObject? _projectile;
        private float _projectileVelocity;
        private float _projectileAccuracy;
        private int _projectileCount;
        private float _aiInterval;
        private float _aiMinRange;
        private float _aiRange;
        private float _aiMaxAngle;
        private bool _attackReferenceCaptured;
        private bool _attackWasOriginallyNull;

        internal bool Capture(GameObject prefab, CreaturePrefabBaselineGroup group)
        {
            ItemDrop? itemDrop = prefab.GetComponent<ItemDrop>();
            ItemDrop.ItemData.SharedData? shared = itemDrop?.m_itemData?.m_shared;
            if (shared == null)
            {
                return false;
            }

            if (!_attackReferenceCaptured)
            {
                _attackWasOriginallyNull = shared.m_attack == null;
                _attackReferenceCaptured = true;
            }

            switch (group)
            {
                case CreaturePrefabBaselineGroup.AttackDamage:
                    _damages = shared.m_damages;
                    _attackForce = shared.m_attackForce;
                    _toolTier = shared.m_toolTier;
                    return true;
                case CreaturePrefabBaselineGroup.AttackStatusEffect:
                    _statusEffect = shared.m_attackStatusEffect;
                    _statusEffectChance = shared.m_attackStatusEffectChance;
                    return true;
                case CreaturePrefabBaselineGroup.AttackAi:
                    _aiInterval = shared.m_aiAttackInterval;
                    _aiMinRange = shared.m_aiAttackRangeMin;
                    _aiRange = shared.m_aiAttackRange;
                    _aiMaxAngle = shared.m_aiAttackMaxAngle;
                    return true;
            }

            Attack? attack = shared.m_attack;
            if (attack == null)
            {
                return true;
            }

            switch (group)
            {
                case CreaturePrefabBaselineGroup.AttackTuple:
                    _attackType = attack.m_attackType;
                    _attackAnimation = attack.m_attackAnimation;
                    return true;
                case CreaturePrefabBaselineGroup.AttackProjectile:
                    _projectile = attack.m_attackProjectile;
                    _projectileVelocity = attack.m_projectileVel;
                    _projectileAccuracy = attack.m_projectileAccuracy;
                    _projectileCount = attack.m_projectiles;
                    return true;
                default:
                    return false;
            }
        }

        internal void Restore(GameObject prefab, CreaturePrefabBaselineGroup group)
        {
            ItemDrop? itemDrop = prefab.GetComponent<ItemDrop>();
            ItemDrop.ItemData.SharedData? shared = itemDrop?.m_itemData?.m_shared;
            if (shared == null)
            {
                return;
            }

            if (_attackReferenceCaptured && _attackWasOriginallyNull)
            {
                shared.m_attack = null;
            }

            switch (group)
            {
                case CreaturePrefabBaselineGroup.AttackDamage:
                    shared.m_damages = _damages;
                    shared.m_attackForce = _attackForce;
                    shared.m_toolTier = _toolTier;
                    return;
                case CreaturePrefabBaselineGroup.AttackStatusEffect:
                    shared.m_attackStatusEffect = _statusEffect;
                    shared.m_attackStatusEffectChance = _statusEffectChance;
                    return;
                case CreaturePrefabBaselineGroup.AttackAi:
                    shared.m_aiAttackInterval = _aiInterval;
                    shared.m_aiAttackRangeMin = _aiMinRange;
                    shared.m_aiAttackRange = _aiRange;
                    shared.m_aiAttackMaxAngle = _aiMaxAngle;
                    return;
            }

            if (shared.m_attack == null)
            {
                return;
            }

            switch (group)
            {
                case CreaturePrefabBaselineGroup.AttackTuple:
                    shared.m_attack.m_attackType = _attackType;
                    shared.m_attack.m_attackAnimation = _attackAnimation;
                    break;
                case CreaturePrefabBaselineGroup.AttackProjectile:
                    shared.m_attack.m_attackProjectile = _projectile;
                    shared.m_attack.m_projectileVel = _projectileVelocity;
                    shared.m_attack.m_projectileAccuracy = _projectileAccuracy;
                    shared.m_attack.m_projectiles = _projectileCount;
                    break;
            }
        }
    }

    private sealed class ProjectileState
    {
        private GameObject? _spawnOnHit;
        private GameObject[]? _spawnPrefabs;

        internal bool Capture(GameObject prefab, CreaturePrefabBaselineGroup group)
        {
            switch (group)
            {
                case CreaturePrefabBaselineGroup.ProjectileSpawnOnHit:
                {
                    Projectile? projectile = prefab.GetComponent<Projectile>();
                    if (projectile == null)
                    {
                        return false;
                    }

                    _spawnOnHit = projectile.m_spawnOnHit;
                    return true;
                }
                case CreaturePrefabBaselineGroup.SpawnAbilitySpawnPrefabs:
                {
                    SpawnAbility? spawnAbility = prefab.GetComponent<SpawnAbility>();
                    if (spawnAbility == null)
                    {
                        return false;
                    }

                    _spawnPrefabs = spawnAbility.m_spawnPrefab == null
                        ? null
                        : (GameObject[])spawnAbility.m_spawnPrefab.Clone();
                    return true;
                }
                default:
                    return false;
            }
        }

        internal void Restore(GameObject prefab, CreaturePrefabBaselineGroup group)
        {
            switch (group)
            {
                case CreaturePrefabBaselineGroup.ProjectileSpawnOnHit:
                {
                    Projectile? projectile = prefab.GetComponent<Projectile>();
                    if (projectile != null)
                    {
                        projectile.m_spawnOnHit = _spawnOnHit;
                    }

                    break;
                }
                case CreaturePrefabBaselineGroup.SpawnAbilitySpawnPrefabs:
                {
                    SpawnAbility? spawnAbility = prefab.GetComponent<SpawnAbility>();
                    if (spawnAbility != null)
                    {
                        spawnAbility.m_spawnPrefab = _spawnPrefabs == null
                            ? null
                            : (GameObject[])_spawnPrefabs.Clone();
                    }

                    break;
                }
            }
        }
    }

    private sealed class CharacterState
    {
        private string _name = "";
        private Character.Faction _faction;
        private bool _boss;
        private bool _dontHideBossHud;
        private string _bossEvent = "";
        private string _globalKey = "";
        private float _health;
        private float _regenAllHpTime;
        private HitData.DamageModifiers _damageModifiers;
        private readonly float[] _speed = new float[7];
        private readonly float[] _jump = new float[5];
        private bool _canSwim;
        private readonly float[] _swim = new float[4];
        private bool _flying;
        private readonly float[] _flight = new float[3];

        internal bool Capture(GameObject prefab, CreaturePrefabBaselineGroup group)
        {
            Character? character = prefab.GetComponent<Character>();
            if (character == null)
            {
                return false;
            }

            switch (group)
            {
                case CreaturePrefabBaselineGroup.CharacterIdentity:
                    _name = character.m_name;
                    _faction = character.m_faction;
                    break;
                case CreaturePrefabBaselineGroup.CharacterBoss:
                    _boss = character.m_boss;
                    _dontHideBossHud = character.m_dontHideBossHud;
                    _bossEvent = character.m_bossEvent;
                    break;
                case CreaturePrefabBaselineGroup.CharacterGlobalKey:
                    _globalKey = character.m_defeatSetGlobalKey;
                    break;
                case CreaturePrefabBaselineGroup.CharacterHealth:
                    _health = character.m_health;
                    _regenAllHpTime = character.m_regenAllHPTime;
                    break;
                case CreaturePrefabBaselineGroup.CharacterDamageModifiers:
                    _damageModifiers = character.m_damageModifiers;
                    break;
                case CreaturePrefabBaselineGroup.CharacterSpeed:
                    _speed[0] = character.m_crouchSpeed;
                    _speed[1] = character.m_walkSpeed;
                    _speed[2] = character.m_speed;
                    _speed[3] = character.m_turnSpeed;
                    _speed[4] = character.m_runSpeed;
                    _speed[5] = character.m_runTurnSpeed;
                    _speed[6] = character.m_acceleration;
                    break;
                case CreaturePrefabBaselineGroup.CharacterJump:
                    _jump[0] = character.m_jumpForce;
                    _jump[1] = character.m_jumpForceForward;
                    _jump[2] = character.m_jumpForceTiredFactor;
                    _jump[3] = character.m_airControl;
                    _jump[4] = character.m_jumpStaminaUsage;
                    break;
                case CreaturePrefabBaselineGroup.CharacterSwim:
                    _canSwim = character.m_canSwim;
                    _swim[0] = character.m_swimDepth;
                    _swim[1] = character.m_swimSpeed;
                    _swim[2] = character.m_swimTurnSpeed;
                    _swim[3] = character.m_swimAcceleration;
                    break;
                case CreaturePrefabBaselineGroup.CharacterFlight:
                    _flying = character.m_flying;
                    _flight[0] = character.m_flySlowSpeed;
                    _flight[1] = character.m_flyFastSpeed;
                    _flight[2] = character.m_flyTurnSpeed;
                    break;
                default:
                    return false;
            }

            return true;
        }

        internal void Restore(GameObject prefab, CreaturePrefabBaselineGroup group)
        {
            Character? character = prefab.GetComponent<Character>();
            if (character == null)
            {
                return;
            }

            switch (group)
            {
                case CreaturePrefabBaselineGroup.CharacterIdentity:
                    character.m_name = _name;
                    character.m_faction = _faction;
                    break;
                case CreaturePrefabBaselineGroup.CharacterBoss:
                    character.m_boss = _boss;
                    character.m_dontHideBossHud = _dontHideBossHud;
                    character.m_bossEvent = _bossEvent;
                    break;
                case CreaturePrefabBaselineGroup.CharacterGlobalKey:
                    character.m_defeatSetGlobalKey = _globalKey;
                    break;
                case CreaturePrefabBaselineGroup.CharacterHealth:
                    character.m_health = _health;
                    character.m_regenAllHPTime = _regenAllHpTime;
                    break;
                case CreaturePrefabBaselineGroup.CharacterDamageModifiers:
                    character.m_damageModifiers = _damageModifiers;
                    break;
                case CreaturePrefabBaselineGroup.CharacterSpeed:
                    character.m_crouchSpeed = _speed[0];
                    character.m_walkSpeed = _speed[1];
                    character.m_speed = _speed[2];
                    character.m_turnSpeed = _speed[3];
                    character.m_runSpeed = _speed[4];
                    character.m_runTurnSpeed = _speed[5];
                    character.m_acceleration = _speed[6];
                    break;
                case CreaturePrefabBaselineGroup.CharacterJump:
                    character.m_jumpForce = _jump[0];
                    character.m_jumpForceForward = _jump[1];
                    character.m_jumpForceTiredFactor = _jump[2];
                    character.m_airControl = _jump[3];
                    character.m_jumpStaminaUsage = _jump[4];
                    break;
                case CreaturePrefabBaselineGroup.CharacterSwim:
                    character.m_canSwim = _canSwim;
                    character.m_swimDepth = _swim[0];
                    character.m_swimSpeed = _swim[1];
                    character.m_swimTurnSpeed = _swim[2];
                    character.m_swimAcceleration = _swim[3];
                    break;
                case CreaturePrefabBaselineGroup.CharacterFlight:
                    character.m_flying = _flying;
                    character.m_flySlowSpeed = _flight[0];
                    character.m_flyFastSpeed = _flight[1];
                    character.m_flyTurnSpeed = _flight[2];
                    break;
            }
        }
    }

    private sealed class BaseAiState
    {
        private float _viewRange;
        private float _viewAngle;
        private float _hearRange;
        private bool _mistVision;
        private float _idleSoundInterval;
        private float _idleSoundChance;
        private bool _patrol;
        private Pathfinding.AgentType _pathAgentType;
        private float _moveMinAngle;
        private bool _smoothMovement;
        private bool _serpentMovement;
        private float _serpentTurnRadius;
        private readonly float[] _randomMove = new float[4];
        private bool _randomFly;
        private readonly float[] _flight = new float[9];
        private readonly bool[] _avoid = new bool[6];
        private readonly float[] _flee = new float[3];
        private bool _aggravatable;
        private bool _passiveAggressive;
        private readonly string[] _messages = new string[3];

        internal bool Capture(GameObject prefab, CreaturePrefabBaselineGroup group)
        {
            BaseAI? ai = prefab.GetComponent<BaseAI>();
            if (ai == null)
            {
                return false;
            }

            switch (group)
            {
                case CreaturePrefabBaselineGroup.BaseAiSenses:
                    _viewRange = ai.m_viewRange;
                    _viewAngle = ai.m_viewAngle;
                    _hearRange = ai.m_hearRange;
                    _mistVision = ai.m_mistVision;
                    break;
                case CreaturePrefabBaselineGroup.BaseAiIdleSound:
                    _idleSoundInterval = ai.m_idleSoundInterval;
                    _idleSoundChance = ai.m_idleSoundChance;
                    break;
                case CreaturePrefabBaselineGroup.BaseAiMovement:
                    _patrol = ai.m_patrol;
                    _pathAgentType = ai.m_pathAgentType;
                    _moveMinAngle = ai.m_moveMinAngle;
                    _smoothMovement = ai.m_smoothMovement;
                    break;
                case CreaturePrefabBaselineGroup.BaseAiSerpent:
                    _serpentMovement = ai.m_serpentMovement;
                    _serpentTurnRadius = ai.m_serpentTurnRadius;
                    break;
                case CreaturePrefabBaselineGroup.BaseAiRandomMove:
                    _randomMove[0] = ai.m_jumpInterval;
                    _randomMove[1] = ai.m_randomCircleInterval;
                    _randomMove[2] = ai.m_randomMoveInterval;
                    _randomMove[3] = ai.m_randomMoveRange;
                    break;
                case CreaturePrefabBaselineGroup.BaseAiFlight:
                    _randomFly = ai.m_randomFly;
                    _flight[0] = ai.m_chanceToTakeoff;
                    _flight[1] = ai.m_chanceToLand;
                    _flight[2] = ai.m_groundDuration;
                    _flight[3] = ai.m_airDuration;
                    _flight[4] = ai.m_maxLandAltitude;
                    _flight[5] = ai.m_takeoffTime;
                    _flight[6] = ai.m_flyAltitudeMin;
                    _flight[7] = ai.m_flyAltitudeMax;
                    _flight[8] = ai.m_flyAbsMinAltitude;
                    break;
                case CreaturePrefabBaselineGroup.BaseAiAvoid:
                    _avoid[0] = ai.m_avoidFire;
                    _avoid[1] = ai.m_afraidOfFire;
                    _avoid[2] = ai.m_avoidWater;
                    _avoid[3] = ai.m_avoidLava;
                    _avoid[4] = ai.m_skipLavaTargets;
                    _avoid[5] = ai.m_avoidLavaFlee;
                    break;
                case CreaturePrefabBaselineGroup.BaseAiFlee:
                    _flee[0] = ai.m_fleeRange;
                    _flee[1] = ai.m_fleeAngle;
                    _flee[2] = ai.m_fleeInterval;
                    break;
                case CreaturePrefabBaselineGroup.BaseAiAggressive:
                    _aggravatable = ai.m_aggravatable;
                    _passiveAggressive = ai.m_passiveAggresive;
                    break;
                case CreaturePrefabBaselineGroup.BaseAiMessages:
                    _messages[0] = ai.m_spawnMessage;
                    _messages[1] = ai.m_deathMessage;
                    _messages[2] = ai.m_alertedMessage;
                    break;
                default:
                    return false;
            }

            return true;
        }

        internal void Restore(GameObject prefab, CreaturePrefabBaselineGroup group)
        {
            BaseAI? ai = prefab.GetComponent<BaseAI>();
            if (ai == null)
            {
                return;
            }

            switch (group)
            {
                case CreaturePrefabBaselineGroup.BaseAiSenses:
                    ai.m_viewRange = _viewRange;
                    ai.m_viewAngle = _viewAngle;
                    ai.m_hearRange = _hearRange;
                    ai.m_mistVision = _mistVision;
                    break;
                case CreaturePrefabBaselineGroup.BaseAiIdleSound:
                    ai.m_idleSoundInterval = _idleSoundInterval;
                    ai.m_idleSoundChance = _idleSoundChance;
                    break;
                case CreaturePrefabBaselineGroup.BaseAiMovement:
                    ai.m_patrol = _patrol;
                    ai.m_pathAgentType = _pathAgentType;
                    ai.m_moveMinAngle = _moveMinAngle;
                    ai.m_smoothMovement = _smoothMovement;
                    break;
                case CreaturePrefabBaselineGroup.BaseAiSerpent:
                    ai.m_serpentMovement = _serpentMovement;
                    ai.m_serpentTurnRadius = _serpentTurnRadius;
                    break;
                case CreaturePrefabBaselineGroup.BaseAiRandomMove:
                    ai.m_jumpInterval = _randomMove[0];
                    ai.m_randomCircleInterval = _randomMove[1];
                    ai.m_randomMoveInterval = _randomMove[2];
                    ai.m_randomMoveRange = _randomMove[3];
                    break;
                case CreaturePrefabBaselineGroup.BaseAiFlight:
                    ai.m_randomFly = _randomFly;
                    ai.m_chanceToTakeoff = _flight[0];
                    ai.m_chanceToLand = _flight[1];
                    ai.m_groundDuration = _flight[2];
                    ai.m_airDuration = _flight[3];
                    ai.m_maxLandAltitude = _flight[4];
                    ai.m_takeoffTime = _flight[5];
                    ai.m_flyAltitudeMin = _flight[6];
                    ai.m_flyAltitudeMax = _flight[7];
                    ai.m_flyAbsMinAltitude = _flight[8];
                    break;
                case CreaturePrefabBaselineGroup.BaseAiAvoid:
                    ai.m_avoidFire = _avoid[0];
                    ai.m_afraidOfFire = _avoid[1];
                    ai.m_avoidWater = _avoid[2];
                    ai.m_avoidLava = _avoid[3];
                    ai.m_skipLavaTargets = _avoid[4];
                    ai.m_avoidLavaFlee = _avoid[5];
                    break;
                case CreaturePrefabBaselineGroup.BaseAiFlee:
                    ai.m_fleeRange = _flee[0];
                    ai.m_fleeAngle = _flee[1];
                    ai.m_fleeInterval = _flee[2];
                    break;
                case CreaturePrefabBaselineGroup.BaseAiAggressive:
                    ai.m_aggravatable = _aggravatable;
                    ai.m_passiveAggresive = _passiveAggressive;
                    break;
                case CreaturePrefabBaselineGroup.BaseAiMessages:
                    ai.m_spawnMessage = _messages[0];
                    ai.m_deathMessage = _messages[1];
                    ai.m_alertedMessage = _messages[2];
                    break;
            }
        }
    }

    private sealed class MonsterAiState
    {
        private float _alertRange;
        private bool _enableHuntPlayer;
        private bool _attackPlayerObjects;
        private int _privateAreaTriggerThreshold;
        private readonly float[] _chase = new float[4];
        private readonly float[] _circle = new float[3];
        private bool _fleeIfHurt;
        private float _fleeUnreachableSinceAttacking;
        private float _fleeUnreachableSinceHurt;
        private bool _fleeIfNotAlerted;
        private float _fleeIfLowHealth;
        private float _fleeTimeSinceHurt;
        private bool _fleeInLava;
        private bool _circulateWhileCharging;
        private bool _circulateWhileChargingFlying;
        private bool _sleeping;
        private float _wakeupRange;
        private bool _noiseWakeup;
        private float _maxNoiseWakeupRange;
        private float _wakeUpDelayMin;
        private float _wakeUpDelayMax;
        private float _fallAsleepDistance;
        private bool _avoidLand;

        internal bool Capture(GameObject prefab, CreaturePrefabBaselineGroup group)
        {
            MonsterAI? ai = prefab.GetComponent<MonsterAI>();
            if (ai == null)
            {
                return false;
            }

            switch (group)
            {
                case CreaturePrefabBaselineGroup.MonsterAiAlertRange:
                    _alertRange = ai.m_alertRange;
                    break;
                case CreaturePrefabBaselineGroup.MonsterAiHunt:
                    _enableHuntPlayer = ai.m_enableHuntPlayer;
                    _attackPlayerObjects = ai.m_attackPlayerObjects;
                    _privateAreaTriggerThreshold = ai.m_privateAreaTriggerTreshold;
                    break;
                case CreaturePrefabBaselineGroup.MonsterAiChase:
                    _chase[0] = ai.m_interceptTimeMin;
                    _chase[1] = ai.m_interceptTimeMax;
                    _chase[2] = ai.m_maxChaseDistance;
                    _chase[3] = ai.m_minAttackInterval;
                    break;
                case CreaturePrefabBaselineGroup.MonsterAiCircle:
                    _circle[0] = ai.m_circleTargetInterval;
                    _circle[1] = ai.m_circleTargetDuration;
                    _circle[2] = ai.m_circleTargetDistance;
                    break;
                case CreaturePrefabBaselineGroup.MonsterAiHurtFlee:
                    _fleeIfHurt = ai.m_fleeIfHurtWhenTargetCantBeReached;
                    _fleeUnreachableSinceAttacking = ai.m_fleeUnreachableSinceAttacking;
                    _fleeUnreachableSinceHurt = ai.m_fleeUnreachableSinceHurt;
                    _fleeIfNotAlerted = ai.m_fleeIfNotAlerted;
                    _fleeIfLowHealth = ai.m_fleeIfLowHealth;
                    _fleeTimeSinceHurt = ai.m_fleeTimeSinceHurt;
                    _fleeInLava = ai.m_fleeInLava;
                    break;
                case CreaturePrefabBaselineGroup.MonsterAiCharge:
                    _circulateWhileCharging = ai.m_circulateWhileCharging;
                    _circulateWhileChargingFlying = ai.m_circulateWhileChargingFlying;
                    break;
                case CreaturePrefabBaselineGroup.MonsterAiSleep:
                    _sleeping = ai.m_sleeping;
                    _wakeupRange = ai.m_wakeupRange;
                    _noiseWakeup = ai.m_noiseWakeup;
                    _maxNoiseWakeupRange = ai.m_maxNoiseWakeupRange;
                    _wakeUpDelayMin = ai.m_wakeUpDelayMin;
                    _wakeUpDelayMax = ai.m_wakeUpDelayMax;
                    _fallAsleepDistance = ai.m_fallAsleepDistance;
                    break;
                case CreaturePrefabBaselineGroup.MonsterAiAvoidLand:
                    _avoidLand = ai.m_avoidLand;
                    break;
                default:
                    return false;
            }

            return true;
        }

        internal void Restore(GameObject prefab, CreaturePrefabBaselineGroup group)
        {
            MonsterAI? ai = prefab.GetComponent<MonsterAI>();
            if (ai == null)
            {
                return;
            }

            switch (group)
            {
                case CreaturePrefabBaselineGroup.MonsterAiAlertRange:
                    ai.m_alertRange = _alertRange;
                    break;
                case CreaturePrefabBaselineGroup.MonsterAiHunt:
                    ai.m_enableHuntPlayer = _enableHuntPlayer;
                    ai.m_attackPlayerObjects = _attackPlayerObjects;
                    ai.m_privateAreaTriggerTreshold = _privateAreaTriggerThreshold;
                    break;
                case CreaturePrefabBaselineGroup.MonsterAiChase:
                    ai.m_interceptTimeMin = _chase[0];
                    ai.m_interceptTimeMax = _chase[1];
                    ai.m_maxChaseDistance = _chase[2];
                    ai.m_minAttackInterval = _chase[3];
                    break;
                case CreaturePrefabBaselineGroup.MonsterAiCircle:
                    ai.m_circleTargetInterval = _circle[0];
                    ai.m_circleTargetDuration = _circle[1];
                    ai.m_circleTargetDistance = _circle[2];
                    break;
                case CreaturePrefabBaselineGroup.MonsterAiHurtFlee:
                    ai.m_fleeIfHurtWhenTargetCantBeReached = _fleeIfHurt;
                    ai.m_fleeUnreachableSinceAttacking = _fleeUnreachableSinceAttacking;
                    ai.m_fleeUnreachableSinceHurt = _fleeUnreachableSinceHurt;
                    ai.m_fleeIfNotAlerted = _fleeIfNotAlerted;
                    ai.m_fleeIfLowHealth = _fleeIfLowHealth;
                    ai.m_fleeTimeSinceHurt = _fleeTimeSinceHurt;
                    ai.m_fleeInLava = _fleeInLava;
                    break;
                case CreaturePrefabBaselineGroup.MonsterAiCharge:
                    ai.m_circulateWhileCharging = _circulateWhileCharging;
                    ai.m_circulateWhileChargingFlying = _circulateWhileChargingFlying;
                    break;
                case CreaturePrefabBaselineGroup.MonsterAiSleep:
                    ai.m_sleeping = _sleeping;
                    ai.m_wakeupRange = _wakeupRange;
                    ai.m_noiseWakeup = _noiseWakeup;
                    ai.m_maxNoiseWakeupRange = _maxNoiseWakeupRange;
                    ai.m_wakeUpDelayMin = _wakeUpDelayMin;
                    ai.m_wakeUpDelayMax = _wakeUpDelayMax;
                    ai.m_fallAsleepDistance = _fallAsleepDistance;
                    break;
                case CreaturePrefabBaselineGroup.MonsterAiAvoidLand:
                    ai.m_avoidLand = _avoidLand;
                    break;
            }
        }
    }

    private sealed class HumanoidState
    {
        private GameObject[]? _defaultItems;
        private GameObject[]? _randomWeapon;
        private GameObject[]? _randomArmor;
        private GameObject[]? _randomShield;
        private Humanoid.RandomItem[]? _randomItems;
        private Humanoid.ItemSet[]? _randomSets;

        internal bool Capture(GameObject prefab, CreaturePrefabBaselineGroup group)
        {
            Humanoid? humanoid = prefab.GetComponent<Humanoid>();
            if (humanoid == null)
            {
                return false;
            }

            switch (group)
            {
                case CreaturePrefabBaselineGroup.HumanoidDefaultItems:
                    _defaultItems = CloneArray(humanoid.m_defaultItems);
                    break;
                case CreaturePrefabBaselineGroup.HumanoidRandomWeapon:
                    _randomWeapon = CloneArray(humanoid.m_randomWeapon);
                    break;
                case CreaturePrefabBaselineGroup.HumanoidRandomArmor:
                    _randomArmor = CloneArray(humanoid.m_randomArmor);
                    break;
                case CreaturePrefabBaselineGroup.HumanoidRandomShield:
                    _randomShield = CloneArray(humanoid.m_randomShield);
                    break;
                case CreaturePrefabBaselineGroup.HumanoidRandomItems:
                    _randomItems = CloneRandomItems(humanoid.m_randomItems);
                    break;
                case CreaturePrefabBaselineGroup.HumanoidRandomSets:
                    _randomSets = CloneItemSets(humanoid.m_randomSets);
                    break;
                default:
                    return false;
            }

            return true;
        }

        internal void Restore(GameObject prefab, CreaturePrefabBaselineGroup group)
        {
            Humanoid? humanoid = prefab.GetComponent<Humanoid>();
            if (humanoid == null)
            {
                return;
            }

            switch (group)
            {
                case CreaturePrefabBaselineGroup.HumanoidDefaultItems:
                    humanoid.m_defaultItems = CloneArray(_defaultItems);
                    break;
                case CreaturePrefabBaselineGroup.HumanoidRandomWeapon:
                    humanoid.m_randomWeapon = CloneArray(_randomWeapon);
                    break;
                case CreaturePrefabBaselineGroup.HumanoidRandomArmor:
                    humanoid.m_randomArmor = CloneArray(_randomArmor);
                    break;
                case CreaturePrefabBaselineGroup.HumanoidRandomShield:
                    humanoid.m_randomShield = CloneArray(_randomShield);
                    break;
                case CreaturePrefabBaselineGroup.HumanoidRandomItems:
                    humanoid.m_randomItems = CloneRandomItems(_randomItems);
                    break;
                case CreaturePrefabBaselineGroup.HumanoidRandomSets:
                    humanoid.m_randomSets = CloneItemSets(_randomSets);
                    break;
            }
        }

        private static GameObject[]? CloneArray(GameObject[]? values)
        {
            return values == null ? null : (GameObject[])values.Clone();
        }

        private static Humanoid.RandomItem[]? CloneRandomItems(Humanoid.RandomItem[]? values)
        {
            return values?.Select(value => value == null
                    ? null!
                    : new Humanoid.RandomItem { m_prefab = value.m_prefab, m_chance = value.m_chance })
                .ToArray();
        }

        private static Humanoid.ItemSet[]? CloneItemSets(Humanoid.ItemSet[]? values)
        {
            return values?.Select(value => value == null
                    ? null!
                    : new Humanoid.ItemSet
                    {
                        m_name = value.m_name,
                        m_items = CloneArray(value.m_items)
                    })
                .ToArray();
        }
    }

    private sealed class RagdollState
    {
        private readonly List<EffectList.EffectData> _effects = new();
        private readonly List<GameObject> _prefabs = new();

        internal bool Capture(GameObject prefab)
        {
            EffectList.EffectData[]? effects = prefab.GetComponent<Character>()?.m_deathEffects?.m_effectPrefabs;
            if (effects == null)
            {
                return false;
            }

            _effects.Clear();
            _prefabs.Clear();
            foreach (EffectList.EffectData effect in effects)
            {
                if (effect == null || effect.m_prefab == null || effect.m_prefab.GetComponent<Ragdoll>() == null)
                {
                    continue;
                }

                _effects.Add(effect);
                _prefabs.Add(effect.m_prefab);
            }

            return _effects.Count > 0;
        }

        internal void Restore()
        {
            for (int index = 0; index < _effects.Count && index < _prefabs.Count; ++index)
            {
                EffectList.EffectData effect = _effects[index];
                if (effect != null)
                {
                    effect.m_prefab = _prefabs[index];
                }
            }
        }
    }

    private sealed class AppearanceState
    {
        private bool _hasVisEquipment;
        private int _modelIndex;
        private Vector3 _skinColor;
        private Vector3 _hairColor;
        private string _visHair = "";
        private string _visBeard = "";
        private bool _hasHumanoid;
        private string _humanoidHair = "";
        private string _humanoidBeard = "";

        internal bool Capture(GameObject prefab)
        {
            VisEquipment? visEquipment = prefab.GetComponent<VisEquipment>();
            Humanoid? humanoid = prefab.GetComponent<Humanoid>();
            _hasVisEquipment = visEquipment != null;
            _hasHumanoid = humanoid != null;
            if (!_hasVisEquipment && !_hasHumanoid)
            {
                return false;
            }

            if (visEquipment != null)
            {
                _modelIndex = visEquipment.m_modelIndex;
                _skinColor = visEquipment.m_skinColor;
                _hairColor = visEquipment.m_hairColor;
                _visHair = visEquipment.m_hairItem;
                _visBeard = visEquipment.m_beardItem;
            }

            if (humanoid != null)
            {
                _humanoidHair = humanoid.m_hairItem;
                _humanoidBeard = humanoid.m_beardItem;
            }

            return true;
        }

        internal void Restore(GameObject prefab)
        {
            if (_hasVisEquipment)
            {
                VisEquipment? visEquipment = prefab.GetComponent<VisEquipment>();
                if (visEquipment != null)
                {
                    visEquipment.m_modelIndex = _modelIndex;
                    visEquipment.m_skinColor = _skinColor;
                    visEquipment.m_hairColor = _hairColor;
                    visEquipment.m_hairItem = _visHair;
                    visEquipment.m_beardItem = _visBeard;
                }
            }

            if (_hasHumanoid)
            {
                Humanoid? humanoid = prefab.GetComponent<Humanoid>();
                if (humanoid != null)
                {
                    humanoid.m_hairItem = _humanoidHair;
                    humanoid.m_beardItem = _humanoidBeard;
                }
            }
        }
    }
}
