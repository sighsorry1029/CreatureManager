using System;
using System.Collections.Generic;
using YamlDotNet.Core;
using YamlDotNet.Serialization;

namespace CreatureManager;

internal sealed class CreatureDefinition
{
    public string? Prefab { get; set; }
    public bool? Enabled { get; set; }
    public string? ClonedFrom { get; set; }
    public string? Ai { get; set; }
    public CharacterDefinition? Character { get; set; }
    public HumanoidDefinition? Humanoid { get; set; }
    public float? Scale { get; set; }
    public List<string>? Textures { get; set; }
    public AppearanceDefinition? Appearance { get; set; }
    public List<string>? AvailableAttackAnimations { get; set; }

    internal bool IsEnabled => Enabled != false;
}

internal sealed class AiDefinition
{
    public string? Ai { get; set; }
    public bool? Enabled { get; set; }
    public string? CopyFrom { get; set; }
    public string? ClonedFrom { get; set; }
    public BaseAiDefinition? BaseAI { get; set; }
    public MonsterAiDefinition? MonsterAI { get; set; }

    internal bool IsEnabled => Enabled != false;
}

internal sealed class BaseAiDefinition
{
    public List<string>? Senses { get; set; }
    public List<float>? IdleSound { get; set; }
    public List<string>? Movement { get; set; }
    public List<string>? Serpent { get; set; }
    public List<float>? RandomMove { get; set; }
    public List<string>? Flight { get; set; }
    public List<string>? Avoid { get; set; }
    public List<float>? Flee { get; set; }
    public List<string>? Aggressive { get; set; }
    public List<string>? Messages { get; set; }
}

internal sealed class MonsterAiDefinition
{
    public float? AlertRange { get; set; }
    public List<string>? Hunt { get; set; }
    public List<float>? Chase { get; set; }
    public List<float>? Circle { get; set; }
    public List<string>? HurtFlee { get; set; }
    public List<string>? Charge { get; set; }
    public List<string>? Sleep { get; set; }
    public bool? AvoidLand { get; set; }
}

internal sealed class AttackDefinition
{
    public string? Prefab { get; set; }
    public bool? Enabled { get; set; }
    public string? ClonedFrom { get; set; }
    public AttackDamageDefinition? Damage { get; set; }
    public List<string>? Attack { get; set; }
    public List<string>? StatusEffect { get; set; }
    public List<string>? Projectile { get; set; }
    public List<float>? Ai { get; set; }

    internal bool IsEnabled => Enabled != false;
}

internal sealed class ProjectileDefinition
{
    public string? Prefab { get; set; }
    public bool? Enabled { get; set; }
    public string? ClonedFrom { get; set; }
    public List<string>? UsedByAttacks { get; set; }
    public ProjectileComponentDefinition? Projectile { get; set; }
    public SpawnAbilityDefinition? SpawnAbility { get; set; }

    internal bool IsEnabled => Enabled != false;
}

internal sealed class ProjectileComponentDefinition
{
    private string? _spawnOnHit;

    [YamlIgnore]
    public string? SpawnOnHit
    {
        get => _spawnOnHit;
        set
        {
            _spawnOnHit = value;
            SpawnOnHitSpecified = true;
        }
    }

    [YamlIgnore]
    public bool SpawnOnHitSpecified { get; private set; }

    [YamlMember(Alias = "spawnOnHit")]
    public ExplicitNullableYamlString? SerializedSpawnOnHit
    {
        get => SpawnOnHitSpecified ? new ExplicitNullableYamlString(_spawnOnHit) : null;
        set
        {
            _spawnOnHit = value?.Value;
            SpawnOnHitSpecified = true;
        }
    }

    internal bool HasSpecifiedFields => SpawnOnHitSpecified;
}

internal sealed class SpawnAbilityDefinition
{
    private List<string>? _spawnPrefabs;

    public List<string>? SpawnPrefabs
    {
        get => _spawnPrefabs;
        set
        {
            _spawnPrefabs = value;
            SpawnPrefabsSpecified = true;
        }
    }

    [YamlIgnore]
    public bool SpawnPrefabsSpecified { get; private set; }
}

internal sealed class ExplicitNullableYamlString : IYamlConvertible
{
    internal string? Value { get; private set; }

    public ExplicitNullableYamlString()
    {
    }

    internal ExplicitNullableYamlString(string? value)
    {
        Value = value;
    }

    public void Read(IParser parser, Type expectedType, ObjectDeserializer nestedObjectDeserializer)
    {
        Value = (string?)nestedObjectDeserializer(typeof(string));
    }

    public void Write(IEmitter emitter, ObjectSerializer nestedObjectSerializer)
    {
        nestedObjectSerializer(Value, typeof(string));
    }
}

internal sealed class FactionDefinition
{
    public string? Faction { get; set; }
    public int? Id { get; set; }
    public List<string>? Friendly { get; set; }
    public List<string>? AggravatedFriendly { get; set; }
    public List<string>? AlertedFriendly { get; set; }
}

internal sealed class LevelDefinition
{
    private Dictionary<string, ModifierDefinition>? _modifiers;

    public string? Target { get; set; }
    public string? Biome { get; set; }
    public List<string>? Prefabs { get; set; }
    public List<float>? Level { get; set; }
    public float? Damage { get; set; }
    public float? DamagePerLevel { get; set; }
    public float? Health { get; set; }
    public float? HealthPerLevel { get; set; }
    public float? ScalePerLevel { get; set; }
    public List<float>? DistanceScaling { get; set; }
    public List<float>? ModifierDistanceScaling { get; set; }
    public Dictionary<string, ModifierDefinition>? Modifiers
    {
        get => _modifiers;
        set => _modifiers = value == null
            ? null
            : new Dictionary<string, ModifierDefinition>(value, StringComparer.OrdinalIgnoreCase);
    }

    public bool ModifiersCleared { get; set; }
    public bool IsPreset { get; set; }
}

internal sealed class ModifierDefinition
{
    public float? Chance { get; set; }
    public float? Power { get; set; }
    public float? Cooldown { get; set; }
    public int? MaxActivations { get; set; }
    public float? MaxRange { get; set; }
    public string? StartEffect { get; set; }
    public float? ProcChance { get; set; }
    public float? Duration { get; set; }
    public float? SecondaryPower { get; set; }
    public float? Radius { get; set; }
    public string? TriggerEffect { get; set; }
    public float? MaxKarmaGain { get; set; }
    public float? FleeHealthRatio { get; set; }
    public int? ReapingHealMaxActivations { get; set; }
    public float? ReapingMaxHealthPerKill { get; set; }
    public float? ReapingMaxHealthCap { get; set; }
    public float? ReapingDamagePerKill { get; set; }
    public float? ReapingDamageCap { get; set; }
    public float? ReapingScalePerKill { get; set; }
    public float? ReapingScaleCap { get; set; }

    internal ModifierDefinition Clone()
    {
        ModifierDefinition clone = new();
        clone.OverlayFrom(this);
        return clone;
    }

    internal void OverlayFrom(ModifierDefinition source)
    {
        if (source.Chance.HasValue) Chance = source.Chance;
        if (source.Power.HasValue) Power = source.Power;
        if (source.Cooldown.HasValue) Cooldown = source.Cooldown;
        if (source.MaxActivations.HasValue) MaxActivations = source.MaxActivations;
        if (source.MaxRange.HasValue) MaxRange = source.MaxRange;
        if (source.StartEffect != null) StartEffect = source.StartEffect;
        if (source.ProcChance.HasValue) ProcChance = source.ProcChance;
        if (source.Duration.HasValue) Duration = source.Duration;
        if (source.SecondaryPower.HasValue) SecondaryPower = source.SecondaryPower;
        if (source.Radius.HasValue) Radius = source.Radius;
        if (source.TriggerEffect != null) TriggerEffect = source.TriggerEffect;
        if (source.MaxKarmaGain.HasValue) MaxKarmaGain = source.MaxKarmaGain;
        if (source.FleeHealthRatio.HasValue) FleeHealthRatio = source.FleeHealthRatio;
        if (source.ReapingHealMaxActivations.HasValue) ReapingHealMaxActivations = source.ReapingHealMaxActivations;
        if (source.ReapingMaxHealthPerKill.HasValue) ReapingMaxHealthPerKill = source.ReapingMaxHealthPerKill;
        if (source.ReapingMaxHealthCap.HasValue) ReapingMaxHealthCap = source.ReapingMaxHealthCap;
        if (source.ReapingDamagePerKill.HasValue) ReapingDamagePerKill = source.ReapingDamagePerKill;
        if (source.ReapingDamageCap.HasValue) ReapingDamageCap = source.ReapingDamageCap;
        if (source.ReapingScalePerKill.HasValue) ReapingScalePerKill = source.ReapingScalePerKill;
        if (source.ReapingScaleCap.HasValue) ReapingScaleCap = source.ReapingScaleCap;
    }
}

internal sealed class ModifierChanceDefinition
{
    public float? Enraged { get; set; }
    public float? Fire { get; set; }
    public float? Frost { get; set; }
    public float? Lightning { get; set; }
    public float? Spirit { get; set; }
    public float? ArmorPiercing { get; set; }
    public float? Staggering { get; set; }
    public float? Undodgeable { get; set; }

    public float? Armored { get; set; }
    public float? Deathward { get; set; }
    public float? Regenerating { get; set; }
    public float? Reflection { get; set; }
    public float? Vortex { get; set; }
    public float? Adaptive { get; set; }
    public float? Unflinching { get; set; }
    public float? Chameleon { get; set; }

    public float? Exposed { get; set; }
    public float? Weakened { get; set; }
    public float? Withered { get; set; }
    public float? Crippling { get; set; }
    public float? Disruptive { get; set; }
    public float? AdrenalineDrain { get; set; }
    public float? Corrosive { get; set; }
    public float? ToxicDeath { get; set; }

    public float? Swift { get; set; }
    public float? AttackSpeed { get; set; }
    public float? Vampiric { get; set; }
    public float? Reaping { get; set; }
    public float? Blink { get; set; }
    public float? Omen { get; set; }
    public float? Knockback { get; set; }
    public float? Blamer { get; set; }
}

internal sealed class ModifierPowerDefinition
{
    public float? Enraged { get; set; }
    public float? Fire { get; set; }
    public float? Frost { get; set; }
    public float? Lightning { get; set; }
    public float? Spirit { get; set; }
    public float? ArmorPiercing { get; set; }
    public float? Staggering { get; set; }
    public float? Undodgeable { get; set; }

    public float? Armored { get; set; }
    public float? Deathward { get; set; }
    public float? DeathwardCooldown { get; set; }
    public int? DeathwardMaxActivations { get; set; }
    public float? Regenerating { get; set; }
    public float? Reflection { get; set; }
    public float? ReflectionProcChance { get; set; }
    public float? Vortex { get; set; }
    public float? Adaptive { get; set; }
    public float? Unflinching { get; set; }
    public float? Chameleon { get; set; }

    public float? Exposed { get; set; }
    public float? ExposedProcChance { get; set; }
    public float? ExposedDuration { get; set; }
    public float? Weakened { get; set; }
    public float? WeakenedProcChance { get; set; }
    public float? WeakenedDuration { get; set; }
    public float? Withered { get; set; }
    public float? WitheredProcChance { get; set; }
    public float? WitheredDuration { get; set; }
    public float? Crippling { get; set; }
    public float? CripplingJump { get; set; }
    public float? CripplingProcChance { get; set; }
    public float? CripplingDuration { get; set; }
    public float? Disruptive { get; set; }
    public float? DisruptiveEitr { get; set; }
    public float? DisruptiveProcChance { get; set; }
    public float? DisruptiveDuration { get; set; }
    public float? AdrenalineDrain { get; set; }
    public float? AdrenalineDrainGainReduction { get; set; }
    public float? AdrenalineDrainProcChance { get; set; }
    public float? AdrenalineDrainDuration { get; set; }
    public float? Corrosive { get; set; }
    public float? CorrosiveProcChance { get; set; }
    public float? CorrosiveDuration { get; set; }
    public float? ToxicDeath { get; set; }
    public float? ToxicDeathRadius { get; set; }
    public string? ToxicDeathTriggerEffect { get; set; }

    public float? Swift { get; set; }
    public float? AttackSpeed { get; set; }
    public float? Vampiric { get; set; }
    public float? Reaping { get; set; }
    public int? ReapingHealMaxActivations { get; set; }
    public float? ReapingMaxHealthPerKill { get; set; }
    public float? ReapingMaxHealthCap { get; set; }
    public float? ReapingDamagePerKill { get; set; }
    public float? ReapingDamageCap { get; set; }
    public float? ReapingScalePerKill { get; set; }
    public float? ReapingScaleCap { get; set; }
    public float? Blink { get; set; }
    public float? BlinkCooldown { get; set; }
    public float? BlinkMaxRange { get; set; }
    public string? BlinkStartEffect { get; set; }
    public float? Omen { get; set; }
    public float? Knockback { get; set; }
    public float? KnockbackCooldown { get; set; }
    public float? Blamer { get; set; }
    public float? BlamerMaxKarmaGain { get; set; }
    public float? BlamerFleeHealthRatio { get; set; }
}

internal sealed class AttackDamageDefinition
{
    public float? Damage { get; set; }
    public float? Blunt { get; set; }
    public float? Slash { get; set; }
    public float? Pierce { get; set; }
    public float? Chop { get; set; }
    public float? Pickaxe { get; set; }
    public float? Fire { get; set; }
    public float? Frost { get; set; }
    public float? Lightning { get; set; }
    public float? Poison { get; set; }
    public float? Spirit { get; set; }
    public float? AttackForce { get; set; }
    public int? ToolTier { get; set; }
}

internal sealed class CharacterDefinition
{
    public string? Name { get; set; }
    public string? Faction { get; set; }
    public string? Boss { get; set; }
    public string? DefeatSetGlobalKey { get; set; }
    public string? Health { get; set; }
    public DamageModifiersDefinition? DamageModifiers { get; set; }
    public string? Speed { get; set; }
    public string? Jump { get; set; }
    public string? Swim { get; set; }
    public string? Flight { get; set; }
}

internal sealed class DamageModifiersDefinition
{
    public string? Blunt { get; set; }
    public string? Slash { get; set; }
    public string? Pierce { get; set; }
    public string? Chop { get; set; }
    public string? Pickaxe { get; set; }
    public string? Fire { get; set; }
    public string? Frost { get; set; }
    public string? Lightning { get; set; }
    public string? Poison { get; set; }
    public string? Spirit { get; set; }
}

internal sealed class HumanoidDefinition
{
    public List<string>? DefaultItems { get; set; }
    public List<string>? RandomWeapon { get; set; }
    public List<string>? RandomArmor { get; set; }
    public List<string>? RandomHair { get; set; }
    public List<string>? RandomShield { get; set; }
    public List<string>? RandomItems { get; set; }
    public List<string>? RandomSets { get; set; }
}

internal sealed class AppearanceDefinition
{
    private string? _hair;
    private string? _beard;

    public string? Hair
    {
        get => _hair;
        set => _hair = value != null && string.IsNullOrWhiteSpace(value) ? "" : value;
    }

    public string? Beard
    {
        get => _beard;
        set => _beard = value != null && string.IsNullOrWhiteSpace(value) ? "" : value;
    }

    public string? HairColor { get; set; }
    public string? SkinColor { get; set; }
    public int? ModelIndex { get; set; }

    internal bool HasSpecifiedFields =>
        Hair != null ||
        Beard != null ||
        HairColor != null ||
        SkinColor != null ||
        ModelIndex.HasValue;
}
