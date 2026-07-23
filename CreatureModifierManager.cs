using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CreatureManager;

internal static class CreatureModifierManager
{
    private const string AppliedKey = "CreatureManager_ModifiersApplied";
    private const string Mask64Key = "CreatureManager_ModifierMask64";
    private const string ArmoredReductionKey = "CreatureManager_ArmoredReduction";
    private const string EnragedBonusKey = "CreatureManager_EnragedBonus";
    private const string DeathwardHealthKey = "CreatureManager_DeathwardHealth";
    private const string DeathwardCooldownKey = "CreatureManager_DeathwardCooldown";
    private const string DeathwardMaxActivationsKey = "CreatureManager_DeathwardMaxActivations";
    private const string DeathwardActivationCountKey = "CreatureManager_DeathwardActivationCount";
    private const string DeathwardNextReadyTimeKey = "CreatureManager_DeathwardNextReadyTime";
    private const string SwiftPowerKey = "CreatureManager_SwiftPower";
    private const string RegeneratingPowerKey = "CreatureManager_RegeneratingPower";
    private const string VampiricPowerKey = "CreatureManager_VampiricPower";
    private const string FirePowerKey = "CreatureManager_FirePower";
    private const string FrostPowerKey = "CreatureManager_FrostPower";
    private const string LightningPowerKey = "CreatureManager_LightningPower";
    private const string SpiritPowerKey = "CreatureManager_SpiritPower";
    private const string ToxicDeathPowerKey = "CreatureManager_ToxicDeathPower";
    private const string ArmorPiercingPowerKey = "CreatureManager_ArmorPiercingPower";
    private const string StaggeringPowerKey = "CreatureManager_StaggeringPower";
    private const string UndodgeableDamageReductionKey = "CreatureManager_UndodgeableDamageReduction";
    private const string AttackSpeedPowerKey = "CreatureManager_AttackSpeedPower";
    private const string ExposedChanceKey = "CreatureManager_ExposedChance";
    private const string ExposedPowerKey = "CreatureManager_ExposedPower";
    private const string ExposedDurationKey = "CreatureManager_ExposedDuration";
    private const string WeakenedChanceKey = "CreatureManager_WeakenedChance";
    private const string WeakenedPowerKey = "CreatureManager_WeakenedPower";
    private const string WeakenedDurationKey = "CreatureManager_WeakenedDuration";
    private const string WitheredChanceKey = "CreatureManager_WitheredChance";
    private const string WitheredPowerKey = "CreatureManager_WitheredPower";
    private const string WitheredDurationKey = "CreatureManager_WitheredDuration";
    private const string ReflectionPowerKey = "CreatureManager_ReflectionPower";
    private const string ReflectionChanceKey = "CreatureManager_ReflectionChance";
    private const string VortexPowerKey = "CreatureManager_VortexPower";
    private const string CripplingChanceKey = "CreatureManager_CripplingChance";
    private const string CripplingPowerKey = "CreatureManager_CripplingPower";
    private const string CripplingJumpPowerKey = "CreatureManager_CripplingJumpPower";
    private const string CripplingDurationKey = "CreatureManager_CripplingDuration";
    private const string DisruptiveChanceKey = "CreatureManager_DisruptiveChance";
    private const string DisruptivePowerKey = "CreatureManager_DisruptivePower";
    private const string DisruptiveEitrPowerKey = "CreatureManager_DisruptiveEitrPower";
    private const string DisruptiveDurationKey = "CreatureManager_DisruptiveDuration";
    private const string AdrenalineDrainChanceKey = "CreatureManager_AdrenalineDrainChance";
    private const string AdrenalineDrainPowerKey = "CreatureManager_AdrenalineDrainPower";
    private const string AdrenalineDrainGainReductionKey = "CreatureManager_AdrenalineDrainGainReduction";
    private const string AdrenalineDrainDurationKey = "CreatureManager_AdrenalineDrainDuration";
    private const string CorrosiveChanceKey = "CreatureManager_CorrosiveChance";
    private const string CorrosivePowerKey = "CreatureManager_CorrosivePower";
    private const string CorrosiveDurationKey = "CreatureManager_CorrosiveDuration";
    private const string ToxicDeathRadiusKey = "CreatureManager_ToxicDeathRadius";
    private const string ToxicDeathTriggerEffectKey = "CreatureManager_ToxicDeathTriggerEffect";
    private const string AdaptivePowerKey = "CreatureManager_AdaptivePower";
    private const string UnflinchingPowerKey = "CreatureManager_UnflinchingPower";
    private const string ChameleonIntervalKey = "CreatureManager_ChameleonInterval";
    private const string ChameleonTypeKey = "CreatureManager_ChameleonType";
    private const string OmenPowerKey = "CreatureManager_OmenPower";
    private const string ReapingPowerKey = "CreatureManager_ReapingPower";
    private const string ReapingHealMaxActivationsKey = "CreatureManager_ReapingHealMaxActivations";
    private const string ReapingMaxHealthPerKillKey = "CreatureManager_ReapingMaxHealthPerKill";
    private const string ReapingMaxHealthCapKey = "CreatureManager_ReapingMaxHealthCap";
    private const string ReapingDamagePerKillKey = "CreatureManager_ReapingDamagePerKill";
    private const string ReapingDamageCapKey = "CreatureManager_ReapingDamageCap";
    private const string ReapingScalePerKillKey = "CreatureManager_ReapingScalePerKill";
    private const string ReapingScaleCapKey = "CreatureManager_ReapingScaleCap";
    private const string BlinkPowerKey = "CreatureManager_BlinkPower";
    private const string BlinkCooldownKey = "CreatureManager_BlinkCooldown";
    private const string BlinkMaxRangeKey = "CreatureManager_BlinkMaxRange";
    private const string BlinkStartEffectKey = "CreatureManager_BlinkStartEffect";
    private const string BlinkNextTimeKey = "CreatureManager_BlinkNextTime";
    private const string BlinkAlertStartTimeKey = "CreatureManager_BlinkAlertStartTime";
    private const string KnockbackPowerKey = "CreatureManager_KnockbackPower";
    private const string KnockbackCooldownKey = "CreatureManager_KnockbackCooldown";
    private const string KnockbackNextReadyTimeKey = "CreatureManager_KnockbackNextReadyTime";
    private const string BlamerKarmaPerSecondKey = "CreatureManager_BlamerKarmaPerSecond";
    private const string BlamerMaxKarmaGainKey = "CreatureManager_BlamerMaxKarmaGain";
    private const string BlamerFleeHealthRatioKey = "CreatureManager_BlamerFleeHealthRatio";
    private const string BlamerAccumulatedKarmaKey = "CreatureManager_BlamerAccumulatedKarma";
    private const string BlamerActiveKey = "CreatureManager_BlamerActive";
    private const string ReapingBaseMaxHealthKey = "CreatureManager_ReapingBaseMaxHealth";
    private const string ReapingHealActivationCountKey = "CreatureManager_ReapingHealActivationCount";
    private const string ReapingBonusHealthKey = "CreatureManager_ReapingBonusHealth";
    private const string ReapingDamageBonusKey = "CreatureManager_ReapingDamageBonus";
    private const string ReapingScaleBonusKey = "CreatureManager_ReapingScaleBonus";
    private const string ReapingBaseScaleKey = "CreatureManager_ReapingBaseScale";
    private const string AdaptiveTypeKey = "CreatureManager_AdaptiveType";
    private const string AdaptiveUntilKey = "CreatureManager_AdaptiveUntil";
    private const string PlayerExposedPowerKey = "CreatureManager_PlayerExposedPower";
    private const string PlayerExposedUntilKey = "CreatureManager_PlayerExposedUntil";
    private const string PlayerWeakenedPowerKey = "CreatureManager_PlayerWeakenedPower";
    private const string PlayerWeakenedUntilKey = "CreatureManager_PlayerWeakenedUntil";
    private const string PlayerWitheredPowerKey = "CreatureManager_PlayerWitheredPower";
    private const string PlayerWitheredUntilKey = "CreatureManager_PlayerWitheredUntil";
    private const string PlayerCripplingPowerKey = "CreatureManager_PlayerCripplingPower";
    private const string PlayerCripplingJumpPowerKey = "CreatureManager_PlayerCripplingJumpPower";
    private const string PlayerCripplingUntilKey = "CreatureManager_PlayerCripplingUntil";
    private const string PlayerDisruptivePowerKey = "CreatureManager_PlayerDisruptivePower";
    private const string PlayerDisruptiveEitrPowerKey = "CreatureManager_PlayerDisruptiveEitrPower";
    private const string PlayerDisruptiveUntilKey = "CreatureManager_PlayerDisruptiveUntil";
    private const string PlayerCorrosivePowerKey = "CreatureManager_PlayerCorrosivePower";
    private const string PlayerCorrosiveUntilKey = "CreatureManager_PlayerCorrosiveUntil";
    private const string LevelContentName = "CreatureManager_LevelContent";
    private const string StarGroupName = "CreatureManager_StarGroup";
    private const string StarSlotNamePrefix = "CreatureManager_StarSlot";
    private const string StarNumberName = "CreatureManager_StarNumber";
    private const string IconContainerName = "CreatureManager_ModifierIcons";
    private const string OffenseIconSlotName = "CreatureManager_ModifierSlot_Offense";
    private const string DefenseIconSlotName = "CreatureManager_ModifierSlot_Defense";
    private const string AfflictionIconSlotName = "CreatureManager_ModifierSlot_Affliction";
    private const string SpecialIconSlotName = "CreatureManager_ModifierSlot_Special";
    private const string BlamerActiveIconName = "CreatureManager_BlamerActiveIcon";
    private const string BossLevelContentName = "CreatureManager_BossLevelContent";
    private const string ResistanceTextName = "CreatureManager_ResistanceText";
    private const string ExposedStatusName = "CreatureManager_ExposedStatus";
    private const string WeakenedStatusName = "CreatureManager_WeakenedStatus";
    private const string WitheredStatusName = "CreatureManager_WitheredStatus";
    private const string CripplingStatusName = "CreatureManager_CripplingStatus";
    private const string DisruptiveStatusName = "CreatureManager_DisruptiveStatus";
    private const string AdrenalineDrainStatusName = "CreatureManager_AdrenalineDrainStatus";
    private const string CorrosiveStatusName = "CreatureManager_CorrosiveStatus";
    private const string ReflectionDamageRequestRpc = "CreatureManager_RequestReflectionDamage";
    private const string ReflectionDamageRpc = "CreatureManager_ReflectionDamage";
    private const string ReflectionEffectRpc = "CreatureManager_ReflectionEffect";
    private const string VortexHitEffectRpc = "CreatureManager_VortexHitEffect";
    private const string VortexHitEffectRequestRpc = "CreatureManager_VortexHitEffectRequest";
    private const string BlinkEffectRpc = "CreatureManager_BlinkEffect";
    private const string DeathwardEffectRpc = "CreatureManager_DeathwardEffect";
    private const string ReapingFeedbackRpc = "CreatureManager_ReapingFeedback";
    private const string ReapingDirectKillRequestRpc = "CreatureManager_RequestReapingDirectKill";
    private const string ReapingDirectKillRpc = "CreatureManager_ReapingDirectKill";
    private const string ReapingRespawnRequestRpc = "CreatureManager_ReapingRespawn";
    private const string KnockbackCooldownRequestRpc = "CreatureManager_RequestKnockbackCooldown";
    private const string KnockbackCooldownRpc = "CreatureManager_CommitKnockbackCooldown";
    private const string BlamerKarmaRequestRpc = "CreatureManager_BlamerKarmaRequest";
    private const string BlamerKarmaResponseRpc = "CreatureManager_BlamerKarmaResponse";
    private const string DeathwardTriggerEffectPrefab = "fx_StaffShield_Break";
    private const string ReapingTriggerEffectPrefab = "fx_tentaroot_death";
    private const float ArmoredDefaultPower = 0.35f;
    private const float EnragedDefaultPower = 0.15f;
    private const float DeathwardDefaultPower = 0.2f;
    private const float DeathwardDefaultCooldown = 10f;
    private const int DeathwardDefaultMaxActivations = 3;
    private const float SwiftDefaultPower = 0.25f;
    private const float RegeneratingDefaultPower = 0.003f;
    private const float VampiricDefaultPower = 0.3f;
    private const float ElementalDefaultPower = 0.25f;
    private const float FireDefaultPower = 0.2f;
    private const float FrostDefaultPower = 0.1f;
    private const float LightningDefaultPower = 0.1f;
    private const float ToxicDeathDefaultPower = 0.25f;
    private const float ToxicDeathDefaultRadius = 8f;
    private const string ToxicDeathDefaultTriggerEffect = "blob_aoe";
    private const float ArmorPiercingDefaultPower = 0.3f;
    private const float StaggeringDefaultPower = 0.5f;
    private const float UndodgeableDefaultDamageReduction = 0.25f;
    private const float AttackSpeedDefaultPower = 0.35f;
    private const float ExposedDefaultPower = 0.2f;
    private const float WeakenedDefaultPower = 0.2f;
    private const float WitheredDefaultPower = 0.5f;
    private const float ReflectionDefaultPower = 0.2f;
    private const float ReflectionDefaultProcChance = 0.5f;
    private const float VortexDefaultPower = 0.5f;
    private const float CripplingDefaultPower = 0.5f;
    private const float DisruptiveDefaultPower = 0.5f;
    private const float AdrenalineDrainDefaultPower = 0.5f;
    private const float AdrenalineDrainDefaultGainReduction = 0.5f;
    private const float AdrenalineDrainDefaultDuration = 5f;
    private const float CorrosiveDefaultPower = 0.5f;
    private const float AdaptiveDefaultPower = 0.35f;
    private const float UnflinchingDefaultPower = 1f;
    private const float ChameleonDefaultInterval = 10f;
    private const float OmenDefaultPower = 0.25f;
    private const float ReapingDefaultPower = 0.1f;
    private const int ReapingDefaultHealMaxActivations = 20;
    private const float ReapingDefaultMaxHealthPerKill = 0.1f;
    private const float ReapingDefaultMaxHealthCap = 2f;
    private const float ReapingDefaultDamagePerKill = 0f;
    private const float ReapingDefaultDamageCap = 0f;
    private const float ReapingDefaultScalePerKill = 0f;
    private const float ReapingDefaultScaleCap = 0f;
    private const float BlinkFixedProcChance = 1f;
    private const float BlinkDefaultCooldown = 6f;
    private const float BlinkDefaultMaxRange = 16f;
    private const string BlinkDefaultStartEffect = "fx_Adrenaline1";
    private const float KnockbackDefaultPower = 150f;
    private const float KnockbackDefaultCooldown = 5f;
    private const float BlamerDefaultKarmaPerSecond = 1f;
    private const float BlamerTickInterval = 1f;
    private const float BlamerDefaultMaxKarmaGain = 60f;
    private const float BlamerDefaultFleeHealthRatio = 0.75f;
    private const float BlamerKarmaRequestTimeout = 5f;
    private const float BlamerServerMinimumRequestInterval = 0.9f;
    private const float BlamerServerTargetValidationRange = 128f;
    private const float BlamerRejectionLogInterval = 10f;
    private const float BlamerGlobalRejectionLogInterval = 2f;
    private const float ReflectionServerMinimumRequestInterval = 0.05f;
    private const float VortexEffectServerMinimumRequestInterval = 0.075f;
    private const float ModifierRequestValidationRange = 128f;
    private const float ReapingDeathSyncTimeout = 2f;
    private const float ReapingDeathPositionTolerance = 8f;
    private const byte VortexPreResolvedFlag = 0x80;
    private const byte VortexProcFlag = 0x40;
    private const byte VortexOriginalHitTypeMask = 0x3F;
    private const float BlinkAiMaxAngle = 90f;
    private const float BlinkDestinationRadius = 2f;
    private const float ReapingRadius = 24f;
    private const int InitialReapingOverlapBufferSize = 128;
    private const float InactivePlayerDebuffProbeInterval = 1f;
    private const float RuntimeModifierReprobeInterval = 1f;
    private const float ModifierHotPathValidationInterval = 1f;
    private const float HudModifierRefreshInterval = 0.25f;
    private const float PlayerDebuffDefaultProcChance = 0.5f;
    private const float PlayerDebuffDuration = 5f;
    private const float ControlDebuffDuration = 3f;
    private const float AdaptiveDuration = 5f;
    private const float EffectPlaybackRange = 100f;
    private const float VortexEffectRequestValidationRange = 64f;
    private const int MaxActiveModifiers = 4;
    private const int ModifierIconSize = 17;
    private const float BlamerActiveIconScale = 2f;
    private const float BlamerActiveIconGap = 2f;
    private const float IconSpacing = 1f;
    private const float LevelContentWidth = 104f;
    private const float LevelContentHeight = 20f;
    private const int StarIconSize = 17;
    private const int StarLayerBaseSize = 16;
    private const float StarNumberBaseFontSize = 15f;
    private const float StarNumberBaseWidth = 20f;
    private const float HudEdgeOpticalBleedRatio = 0.125f;
    private const float StarLayerTolerance = 5f;
    private const float LevelContentBelowHealthGap = 2f;
    private const float ResistanceTextGap = 2f;
    private const float ResistanceLineHeight = 14f;
    private const float ResistanceTextMinWidth = 150f;
    private const float BossContentWidth = 220f;
    private const float BossContentBelowHealthGap = 2f;
    private static readonly string[] SneakMethodNames = { "IsCrouching", "IsCrouch", "IsSneaking", "InSneak", "IsStealth" };
    private static readonly string[] SneakFieldNames = { "m_crouching", "m_crouch", "m_crouchToggled", "m_isCrouching", "m_isCrouch", "m_sneaking" };
    private static Sprite? ArmoredSprite;
    private static Sprite? EnragedSprite;
    private static Sprite? DeathwardSprite;
    private static Sprite? SwiftSprite;
    private static Sprite? RegeneratingSprite;
    private static Sprite? VampiricSprite;
    private static Sprite? FireSprite;
    private static Sprite? FrostSprite;
    private static Sprite? LightningSprite;
    private static Sprite? SpiritSprite;
    private static Sprite? ToxicDeathSprite;
    private static Sprite? ArmorPiercingSprite;
    private static Sprite? StaggeringSprite;
    private static Sprite? UndodgeableSprite;
    private static Sprite? AttackSpeedSprite;
    private static Sprite? ExposedSprite;
    private static Sprite? WeakenedSprite;
    private static Sprite? WitheredSprite;
    private static Sprite? ReflectionSprite;
    private static Sprite? VortexSprite;
    private static Sprite? CripplingSprite;
    private static Sprite? DisruptiveSprite;
    private static Sprite? AdrenalineDrainSprite;
    private static Sprite? CorrosiveSprite;
    private static Sprite? AdaptiveSprite;
    private static Sprite? UnflinchingSprite;
    private static Sprite? ChameleonSprite;
    private static Sprite? OmenSprite;
    private static Sprite? ReapingSprite;
    private static Sprite? BlinkSprite;
    private static Sprite? KnockbackSprite;
    private static Sprite? BlamerSprite;
    private static Sprite? FallbackStarSprite;
    private static readonly ModifierSpec[] ModifierSpecs =
    {
        new(ModifierGroup.Offense, "enraged", "Enraged", ModifierMask.Enraged, EnragedBonusKey, EnragedDefaultPower, GetEnragedSprite, value => value.Enraged, (value, chance) => value.Enraged = chance, value => value.Enraged, (value, power) => value.Enraged = power, power => $"Increases outgoing damage by {FormatPercent(power)}."),
        new(ModifierGroup.Offense, "fire", "Fire", ModifierMask.Fire, FirePowerKey, FireDefaultPower, GetFireSprite, value => value.Fire, (value, chance) => value.Fire = chance, value => value.Fire, (value, power) => value.Fire = power, power => $"Adds fire damage equal to {FormatPercent(power)} of the original hit damage."),
        new(ModifierGroup.Offense, "frost", "Frost", ModifierMask.Frost, FrostPowerKey, FrostDefaultPower, GetFrostSprite, value => value.Frost, (value, chance) => value.Frost = chance, value => value.Frost, (value, power) => value.Frost = power, power => $"Adds frost damage equal to {FormatPercent(power)} of the original hit damage."),
        new(ModifierGroup.Offense, "lightning", "Lightning", ModifierMask.Lightning, LightningPowerKey, LightningDefaultPower, GetLightningSprite, value => value.Lightning, (value, chance) => value.Lightning = chance, value => value.Lightning, (value, power) => value.Lightning = power, power => $"Adds lightning damage equal to {FormatPercent(power)} of the original hit damage."),
        new(ModifierGroup.Offense, "spirit", "Spirit", ModifierMask.Spirit, SpiritPowerKey, ElementalDefaultPower, GetSpiritSprite, value => value.Spirit, (value, chance) => value.Spirit = chance, value => value.Spirit, (value, power) => value.Spirit = power, power => $"Adds spirit damage equal to {FormatPercent(power)} of the original hit damage. Against players, a damaging hit adds that amount to vanilla Spirit damage-over-time; its ticks bypass resistance and armor."),
        new(ModifierGroup.Offense, "armorPiercing", "Armor Piercing", ModifierMask.ArmorPiercing, ArmorPiercingPowerKey, ArmorPiercingDefaultPower, GetArmorPiercingSprite, value => value.ArmorPiercing, (value, chance) => value.ArmorPiercing = chance, value => value.ArmorPiercing, (value, power) => value.ArmorPiercing = power, power => $"Against players, ignores {FormatPercent(power)} of body armor for that hit."),
        new(ModifierGroup.Offense, "staggering", "Staggering", ModifierMask.Staggering, StaggeringPowerKey, StaggeringDefaultPower, GetStaggeringSprite, value => value.Staggering, (value, chance) => value.Staggering = chance, value => value.Staggering, (value, power) => value.Staggering = power, power => $"Increases outgoing normal-hit and block-stagger buildup by {FormatPercent(power)}."),
        new(ModifierGroup.Offense, "undodgeable", "Undodgeable", ModifierMask.Undodgeable, UndodgeableDamageReductionKey, UndodgeableDefaultDamageReduction, GetUndodgeableSprite, value => value.Undodgeable, (value, chance) => value.Undodgeable = chance, value => value.Undodgeable, (value, power) => value.Undodgeable = power, power => $"Attacks against players ignore dodge invulnerability but deal {FormatPercent(power)} less damage. Blocking and parrying remain available.", ClampUndodgeableDamageReduction),

        new(ModifierGroup.Defense, "armored", "Armored", ModifierMask.Armored, ArmoredReductionKey, ArmoredDefaultPower, GetArmoredSprite, value => value.Armored, (value, chance) => value.Armored = chance, value => value.Armored, (value, power) => value.Armored = power, power => $"Reduces incoming damage by {FormatPercent(power)}."),
        new(ModifierGroup.Defense, "deathward", "Deathward", ModifierMask.Deathward, DeathwardHealthKey, DeathwardDefaultPower, GetDeathwardSprite, value => value.Deathward, (value, chance) => value.Deathward = chance, value => value.Deathward, (value, power) => value.Deathward = power, power => DescribeDeathward(power, DeathwardDefaultCooldown, DeathwardDefaultMaxActivations), ClampDeathwardHealth),
        new(ModifierGroup.Defense, "regenerating", "Regenerating", ModifierMask.Regenerating, RegeneratingPowerKey, RegeneratingDefaultPower, GetRegeneratingSprite, value => value.Regenerating, (value, chance) => value.Regenerating = chance, value => value.Regenerating, (value, power) => value.Regenerating = power, power => $"Heals {FormatPercent(power)} of max health every second."),
        new(ModifierGroup.Defense, "reflection", "Reflection", ModifierMask.Reflection, ReflectionPowerKey, ReflectionDefaultPower, GetReflectionSprite, value => value.Reflection, (value, chance) => value.Reflection = chance, value => value.Reflection, (value, power) => value.Reflection = power, power => DescribeReflection(power, ReflectionDefaultProcChance), procChanceKey: ReflectionChanceKey),
        new(ModifierGroup.Defense, "vortex", "Vortex", ModifierMask.Vortex, VortexPowerKey, VortexDefaultPower, GetVortexSprite, value => value.Vortex, (value, chance) => value.Vortex = chance, value => value.Vortex, (value, power) => value.Vortex = power, power => $"On projectile hits, {FormatPercent(power)} proc chance to ignore all damage, push, stagger, and status effects."),
        new(ModifierGroup.Defense, "adaptive", "Adaptive", ModifierMask.Adaptive, AdaptivePowerKey, AdaptiveDefaultPower, GetAdaptiveSprite, value => value.Adaptive, (value, chance) => value.Adaptive = chance, value => value.Adaptive, (value, power) => value.Adaptive = power, power => $"Remembers one hit's dominant damage type for {FormatSeconds(AdaptiveDuration)} without changing it. Matching damage is reduced by {FormatPercent(power)} until the memory expires."),
        new(ModifierGroup.Defense, "unflinching", "Unflinching", ModifierMask.Unflinching, UnflinchingPowerKey, UnflinchingDefaultPower, GetUnflinchingSprite, value => value.Unflinching, (value, chance) => value.Unflinching = chance, value => value.Unflinching, (value, power) => value.Unflinching = power, _ => "Cannot be staggered by normal hits or perfect parries, preventing the vanilla double-damage stagger window."),
        new(ModifierGroup.Defense, "chameleon", "Chameleon", ModifierMask.Chameleon, ChameleonIntervalKey, ChameleonDefaultInterval, GetChameleonSprite, value => value.Chameleon, (value, chance) => value.Chameleon = chance, value => value.Chameleon, (value, interval) => value.Chameleon = interval, interval => DescribeChameleon(interval), ResolveChameleonInterval),

        new(ModifierGroup.Affliction, "exposed", "Exposed", ModifierMask.Exposed, ExposedPowerKey, ExposedDefaultPower, GetExposedSprite, value => value.Exposed, (value, chance) => value.Exposed = chance, value => value.Exposed, (value, power) => value.Exposed = power, power => DescribePlayerAffliction("exposed", "make that player take", power, PlayerDebuffDefaultProcChance, PlayerDebuffDuration, PlayerDebuffDuration, "more damage"), procChanceKey: ExposedChanceKey),
        new(ModifierGroup.Affliction, "weakened", "Weakened", ModifierMask.Weakened, WeakenedPowerKey, WeakenedDefaultPower, GetWeakenedSprite, value => value.Weakened, (value, chance) => value.Weakened = chance, value => value.Weakened, (value, power) => value.Weakened = power, power => DescribePlayerAffliction("weakened", "make that player deal", power, PlayerDebuffDefaultProcChance, PlayerDebuffDuration, PlayerDebuffDuration, "less damage"), procChanceKey: WeakenedChanceKey),
        new(ModifierGroup.Affliction, "withered", "Withered", ModifierMask.Withered, WitheredPowerKey, WitheredDefaultPower, GetWitheredSprite, value => value.Withered, (value, chance) => value.Withered = chance, value => value.Withered, (value, power) => value.Withered = power, power => DescribePlayerAffliction("withered", "reduce that player's healing received by", power, PlayerDebuffDefaultProcChance, PlayerDebuffDuration, PlayerDebuffDuration), procChanceKey: WitheredChanceKey),
        new(ModifierGroup.Affliction, "crippling", "Crippling", ModifierMask.Crippling, CripplingPowerKey, CripplingDefaultPower, GetCripplingSprite, value => value.Crippling, (value, chance) => value.Crippling = chance, value => value.Crippling, (value, power) => value.Crippling = power, power => DescribeCrippling(power, CripplingDefaultPower, PlayerDebuffDefaultProcChance, ControlDebuffDuration), procChanceKey: CripplingChanceKey),
        new(ModifierGroup.Affliction, "disruptive", "Disruptive", ModifierMask.Disruptive, DisruptivePowerKey, DisruptiveDefaultPower, GetDisruptiveSprite, value => value.Disruptive, (value, chance) => value.Disruptive = chance, value => value.Disruptive, (value, power) => value.Disruptive = power, power => DescribeDisruptive(power, DisruptiveDefaultPower, PlayerDebuffDefaultProcChance, ControlDebuffDuration), procChanceKey: DisruptiveChanceKey),
        new(ModifierGroup.Affliction, "adrenalineDrain", "Adrenaline Drain", ModifierMask.AdrenalineDrain, AdrenalineDrainPowerKey, AdrenalineDrainDefaultPower, GetAdrenalineDrainSprite, value => value.AdrenalineDrain, (value, chance) => value.AdrenalineDrain = chance, value => value.AdrenalineDrain, (value, power) => value.AdrenalineDrain = power, power => DescribeAdrenalineDrain(power, AdrenalineDrainDefaultGainReduction, PlayerDebuffDefaultProcChance, AdrenalineDrainDefaultDuration), procChanceKey: AdrenalineDrainChanceKey),
        new(ModifierGroup.Affliction, "corrosive", "Corrosive", ModifierMask.Corrosive, CorrosivePowerKey, CorrosiveDefaultPower, GetCorrosiveSprite, value => value.Corrosive, (value, chance) => value.Corrosive = chance, value => value.Corrosive, (value, power) => value.Corrosive = power, power => DescribeCorrosive(power, PlayerDebuffDefaultProcChance, PlayerDebuffDuration), procChanceKey: CorrosiveChanceKey),
        new(ModifierGroup.Affliction, "toxicDeath", "Toxic Death", ModifierMask.ToxicDeath, ToxicDeathPowerKey, ToxicDeathDefaultPower, GetToxicDeathSprite, value => value.ToxicDeath, (value, chance) => value.ToxicDeath = chance, value => value.ToxicDeath, (value, power) => value.ToxicDeath = power, power => DescribeToxicDeath(power, ToxicDeathDefaultRadius)),

        new(ModifierGroup.Special, "swift", "Swift", ModifierMask.Swift, SwiftPowerKey, SwiftDefaultPower, GetSwiftSprite, value => value.Swift, (value, chance) => value.Swift = chance, value => value.Swift, (value, power) => value.Swift = power, power => $"Increases movement speed, acceleration, and turning speed by {FormatPercent(power)}."),
        new(ModifierGroup.Special, "attackSpeed", "Attack Speed", ModifierMask.AttackSpeed, AttackSpeedPowerKey, AttackSpeedDefaultPower, GetAttackSpeedSprite, value => value.AttackSpeed, (value, chance) => value.AttackSpeed = chance, value => value.AttackSpeed, (value, power) => value.AttackSpeed = power, power => $"Increases attack animation speed by {FormatPercent(power)} and shortens both weapon and creature AI attack intervals to {FormatPercent(1f / Mathf.Max(1f, 1f + power))} of normal."),
        new(ModifierGroup.Special, "vampiric", "Vampiric", ModifierMask.Vampiric, VampiricPowerKey, VampiricDefaultPower, GetVampiricSprite, value => value.Vampiric, (value, chance) => value.Vampiric = chance, value => value.Vampiric, (value, power) => value.Vampiric = power, power => $"Heals the creature for {FormatPercent(power)} of health removed by direct hits. Delayed damage-over-time is excluded."),
        new(ModifierGroup.Special, "reaping", "Reaping", ModifierMask.Reaping, ReapingPowerKey, ReapingDefaultPower, GetReapingSprite, value => value.Reaping, (value, chance) => value.Reaping = chance, value => value.Reaping, (value, power) => value.Reaping = power, power => DescribeReaping(power, ReapingDefaultMaxHealthPerKill, ReapingDefaultDamagePerKill, ReapingDefaultScalePerKill)),
        new(ModifierGroup.Special, "blink", "Blink", ModifierMask.Blink, BlinkPowerKey, BlinkFixedProcChance, GetBlinkSprite, value => value.Blink, (value, chance) => value.Blink = chance, value => value.Blink, (value, power) => value.Blink = power, _ => DescribeBlink(BlinkDefaultCooldown, BlinkDefaultMaxRange)),
        new(ModifierGroup.Special, "omen", "Omen", ModifierMask.Omen, OmenPowerKey, OmenDefaultPower, GetOmenSprite, value => value.Omen, (value, chance) => value.Omen = chance, value => value.Omen, (value, power) => value.Omen = power, power => $"When killed directly by a player or by poison, fire, or spirit damage over time attributed unambiguously to a player, has a {FormatPercent(power)} chance to force an Enforcer summon check. Cooldown blocking follows the server setting."),
        new(ModifierGroup.Special, "juggernaut", "Juggernaut", ModifierMask.Knockback, KnockbackPowerKey, KnockbackDefaultPower, GetKnockbackSprite, value => value.Knockback, (value, chance) => value.Knockback = chance, value => value.Knockback, (value, power) => value.Knockback = power, power => DescribeKnockback(power, KnockbackDefaultCooldown), ClampKnockbackPower),
        new(ModifierGroup.Special, "blamer", "Blamer", ModifierMask.Blamer, BlamerKarmaPerSecondKey, BlamerDefaultKarmaPerSecond, GetBlamerSprite, value => value.Blamer, (value, chance) => value.Blamer = chance, value => value.Blamer, (value, power) => value.Blamer = power, power => DescribeBlamer(power, BlamerDefaultMaxKarmaGain, BlamerDefaultFleeHealthRatio), ResolveBlamerKarmaPerSecond)
    };
    private static readonly ModifierGroup[] ModifierGroupOrder =
    {
        ModifierGroup.Offense,
        ModifierGroup.Defense,
        ModifierGroup.Affliction,
        ModifierGroup.Special
    };
    private static readonly Dictionary<string, ModifierSpec> ModifierSpecsByKey = ModifierSpecs.ToDictionary(spec => spec.Key, StringComparer.OrdinalIgnoreCase);
    private static readonly string[] ModifierKeys = ModifierSpecs.Select(spec => spec.Key).ToArray();
    private static readonly int ExposedStatusHash = ExposedStatusName.GetStableHashCode();
    private static readonly int WeakenedStatusHash = WeakenedStatusName.GetStableHashCode();
    private static readonly int WitheredStatusHash = WitheredStatusName.GetStableHashCode();
    private static readonly int CripplingStatusHash = CripplingStatusName.GetStableHashCode();
    private static readonly int DisruptiveStatusHash = DisruptiveStatusName.GetStableHashCode();
    private static readonly int AdrenalineDrainStatusHash = AdrenalineDrainStatusName.GetStableHashCode();
    private static readonly int CorrosiveStatusHash = CorrosiveStatusName.GetStableHashCode();
    private static readonly Dictionary<int, PlayerDebuffPowers> ActivePlayerDebuffs = new();
    private static readonly Dictionary<int, PlayerRuntimeDebuffs> ActivePlayerRuntimeDebuffs = new();
    private static readonly HashSet<int> PlayerControlDebuffWakeIds = new();
    private static readonly Dictionary<int, float> NextInactivePlayerDebuffProbeTimes = new();
    private static readonly Dictionary<int, ModifierHotPathState> ModifierHotPathStates = new();
    private static readonly Dictionary<int, ModifierHudRefreshState> ModifierHudRefreshStates = new();
    private static readonly Dictionary<int, float> RuntimeModifierReprobeTimes = new();
    private static readonly Dictionary<int, float> PendingReapingHealthBonusRatios = new();
    private static readonly Dictionary<HitData, float> PendingPlayerSpiritDamage = new();
    private static readonly Dictionary<int, DelayedDamageSourceLedger> DelayedDamageSourceLedgers = new();
    private static readonly Dictionary<int, DelayedDamageDeathCredit> PendingDelayedDamageDeathCredits = new();
    private static readonly Dictionary<HitData, VampiricDamageContext> PendingVampiricDamage = new();
    private static readonly Dictionary<HitData, ReflectionDamageContext> PendingReflectionDamage = new();
    private static readonly HashSet<HitData> FinalDeathwardConsumedHits = new();
    private static readonly Dictionary<HitData, bool> PendingVortexProjectileDecisions = new();
    private static readonly Dictionary<HitData, KnockbackHitContext> PendingKnockbackHits = new();
    private static readonly Dictionary<int, HitData> PendingKnockbackReservations = new();
    private static readonly Dictionary<int, float> LocalKnockbackReadyTimes = new();
    private static readonly Dictionary<int, PendingBlamerKarmaRequest> PendingBlamerKarmaRequests = new();
    private static readonly Dictionary<ZDOID, ServerBlamerKarmaState> ServerBlamerKarmaStates = new();
    private static readonly Dictionary<ZDOID, float> NextBlamerRejectionLogTimes = new();
    private static readonly Dictionary<ZDOID, ServerReflectionRequestState> ServerReflectionRequestStates = new();
    private static readonly Dictionary<ZDOID, float> ServerVortexEffectNextAllowedTimes = new();
    private static readonly Dictionary<ZDOID, float> ServerKnockbackNextAllowedTimes = new();
    private static readonly HashSet<ReapingRequestKey> ServerPendingReapingRequests = new();
    private static readonly HashSet<ZDOID> ServerPendingReapingRespawns = new();
    private static readonly Dictionary<ZDOID, HashSet<ZDOID>> ServerAuthorizedReapingDeaths = new();
    private static readonly Stack<BlinkAttackAiOverrideState> BlinkAttackAiOverridePool = new();
    private static readonly FieldInfo? HitRangedField = typeof(HitData).GetField("m_ranged", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
    private static readonly EffectList SuppressedProjectileHitEffects = new();
    private static readonly string[] VortexHitEffectPrefabNames = { "fx_StaffShield_Hit" };
    private static readonly string[] ReflectionEffectPrefabNames = { "fx_ShieldCharge_5", "sfx_metal_shield_blocked_overlay" };
    private static readonly HashSet<string> MissingDeathwardEffectPrefabs = new(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> MissingReapingEffectPrefabs = new(StringComparer.OrdinalIgnoreCase);
    private static ZRoutedRpc? RegisteredRoutedRpc;
    private static FieldInfo? CachedMonsterAiTargetField;
    private static bool MonsterAiTargetLookupDone;
    private static MethodInfo? CachedSneakMethod;
    private static FieldInfo? CachedSneakField;
    private static bool SneakMemberLookupDone;
    private static int HoverRaycastMask = -1;
    private static TMP_FontAsset? CachedHudFont;
    private static readonly HashSet<int> PassiveModifierProbeDoneCharacters = new();
    private static readonly HashSet<int> ActivePassiveModifierCharacters = new();
    private static readonly Dictionary<int, PassiveModifierSchedule> PassiveModifierSchedules = new();
    private static readonly Dictionary<int, Character> ActiveReapingModifierCharacters = new();
    private static readonly Dictionary<int, Character> UnqueryableReapingModifierCharacters = new();
    private static readonly HashSet<int> ReapingNearbyCandidateIds = new(InitialReapingOverlapBufferSize);
    private static readonly List<int> ReapingStaleCandidateIds = new();
    private static Collider[] ReapingOverlapBuffer = new Collider[InitialReapingOverlapBufferSize];
    private static Coroutine? PendingReapingPhysicsSync;
    private static ZNetScene? ReapingPhysicsSyncHost;
    private static readonly ChameleonDamageType[] ChameleonDamageTypes =
    {
        ChameleonDamageType.Blunt,
        ChameleonDamageType.Pierce,
        ChameleonDamageType.Slash,
        ChameleonDamageType.Fire,
        ChameleonDamageType.Poison,
        ChameleonDamageType.Lightning,
        ChameleonDamageType.Frost,
        ChameleonDamageType.Spirit
    };
    private static ConditionalWeakTable<RectTransform, HudIconState> HudIconStates = new();
    private static ConditionalWeakTable<RectTransform, BlamerAlertIconState> BlamerAlertIconStates = new();
    private static ConditionalWeakTable<RectTransform, HudContentState> HudContentStates = new();
    private static int CachedHoverFrame = -1;
    private static float CachedHoverRange;
    private static bool CachedHoverValid;
    private static Character? CachedHoveredCharacter;
    private static int CachedSneakFrame = -1;
    private static bool CachedSneakState;
    private static bool UndodgeableRuntimeFailureReported;
    private static bool DeathwardRuntimeFailureReported;
    private static int VortexHitTypeEncodingState;
    private static long NextBlamerKarmaRequestId;
    private static float NextBlamerGlobalRejectionLogTime;
    private static long NextReflectionRequestId = DateTime.UtcNow.Ticks;
    [ThreadStatic]
    private static float BlockStaggerMultiplier;
    [ThreadStatic]
    private static UndodgeableSourceContext CurrentUndodgeableSourceContext;
    [ThreadStatic]
    private static VortexProjectileImpactContext? CurrentVortexProjectileImpact;
    [ThreadStatic]
    private static RpcDamageContext CurrentRpcDamageContext;
    [ThreadStatic]
    private static DelayedDamageTickContext CurrentDelayedDamageTickContext;

    private static void ApplyHudFont(TMP_Text target, TMP_Text? preferred = null)
    {
        if (preferred != null && preferred.font != null)
        {
            target.font = preferred.font;
            if (preferred.fontSharedMaterial != null)
            {
                target.fontSharedMaterial = preferred.fontSharedMaterial;
            }

            return;
        }

        TMP_FontAsset? font = ResolveHudFont();
        if (font != null)
        {
            target.font = font;
        }
    }

    private static TMP_FontAsset? ResolveHudFont()
    {
        if (CachedHudFont != null)
        {
            return CachedHudFont;
        }

        if (Hud.instance != null && Hud.instance.m_hoverName != null && Hud.instance.m_hoverName.font != null)
        {
            return CachedHudFont = Hud.instance.m_hoverName.font;
        }

        foreach (TMP_FontAsset font in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
        {
            if (font != null && font.name == "Valheim-AveriaSansLibre")
            {
                return CachedHudFont = font;
            }
        }

        if (TMP_Settings.defaultFontAsset != null)
        {
            return CachedHudFont = TMP_Settings.defaultFontAsset;
        }

        foreach (TMP_Text text in Resources.FindObjectsOfTypeAll<TMP_Text>())
        {
            if (text != null && text.font != null)
            {
                return CachedHudFont = text.font;
            }
        }

        foreach (TMP_FontAsset font in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
        {
            if (font != null)
            {
                return CachedHudFont = font;
            }
        }

        return null;
    }

    [Flags]
    private enum ModifierMask : long
    {
        None = 0,
        Armored = 1L << 0,
        Enraged = 1L << 1,
        Deathward = 1L << 2,
        Swift = 1L << 3,
        Regenerating = 1L << 4,
        Vampiric = 1L << 5,
        Fire = 1L << 6,
        Frost = 1L << 7,
        Lightning = 1L << 8,
        ToxicDeath = 1L << 9,
        ArmorPiercing = 1L << 10,
        Staggering = 1L << 11,
        AttackSpeed = 1L << 12,
        Exposed = 1L << 13,
        Weakened = 1L << 14,
        Withered = 1L << 15,
        Reflection = 1L << 16,
        Vortex = 1L << 17,
        Crippling = 1L << 18,
        Disruptive = 1L << 19,
        Adaptive = 1L << 20,
        Omen = 1L << 21,
        Reaping = 1L << 22,
        Spirit = 1L << 23,
        Blink = 1L << 24,
        Knockback = 1L << 25,
        Unflinching = 1L << 26,
        AdrenalineDrain = 1L << 27,
        Corrosive = 1L << 28,
        Undodgeable = 1L << 29,
        Chameleon = 1L << 30,
        Blamer = 1L << 31
    }

    private enum ModifierGroup
    {
        Offense,
        Defense,
        Affliction,
        Special
    }

    internal enum ChameleonDamageType
    {
        None,
        Blunt,
        Pierce,
        Slash,
        Fire,
        Poison,
        Lightning,
        Frost,
        Spirit
    }

    private sealed class PlayerDebuffPowers
    {
        internal float Exposed;
        internal float Weakened;
        internal float Withered;
        internal float Crippling;
        internal float CripplingJump;
        internal float Disruptive;
        internal float DisruptiveEitr;
        internal float Corrosive;
    }

    internal sealed class VortexProjectileImpactContext
    {
        internal VortexProjectileImpactContext(Projectile projectile, Character target, EffectList hitEffects)
        {
            Projectile = projectile;
            Target = target;
            OriginalHitEffects = hitEffects;
        }

        internal Projectile Projectile { get; }
        internal Character Target { get; }
        internal EffectList OriginalHitEffects { get; }
        internal bool DecisionWritten { get; set; }
        internal bool HitEffectsSuppressed { get; set; }
    }

    internal struct VortexProjectileImpactScopeState
    {
        internal VortexProjectileImpactScopeState(
            VortexProjectileImpactContext? current,
            VortexProjectileImpactContext? previous,
            bool changed)
        {
            Current = current;
            Previous = previous;
            Changed = changed;
        }

        internal VortexProjectileImpactContext? Current { get; }
        internal VortexProjectileImpactContext? Previous { get; }
        internal bool Changed { get; }
    }

    internal struct VortexDirectProjectileDamageState
    {
        internal VortexDirectProjectileDamageState(HitData hit, HitData.HitType originalHitType)
        {
            Hit = hit;
            OriginalHitType = originalHitType;
        }

        internal HitData? Hit { get; }
        internal HitData.HitType OriginalHitType { get; }
        internal bool IsActive => Hit != null;
    }

    private sealed class PlayerRuntimeDebuffs
    {
        internal bool MovementApplied;
        internal float MovementPower;
        internal float JumpPower;
        internal float OriginalCrouchSpeed;
        internal float OriginalWalkSpeed;
        internal float OriginalSpeed;
        internal float OriginalRunSpeed;
        internal float OriginalJumpForce;
        internal float OriginalJumpForceForward;
        internal float LastStamina = -1f;
        internal float LastEitr = -1f;
        internal readonly Dictionary<ItemDrop.ItemData, float> DurabilitySnapshots = new();
        internal readonly List<ItemDrop.ItemData> CurrentDurabilityItems = new();
        internal readonly List<ItemDrop.ItemData> RemovedDurabilityItems = new();
    }

    private sealed class PassiveModifierSchedule
    {
        internal float NextRegeneration;
        internal float NextChameleon;
        internal float NextBlamer;
    }

    private enum BlamerKarmaAddResult
    {
        Failed,
        Added,
        Pending
    }

    private sealed class PendingBlamerKarmaRequest
    {
        internal long RequestId;
        internal float SentAt;
        internal ZDOID TargetId;
    }

    private sealed class ServerBlamerKarmaState
    {
        internal float Accumulated;
        internal float NextAllowedTime;
        internal long RequestOwner;
        internal long LastRequestId;
        internal bool HasCachedResponse;
        internal bool LastAccepted;
        internal float LastResponseAccumulated;
    }

    private sealed class ServerReflectionRequestState
    {
        internal long RequestOwner;
        internal long LastRequestId;
        internal float NextAllowedTime;
    }

    private readonly struct ReapingRequestKey : IEquatable<ReapingRequestKey>
    {
        internal ReapingRequestKey(ZDOID reaper, ZDOID dead)
        {
            Reaper = reaper;
            Dead = dead;
        }

        internal ZDOID Reaper { get; }
        internal ZDOID Dead { get; }

        public bool Equals(ReapingRequestKey other)
        {
            return Reaper.Equals(other.Reaper) && Dead.Equals(other.Dead);
        }

        public override bool Equals(object? obj)
        {
            return obj is ReapingRequestKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Reaper.GetHashCode() * 397) ^ Dead.GetHashCode();
            }
        }
    }

    private sealed class ModifierHotPathState
    {
        internal Character Character = null!;
        internal ModifierMask Mask;
        internal float SwiftFactor = 1f;
        internal float AttackSpeedFactor = 1f;
        internal bool UndodgeableEffectActive;
        internal float UndodgeableDamageReduction;
        internal int EligibilityFrame = -1;
        internal bool EffectsAllowed;
        internal float NextValidationTime;
    }

    private sealed class ModifierHudRefreshState
    {
        internal Character Character = null!;
        internal GameObject? HudGui;
        internal float NextRefreshTime;
        internal bool Result;
        internal ModifierMask Visible;
        internal float ArmoredReduction;
        internal float EnragedBonus;
        internal bool BlamerActive;
    }

    private sealed class HudIconState
    {
        internal bool Initialized;
        internal bool SlotsInitialized;
        internal ModifierMask Visible;
        internal int Armored;
        internal int Enraged;
        internal CreatureManagerPlugin.ModifierIconLayout Layout;
        internal readonly Image?[] Slots = new Image?[MaxActiveModifiers];

        internal bool Matches(
            ModifierMask visible,
            int armored,
            int enraged,
            CreatureManagerPlugin.ModifierIconLayout layout)
        {
            return Initialized && Visible == visible && Armored == armored && Enraged == enraged && Layout == layout;
        }

        internal void Set(
            ModifierMask visible,
            int armored,
            int enraged,
            CreatureManagerPlugin.ModifierIconLayout layout)
        {
            Initialized = true;
            Visible = visible;
            Armored = armored;
            Enraged = enraged;
            Layout = layout;
        }
    }

    private sealed class BlamerAlertIconState
    {
        internal Image? Icon;
        internal bool VisibilityInitialized;
        internal bool Visible;
    }

    private sealed class HudContentState
    {
        internal bool LevelContentSearched;
        internal RectTransform? LevelContent;
        internal bool BossContentSearched;
        internal RectTransform? BossContent;
        internal bool ResistanceTextSearched;
        internal TextMeshProUGUI? ResistanceText;
    }

    private readonly struct ReapingSettings
    {
        internal ReapingSettings(
            float healPerKill,
            int healMaxActivations,
            float maxHealthPerKill,
            float maxHealthCap,
            float damagePerKill,
            float damageCap,
            float scalePerKill,
            float scaleCap)
        {
            HealPerKill = Mathf.Max(0f, healPerKill);
            HealMaxActivations = Math.Max(1, healMaxActivations);
            MaxHealthPerKill = Mathf.Max(0f, maxHealthPerKill);
            MaxHealthCap = Mathf.Max(0f, maxHealthCap);
            DamagePerKill = Mathf.Max(0f, damagePerKill);
            DamageCap = Mathf.Max(0f, damageCap);
            ScalePerKill = Mathf.Max(0f, scalePerKill);
            ScaleCap = Mathf.Max(0f, scaleCap);
        }

        internal float HealPerKill { get; }
        internal int HealMaxActivations { get; }
        internal float MaxHealthPerKill { get; }
        internal float MaxHealthCap { get; }
        internal float DamagePerKill { get; }
        internal float DamageCap { get; }
        internal float ScalePerKill { get; }
        internal float ScaleCap { get; }
        internal bool HasAnyGain => HealPerKill > 0f && HealMaxActivations > 0 || (MaxHealthPerKill > 0f && MaxHealthCap > 0f) ||
                                     (DamagePerKill > 0f && DamageCap > 0f) ||
                                    (ScalePerKill > 0f && ScaleCap > 0f);
    }

    internal sealed class BlinkAttackAiOverrideState
    {
        private readonly List<Entry> _entries = new();

        internal int Count => _entries.Count;

        internal void Override(ItemDrop.ItemData.SharedData shared, float maxRange, float maxAngle)
        {
            foreach (Entry entry in _entries)
            {
                if (ReferenceEquals(entry.Shared, shared))
                {
                    return;
                }
            }

            float expandedRange = Mathf.Max(shared.m_aiAttackRange, maxRange);
            float expandedAngle = Mathf.Max(shared.m_aiAttackMaxAngle, maxAngle);
            if (Mathf.Approximately(expandedRange, shared.m_aiAttackRange) &&
                Mathf.Approximately(expandedAngle, shared.m_aiAttackMaxAngle))
            {
                return;
            }

            _entries.Add(new Entry(shared, shared.m_aiAttackRange, shared.m_aiAttackMaxAngle));
            shared.m_aiAttackRange = expandedRange;
            shared.m_aiAttackMaxAngle = expandedAngle;
        }

        internal void Restore()
        {
            for (int index = _entries.Count - 1; index >= 0; index--)
            {
                Entry entry = _entries[index];
                entry.Shared.m_aiAttackRange = entry.Range;
                entry.Shared.m_aiAttackMaxAngle = entry.MaxAngle;
            }

            _entries.Clear();
        }

        private readonly struct Entry
        {
            internal Entry(ItemDrop.ItemData.SharedData shared, float range, float maxAngle)
            {
                Shared = shared;
                Range = range;
                MaxAngle = maxAngle;
            }

            internal ItemDrop.ItemData.SharedData Shared { get; }
            internal float Range { get; }
            internal float MaxAngle { get; }
        }
    }

    internal enum DelayedDamageAttributionKind
    {
        Unknown,
        Unattributed,
        Exact,
        Ambiguous
    }

    internal readonly struct DelayedDamageAttribution
    {
        internal DelayedDamageAttribution(
            DelayedDamageAttributionKind kind,
            ZDOID source,
            bool sourceWasPlayer = false)
        {
            Kind = kind;
            Source = source;
            SourceWasPlayer = sourceWasPlayer;
        }

        internal DelayedDamageAttributionKind Kind { get; }
        internal ZDOID Source { get; }
        internal bool SourceWasPlayer { get; }
        internal bool IsExact => Kind == DelayedDamageAttributionKind.Exact && Source != ZDOID.None;

        internal static DelayedDamageAttribution FromSource(ZDOID source, bool sourceWasPlayer)
        {
            return source == ZDOID.None
                ? new DelayedDamageAttribution(DelayedDamageAttributionKind.Unattributed, ZDOID.None)
                : new DelayedDamageAttribution(DelayedDamageAttributionKind.Exact, source, sourceWasPlayer);
        }
    }

    private sealed class DelayedDamageSourceLedger
    {
        internal Character Target = null!;
        internal SE_Poison? PoisonStatus;
        internal DelayedDamageAttribution Poison;
        internal SE_Burning? FireStatus;
        internal DelayedDamageAttribution Fire;
        internal SE_Burning? SpiritStatus;
        internal DelayedDamageAttribution Spirit;
    }

    private readonly struct DelayedDamageDeathCredit
    {
        internal DelayedDamageDeathCredit(Character target, ZDOID source, bool sourceWasPlayer)
        {
            Target = target;
            Source = source;
            SourceWasPlayer = sourceWasPlayer;
        }

        internal Character Target { get; }
        internal ZDOID Source { get; }
        internal bool SourceWasPlayer { get; }
    }

    internal enum DeathAttributionKind
    {
        None,
        Direct,
        Delayed
    }

    internal readonly struct FinalDeathAttribution
    {
        internal FinalDeathAttribution(
            ZDOID source,
            DeathAttributionKind kind,
            bool sourceWasPlayer,
            Character? resolvedSource)
        {
            Source = source;
            Kind = kind;
            SourceWasPlayer = sourceWasPlayer;
            ResolvedSource = resolvedSource;
        }

        internal ZDOID Source { get; }
        internal DeathAttributionKind Kind { get; }
        internal bool SourceWasPlayer { get; }
        internal Character? ResolvedSource { get; }
        internal bool HasSource => Source != ZDOID.None && Kind != DeathAttributionKind.None;
    }

    internal struct RpcDamageContext
    {
        internal Character? Target;
        internal HitData? Hit;
        internal float ArmorPiercing;
        internal float ResolvedDelayedDamage;
        internal bool DelayedDamageCaptured;
    }

    internal struct DelayedDamageTickContext
    {
        internal Character? Target;
        internal HitData.HitType HitType;
        internal DelayedDamageAttribution Attribution;
    }

    internal readonly struct RpcDamageScopeState
    {
        private readonly RpcDamageContext _previous;

        internal RpcDamageScopeState(
            RpcDamageContext previous,
            ChameleonDamageOverrideState chameleon)
        {
            _previous = previous;
            Chameleon = chameleon;
            Changed = true;
        }

        internal ChameleonDamageOverrideState Chameleon { get; }
        internal RpcDamageContext Previous => _previous;
        internal bool Changed { get; }
    }

    internal readonly struct DelayedDamageTickScopeState
    {
        private readonly DelayedDamageTickContext _previous;

        internal DelayedDamageTickScopeState(DelayedDamageTickContext previous)
        {
            _previous = previous;
            Changed = true;
        }

        internal DelayedDamageTickContext Previous => _previous;
        internal bool Changed { get; }
    }

    internal readonly struct ChameleonDamageOverrideState
    {
        internal ChameleonDamageOverrideState(
            Character target,
            HitData.DamageModifiers original,
            ChameleonDamageType damageType)
        {
            Target = target;
            Original = original;
            DamageType = damageType;
        }

        internal Character? Target { get; }
        internal HitData.DamageModifiers Original { get; }
        internal ChameleonDamageType DamageType { get; }
        internal bool IsActive => Target != null;
    }

    internal enum UndodgeableSourcePath
    {
        None = 0,
        Melee = 1,
        Area = 2,
        ProjectileTarget = 3,
        AoeTarget = 4
    }

    internal struct UndodgeableSourceContext
    {
        internal UndodgeableSourceContext(
            Character attacker,
            Player? intendedTarget,
            UndodgeableSourcePath path,
            bool sourceDodgeable)
        {
            Attacker = attacker;
            IntendedTarget = intendedTarget;
            Path = path;
            SourceDodgeable = sourceDodgeable;
        }

        internal Character? Attacker { get; }
        internal Player? IntendedTarget { get; }
        internal UndodgeableSourcePath Path { get; }
        internal bool SourceDodgeable { get; }
        internal bool IsActive => Attacker != null && SourceDodgeable;
    }

    internal readonly struct UndodgeableSourceScopeState
    {
        internal UndodgeableSourceScopeState(UndodgeableSourceContext previous, bool changed)
        {
            Previous = previous;
            Changed = changed;
        }

        internal UndodgeableSourceContext Previous { get; }
        internal bool Changed { get; }
    }

    internal struct UndodgeableScopeState
    {
        internal UndodgeableScopeState(UndodgeableSourceScopeState source)
        {
            Source = source;
        }

        internal UndodgeableSourceScopeState Source { get; }
        internal bool IsActive => Source.Changed;
    }

    internal struct UndodgeableDamageScopeState
    {
        internal UndodgeableDamageScopeState(UndodgeableSourceContext restore, bool changed)
        {
            Restore = restore;
            Changed = changed;
        }

        internal UndodgeableSourceContext Restore { get; }
        internal bool Changed { get; }
    }

    internal readonly struct BlamerFleeOverrideState
    {
        internal BlamerFleeOverrideState(MonsterAI monsterAI, float fleeIfLowHealth, float fleeTimeSinceHurt)
        {
            MonsterAI = monsterAI;
            FleeIfLowHealth = fleeIfLowHealth;
            FleeTimeSinceHurt = fleeTimeSinceHurt;
        }

        internal MonsterAI? MonsterAI { get; }
        internal float FleeIfLowHealth { get; }
        internal float FleeTimeSinceHurt { get; }
        internal bool IsActive => MonsterAI != null;
    }

    internal readonly struct DirectDamageState
    {
        internal DirectDamageState(
            Character? vampiricAttacker,
            float vampiricPower,
            Character? reflectionAttacker,
            float reflectionPower,
            Vector3 reflectionHitPoint,
            float healthBefore)
        {
            VampiricAttacker = vampiricAttacker;
            VampiricPower = vampiricPower;
            ReflectionAttacker = reflectionAttacker;
            ReflectionPower = reflectionPower;
            ReflectionHitPoint = reflectionHitPoint;
            HealthBefore = healthBefore;
        }

        internal Character? VampiricAttacker { get; }
        internal float VampiricPower { get; }
        internal Character? ReflectionAttacker { get; }
        internal float ReflectionPower { get; }
        internal Vector3 ReflectionHitPoint { get; }
        internal float HealthBefore { get; }
        internal bool HasVampiric => VampiricAttacker != null && VampiricPower > 0f;
        internal bool HasReflection => ReflectionAttacker != null && ReflectionPower > 0f;
        internal bool IsValid => HealthBefore > 0f && (HasVampiric || HasReflection);
    }

    internal readonly struct ApplyDamageState
    {
        internal ApplyDamageState(
            DirectDamageState directDamage,
            bool eligibleAtEntry,
            float healthBefore)
        {
            DirectDamage = directDamage;
            EligibleAtEntry = eligibleAtEntry;
            HealthBefore = healthBefore;
        }

        internal DirectDamageState DirectDamage { get; }
        internal bool EligibleAtEntry { get; }
        internal float HealthBefore { get; }
    }

    private readonly struct VampiricDamageContext
    {
        internal VampiricDamageContext(Character attacker, Character target, float power)
        {
            Attacker = attacker;
            Target = target;
            Power = power;
        }

        internal Character Attacker { get; }
        internal Character Target { get; }
        internal float Power { get; }
    }

    private readonly struct ReflectionDamageContext
    {
        internal ReflectionDamageContext(Character attacker, Character target, float power, Vector3 hitPoint)
        {
            Attacker = attacker;
            Target = target;
            Power = power;
            HitPoint = hitPoint;
        }

        internal Character Attacker { get; }
        internal Character Target { get; }
        internal float Power { get; }
        internal Vector3 HitPoint { get; }
    }

    private readonly struct KnockbackHitContext
    {
        internal KnockbackHitContext(Character attacker, float cooldown)
        {
            Attacker = attacker;
            AttackerId = attacker.GetInstanceID();
            Cooldown = Mathf.Max(0f, cooldown);
        }

        internal Character Attacker { get; }
        internal int AttackerId { get; }
        internal float Cooldown { get; }
    }

    internal readonly struct BlockAttackModifierState
    {
        internal BlockAttackModifierState(
            float previousStaggerMultiplier,
            Character? unflinchingAttacker,
            bool originalStaggerWhenBlocked)
        {
            PreviousStaggerMultiplier = previousStaggerMultiplier;
            UnflinchingAttacker = unflinchingAttacker;
            OriginalStaggerWhenBlocked = originalStaggerWhenBlocked;
        }

        internal float PreviousStaggerMultiplier { get; }
        internal Character? UnflinchingAttacker { get; }
        internal bool OriginalStaggerWhenBlocked { get; }
    }

    private sealed class ModifierSpec
    {
        private readonly Func<float?, float> _powerClamp;
        private readonly Func<float, string> _description;

        internal ModifierSpec(
            ModifierGroup group,
            string key,
            string displayName,
            ModifierMask mask,
            string powerKey,
            float defaultPower,
            Func<Sprite> sprite,
            Func<ModifierChanceDefinition, float?> getChance,
            Action<ModifierChanceDefinition, float> setChance,
            Func<ModifierPowerDefinition, float?> getPower,
            Action<ModifierPowerDefinition, float> setPower,
            Func<float, string> description,
            Func<float?, float>? powerClamp = null,
            string? procChanceKey = null)
        {
            Group = group;
            Key = key;
            DisplayName = displayName;
            Mask = mask;
            PowerKey = powerKey;
            DefaultPower = defaultPower;
            Sprite = sprite;
            GetChance = getChance;
            SetChance = setChance;
            GetPower = getPower;
            SetPower = setPower;
            ProcChanceKey = procChanceKey;
            _powerClamp = powerClamp ?? (value => ClampPower(value, defaultPower));
            _description = description;
        }

        internal ModifierGroup Group { get; }
        internal string Key { get; }
        internal string DisplayName { get; }
        internal ModifierMask Mask { get; }
        internal string PowerKey { get; }
        internal float DefaultPower { get; }
        internal Func<Sprite> Sprite { get; }
        internal Func<ModifierChanceDefinition, float?> GetChance { get; }
        internal Action<ModifierChanceDefinition, float> SetChance { get; }
        internal Func<ModifierPowerDefinition, float?> GetPower { get; }
        internal Action<ModifierPowerDefinition, float> SetPower { get; }
        internal string? ProcChanceKey { get; }

        internal float ResolvePower(float? configuredPower)
        {
            return _powerClamp(configuredPower);
        }

        internal string Describe(float power)
        {
            return _description(power);
        }
    }

    private enum AdaptiveDamageType
    {
        None = 0,
        Physical = 1,
        Fire = 2,
        Frost = 3,
        Lightning = 4,
        Poison = 5,
        Spirit = 6,
        Blunt = 7,
        Slash = 8,
        Pierce = 9,
        Chop = 10,
        Pickaxe = 11
    }

    private readonly struct DamageSnapshot
    {
        private readonly float Damage;
        private readonly float Blunt;
        private readonly float Slash;
        private readonly float Pierce;
        private readonly float Chop;
        private readonly float Pickaxe;
        private readonly float Fire;
        private readonly float Frost;
        private readonly float Lightning;
        private readonly float Poison;
        private readonly float Spirit;

        private DamageSnapshot(HitData.DamageTypes damage)
        {
            Damage = damage.m_damage;
            Blunt = damage.m_blunt;
            Slash = damage.m_slash;
            Pierce = damage.m_pierce;
            Chop = damage.m_chop;
            Pickaxe = damage.m_pickaxe;
            Fire = damage.m_fire;
            Frost = damage.m_frost;
            Lightning = damage.m_lightning;
            Poison = damage.m_poison;
            Spirit = damage.m_spirit;
        }

        internal float Total => Damage + Blunt + Slash + Pierce + Chop + Pickaxe + Fire + Frost + Lightning + Poison + Spirit;
        internal float Physical => Damage + Blunt + Slash + Pierce + Chop + Pickaxe;

        internal static DamageSnapshot From(HitData.DamageTypes damage)
        {
            return new DamageSnapshot(damage);
        }

        internal AdaptiveDamageType DominantType()
        {
            AdaptiveDamageType type = AdaptiveDamageType.None;
            float value = 0f;
            Consider(Damage, AdaptiveDamageType.Physical, ref value, ref type);
            Consider(Blunt, AdaptiveDamageType.Blunt, ref value, ref type);
            Consider(Slash, AdaptiveDamageType.Slash, ref value, ref type);
            Consider(Pierce, AdaptiveDamageType.Pierce, ref value, ref type);
            Consider(Chop, AdaptiveDamageType.Chop, ref value, ref type);
            Consider(Pickaxe, AdaptiveDamageType.Pickaxe, ref value, ref type);
            Consider(Fire, AdaptiveDamageType.Fire, ref value, ref type);
            Consider(Frost, AdaptiveDamageType.Frost, ref value, ref type);
            Consider(Lightning, AdaptiveDamageType.Lightning, ref value, ref type);
            Consider(Poison, AdaptiveDamageType.Poison, ref value, ref type);
            Consider(Spirit, AdaptiveDamageType.Spirit, ref value, ref type);
            return type;
        }

        private static void Consider(float candidate, AdaptiveDamageType candidateType, ref float value, ref AdaptiveDamageType type)
        {
            if (candidate > value)
            {
                value = candidate;
                type = candidateType;
            }
        }
    }

    private delegate bool IconShape(int x, int y, out bool isBorder);
    private delegate IconTone IconToneShape(int x, int y);

    private enum IconTone : byte
    {
        Clear,
        Primary,
        Secondary,
        Accent
    }

    internal static IReadOnlyList<string> GetKnownModifierKeys()
    {
        return ModifierKeys;
    }

    internal static bool IsKnownModifier(string modifier)
    {
        return TryGetModifierSpec(modifier, out _);
    }

    internal static bool TryNormalizeForcedModifierKeys(
        IEnumerable<string> modifiers,
        out List<string> normalized,
        out string error)
    {
        normalized = new List<string>();
        error = "";
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        foreach (string rawModifier in modifiers ?? Enumerable.Empty<string>())
        {
            string modifier = (rawModifier ?? "").Trim();
            if (modifier.Length == 0)
            {
                continue;
            }

            if (!TryGetModifierSpec(modifier, out ModifierSpec spec))
            {
                error = $"Unknown modifier '{modifier}'.";
                return false;
            }

            if (seen.Add(spec.Key))
            {
                normalized.Add(spec.Key);
            }
        }

        if (normalized.Count > MaxActiveModifiers)
        {
            error = $"At most {MaxActiveModifiers} modifiers can be assigned to one creature.";
            return false;
        }

        return true;
    }

    internal static bool TryGetModifierSprite(string modifier, out Sprite sprite)
    {
        if (TryGetModifierSpec(modifier, out ModifierSpec spec))
        {
            sprite = spec.Sprite();
            return true;
        }

        sprite = null!;
        return false;
    }

    internal static string GetModifierDisplayName(string modifier)
    {
        if (!TryGetModifierSpec(modifier, out ModifierSpec spec))
        {
            return modifier.Trim();
        }

        return CreatureLocalization.Localize($"cm_modifier_{GetModifierLocalizationId(spec.Key)}_name", spec.DisplayName);
    }

    internal static string GetModifierGroupHeading(string modifier)
    {
        if (!TryGetModifierSpec(modifier, out ModifierSpec spec))
        {
            return string.Empty;
        }

        return spec.Group switch
        {
            ModifierGroup.Offense => CreatureLocalization.Localize("cm_group_offense", "Offense: Enraged to Undodgeable"),
            ModifierGroup.Defense => CreatureLocalization.Localize("cm_group_defense", "Defense: Armored to Chameleon"),
            ModifierGroup.Affliction => CreatureLocalization.Localize("cm_group_affliction", "Affliction: Exposed to Toxic Death"),
            ModifierGroup.Special => CreatureLocalization.Localize("cm_group_special", "Special: Swift to Blamer"),
            _ => string.Empty
        };
    }

    internal static float ResolveModifierPower(string modifier, float? configuredPower)
    {
        return TryGetModifierSpec(modifier, out ModifierSpec spec) ? spec.ResolvePower(configuredPower) : 0f;
    }

    internal static string GetModifierCompendiumText(string modifier, ModifierDefinition definition)
    {
        if (!TryGetModifierSpec(modifier, out ModifierSpec spec))
        {
            return $"Power: {FormatPercent(definition.Power ?? 0f)}.";
        }

        float power = spec.ResolvePower(definition.Power);
        return spec.Mask switch
        {
            ModifierMask.Exposed => DescribePlayerAffliction("exposed", "make that player take", power, definition.ProcChance, definition.Duration, PlayerDebuffDuration, "more damage"),
            ModifierMask.Weakened => DescribePlayerAffliction("weakened", "make that player deal", power, definition.ProcChance, definition.Duration, PlayerDebuffDuration, "less damage"),
            ModifierMask.Withered => DescribePlayerAffliction("withered", "reduce that player's healing received by", power, definition.ProcChance, definition.Duration, PlayerDebuffDuration),
            ModifierMask.Crippling => DescribeCrippling(
                power,
                Mathf.Clamp01(definition.SecondaryPower ?? CripplingDefaultPower),
                ResolvePlayerDebuffProcChance(definition.ProcChance),
                ResolvePlayerDebuffDuration(definition.Duration, ControlDebuffDuration)),
            ModifierMask.Disruptive => DescribeDisruptive(
                power,
                Mathf.Clamp01(definition.SecondaryPower ?? DisruptiveDefaultPower),
                ResolvePlayerDebuffProcChance(definition.ProcChance),
                ResolvePlayerDebuffDuration(definition.Duration, ControlDebuffDuration)),
            ModifierMask.Reflection => DescribeReflection(power, ResolvePlayerDebuffProcChance(definition.ProcChance, ReflectionDefaultProcChance)),
            ModifierMask.AdrenalineDrain => DescribeAdrenalineDrain(
                power,
                Mathf.Clamp01(definition.SecondaryPower ?? AdrenalineDrainDefaultGainReduction),
                ResolvePlayerDebuffProcChance(definition.ProcChance),
                ResolvePlayerDebuffDuration(definition.Duration, AdrenalineDrainDefaultDuration)),
            ModifierMask.Corrosive => DescribeCorrosive(
                power,
                ResolvePlayerDebuffProcChance(definition.ProcChance),
                ResolvePlayerDebuffDuration(definition.Duration, PlayerDebuffDuration)),
            ModifierMask.ToxicDeath => DescribeToxicDeath(power, Mathf.Max(0f, definition.Radius ?? ToxicDeathDefaultRadius)),
            ModifierMask.Blink => DescribeBlink(ResolveBlinkCooldown(definition.Cooldown), ResolveBlinkMaxRange(definition.MaxRange)),
            ModifierMask.Knockback => DescribeKnockback(power, ResolveKnockbackCooldown(definition.Cooldown)),
            ModifierMask.Blamer => DescribeBlamer(
                power,
                ResolveBlamerMaxKarmaGain(definition.MaxKarmaGain),
                ResolveBlamerFleeHealthRatio(definition.FleeHealthRatio)),
            ModifierMask.Deathward => DescribeDeathward(power, ResolveDeathwardCooldown(definition.Cooldown), ResolveDeathwardMaxActivations(definition.MaxActivations)),
            ModifierMask.Reaping => DescribeReaping(
                power,
                definition.ReapingMaxHealthPerKill ?? ReapingDefaultMaxHealthPerKill,
                definition.ReapingDamagePerKill ?? ReapingDefaultDamagePerKill,
                definition.ReapingScalePerKill ?? ReapingDefaultScalePerKill),
            _ => DescribeSimpleModifier(spec, power)
        };
    }

    private static string DescribeSimpleModifier(ModifierSpec spec, float power)
    {
        string fallback = spec.Describe(power);
        return spec.Mask switch
        {
            ModifierMask.Enraged => LocalizeModifierDescription("enraged", fallback, ("power", FormatPercent(power))),
            ModifierMask.Fire => LocalizeModifierDescription("fire", fallback, ("power", FormatPercent(power))),
            ModifierMask.Frost => LocalizeModifierDescription("frost", fallback, ("power", FormatPercent(power))),
            ModifierMask.Lightning => LocalizeModifierDescription("lightning", fallback, ("power", FormatPercent(power))),
            ModifierMask.Spirit => LocalizeModifierDescription("spirit", fallback, ("power", FormatPercent(power))),
            ModifierMask.ArmorPiercing => LocalizeModifierDescription("armor_piercing", fallback, ("power", FormatPercent(power))),
            ModifierMask.Staggering => LocalizeModifierDescription("staggering", fallback, ("power", FormatPercent(power))),
            ModifierMask.Undodgeable => LocalizeModifierDescription(
                "undodgeable",
                fallback,
                ("power", FormatPercent(power))),
            ModifierMask.Armored => LocalizeModifierDescription("armored", fallback, ("power", FormatPercent(power))),
            ModifierMask.Regenerating => LocalizeModifierDescription("regenerating", fallback, ("power", FormatPercent(power))),
            ModifierMask.Vortex => LocalizeModifierDescription("vortex", fallback, ("power", FormatPercent(power))),
            ModifierMask.Adaptive => LocalizeModifierDescription(
                "adaptive",
                fallback,
                ("duration", FormatSeconds(AdaptiveDuration)),
                ("power", FormatPercent(power))),
            ModifierMask.Unflinching => LocalizeModifierDescription("unflinching", fallback),
            ModifierMask.Chameleon => LocalizeModifierDescription("chameleon", fallback, ("interval", FormatSeconds(power))),
            ModifierMask.Swift => LocalizeModifierDescription("swift", fallback, ("power", FormatPercent(power))),
            ModifierMask.AttackSpeed => LocalizeModifierDescription(
                "attack_speed",
                fallback,
                ("power", FormatPercent(power)),
                ("interval", FormatPercent(1f / Mathf.Max(1f, 1f + power)))),
            ModifierMask.Vampiric => LocalizeModifierDescription("vampiric", fallback, ("power", FormatPercent(power))),
            ModifierMask.Omen => LocalizeModifierDescription("omen", fallback, ("power", FormatPercent(power))),
            _ => fallback
        };
    }

    private static string GetModifierLocalizationId(string modifier)
    {
        StringBuilder builder = new(modifier.Length + 4);
        for (int i = 0; i < modifier.Length; i++)
        {
            char character = modifier[i];
            if (i > 0 && char.IsUpper(character))
            {
                builder.Append('_');
            }

            builder.Append(char.ToLowerInvariant(character));
        }

        return builder.ToString();
    }

    private static string LocalizeModifierDescription(
        string modifierId,
        string fallback,
        params (string Name, string Value)[] placeholders)
    {
        return CreatureLocalization.Format($"cm_modifier_{modifierId}_description", fallback, placeholders);
    }

    private static string DescribePlayerAffliction(
        string modifierId,
        string action,
        float power,
        float? configuredProcChance,
        float? configuredDuration,
        float fallbackDuration,
        string suffix = "")
    {
        float procChance = ResolvePlayerDebuffProcChance(configuredProcChance);
        float duration = ResolvePlayerDebuffDuration(configuredDuration, fallbackDuration);
        string effect = suffix.Length == 0 ? $"{FormatPercent(power)}" : $"{FormatPercent(power)} {suffix}";
        string fallback = $"On damaging a player, has a {FormatPercent(procChance)} chance to {action} {effect} for {FormatSeconds(duration)}.";
        return LocalizeModifierDescription(
            modifierId,
            fallback,
            ("chance", FormatPercent(procChance)),
            ("power", FormatPercent(power)),
            ("duration", FormatSeconds(duration)));
    }

    private static string DescribeCrippling(float movement, float jump, float procChance, float duration)
    {
        string fallback = $"On damaging a player, has a {FormatPercent(procChance)} chance to reduce movement by {FormatPercent(movement)} and jump force by {FormatPercent(jump)} for {FormatSeconds(duration)}.";
        return LocalizeModifierDescription(
            "crippling",
            fallback,
            ("chance", FormatPercent(procChance)),
            ("movement", FormatPercent(movement)),
            ("jump", FormatPercent(jump)),
            ("duration", FormatSeconds(duration)));
    }

    private static string DescribeDisruptive(float stamina, float eitr, float procChance, float duration)
    {
        string fallback = $"On damaging a player, has a {FormatPercent(procChance)} chance to reduce stamina recovery by {FormatPercent(stamina)} and eitr recovery by {FormatPercent(eitr)} for {FormatSeconds(duration)}.";
        return LocalizeModifierDescription(
            "disruptive",
            fallback,
            ("chance", FormatPercent(procChance)),
            ("stamina", FormatPercent(stamina)),
            ("eitr", FormatPercent(eitr)),
            ("duration", FormatSeconds(duration)));
    }

    private static string DescribeReflection(float power, float procChance)
    {
        string fallback = $"On direct melee hits, has a {FormatPercent(procChance)} chance to remove health from the hostile attacker equal to {FormatPercent(power)} of the creature's actual health loss. The reflected health loss bypasses defense and resistance.";
        return LocalizeModifierDescription(
            "reflection",
            fallback,
            ("chance", FormatPercent(procChance)),
            ("power", FormatPercent(power)));
    }

    private static string DescribeAdrenalineDrain(float power, float gainReduction, float procChance, float duration)
    {
        string fallback = $"On damaging a player, has a {FormatPercent(procChance)} chance to remove {FormatPercent(power)} of current adrenaline and reduce adrenaline gained by {FormatPercent(gainReduction)} for {FormatSeconds(duration)}.";
        return LocalizeModifierDescription(
            "adrenaline_drain",
            fallback,
            ("chance", FormatPercent(procChance)),
            ("power", FormatPercent(power)),
            ("gain", FormatPercent(gainReduction)),
            ("duration", FormatSeconds(duration)));
    }

    private static string DescribeCorrosive(float power, float procChance, float duration)
    {
        string fallback = $"On damaging a player, has a {FormatPercent(procChance)} chance to increase durability loss for equipped helmet, chest, legs, cape, weapons, and shield by {FormatPercent(power)} for {FormatSeconds(duration)}.";
        return LocalizeModifierDescription(
            "corrosive",
            fallback,
            ("chance", FormatPercent(procChance)),
            ("power", FormatPercent(power)),
            ("duration", FormatSeconds(duration)));
    }

    private static string DescribeToxicDeath(float power, float radius)
    {
        string formattedRadius = radius.ToString("0.##", CultureInfo.InvariantCulture) + "m";
        string fallback = $"On death, poisons players within {formattedRadius} for {FormatPercent(power)} of each player's max health.";
        return LocalizeModifierDescription(
            "toxic_death",
            fallback,
            ("radius", formattedRadius),
            ("power", FormatPercent(power)));
    }

    private static bool TryGetModifierSpec(string modifier, out ModifierSpec spec)
    {
        if (ModifierSpecsByKey.TryGetValue(modifier.Trim(), out ModifierSpec found))
        {
            spec = found;
            return true;
        }

        spec = null!;
        return false;
    }

    internal static bool TrySetModifierChance(ModifierChanceDefinition chances, string modifier, float chance)
    {
        if (!TryGetModifierSpec(modifier, out ModifierSpec spec))
        {
            return false;
        }

        spec.SetChance(chances, chance);
        return true;
    }

    internal static bool TrySetModifierPower(ModifierPowerDefinition powers, string modifier, float power)
    {
        if (!TryGetModifierSpec(modifier, out ModifierSpec spec))
        {
            return false;
        }

        spec.SetPower(powers, power);
        return true;
    }

    private static string FormatPercent(float value)
    {
        return $"{(Mathf.Max(0f, value) * 100f).ToString("0.##", CultureInfo.InvariantCulture)}%";
    }

    private static string FormatSeconds(float seconds)
    {
        string value = seconds.ToString("0.##", CultureInfo.InvariantCulture);
        return CreatureLocalization.Format("cm_unit_seconds", $"{value}s", ("value", value));
    }

    private static string FormatMeters(float meters)
    {
        return $"{meters.ToString("0.##", CultureInfo.InvariantCulture)}m";
    }

    private static string DescribeBlink(float cooldown, float maxRange)
    {
        string fallback = $"Can teleport near a player target within {FormatMeters(maxRange)} every {FormatSeconds(cooldown)}.";
        string description = LocalizeModifierDescription(
            "blink",
            fallback,
            ("range", FormatMeters(maxRange)),
            ("cooldown", FormatSeconds(cooldown)));
        float gracePeriod = ResolveBlinkAlertGracePeriod();
        if (gracePeriod <= 0f)
        {
            return description;
        }

        string graceDescription = CreatureLocalization.Format(
            "cm_modifier_blink_alert_grace",
            "Blink and its extended attack range are disabled for {duration} after becoming alerted.",
            ("duration", FormatSeconds(gracePeriod)));
        return $"{description} {graceDescription}";
    }

    private static string DescribeDeathward(float healthRatio, float cooldown, int maxActivations)
    {
        string fallback = $"Cancels lethal damage and restores health to {FormatPercent(healthRatio)} of max health, with a {FormatSeconds(cooldown)} cooldown and a limit of {maxActivations} activations per creature.";
        return LocalizeModifierDescription(
            "deathward",
            fallback,
            ("health", FormatPercent(healthRatio)),
            ("cooldown", FormatSeconds(cooldown)),
            ("activations", maxActivations.ToString(CultureInfo.InvariantCulture)));
    }

    private static string DescribeKnockback(float minimumPushForce, float cooldown)
    {
        string force = minimumPushForce.ToString("0.##", CultureInfo.InvariantCulture);
        string fallback = $"On a damaging hit against a player, raises push force to at least {force}. A hit that reaches vanilla pushback starts a {FormatSeconds(cooldown)} cooldown. Attack hits cannot push this creature.";
        return LocalizeModifierDescription(
            "juggernaut",
            fallback,
            ("force", force),
            ("cooldown", FormatSeconds(cooldown)));
    }

    private static string DescribeChameleon(float interval)
    {
        string fallback = $"While alerted, becomes immune to one non-immune damage type and changes that immunity every {FormatSeconds(interval)}.";
        return LocalizeModifierDescription(
            "chameleon",
            fallback,
            ("interval", FormatSeconds(interval)));
    }

    private static string DescribeBlamer(float karmaPerSecond, float maxKarmaGain, float fleeHealthRatio)
    {
        string karma = karmaPerSecond.ToString("0.##", CultureInfo.InvariantCulture);
        string cap = maxKarmaGain <= 0f
            ? CreatureLocalization.Localize("cm_value_unlimited", "unlimited")
            : maxKarmaGain.ToString("0.##", CultureInfo.InvariantCulture);
        string health = FormatPercent(fleeHealthRatio);
        string fallback = $"Below {health} health, flees from hostile players and adds {karma} Karma per second to its current region while fleeing, up to {cap} Karma over its lifetime.";
        return LocalizeModifierDescription(
            "blamer",
            fallback,
            ("health", health),
            ("karma", karma),
            ("cap", cap));
    }

    private static string DescribeReaping(
        float healPerKill,
        float maxHealthPerKill,
        float damagePerKill,
        float scalePerKill)
    {
        string fallback = $"When a player or this creature kills a nearby creature, this creature heals for {FormatPercent(healPerKill)} of its base max health and gains {FormatPercent(maxHealthPerKill)} base max health, {FormatPercent(damagePerKill)} outgoing damage, and {FormatPercent(scalePerKill)} size.";
        return LocalizeModifierDescription(
            "reaping",
            fallback,
            ("heal", FormatPercent(healPerKill)),
            ("health", FormatPercent(maxHealthPerKill)),
            ("damage", FormatPercent(damagePerKill)),
            ("scale", FormatPercent(scalePerKill)));
    }

    private static float ResolveDeathwardCooldown(float? configuredCooldown)
    {
        return !configuredCooldown.HasValue || float.IsNaN(configuredCooldown.Value)
            ? DeathwardDefaultCooldown
            : Mathf.Max(0f, configuredCooldown.Value);
    }

    private static int ResolveDeathwardMaxActivations(int? configuredMaxActivations)
    {
        return !configuredMaxActivations.HasValue || configuredMaxActivations.Value < 1
            ? DeathwardDefaultMaxActivations
            : configuredMaxActivations.Value;
    }

    private static float ResolveBlinkCooldown(float? configuredCooldown)
    {
        return !configuredCooldown.HasValue || float.IsNaN(configuredCooldown.Value)
            ? BlinkDefaultCooldown
            : Mathf.Max(0f, configuredCooldown.Value);
    }

    private static float ResolveKnockbackCooldown(float? configuredCooldown)
    {
        return !configuredCooldown.HasValue ||
               float.IsNaN(configuredCooldown.Value) ||
               float.IsInfinity(configuredCooldown.Value)
            ? KnockbackDefaultCooldown
            : Mathf.Max(0f, configuredCooldown.Value);
    }

    private static float ResolveChameleonInterval(float? configuredInterval)
    {
        return !configuredInterval.HasValue ||
               float.IsNaN(configuredInterval.Value) ||
               float.IsInfinity(configuredInterval.Value)
            ? ChameleonDefaultInterval
            : Mathf.Max(0.1f, configuredInterval.Value);
    }

    private static float ResolveBlamerKarmaPerSecond(float? configuredPower)
    {
        return !configuredPower.HasValue || float.IsNaN(configuredPower.Value) || float.IsInfinity(configuredPower.Value)
            ? BlamerDefaultKarmaPerSecond
            : Mathf.Max(0f, configuredPower.Value);
    }

    private static float ResolveBlamerMaxKarmaGain(float? configuredCap)
    {
        return !configuredCap.HasValue || float.IsNaN(configuredCap.Value) || float.IsInfinity(configuredCap.Value)
            ? BlamerDefaultMaxKarmaGain
            : Mathf.Max(0f, configuredCap.Value);
    }

    private static float ResolveBlamerFleeHealthRatio(float? configuredRatio)
    {
        return !configuredRatio.HasValue || float.IsNaN(configuredRatio.Value) || float.IsInfinity(configuredRatio.Value)
            ? BlamerDefaultFleeHealthRatio
            : Mathf.Clamp01(configuredRatio.Value);
    }

    private static float ClampKnockbackPower(float? configuredPower)
    {
        return !configuredPower.HasValue || float.IsNaN(configuredPower.Value)
            ? KnockbackDefaultPower
            : Mathf.Max(0f, configuredPower.Value);
    }

    private static float ResolveBlinkMaxRange(float? configuredMaxRange)
    {
        return Mathf.Max(0f, configuredMaxRange ?? BlinkDefaultMaxRange);
    }

    private static string ResolveBlinkStartEffect(string? configuredStartEffect)
    {
        string effect = (configuredStartEffect ?? BlinkDefaultStartEffect).Trim();
        return string.Equals(effect, "none", StringComparison.OrdinalIgnoreCase) ? "" : effect;
    }

    internal static void RegisterRpcs()
    {
        if (ZRoutedRpc.instance == null || ReferenceEquals(RegisteredRoutedRpc, ZRoutedRpc.instance))
        {
            return;
        }

        ZRoutedRpc.instance.Register<ZPackage>(VortexHitEffectRpc, RPC_VortexHitEffect);
        ZRoutedRpc.instance.Register<ZPackage>(ReflectionEffectRpc, RPC_ReflectionEffect);
        ZRoutedRpc.instance.Register<ZPackage>(BlinkEffectRpc, RPC_BlinkEffect);
        ZRoutedRpc.instance.Register<ZPackage>(DeathwardEffectRpc, RPC_DeathwardEffect);
        ZRoutedRpc.instance.Register<ZPackage>(ReapingFeedbackRpc, RPC_ReapingFeedback);
        EnsureVortexHitTypeEncodingSupported();
        RegisteredRoutedRpc = ZRoutedRpc.instance;
    }

    internal static void RegisterCharacterRpcs(Character character)
    {
        if (character == null || character.m_nview == null)
        {
            return;
        }

        character.m_nview.Register<long, ZDOID, float>(
            ReflectionDamageRequestRpc,
            (sender, requestId, targetId, amount) =>
                RPC_ReflectionDamageRequest(character, sender, requestId, targetId, amount));
        character.m_nview.Register<ZDOID, float>(
            ReflectionDamageRpc,
            (sender, sourceId, amount) => ApplyAuthorizedReflectionDamage(character, sender, sourceId, amount));
        character.m_nview.Register<ZDOID>(
            KnockbackCooldownRequestRpc,
            (sender, targetId) => RPC_KnockbackCooldownRequest(character, sender, targetId));
        character.m_nview.Register<float>(
            KnockbackCooldownRpc,
            (sender, nextReadyTime) => CommitAuthorizedKnockbackCooldown(character, sender, nextReadyTime));
        character.m_nview.Register<ZDOID, Vector3>(
            ReapingDirectKillRequestRpc,
            (sender, deadId, deathPosition) =>
                RPC_ReapingDirectKillRequest(character, sender, deadId, deathPosition));
        character.m_nview.Register<ZDOID, Vector3>(
            ReapingDirectKillRpc,
            (sender, deadId, deathPosition) =>
                ApplyAuthorizedReapingGain(character, sender, deadId, deathPosition));
        character.m_nview.Register(
            ReapingRespawnRequestRpc,
            sender => RPC_ReapingRespawnRequest(character, sender));
        character.m_nview.Register<Vector3>(
            VortexHitEffectRequestRpc,
            (sender, position) => RPC_VortexHitEffectRequest(character, sender, position));
        character.m_nview.Register<long, ZDOID>(
            BlamerKarmaRequestRpc,
            (sender, requestId, targetId) => RPC_BlamerKarmaRequest(character, sender, requestId, targetId));
        character.m_nview.Register<long, bool, float>(
            BlamerKarmaResponseRpc,
            (sender, requestId, accepted, accumulated) =>
                RPC_BlamerKarmaResponse(character, sender, requestId, accepted, accumulated));
    }

    internal static void ForgetCharacter(Character character)
    {
        if (character == null)
        {
            return;
        }

        int id = character.GetInstanceID();
        UntrackRuntimeModifiers(character);
        ActivePlayerDebuffs.Remove(id);
        ActivePlayerRuntimeDebuffs.Remove(id);
        PlayerControlDebuffWakeIds.Remove(id);
        NextInactivePlayerDebuffProbeTimes.Remove(id);
        ModifierHotPathStates.Remove(id);
        ModifierHudRefreshStates.Remove(id);
        RuntimeModifierReprobeTimes.Remove(id);
        PendingReapingHealthBonusRatios.Remove(id);
        DelayedDamageSourceLedgers.Remove(id);
        PendingDelayedDamageDeathCredits.Remove(id);
        PendingKnockbackReservations.Remove(id);
        LocalKnockbackReadyTimes.Remove(id);
        ResetBlamerKarmaNetworkState(character);
        ZDOID characterId = character.GetZDOID();
        if (characterId != ZDOID.None)
        {
            ServerReflectionRequestStates.Remove(characterId);
            ServerVortexEffectNextAllowedTimes.Remove(characterId);
            ServerKnockbackNextAllowedTimes.Remove(characterId);
            ServerAuthorizedReapingDeaths.Remove(characterId);
            ServerPendingReapingRequests.RemoveWhere(request => request.Reaper.Equals(characterId));
            ServerPendingReapingRespawns.Remove(characterId);
            ClearReapingDeathAuthorization(characterId);
        }
    }

    private static void ResetBlamerKarmaNetworkState(Character character)
    {
        PendingBlamerKarmaRequests.Remove(character.GetInstanceID());
        ZDOID characterId = character.GetZDOID();
        if (characterId != ZDOID.None)
        {
            ServerBlamerKarmaStates.Remove(characterId);
            NextBlamerRejectionLogTimes.Remove(characterId);
        }
    }

    internal static void ResetRuntimeState()
    {
        ActivePlayerDebuffs.Clear();
        ActivePlayerRuntimeDebuffs.Clear();
        PlayerControlDebuffWakeIds.Clear();
        NextInactivePlayerDebuffProbeTimes.Clear();
        ModifierHotPathStates.Clear();
        ModifierHudRefreshStates.Clear();
        RuntimeModifierReprobeTimes.Clear();
        PendingReapingHealthBonusRatios.Clear();
        DelayedDamageSourceLedgers.Clear();
        PendingDelayedDamageDeathCredits.Clear();
        PassiveModifierProbeDoneCharacters.Clear();
        ActivePassiveModifierCharacters.Clear();
        PassiveModifierSchedules.Clear();
        ActiveReapingModifierCharacters.Clear();
        UnqueryableReapingModifierCharacters.Clear();
        ReapingNearbyCandidateIds.Clear();
        ReapingStaleCandidateIds.Clear();
        ReapingOverlapBuffer = new Collider[InitialReapingOverlapBufferSize];
        CancelPendingReapingPhysicsSync();
        BlinkAttackAiOverridePool.Clear();
        HudIconStates = new ConditionalWeakTable<RectTransform, HudIconState>();
        BlamerAlertIconStates = new ConditionalWeakTable<RectTransform, BlamerAlertIconState>();
        HudContentStates = new ConditionalWeakTable<RectTransform, HudContentState>();
        // Registration follows the ZRoutedRpc instance lifetime. It has no unregister API and Register uses Dictionary.Add.
        CachedHoveredCharacter = null;
        CachedHoverFrame = -1;
        CachedHoverValid = false;
        CachedSneakFrame = -1;
        CachedSneakState = false;
        PendingPlayerSpiritDamage.Clear();
        PendingVampiricDamage.Clear();
        PendingReflectionDamage.Clear();
        FinalDeathwardConsumedHits.Clear();
        PendingVortexProjectileDecisions.Clear();
        PendingKnockbackHits.Clear();
        PendingKnockbackReservations.Clear();
        LocalKnockbackReadyTimes.Clear();
        PendingBlamerKarmaRequests.Clear();
        ServerBlamerKarmaStates.Clear();
        NextBlamerRejectionLogTimes.Clear();
        ServerReflectionRequestStates.Clear();
        ServerVortexEffectNextAllowedTimes.Clear();
        ServerKnockbackNextAllowedTimes.Clear();
        ServerPendingReapingRequests.Clear();
        ServerPendingReapingRespawns.Clear();
        ServerAuthorizedReapingDeaths.Clear();
        NextBlamerKarmaRequestId = 0;
        NextBlamerGlobalRejectionLogTime = 0f;
        NextReflectionRequestId = DateTime.UtcNow.Ticks;
        CurrentVortexProjectileImpact = null;
        CurrentRpcDamageContext = default;
        CurrentDelayedDamageTickContext = default;
        MissingDeathwardEffectPrefabs.Clear();
        MissingReapingEffectPrefabs.Clear();
        UndodgeableRuntimeFailureReported = false;
        DeathwardRuntimeFailureReported = false;
        CurrentUndodgeableSourceContext = default;
        BlockStaggerMultiplier = 0f;
    }

    internal static void TryRollModifiers(Character character)
    {
        if (character == null || character.IsPlayer() || !CreatureLevelManager.IsLevelSystemEnabled())
        {
            return;
        }

        ZNetView? nview = character.m_nview;
        if (nview == null || !nview.IsValid())
        {
            return;
        }

        if (!nview.IsOwner())
        {
            return;
        }

        ZDO zdo = nview.GetZDO();
        if (zdo == null)
        {
            return;
        }

        if (zdo.GetBool(AppliedKey, false))
        {
            if (CreatureLevelManager.AllowsModifierEffects(character))
            {
                ApplyRuntimeModifierStats(character, zdo);
            }

            return;
        }

        if (!CreatureLevelManager.ShouldRollModifiers(character))
        {
            return;
        }

        bool hasEnforcerModifiers = CreatureKarmaManager.TryGetEnforcerModifierDefinitions(
            character,
            out Dictionary<string, ModifierDefinition> enforcerModifiers,
            out bool modifierFallbackBlocked);

        ModifierChanceDefinition chances = new();
        bool hasChances = false;
        if (!hasEnforcerModifiers || !modifierFallbackBlocked)
        {
            hasChances = CreatureLevelManager.TrySelectModifierChances(character, out chances);
        }

        if (hasEnforcerModifiers)
        {
            hasChances = ApplyModifierChances(chances, enforcerModifiers) || hasChances;
        }

        ModifierMask mask = hasChances ? RollConfiguredMask(character, chances) : ModifierMask.None;

        ModifierPowerDefinition powers = new();
        bool hasPowers = false;
        if (!hasEnforcerModifiers || !modifierFallbackBlocked)
        {
            hasPowers = CreatureLevelManager.TrySelectModifierPowers(character, out powers);
        }

        if (hasEnforcerModifiers)
        {
            hasPowers = ApplyModifierPowers(powers, enforcerModifiers) || hasPowers;
        }

        if (!hasPowers)
        {
            powers = new ModifierPowerDefinition();
        }

        StoreInitialModifierState(character, zdo, mask, powers);
    }

    internal static bool TryApplyForcedModifiers(
        Character character,
        IReadOnlyCollection<string> modifiers,
        out string error)
    {
        error = "";
        if (!CreatureLevelManager.IsLevelSystemEnabled())
        {
            error = "CreatureManager level system is disabled.";
            return false;
        }

        if (character == null || character.IsPlayer())
        {
            error = "The spawned prefab is not a supported creature.";
            return false;
        }

        if (!TryNormalizeForcedModifierKeys(modifiers, out List<string> normalized, out error))
        {
            return false;
        }

        ZNetView? nview = character.m_nview;
        if (nview == null || !nview.IsValid() || !nview.IsOwner())
        {
            error = "The spawned creature has no owned network state.";
            return false;
        }

        ZDO zdo = nview.GetZDO();
        if (zdo == null)
        {
            error = "The spawned creature has no ZDO.";
            return false;
        }

        ModifierMask mask = ModifierMask.None;
        foreach (string modifier in normalized)
        {
            mask |= ModifierSpecsByKey[modifier].Mask;
        }

        if (!CreatureLevelManager.TrySelectModifierPowers(character, out ModifierPowerDefinition powers))
        {
            powers = new ModifierPowerDefinition();
        }

        StoreInitialModifierState(character, zdo, mask, powers);
        return true;
    }

    private static void StoreInitialModifierState(Character character, ZDO zdo, ModifierMask mask, ModifierPowerDefinition powers)
    {
        UntrackRuntimeModifiers(character);
        ResetBlamerKarmaNetworkState(character);
        InvalidateModifierCaches(character);
        zdo.Set(AppliedKey, false);
        ClearStoredRuntimeModifierState(zdo);
        SetStoredModifierMask(zdo, mask);
        StoreModifierPowers(zdo, mask, powers);
        zdo.Set(AppliedKey, true);
        RefreshModifierHotPathState(character, zdo);
        if (HasModifier(mask, ModifierMask.Chameleon))
        {
            InitializeChameleonState(character, zdo);
        }

        if (HasModifier(mask, ModifierMask.Blamer))
        {
            GetPassiveModifierSchedule(character, zdo, Time.time).NextBlamer = Time.time + BlamerTickInterval;
        }

        if (HasModifier(mask, ModifierMask.Blink) && character.GetBaseAI()?.IsAlerted() == true)
        {
            zdo.Set(BlinkAlertStartTimeKey, GetNetworkTimeSeconds());
        }

        ApplyRuntimeModifierStats(character, zdo);
    }

    internal static bool InheritModifiers(Character source, Character target)
    {
        if (!CreatureLevelManager.IsLevelSystemEnabled() || source == null || target == null || source == target || target.IsPlayer())
        {
            return false;
        }

        if (!TryGetZdo(source, out ZDO sourceZdo) || !TryGetZdo(target, out ZDO targetZdo))
        {
            return false;
        }

        ZNetView? targetView = target.m_nview;
        if (targetView == null || !targetView.IsValid() || !targetView.IsOwner())
        {
            return false;
        }

        if (!sourceZdo.GetBool(AppliedKey, false))
        {
            TryRollModifiers(source);
        }

        if (!sourceZdo.GetBool(AppliedKey, false))
        {
            return false;
        }

        UntrackRuntimeModifiers(target);
        ResetBlamerKarmaNetworkState(target);
        InvalidateModifierCaches(target);
        targetZdo.Set(AppliedKey, false);
        ClearStoredRuntimeModifierState(targetZdo);
        ModifierMask inheritedMask = GetStoredModifierMask(sourceZdo);
        CaptureInheritedReapingHealthRatio(sourceZdo, target, inheritedMask);
        SetStoredModifierMask(targetZdo, inheritedMask);
        CopyStoredModifierPowers(sourceZdo, targetZdo);
        CopyStoredRuntimeModifierState(sourceZdo, targetZdo, inheritedMask);
        targetZdo.Set(AppliedKey, true);
        RefreshModifierHotPathState(target, targetZdo);
        PassiveModifierSchedule? schedule = null;
        if (HasModifier(inheritedMask, ModifierMask.Chameleon))
        {
            schedule = GetPassiveModifierSchedule(target, targetZdo, Time.time);
            float interval = ResolveChameleonInterval(targetZdo.GetFloat(ChameleonIntervalKey, ChameleonDefaultInterval));
            schedule.NextChameleon = Time.time + interval;
        }

        if (HasModifier(inheritedMask, ModifierMask.Blamer))
        {
            schedule ??= GetPassiveModifierSchedule(target, targetZdo, Time.time);
            schedule.NextBlamer = Time.time + BlamerTickInterval;
        }

        ApplyRuntimeModifierStats(target, targetZdo);
        return true;
    }

    private static void CaptureInheritedReapingHealthRatio(ZDO source, Character target, ModifierMask mask)
    {
        int targetId = target.GetInstanceID();
        PendingReapingHealthBonusRatios.Remove(targetId);
        if (!HasModifier(mask, ModifierMask.Reaping))
        {
            return;
        }

        float sourceBaseMaxHealth = source.GetFloat(ReapingBaseMaxHealthKey, 0f);
        float sourceBonusHealth = source.GetFloat(ReapingBonusHealthKey, 0f);
        if (sourceBaseMaxHealth > 0f && sourceBonusHealth > 0f)
        {
            PendingReapingHealthBonusRatios[targetId] = sourceBonusHealth / sourceBaseMaxHealth;
        }
    }

    private static void ClearStoredRuntimeModifierState(ZDO zdo)
    {
        zdo.RemoveFloat(DeathwardNextReadyTimeKey);
        zdo.RemoveInt(DeathwardActivationCountKey);
        zdo.RemoveFloat(BlinkNextTimeKey);
        zdo.RemoveFloat(BlinkAlertStartTimeKey);
        zdo.RemoveFloat(KnockbackNextReadyTimeKey);
        zdo.RemoveFloat(ReapingBaseMaxHealthKey);
        zdo.RemoveInt(ReapingHealActivationCountKey);
        zdo.RemoveFloat(ReapingBonusHealthKey);
        zdo.RemoveFloat(ReapingDamageBonusKey);
        zdo.RemoveFloat(ReapingScaleBonusKey);
        zdo.RemoveVec3(ReapingBaseScaleKey);
        zdo.RemoveInt(AdaptiveTypeKey);
        zdo.RemoveFloat(AdaptiveUntilKey);
        zdo.RemoveInt(ChameleonTypeKey);
        zdo.RemoveFloat(BlamerAccumulatedKarmaKey);
        zdo.RemoveInt(BlamerActiveKey);
    }

    private static void CopyStoredRuntimeModifierState(ZDO source, ZDO target, ModifierMask mask)
    {
        if (HasModifier(mask, ModifierMask.Deathward))
        {
            CopyNonZeroFloat(source, target, DeathwardNextReadyTimeKey);
            CopyNonZeroInt(source, target, DeathwardActivationCountKey);
        }

        if (HasModifier(mask, ModifierMask.Knockback))
        {
            CopyNonZeroFloat(source, target, KnockbackNextReadyTimeKey);
        }

        if (HasModifier(mask, ModifierMask.Blink))
        {
            CopyNonZeroFloat(source, target, BlinkNextTimeKey);
            CopyNonZeroFloat(source, target, BlinkAlertStartTimeKey);
        }

        if (HasModifier(mask, ModifierMask.Reaping))
        {
            CopyNonZeroInt(source, target, ReapingHealActivationCountKey);
            CopyNonZeroFloat(source, target, ReapingBonusHealthKey);
            CopyNonZeroFloat(source, target, ReapingDamageBonusKey);
            CopyNonZeroFloat(source, target, ReapingScaleBonusKey);
        }

        if (HasModifier(mask, ModifierMask.Adaptive))
        {
            CopyNonZeroInt(source, target, AdaptiveTypeKey);
            CopyNonZeroFloat(source, target, AdaptiveUntilKey);
        }

        if (HasModifier(mask, ModifierMask.Chameleon))
        {
            CopyNonZeroInt(source, target, ChameleonTypeKey);
        }

        if (HasModifier(mask, ModifierMask.Blamer))
        {
            CopyNonZeroFloat(source, target, BlamerAccumulatedKarmaKey);
        }
    }

    private static void CopyNonZeroFloat(ZDO source, ZDO target, string key)
    {
        float value = source.GetFloat(key, 0f);
        if (value != 0f)
        {
            target.Set(key, value);
        }
    }

    private static void CopyNonZeroInt(ZDO source, ZDO target, string key)
    {
        int value = source.GetInt(key, 0);
        if (value != 0)
        {
            target.Set(key, value);
        }
    }

    internal static bool NeedsPassiveModifierUpdate(Character character)
    {
        if (character == null || character.IsPlayer())
        {
            return false;
        }

        int id = character.GetInstanceID();
        if (!CreatureLevelManager.IsLevelSystemEnabled())
        {
            PassiveModifierSchedules.Remove(id);
            return false;
        }

        if (ActivePassiveModifierCharacters.Contains(id))
        {
            ZNetView? activeView = character.m_nview;
            if (activeView == null || !activeView.IsValid() || !activeView.IsOwner())
            {
                PassiveModifierSchedules.Remove(id);
                if (TryGetZdo(character, out ZDO ownershipZdo) && ownershipZdo.GetBool(AppliedKey, false))
                {
                    TrackRuntimeModifiers(character, ownershipZdo);
                }
                else
                {
                    ActivePassiveModifierCharacters.Remove(id);
                    PassiveModifierProbeDoneCharacters.Remove(id);
                    RuntimeModifierReprobeTimes[id] = Time.unscaledTime + RuntimeModifierReprobeInterval;
                }

                return false;
            }

            return !TryGetZdo(character, out ZDO activeZdo) ||
                   IsPassiveModifierUpdateDue(character, activeZdo, Time.time);
        }

        if (PassiveModifierProbeDoneCharacters.Contains(id))
        {
            return false;
        }

        if (RuntimeModifierReprobeTimes.TryGetValue(id, out float nextProbe))
        {
            if (Time.unscaledTime < nextProbe)
            {
                return false;
            }

            RuntimeModifierReprobeTimes.Remove(id);
        }

        if (TryGetZdo(character, out ZDO zdo) && zdo.GetBool(AppliedKey, false))
        {
            TrackRuntimeModifiers(character, zdo);
        }
        else
        {
            RuntimeModifierReprobeTimes[id] = Time.unscaledTime + RuntimeModifierReprobeInterval;
            return false;
        }

        ZNetView? nview = character.m_nview;
        return ActivePassiveModifierCharacters.Contains(id) &&
               nview != null &&
               nview.IsValid() &&
               nview.IsOwner();
    }

    private static bool IsPassiveModifierUpdateDue(Character character, ZDO zdo, float now)
    {
        ModifierMask mask = GetStoredModifierMask(zdo);
        PassiveModifierSchedule schedule = GetPassiveModifierSchedule(character, zdo, now);
        return (HasModifier(mask, ModifierMask.Regenerating) && now >= schedule.NextRegeneration) ||
               (HasModifier(mask, ModifierMask.Chameleon) && now >= schedule.NextChameleon) ||
               (HasModifier(mask, ModifierMask.Blamer) && now >= schedule.NextBlamer);
    }

    private static PassiveModifierSchedule GetPassiveModifierSchedule(Character character, ZDO zdo, float now)
    {
        int id = character.GetInstanceID();
        if (!PassiveModifierSchedules.TryGetValue(id, out PassiveModifierSchedule schedule))
        {
            float chameleonInterval = ResolveChameleonInterval(zdo.GetFloat(ChameleonIntervalKey, ChameleonDefaultInterval));
            schedule = new PassiveModifierSchedule
            {
                NextRegeneration = now + 1f,
                NextChameleon = now + chameleonInterval,
                NextBlamer = now + BlamerTickInterval
            };
            PassiveModifierSchedules[id] = schedule;
        }

        return schedule;
    }

    private static void TrackRuntimeModifiers(Character character, ZDO zdo)
    {
        int id = character.GetInstanceID();
        PassiveModifierProbeDoneCharacters.Add(id);
        ModifierMask mask = GetStoredModifierMask(zdo);
        if (character.IsDead())
        {
            ActivePassiveModifierCharacters.Remove(id);
            PassiveModifierSchedules.Remove(id);
            ActiveReapingModifierCharacters.Remove(id);
            UnqueryableReapingModifierCharacters.Remove(id);
            RuntimeModifierReprobeTimes.Remove(id);
            return;
        }

        if (!CreatureLevelManager.AllowsModifierEffects(character))
        {
            ZNetView? disabledView = character.m_nview;
            if (HasModifier(mask, ModifierMask.Blamer) &&
                disabledView != null &&
                disabledView.IsValid() &&
                disabledView.IsOwner())
            {
                SetBlamerActive(character, zdo, false);
            }

            ActivePassiveModifierCharacters.Remove(id);
            PassiveModifierSchedules.Remove(id);
            ActiveReapingModifierCharacters.Remove(id);
            UnqueryableReapingModifierCharacters.Remove(id);
            ModifierMask trackedMask = ModifierMask.Regenerating | ModifierMask.Chameleon | ModifierMask.Blamer | ModifierMask.Reaping;
            if ((mask & trackedMask) != 0)
            {
                PassiveModifierProbeDoneCharacters.Remove(id);
                RuntimeModifierReprobeTimes[id] = Time.unscaledTime + RuntimeModifierReprobeInterval;
            }
            else
            {
                RuntimeModifierReprobeTimes.Remove(id);
            }

            return;
        }

        bool hasRegenerating = HasModifier(mask, ModifierMask.Regenerating) && zdo.GetFloat(RegeneratingPowerKey, 0f) > 0f;
        bool hasChameleon = HasModifier(mask, ModifierMask.Chameleon) &&
                            zdo.GetFloat(ChameleonIntervalKey, 0f) > 0f &&
                            HasEligibleChameleonType(character);
        ZNetView? nview = character.m_nview;
        bool canRunPassiveModifier = nview != null && nview.IsValid() && nview.IsOwner();
        if (!canRunPassiveModifier)
        {
            PendingBlamerKarmaRequests.Remove(id);
        }

        bool hasStoredBlamer = HasModifier(mask, ModifierMask.Blamer);
        bool blamerHasKarmaRemaining = HasBlamerKarmaRemaining(zdo);
        if (hasStoredBlamer && !blamerHasKarmaRemaining && canRunPassiveModifier)
        {
            ExhaustBlamer(character, zdo);
            mask = GetStoredModifierMask(zdo);
            hasStoredBlamer = false;
        }

        float blamerPower = zdo.GetFloat(BlamerKarmaPerSecondKey, 0f);
        float blamerFleeHealthRatio = zdo.GetFloat(BlamerFleeHealthRatioKey, BlamerDefaultFleeHealthRatio);
        bool hasBlamer = hasStoredBlamer &&
                         blamerPower > 0f &&
                         blamerFleeHealthRatio > 0f &&
                         blamerHasKarmaRemaining &&
                         !character.IsTamed() &&
                         character.GetBaseAI() is MonsterAI &&
                         !CreatureKarmaManager.IsKarmaSummonedCreature(character);
        if (hasStoredBlamer && !hasBlamer && canRunPassiveModifier)
        {
            SetBlamerActive(character, zdo, false);
        }

        bool hasReaping = HasModifier(mask, ModifierMask.Reaping) && HasStoredReapingSettings(zdo);
        bool hasPassiveModifier = hasRegenerating || hasChameleon || hasBlamer;
        if (hasPassiveModifier && canRunPassiveModifier)
        {
            ActivePassiveModifierCharacters.Add(id);
        }
        else
        {
            ActivePassiveModifierCharacters.Remove(id);
            PassiveModifierSchedules.Remove(id);
        }

        bool needsBlamerOwnerCleanup = hasStoredBlamer && !blamerHasKarmaRemaining;
        if ((hasPassiveModifier || needsBlamerOwnerCleanup) && !canRunPassiveModifier)
        {
            PassiveModifierProbeDoneCharacters.Remove(id);
            RuntimeModifierReprobeTimes[id] = Time.unscaledTime + RuntimeModifierReprobeInterval;
        }
        else
        {
            RuntimeModifierReprobeTimes.Remove(id);
        }

        if (hasReaping)
        {
            bool needsColliderClassification =
                !ActiveReapingModifierCharacters.TryGetValue(id, out Character tracked) ||
                !ReferenceEquals(tracked, character);
            ActiveReapingModifierCharacters[id] = character;
            if (needsColliderClassification)
            {
                if (HasQueryableReapingCollider(character))
                {
                    UnqueryableReapingModifierCharacters.Remove(id);
                }
                else
                {
                    UnqueryableReapingModifierCharacters[id] = character;
                }
            }
        }
        else
        {
            ActiveReapingModifierCharacters.Remove(id);
            UnqueryableReapingModifierCharacters.Remove(id);
        }
    }

    private static void UntrackRuntimeModifiers(Character character)
    {
        if (character == null)
        {
            return;
        }

        int id = character.GetInstanceID();
        PassiveModifierProbeDoneCharacters.Remove(id);
        ActivePassiveModifierCharacters.Remove(id);
        PassiveModifierSchedules.Remove(id);
        ActiveReapingModifierCharacters.Remove(id);
        UnqueryableReapingModifierCharacters.Remove(id);
        RuntimeModifierReprobeTimes.Remove(id);
    }

    private static bool HasQueryableReapingCollider(Character character)
    {
        int layerMask = GetReapingCharacterLayerMask();
        foreach (Collider collider in character.GetComponentsInChildren<Collider>(true))
        {
            if (collider != null &&
                collider.enabled &&
                collider.gameObject.activeInHierarchy &&
                (layerMask & (1 << collider.gameObject.layer)) != 0)
            {
                return true;
            }
        }

        return false;
    }

    private static void StoreModifierPowers(ZDO zdo, ModifierMask mask, ModifierPowerDefinition powers)
    {
        foreach (ModifierSpec spec in ModifierSpecs)
        {
            bool active = HasModifier(mask, spec.Mask);
            if (spec.ProcChanceKey != null)
            {
                if (active)
                {
                    zdo.Set(spec.ProcChanceKey, ResolveStoredPlayerDebuffProcChance(spec.Mask, powers));
                }
                else
                {
                    zdo.RemoveFloat(spec.ProcChanceKey);
                }
            }

            if (active)
            {
                zdo.Set(spec.PowerKey, spec.ResolvePower(spec.GetPower(powers)));
            }
            else
            {
                zdo.RemoveFloat(spec.PowerKey);
            }
        }

        bool exposedActive = HasModifier(mask, ModifierMask.Exposed);
        SetActiveFloat(zdo, ExposedDurationKey, exposedActive, ResolvePlayerDebuffDuration(powers.ExposedDuration, PlayerDebuffDuration));
        bool weakenedActive = HasModifier(mask, ModifierMask.Weakened);
        SetActiveFloat(zdo, WeakenedDurationKey, weakenedActive, ResolvePlayerDebuffDuration(powers.WeakenedDuration, PlayerDebuffDuration));
        bool witheredActive = HasModifier(mask, ModifierMask.Withered);
        SetActiveFloat(zdo, WitheredDurationKey, witheredActive, ResolvePlayerDebuffDuration(powers.WitheredDuration, PlayerDebuffDuration));
        bool cripplingActive = HasModifier(mask, ModifierMask.Crippling);
        SetActiveFloat(zdo, CripplingJumpPowerKey, cripplingActive, Mathf.Clamp01(powers.CripplingJump ?? CripplingDefaultPower));
        SetActiveFloat(zdo, CripplingDurationKey, cripplingActive, ResolvePlayerDebuffDuration(powers.CripplingDuration, ControlDebuffDuration));
        bool disruptiveActive = HasModifier(mask, ModifierMask.Disruptive);
        SetActiveFloat(zdo, DisruptiveEitrPowerKey, disruptiveActive, Mathf.Clamp01(powers.DisruptiveEitr ?? DisruptiveDefaultPower));
        SetActiveFloat(zdo, DisruptiveDurationKey, disruptiveActive, ResolvePlayerDebuffDuration(powers.DisruptiveDuration, ControlDebuffDuration));
        bool adrenalineDrainActive = HasModifier(mask, ModifierMask.AdrenalineDrain);
        SetActiveFloat(zdo, AdrenalineDrainGainReductionKey, adrenalineDrainActive, Mathf.Clamp01(powers.AdrenalineDrainGainReduction ?? AdrenalineDrainDefaultGainReduction));
        SetActiveFloat(zdo, AdrenalineDrainDurationKey, adrenalineDrainActive, ResolvePlayerDebuffDuration(powers.AdrenalineDrainDuration, AdrenalineDrainDefaultDuration));
        bool corrosiveActive = HasModifier(mask, ModifierMask.Corrosive);
        SetActiveFloat(zdo, CorrosiveDurationKey, corrosiveActive, ResolvePlayerDebuffDuration(powers.CorrosiveDuration, PlayerDebuffDuration));
        bool toxicDeathActive = HasModifier(mask, ModifierMask.ToxicDeath);
        SetActiveFloat(zdo, ToxicDeathRadiusKey, toxicDeathActive, Mathf.Max(0f, powers.ToxicDeathRadius ?? ToxicDeathDefaultRadius));
        SetActiveString(
            zdo,
            ToxicDeathTriggerEffectKey,
            toxicDeathActive,
            ResolveTriggerEffect(powers.ToxicDeathTriggerEffect, ToxicDeathDefaultTriggerEffect));

        bool deathwardActive = HasModifier(mask, ModifierMask.Deathward);
        SetActiveFloat(zdo, DeathwardCooldownKey, deathwardActive, ResolveDeathwardCooldown(powers.DeathwardCooldown));
        SetActiveInt(zdo, DeathwardMaxActivationsKey, deathwardActive, ResolveDeathwardMaxActivations(powers.DeathwardMaxActivations));
        bool reapingActive = HasModifier(mask, ModifierMask.Reaping);
        SetActiveInt(zdo, ReapingHealMaxActivationsKey, reapingActive, Math.Max(1, powers.ReapingHealMaxActivations ?? ReapingDefaultHealMaxActivations));
        SetActiveFloat(zdo, ReapingMaxHealthPerKillKey, reapingActive, Mathf.Max(0f, powers.ReapingMaxHealthPerKill ?? ReapingDefaultMaxHealthPerKill));
        SetActiveFloat(zdo, ReapingMaxHealthCapKey, reapingActive, Mathf.Max(0f, powers.ReapingMaxHealthCap ?? ReapingDefaultMaxHealthCap));
        SetActiveFloat(zdo, ReapingDamagePerKillKey, reapingActive, Mathf.Max(0f, powers.ReapingDamagePerKill ?? ReapingDefaultDamagePerKill));
        SetActiveFloat(zdo, ReapingDamageCapKey, reapingActive, Mathf.Max(0f, powers.ReapingDamageCap ?? ReapingDefaultDamageCap));
        SetActiveFloat(zdo, ReapingScalePerKillKey, reapingActive, Mathf.Max(0f, powers.ReapingScalePerKill ?? ReapingDefaultScalePerKill));
        SetActiveFloat(zdo, ReapingScaleCapKey, reapingActive, Mathf.Max(0f, powers.ReapingScaleCap ?? ReapingDefaultScaleCap));
        bool blinkActive = HasModifier(mask, ModifierMask.Blink);
        SetActiveFloat(zdo, BlinkCooldownKey, blinkActive, ResolveBlinkCooldown(powers.BlinkCooldown));
        SetActiveFloat(zdo, BlinkMaxRangeKey, blinkActive, ResolveBlinkMaxRange(powers.BlinkMaxRange));
        SetActiveString(zdo, BlinkStartEffectKey, blinkActive, ResolveBlinkStartEffect(powers.BlinkStartEffect));

        bool knockbackActive = HasModifier(mask, ModifierMask.Knockback);
        SetActiveFloat(zdo, KnockbackCooldownKey, knockbackActive, ResolveKnockbackCooldown(powers.KnockbackCooldown));
        bool blamerActive = HasModifier(mask, ModifierMask.Blamer);
        SetActiveFloat(zdo, BlamerMaxKarmaGainKey, blamerActive, ResolveBlamerMaxKarmaGain(powers.BlamerMaxKarmaGain));
        SetActiveFloat(zdo, BlamerFleeHealthRatioKey, blamerActive, ResolveBlamerFleeHealthRatio(powers.BlamerFleeHealthRatio));
    }

    private static void SetActiveFloat(ZDO zdo, string key, bool active, float value)
    {
        if (active)
        {
            zdo.Set(key, value);
        }
        else
        {
            zdo.RemoveFloat(key);
        }
    }

    private static void SetActiveInt(ZDO zdo, string key, bool active, int value)
    {
        if (active)
        {
            zdo.Set(key, value);
        }
        else
        {
            zdo.RemoveInt(key);
        }
    }

    private static void SetActiveString(ZDO zdo, string key, bool active, string value)
    {
        // ZDO has no public RemoveString API. An empty value prevents stale modifier
        // configuration from surviving a mask change while keeping the write portable.
        zdo.Set(key, active ? value : "");
    }

    private static void CopyStoredModifierPowers(ZDO source, ZDO target)
    {
        ModifierMask mask = GetStoredModifierMask(source);
        foreach (ModifierSpec spec in ModifierSpecs)
        {
            bool active = HasModifier(mask, spec.Mask);
            if (spec.ProcChanceKey != null)
            {
                SetActiveFloat(
                    target,
                    spec.ProcChanceKey,
                    active,
                    source.GetFloat(spec.ProcChanceKey, 0f));
            }

            SetActiveFloat(target, spec.PowerKey, active, source.GetFloat(spec.PowerKey, 0f));
        }

        bool exposedActive = HasModifier(mask, ModifierMask.Exposed);
        SetActiveFloat(target, ExposedDurationKey, exposedActive, source.GetFloat(ExposedDurationKey, 0f));
        bool weakenedActive = HasModifier(mask, ModifierMask.Weakened);
        SetActiveFloat(target, WeakenedDurationKey, weakenedActive, source.GetFloat(WeakenedDurationKey, 0f));
        bool witheredActive = HasModifier(mask, ModifierMask.Withered);
        SetActiveFloat(target, WitheredDurationKey, witheredActive, source.GetFloat(WitheredDurationKey, 0f));
        bool cripplingActive = HasModifier(mask, ModifierMask.Crippling);
        SetActiveFloat(target, CripplingJumpPowerKey, cripplingActive, source.GetFloat(CripplingJumpPowerKey, 0f));
        SetActiveFloat(target, CripplingDurationKey, cripplingActive, source.GetFloat(CripplingDurationKey, 0f));
        bool disruptiveActive = HasModifier(mask, ModifierMask.Disruptive);
        SetActiveFloat(target, DisruptiveEitrPowerKey, disruptiveActive, source.GetFloat(DisruptiveEitrPowerKey, 0f));
        SetActiveFloat(target, DisruptiveDurationKey, disruptiveActive, source.GetFloat(DisruptiveDurationKey, 0f));
        bool adrenalineDrainActive = HasModifier(mask, ModifierMask.AdrenalineDrain);
        SetActiveFloat(target, AdrenalineDrainGainReductionKey, adrenalineDrainActive, source.GetFloat(AdrenalineDrainGainReductionKey, 0f));
        SetActiveFloat(target, AdrenalineDrainDurationKey, adrenalineDrainActive, source.GetFloat(AdrenalineDrainDurationKey, 0f));
        bool corrosiveActive = HasModifier(mask, ModifierMask.Corrosive);
        SetActiveFloat(target, CorrosiveDurationKey, corrosiveActive, source.GetFloat(CorrosiveDurationKey, 0f));
        bool toxicDeathActive = HasModifier(mask, ModifierMask.ToxicDeath);
        SetActiveFloat(target, ToxicDeathRadiusKey, toxicDeathActive, source.GetFloat(ToxicDeathRadiusKey, 0f));
        SetActiveString(
            target,
            ToxicDeathTriggerEffectKey,
            toxicDeathActive,
            source.GetString(ToxicDeathTriggerEffectKey, ""));

        bool deathwardActive = HasModifier(mask, ModifierMask.Deathward);
        SetActiveFloat(target, DeathwardCooldownKey, deathwardActive, source.GetFloat(DeathwardCooldownKey, 0f));
        SetActiveInt(target, DeathwardMaxActivationsKey, deathwardActive, source.GetInt(DeathwardMaxActivationsKey, 0));
        bool reapingActive = HasModifier(mask, ModifierMask.Reaping);
        SetActiveInt(target, ReapingHealMaxActivationsKey, reapingActive, source.GetInt(ReapingHealMaxActivationsKey, 0));
        SetActiveFloat(target, ReapingMaxHealthPerKillKey, reapingActive, source.GetFloat(ReapingMaxHealthPerKillKey, 0f));
        SetActiveFloat(target, ReapingMaxHealthCapKey, reapingActive, source.GetFloat(ReapingMaxHealthCapKey, 0f));
        SetActiveFloat(target, ReapingDamagePerKillKey, reapingActive, source.GetFloat(ReapingDamagePerKillKey, 0f));
        SetActiveFloat(target, ReapingDamageCapKey, reapingActive, source.GetFloat(ReapingDamageCapKey, 0f));
        SetActiveFloat(target, ReapingScalePerKillKey, reapingActive, source.GetFloat(ReapingScalePerKillKey, 0f));
        SetActiveFloat(target, ReapingScaleCapKey, reapingActive, source.GetFloat(ReapingScaleCapKey, 0f));
        bool blinkActive = HasModifier(mask, ModifierMask.Blink);
        SetActiveFloat(target, BlinkCooldownKey, blinkActive, source.GetFloat(BlinkCooldownKey, 0f));
        SetActiveFloat(target, BlinkMaxRangeKey, blinkActive, source.GetFloat(BlinkMaxRangeKey, 0f));
        SetActiveString(target, BlinkStartEffectKey, blinkActive, source.GetString(BlinkStartEffectKey, ""));

        bool knockbackActive = HasModifier(mask, ModifierMask.Knockback);
        SetActiveFloat(target, KnockbackCooldownKey, knockbackActive, source.GetFloat(KnockbackCooldownKey, 0f));
        bool blamerActive = HasModifier(mask, ModifierMask.Blamer);
        SetActiveFloat(target, BlamerMaxKarmaGainKey, blamerActive, source.GetFloat(BlamerMaxKarmaGainKey, 0f));
        SetActiveFloat(target, BlamerFleeHealthRatioKey, blamerActive, source.GetFloat(BlamerFleeHealthRatioKey, 0f));
    }

    private static float ClampPower(float? value, float fallback)
    {
        return Mathf.Clamp01(value ?? fallback);
    }

    private static float ClampUndodgeableDamageReduction(float? value)
    {
        float reduction = value ?? UndodgeableDefaultDamageReduction;
        return float.IsNaN(reduction) || float.IsInfinity(reduction)
            ? UndodgeableDefaultDamageReduction
            : Mathf.Clamp01(reduction);
    }

    private static float ClampChance(float? value)
    {
        return !value.HasValue || float.IsNaN(value.Value) ? 0f : Mathf.Clamp(value.Value, 0f, 100f);
    }

    private static float ResolvePlayerDebuffProcChance(float? value, float fallback = PlayerDebuffDefaultProcChance)
    {
        return Mathf.Clamp01(value ?? fallback);
    }

    private static float ResolveStoredPlayerDebuffProcChance(ModifierMask mask, ModifierPowerDefinition powers)
    {
        float? configured = mask switch
        {
            ModifierMask.Exposed => powers.ExposedProcChance,
            ModifierMask.Weakened => powers.WeakenedProcChance,
            ModifierMask.Withered => powers.WitheredProcChance,
            ModifierMask.Crippling => powers.CripplingProcChance,
            ModifierMask.Disruptive => powers.DisruptiveProcChance,
            ModifierMask.AdrenalineDrain => powers.AdrenalineDrainProcChance,
            ModifierMask.Corrosive => powers.CorrosiveProcChance,
            ModifierMask.Reflection => powers.ReflectionProcChance,
            _ => null
        };
        float fallback = mask == ModifierMask.Reflection ? ReflectionDefaultProcChance : PlayerDebuffDefaultProcChance;
        return ResolvePlayerDebuffProcChance(configured, fallback) * 100f;
    }

    private static float ResolvePlayerDebuffDuration(float? value, float fallback)
    {
        return Mathf.Max(0.1f, value ?? fallback);
    }

    private static string ResolveTriggerEffect(string? configured, string fallback)
    {
        string value = configured == null ? fallback : configured.Trim();
        return string.Equals(value, "none", StringComparison.OrdinalIgnoreCase) ? "" : value;
    }

    private static float ClampDeathwardHealth(float? value)
    {
        return Mathf.Clamp(value ?? DeathwardDefaultPower, 0.01f, 1f);
    }

    private static ModifierMask RollConfiguredMask(Character character, ModifierChanceDefinition chances)
    {
        ModifierMask mask = ModifierMask.None;
        foreach (ModifierGroup group in ModifierGroupOrder)
        {
            float total = 0f;
            foreach (ModifierSpec spec in ModifierSpecs)
            {
                if (spec.Group == group && IsModifierApplicable(character, spec))
                {
                    total += ClampChance(spec.GetChance(chances));
                }
            }

            if (total <= 0f)
            {
                continue;
            }

            float roll = UnityEngine.Random.Range(0f, total > 100f ? total : 100f);
            ModifierMask lastCandidate = ModifierMask.None;
            foreach (ModifierSpec spec in ModifierSpecs)
            {
                if (spec.Group != group || !IsModifierApplicable(character, spec))
                {
                    continue;
                }

                float chance = ClampChance(spec.GetChance(chances));
                if (chance <= 0f)
                {
                    continue;
                }

                lastCandidate = spec.Mask;
                if (roll < chance)
                {
                    mask |= spec.Mask;
                    lastCandidate = ModifierMask.None;
                    break;
                }

                roll -= chance;
            }

            // Float Random.Range can return its upper bound; a full group must still select one.
            if (total >= 100f && lastCandidate != ModifierMask.None)
            {
                mask |= lastCandidate;
            }
        }

        return mask;
    }

    private static bool IsModifierApplicable(Character character, ModifierSpec spec)
    {
        return spec.Mask switch
        {
            ModifierMask.Unflinching => character.m_staggerWhenBlocked || character.m_staggerDamageFactor > 0f,
            ModifierMask.Chameleon => HasEligibleChameleonType(character),
            ModifierMask.Blamer => !character.IsTamed() && character.GetBaseAI() is MonsterAI,
            _ => true
        };
    }

    internal static bool ApplyModifierChances(ModifierChanceDefinition chances, Dictionary<string, ModifierDefinition> modifiers)
    {
        bool has = false;
        foreach (ModifierSpec spec in ModifierSpecs)
        {
            if (!modifiers.TryGetValue(spec.Key, out ModifierDefinition definition) || !definition.Chance.HasValue)
            {
                continue;
            }

            spec.SetChance(chances, definition.Chance.Value);
            has = true;
        }

        return has;
    }

    internal static bool ApplyModifierPowers(ModifierPowerDefinition powers, Dictionary<string, ModifierDefinition> modifiers)
    {
        bool has = false;
        foreach (ModifierSpec spec in ModifierSpecs)
        {
            if (!modifiers.TryGetValue(spec.Key, out ModifierDefinition definition) || !definition.Power.HasValue)
            {
                continue;
            }

            spec.SetPower(powers, definition.Power.Value);
            has = true;
        }

        if (modifiers.TryGetValue("deathward", out ModifierDefinition deathward) && deathward.Cooldown.HasValue)
        {
            powers.DeathwardCooldown = Mathf.Max(0f, deathward.Cooldown.Value);
            has = true;
        }

        if (deathward?.MaxActivations.HasValue == true)
        {
            powers.DeathwardMaxActivations = ResolveDeathwardMaxActivations(deathward.MaxActivations);
            has = true;
        }

        if (modifiers.TryGetValue("exposed", out ModifierDefinition exposed))
        {
            if (exposed.ProcChance.HasValue) { powers.ExposedProcChance = Mathf.Clamp01(exposed.ProcChance.Value); has = true; }
            if (exposed.Duration.HasValue) { powers.ExposedDuration = ResolvePlayerDebuffDuration(exposed.Duration, PlayerDebuffDuration); has = true; }
        }

        if (modifiers.TryGetValue("weakened", out ModifierDefinition weakened))
        {
            if (weakened.ProcChance.HasValue) { powers.WeakenedProcChance = Mathf.Clamp01(weakened.ProcChance.Value); has = true; }
            if (weakened.Duration.HasValue) { powers.WeakenedDuration = ResolvePlayerDebuffDuration(weakened.Duration, PlayerDebuffDuration); has = true; }
        }

        if (modifiers.TryGetValue("withered", out ModifierDefinition withered))
        {
            if (withered.ProcChance.HasValue) { powers.WitheredProcChance = Mathf.Clamp01(withered.ProcChance.Value); has = true; }
            if (withered.Duration.HasValue) { powers.WitheredDuration = ResolvePlayerDebuffDuration(withered.Duration, PlayerDebuffDuration); has = true; }
        }

        if (modifiers.TryGetValue("crippling", out ModifierDefinition crippling))
        {
            if (crippling.SecondaryPower.HasValue) { powers.CripplingJump = Mathf.Clamp01(crippling.SecondaryPower.Value); has = true; }
            if (crippling.ProcChance.HasValue) { powers.CripplingProcChance = Mathf.Clamp01(crippling.ProcChance.Value); has = true; }
            if (crippling.Duration.HasValue) { powers.CripplingDuration = ResolvePlayerDebuffDuration(crippling.Duration, ControlDebuffDuration); has = true; }
        }

        if (modifiers.TryGetValue("disruptive", out ModifierDefinition disruptive))
        {
            if (disruptive.SecondaryPower.HasValue) { powers.DisruptiveEitr = Mathf.Clamp01(disruptive.SecondaryPower.Value); has = true; }
            if (disruptive.ProcChance.HasValue) { powers.DisruptiveProcChance = Mathf.Clamp01(disruptive.ProcChance.Value); has = true; }
            if (disruptive.Duration.HasValue) { powers.DisruptiveDuration = ResolvePlayerDebuffDuration(disruptive.Duration, ControlDebuffDuration); has = true; }
        }

        if (modifiers.TryGetValue("reflection", out ModifierDefinition reflection) && reflection.ProcChance.HasValue)
        {
            powers.ReflectionProcChance = ResolvePlayerDebuffProcChance(reflection.ProcChance, ReflectionDefaultProcChance);
            has = true;
        }

        if (modifiers.TryGetValue("adrenalineDrain", out ModifierDefinition adrenalineDrain))
        {
            if (adrenalineDrain.SecondaryPower.HasValue) { powers.AdrenalineDrainGainReduction = Mathf.Clamp01(adrenalineDrain.SecondaryPower.Value); has = true; }
            if (adrenalineDrain.ProcChance.HasValue) { powers.AdrenalineDrainProcChance = Mathf.Clamp01(adrenalineDrain.ProcChance.Value); has = true; }
            if (adrenalineDrain.Duration.HasValue) { powers.AdrenalineDrainDuration = ResolvePlayerDebuffDuration(adrenalineDrain.Duration, AdrenalineDrainDefaultDuration); has = true; }
        }

        if (modifiers.TryGetValue("corrosive", out ModifierDefinition corrosive))
        {
            if (corrosive.ProcChance.HasValue) { powers.CorrosiveProcChance = Mathf.Clamp01(corrosive.ProcChance.Value); has = true; }
            if (corrosive.Duration.HasValue) { powers.CorrosiveDuration = ResolvePlayerDebuffDuration(corrosive.Duration, PlayerDebuffDuration); has = true; }
        }

        if (modifiers.TryGetValue("toxicDeath", out ModifierDefinition toxicDeath))
        {
            if (toxicDeath.Radius.HasValue) { powers.ToxicDeathRadius = Mathf.Max(0f, toxicDeath.Radius.Value); has = true; }
            if (toxicDeath.TriggerEffect != null) { powers.ToxicDeathTriggerEffect = toxicDeath.TriggerEffect; has = true; }
        }

        if (modifiers.TryGetValue("reaping", out ModifierDefinition reaping))
        {
            if (reaping.ReapingHealMaxActivations.HasValue)
            {
                powers.ReapingHealMaxActivations = Math.Max(1, reaping.ReapingHealMaxActivations.Value);
                has = true;
            }

            if (reaping.ReapingMaxHealthPerKill.HasValue)
            {
                powers.ReapingMaxHealthPerKill = Mathf.Max(0f, reaping.ReapingMaxHealthPerKill.Value);
                has = true;
            }

            if (reaping.ReapingMaxHealthCap.HasValue)
            {
                powers.ReapingMaxHealthCap = Mathf.Max(0f, reaping.ReapingMaxHealthCap.Value);
                has = true;
            }

            if (reaping.ReapingDamagePerKill.HasValue)
            {
                powers.ReapingDamagePerKill = Mathf.Max(0f, reaping.ReapingDamagePerKill.Value);
                has = true;
            }

            if (reaping.ReapingDamageCap.HasValue)
            {
                powers.ReapingDamageCap = Mathf.Max(0f, reaping.ReapingDamageCap.Value);
                has = true;
            }

            if (reaping.ReapingScalePerKill.HasValue)
            {
                powers.ReapingScalePerKill = Mathf.Max(0f, reaping.ReapingScalePerKill.Value);
                has = true;
            }

            if (reaping.ReapingScaleCap.HasValue)
            {
                powers.ReapingScaleCap = Mathf.Max(0f, reaping.ReapingScaleCap.Value);
                has = true;
            }
        }

        if (modifiers.TryGetValue("blink", out ModifierDefinition blink) && blink.Cooldown.HasValue)
        {
            powers.BlinkCooldown = Mathf.Max(0f, blink.Cooldown.Value);
            has = true;
        }

        if (blink?.MaxRange.HasValue == true)
        {
            powers.BlinkMaxRange = Mathf.Max(0f, blink.MaxRange.Value);
            has = true;
        }

        if (blink?.StartEffect != null)
        {
            powers.BlinkStartEffect = blink.StartEffect;
            has = true;
        }

        if (modifiers.TryGetValue("juggernaut", out ModifierDefinition knockback) && knockback.Cooldown.HasValue)
        {
            powers.KnockbackCooldown = ResolveKnockbackCooldown(knockback.Cooldown);
            has = true;
        }

        if (modifiers.TryGetValue("blamer", out ModifierDefinition blamer))
        {
            if (blamer.MaxKarmaGain.HasValue)
            {
                powers.BlamerMaxKarmaGain = ResolveBlamerMaxKarmaGain(blamer.MaxKarmaGain);
                has = true;
            }

            if (blamer.FleeHealthRatio.HasValue)
            {
                powers.BlamerFleeHealthRatio = ResolveBlamerFleeHealthRatio(blamer.FleeHealthRatio);
                has = true;
            }
        }

        return has;
    }

    internal static RpcDamageScopeState BeginRpcDamageScope(Character target, HitData hit)
    {
        RpcDamageContext previous = CurrentRpcDamageContext;
        CurrentRpcDamageContext = new RpcDamageContext
        {
            Target = target,
            Hit = hit
        };
        try
        {
            return new RpcDamageScopeState(previous, BeginChameleonDamageOverride(target));
        }
        catch
        {
            CurrentRpcDamageContext = previous;
            throw;
        }
    }

    internal static void EndRpcDamageScope(RpcDamageScopeState state)
    {
        if (!state.Changed)
        {
            return;
        }

        try
        {
            EndChameleonDamageOverride(state.Chameleon);
        }
        finally
        {
            CurrentRpcDamageContext = state.Previous;
        }
    }

    internal static void CapturePostMitigationDelayedDamage(Character target, HitData hit)
    {
        if (target == null ||
            hit == null ||
            target is not Player ||
            !ReferenceEquals(CurrentRpcDamageContext.Target, target) ||
            !ReferenceEquals(CurrentRpcDamageContext.Hit, hit))
        {
            return;
        }

        // Vanilla moves these channels into status-effect pools and zeroes them before ApplyDamage.
        float poison = SanitizePositiveDamage(hit.m_damage.m_poison);
        float fire = SanitizePositiveDamage(hit.m_damage.m_fire);
        float spirit = SanitizePositiveDamage(hit.m_damage.m_spirit);
        CurrentRpcDamageContext.ResolvedDelayedDamage = SanitizePositiveDamage(poison + fire + spirit);
        CurrentRpcDamageContext.DelayedDamageCaptured = true;
    }

    internal static ChameleonDamageOverrideState BeginChameleonDamageOverride(Character target)
    {
        if (!CreatureLevelManager.IsLevelSystemEnabled() ||
            target == null ||
            target.IsPlayer() ||
            !CreatureLevelManager.AllowsModifierEffects(target) ||
            target.GetBaseAI() is not BaseAI baseAI ||
            !baseAI.IsAlerted() ||
            !TryGetZdo(target, out ZDO zdo) ||
            !HasModifier(zdo, ModifierMask.Chameleon))
        {
            return default;
        }

        ChameleonDamageType type = GetChameleonDamageType(zdo);
        if (type == ChameleonDamageType.None ||
            GetChameleonDamageModifier(target.m_damageModifiers, type) == HitData.DamageModifier.Immune)
        {
            return default;
        }

        HitData.DamageModifiers original = target.m_damageModifiers;
        HitData.DamageModifiers overridden = original;
        SetChameleonDamageModifier(ref overridden, type, HitData.DamageModifier.Immune);
        target.m_damageModifiers = overridden;
        return new ChameleonDamageOverrideState(target, original, type);
    }

    internal static void EndChameleonDamageOverride(ChameleonDamageOverrideState state)
    {
        if (!state.IsActive || state.Target == null)
        {
            return;
        }

        HitData.DamageModifiers current = state.Target.m_damageModifiers;
        SetChameleonDamageModifier(
            ref current,
            state.DamageType,
            GetChameleonDamageModifier(state.Original, state.DamageType));
        state.Target.m_damageModifiers = current;
    }

    internal static VortexProjectileImpactScopeState BeginVortexProjectileImpact(
        Projectile projectile,
        Collider? collider,
        bool water)
    {
        VortexProjectileImpactContext? previous = CurrentVortexProjectileImpact;
        CurrentVortexProjectileImpact = null;

        if (!CreatureLevelManager.IsLevelSystemEnabled() ||
            projectile == null ||
            collider == null ||
            water ||
            projectile.m_aoe > 0f)
        {
            return new VortexProjectileImpactScopeState(null, previous, changed: true);
        }

        GameObject hitObject = Projectile.FindHitObject(collider);
        Character? target = hitObject != null
            ? hitObject.GetComponent<Character>() ?? hitObject.GetComponentInParent<Character>()
            : null;
        if (target == null || target.IsPlayer())
        {
            return new VortexProjectileImpactScopeState(null, previous, changed: true);
        }

        VortexProjectileImpactContext context = new(projectile, target, projectile.m_hitEffects);
        CurrentVortexProjectileImpact = context;
        return new VortexProjectileImpactScopeState(context, previous, changed: true);
    }

    internal static void EndVortexProjectileImpact(ref VortexProjectileImpactScopeState state)
    {
        if (!state.Changed)
        {
            state = default;
            return;
        }

        VortexProjectileImpactContext? context = state.Current;
        VortexProjectileImpactContext? previous = state.Previous;
        state = default;

        if (context != null && context.HitEffectsSuppressed && context.Projectile != null)
        {
            context.Projectile.m_hitEffects = context.OriginalHitEffects;
        }

        if (ReferenceEquals(CurrentVortexProjectileImpact, context))
        {
            CurrentVortexProjectileImpact = previous;
        }
    }

    internal static VortexDirectProjectileDamageState PrepareVortexDirectProjectileDamage(
        Character target,
        HitData hit)
    {
        VortexProjectileImpactContext? context = CurrentVortexProjectileImpact;
        if (context == null ||
            context.DecisionWritten ||
            target == null ||
            hit == null ||
            !ReferenceEquals(context.Target, target) ||
            !IsProjectileHit(hit) ||
            !TryGetModifierPower(target, ModifierMask.Vortex, VortexPowerKey, VortexDefaultPower, out float chance))
        {
            return default;
        }

        if (!EnsureVortexHitTypeEncodingSupported())
        {
            return default;
        }

        bool procced = chance > 0f && UnityEngine.Random.Range(0f, 1f) < Mathf.Clamp01(chance);
        if (!TryEncodeVortexHitType(hit.m_hitType, procced, out HitData.HitType encodedHitType))
        {
            return default;
        }

        context.DecisionWritten = true;
        HitData.HitType originalHitType = hit.m_hitType;
        hit.m_hitType = encodedHitType;

        if (procced)
        {
            NeutralizeVortexProjectileHit(hit);
            context.Projectile.m_hitEffects = SuppressedProjectileHitEffects;
            context.HitEffectsSuppressed = true;
        }

        return new VortexDirectProjectileDamageState(hit, originalHitType);
    }

    internal static void RestoreVortexDirectProjectileDamage(ref VortexDirectProjectileDamageState state)
    {
        if (!state.IsActive)
        {
            state = default;
            return;
        }

        HitData hit = state.Hit!;
        HitData.HitType originalHitType = state.OriginalHitType;
        state = default;
        hit.m_hitType = originalHitType;
    }

    internal static void CaptureVortexProjectileDecision(HitData hit)
    {
        if (hit == null)
        {
            return;
        }

        if (!EnsureVortexHitTypeEncodingSupported() ||
            !TryDecodeVortexHitType(hit.m_hitType, out HitData.HitType originalHitType, out bool procced))
        {
            return;
        }

        hit.m_hitType = originalHitType;
        PendingVortexProjectileDecisions[hit] = procced;
    }

    private static bool EnsureVortexHitTypeEncodingSupported()
    {
        if (VortexHitTypeEncodingState != 0)
        {
            return VortexHitTypeEncodingState > 0;
        }

        bool supported;
        try
        {
            supported = Enum.GetValues(typeof(HitData.HitType))
                .Cast<HitData.HitType>()
                .All(value =>
                {
                    int raw = Convert.ToInt32(value, CultureInfo.InvariantCulture);
                    return raw >= 0 && raw <= VortexOriginalHitTypeMask;
                });
        }
        catch
        {
            supported = false;
        }

        VortexHitTypeEncodingState = supported ? 1 : -1;
        if (!supported)
        {
            CreatureManagerPlugin.Log.LogError(
                "Vortex projectile pre-resolution is disabled because HitData.HitType no longer fits in the reserved 6-bit transport range.");
        }

        return supported;
    }

    private static bool TryEncodeVortexHitType(
        HitData.HitType original,
        bool procced,
        out HitData.HitType encoded)
    {
        int raw = Convert.ToInt32(original, CultureInfo.InvariantCulture);
        if (raw < 0 || raw > VortexOriginalHitTypeMask)
        {
            encoded = original;
            return false;
        }

        encoded = (HitData.HitType)(raw |
                                    VortexPreResolvedFlag |
                                    (procced ? VortexProcFlag : 0));
        return true;
    }

    private static bool TryDecodeVortexHitType(
        HitData.HitType encoded,
        out HitData.HitType original,
        out bool procced)
    {
        int raw = Convert.ToInt32(encoded, CultureInfo.InvariantCulture);
        if ((raw & VortexPreResolvedFlag) == 0 ||
            (raw & ~(VortexPreResolvedFlag | VortexProcFlag | VortexOriginalHitTypeMask)) != 0)
        {
            original = encoded;
            procced = false;
            return false;
        }

        original = (HitData.HitType)(raw & VortexOriginalHitTypeMask);
        procced = (raw & VortexProcFlag) != 0;
        return true;
    }

    internal static bool ApplyDamageModifiers(Character target, HitData hit)
    {
        if (!CreatureLevelManager.IsLevelSystemEnabled() || target == null || hit == null)
        {
            return true;
        }

        ZNetView? targetView = target.m_nview;
        if (targetView != null && targetView.IsValid() && !targetView.IsOwner())
        {
            return true;
        }

        DamageSnapshot incoming = DamageSnapshot.From(hit.m_damage);
        Character attacker = hit.GetAttacker();
        bool targetHasModifiers = HasAnyActiveModifier(target);
        if (targetHasModifiers && TryApplyVortexProjectileIgnore(target, hit))
        {
            return false;
        }

        if (targetHasModifiers)
        {
            ApplyAdaptiveReduction(target, hit, incoming);
        }

        bool attackerHasModifiers = attacker != null && HasAnyActiveModifier(attacker);

        if (attacker != null && CreatureLevelManager.TryGetDamageMultiplier(attacker, out float damageMultiplier))
        {
            hit.m_damage.Modify(damageMultiplier);
        }

        if (attackerHasModifiers && TryGetReapingDamageBonus(attacker!, out float reapingDamageBonus))
        {
            hit.m_damage.Modify(1f + reapingDamageBonus);
        }

        if (attackerHasModifiers && TryGetEnragedBonus(attacker!, out float bonus))
        {
            hit.m_damage.Modify(1f + bonus);
        }

        if (attackerHasModifiers)
        {
            ApplyOutgoingModifierEffects(attacker!, target, hit);
        }

        if (attacker is Player playerAttacker)
        {
            ApplyPlayerOutgoingDebuffs(playerAttacker, hit);
        }

        if (target is Player playerTarget)
        {
            ApplyPlayerIncomingDebuffs(playerTarget, hit);
        }

        if (targetHasModifiers && TryGetArmoredReduction(target, out float reduction))
        {
            hit.m_damage.Modify(1f - reduction);
        }

        if (targetHasModifiers)
        {
            if (hit.GetTotalDamage() > 0.1f)
            {
                QueueReflectionDamage(target, attacker, hit);
                StoreAdaptiveMemory(target, incoming);
            }

            ApplyIncomingControlImmunities(target, hit);
        }

        return true;
    }

    private static void ApplyIncomingControlImmunities(Character target, HitData hit)
    {
        if (!TryGetZdo(target, out ZDO zdo))
        {
            return;
        }

        ModifierMask mask = GetStoredModifierMask(zdo);
        if (HasModifier(mask, ModifierMask.Unflinching))
        {
            hit.m_staggerMultiplier = 0f;
        }

        if (HasModifier(mask, ModifierMask.Knockback))
        {
            hit.m_pushForce = 0f;
        }
    }

    private static void QueueReflectionDamage(Character target, Character? attacker, HitData hit)
    {
        if (attacker == null ||
            IsProjectileHit(hit) ||
            !IsHostileAttacker(target, attacker) ||
            !TryGetProcModifierState(target, ModifierMask.Reflection, ReflectionChanceKey, out ZDO zdo, out float procChance) ||
            UnityEngine.Random.Range(0f, 100f) >= procChance)
        {
            return;
        }

        float power = Mathf.Clamp01(zdo.GetFloat(ReflectionPowerKey, ReflectionDefaultPower));
        if (power <= 0f)
        {
            return;
        }

        PendingReflectionDamage[hit] = new ReflectionDamageContext(attacker, target, power, hit.m_point);
    }

    private static bool IsHostileAttacker(Character target, Character attacker)
    {
        if (target == null || attacker == null || target == attacker)
        {
            return false;
        }

        try
        {
            return BaseAI.IsEnemy(target, attacker) || BaseAI.IsEnemy(attacker, target);
        }
        catch
        {
            return attacker.IsPlayer();
        }
    }

    private static bool TryApplyVortexProjectileIgnore(Character target, HitData hit)
    {
        if (!IsProjectileHit(hit))
        {
            return false;
        }

        bool hasPreResolvedDecision = PendingVortexProjectileDecisions.TryGetValue(hit, out bool preResolvedProc);
        if (hasPreResolvedDecision)
        {
            PendingVortexProjectileDecisions.Remove(hit);
        }

        if (!TryGetModifierPower(target, ModifierMask.Vortex, VortexPowerKey, VortexDefaultPower, out float chance))
        {
            return false;
        }

        bool procced = hasPreResolvedDecision
            ? preResolvedProc
            : chance > 0f && UnityEngine.Random.Range(0f, 1f) < Mathf.Clamp01(chance);
        if (!procced)
        {
            return false;
        }

        PlayVortexHitEffects(target, hit.m_point);
        NeutralizeVortexProjectileHit(hit);
        return true;
    }

    private static void NeutralizeVortexProjectileHit(HitData hit)
    {
        hit.m_damage.Modify(0f);
        hit.m_pushForce = 0f;
        hit.m_staggerMultiplier = 0f;
        hit.m_statusEffectHash = 0;
        hit.m_backstabBonus = 1f;
        hit.m_blockable = false;
        hit.m_healthReturn = 0f;
        hit.m_attacker = ZDOID.None;
    }

    private static void PlayVortexHitEffects(Character target, Vector3 hitPoint)
    {
        Vector3 position = hitPoint.sqrMagnitude > 0.001f ? hitPoint : target.GetCenterPoint();
        ZDOID targetId = target.GetZDOID();

        if (ZNet.instance != null && ZRoutedRpc.instance != null)
        {
            if (ZNet.instance.IsServer())
            {
                BroadcastVortexHitEffects(position, targetId);
                return;
            }

            ZNetView? targetView = target.m_nview;
            if (targetView != null && targetView.IsValid() && targetView.IsOwner())
            {
                try
                {
                    targetView.InvokeRPC(
                        ZRoutedRpc.instance.GetServerPeerID(),
                        VortexHitEffectRequestRpc,
                        position);
                    return;
                }
                catch (Exception ex)
                {
                    CreatureManagerPlugin.Log.LogWarning($"Failed to request Vortex hit effects: {ex.Message}");
                }
            }
        }

        PlayVortexHitEffectsLocal(position, targetId, target.transform);
    }

    private static void RPC_VortexHitEffectRequest(Character target, long sender, Vector3 position)
    {
        if (ZNet.instance == null ||
            !ZNet.instance.IsServer() ||
            ZRoutedRpc.instance == null ||
            target == null ||
            target.IsDead() ||
            !IsFinite(position) ||
            !TryGetZdo(target, out ZDO zdo) ||
            zdo.GetOwner() != sender ||
            !TryGetModifierPower(target, ModifierMask.Vortex, VortexPowerKey, VortexDefaultPower, out _))
        {
            return;
        }

        Vector3 offset = position - target.GetCenterPoint();
        if (offset.sqrMagnitude > VortexEffectRequestValidationRange * VortexEffectRequestValidationRange)
        {
            return;
        }

        ZDOID targetId = target.GetZDOID();
        if (targetId == ZDOID.None)
        {
            return;
        }

        float now = Time.unscaledTime;
        if (ServerVortexEffectNextAllowedTimes.TryGetValue(targetId, out float nextAllowedTime) &&
            now < nextAllowedTime)
        {
            return;
        }

        ServerVortexEffectNextAllowedTimes[targetId] = now + VortexEffectServerMinimumRequestInterval;
        BroadcastVortexHitEffects(position, targetId);
    }

    private static void BroadcastVortexHitEffects(Vector3 position, ZDOID targetId)
    {
        if (ZRoutedRpc.instance == null)
        {
            return;
        }

        ZPackage package = new();
        package.Write(position);
        package.Write(targetId);
        ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, VortexHitEffectRpc, package);
    }

    private static void RPC_VortexHitEffect(long sender, ZPackage package)
    {
        if (!IsTrustedServerRpc(sender))
        {
            return;
        }

        try
        {
            Vector3 position = package.ReadVector3();
            ZDOID targetId = package.ReadZDOID();
            PlayVortexHitEffectsLocal(position, targetId);
        }
        catch (Exception ex)
        {
            CreatureManagerPlugin.Log.LogWarning($"Failed to play Vortex hit effects: {ex.Message}");
        }
    }

    private static void RPC_ReflectionEffect(long sender, ZPackage package)
    {
        if (!IsTrustedServerRpc(sender))
        {
            return;
        }

        try
        {
            Vector3 position = package.ReadVector3();
            Vector3 direction = package.ReadVector3();
            PlayReflectionEffectsLocal(position, direction);
        }
        catch (Exception ex)
        {
            CreatureManagerPlugin.Log.LogWarning($"Failed to play Reflection effects: {ex.Message}");
        }
    }

    private static void RPC_BlinkEffect(long sender, ZPackage package)
    {
        if (!IsTrustedServerRpc(sender))
        {
            return;
        }

        try
        {
            Vector3 position = package.ReadVector3();
            string prefabName = package.ReadString();
            PlayBlinkStartEffectLocal(position, prefabName);
        }
        catch (Exception ex)
        {
            CreatureManagerPlugin.Log.LogWarning($"Failed to play Blink start effect: {ex.Message}");
        }
    }

    private static void RPC_DeathwardEffect(long sender, ZPackage package)
    {
        if (!IsTrustedServerRpc(sender))
        {
            return;
        }

        try
        {
            Vector3 position = package.ReadVector3();
            PlayDeathwardTriggerEffectsLocal(position);
        }
        catch (Exception ex)
        {
            CreatureManagerPlugin.Log.LogWarning($"Failed to play Deathward trigger effects: {ex.Message}");
        }
    }

    private static void RPC_ReapingFeedback(long sender, ZPackage package)
    {
        if (!IsTrustedServerRpc(sender))
        {
            return;
        }

        try
        {
            Vector3 position = package.ReadVector3();
            ZDOID characterId = package.ReadZDOID();
            bool scaleChanged = package.ReadBool();
            Vector3 scale = package.ReadVector3();
            ApplyReapingFeedbackLocal(position, characterId, scaleChanged, scale);
        }
        catch (Exception ex)
        {
            CreatureManagerPlugin.Log.LogWarning($"Failed to apply Reaping feedback: {ex.Message}");
        }
    }

    private static void PlayDeathwardTriggerEffects(Vector3 position)
    {
        if (ZNet.instance != null && ZNet.instance.IsServer() && ZRoutedRpc.instance != null)
        {
            ZPackage package = new();
            package.Write(position);
            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, DeathwardEffectRpc, package);
            return;
        }

        PlayDeathwardTriggerEffectsLocal(position);
    }

    private static void BroadcastReapingFeedback(Character character, bool scaleChanged)
    {
        Vector3 position = character.GetCenterPoint();
        ZDOID characterId = character.GetZDOID();
        Vector3 scale = character.transform.localScale;
        if (ZNet.instance != null && ZNet.instance.IsServer() && ZRoutedRpc.instance != null)
        {
            ZPackage package = new();
            package.Write(position);
            package.Write(characterId);
            package.Write(scaleChanged);
            package.Write(scale);
            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, ReapingFeedbackRpc, package);
            return;
        }

        ApplyReapingFeedbackLocal(position, characterId, scaleChanged, scale);
    }

    private static void PlayBlinkStartEffect(Vector3 position, string prefabName)
    {
        if (prefabName.Length == 0)
        {
            return;
        }

        if (ZNet.instance != null && ZNet.instance.IsServer() && ZRoutedRpc.instance != null)
        {
            ZPackage package = new();
            package.Write(position);
            package.Write(prefabName);
            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, BlinkEffectRpc, package);
            return;
        }

        PlayBlinkStartEffectLocal(position, prefabName);
    }

    private static bool IsTrustedServerRpc(long sender)
    {
        return ZRoutedRpc.instance != null &&
               sender == ZRoutedRpc.instance.GetServerPeerID();
    }

    private static void PlayVortexHitEffectsLocal(Vector3 position, ZDOID targetId, Transform? knownTarget = null)
    {
        if (ZNetScene.instance == null || !IsEffectWithinPlaybackRange(position))
        {
            return;
        }

        Transform? parent = knownTarget;
        if (parent == null && targetId != ZDOID.None)
        {
            GameObject instance = ZNetScene.instance.FindInstance(targetId);
            parent = instance != null ? instance.transform : null;
        }

        foreach (string prefabName in VortexHitEffectPrefabNames)
        {
            GameObject prefab = ZNetScene.instance.GetPrefab(prefabName);
            if (prefab != null)
            {
                GameObject effect = UnityEngine.Object.Instantiate(prefab, position, Quaternion.identity);
                if (parent != null)
                {
                    effect.transform.SetParent(parent, true);
                }
            }
        }
    }

    private static void PlayReflectionEffects(Character source, Character target, Vector3 hitPoint)
    {
        Vector3 position = hitPoint.sqrMagnitude > 0.001f ? hitPoint : source.GetCenterPoint();
        Vector3 direction = target.GetCenterPoint() - position;
        if (direction.sqrMagnitude <= 0.001f)
        {
            direction = source.transform.forward;
        }

        if (ZNet.instance != null && ZNet.instance.IsServer() && ZRoutedRpc.instance != null)
        {
            ZPackage package = new();
            package.Write(position);
            package.Write(direction);
            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, ReflectionEffectRpc, package);
            return;
        }

        PlayReflectionEffectsLocal(position, direction);
    }

    private static void PlayReflectionEffectsLocal(Vector3 position, Vector3 direction)
    {
        if (ZNetScene.instance == null || !IsEffectWithinPlaybackRange(position))
        {
            return;
        }

        Quaternion rotation = direction.sqrMagnitude > 0.001f
            ? Quaternion.LookRotation(direction.normalized, Vector3.up)
            : Quaternion.identity;
        foreach (string prefabName in ReflectionEffectPrefabNames)
        {
            GameObject prefab = ZNetScene.instance.GetPrefab(prefabName);
            if (prefab != null)
            {
                UnityEngine.Object.Instantiate(prefab, position, rotation);
            }
        }
    }

    private static void PlayBlinkStartEffectLocal(Vector3 position, string prefabName)
    {
        if (prefabName.Length == 0 || ZNetScene.instance == null || !IsEffectWithinPlaybackRange(position))
        {
            return;
        }

        GameObject prefab = ZNetScene.instance.GetPrefab(prefabName);
        if (prefab != null)
        {
            UnityEngine.Object.Instantiate(prefab, position, Quaternion.identity);
        }
    }

    private static void PlayDeathwardTriggerEffectsLocal(Vector3 position)
    {
        PlayConfiguredEffectsLocal(position, DeathwardTriggerEffectPrefab, MissingDeathwardEffectPrefabs, "Deathward");
    }

    private static void ApplyReapingFeedbackLocal(
        Vector3 position,
        ZDOID characterId,
        bool scaleChanged,
        Vector3 scale)
    {
        if (scaleChanged && ZNetScene.instance != null)
        {
            GameObject instance = ZNetScene.instance.FindInstance(characterId);
            if (instance != null && scale != Vector3.zero)
            {
                instance.transform.localScale = scale;
                ScheduleReapingPhysicsSync();
            }
        }

        PlayConfiguredEffectsLocal(position, ReapingTriggerEffectPrefab, MissingReapingEffectPrefabs, "Reaping");
    }

    private static void ScheduleReapingPhysicsSync()
    {
        if (PendingReapingPhysicsSync != null)
        {
            return;
        }

        ZNetScene? host = ZNetScene.instance;
        if (host == null)
        {
            Physics.SyncTransforms();
            return;
        }

        ReapingPhysicsSyncHost = host;
        PendingReapingPhysicsSync = host.StartCoroutine(FlushReapingPhysicsTransformsNextFrame());
        if (PendingReapingPhysicsSync == null)
        {
            ReapingPhysicsSyncHost = null;
            Physics.SyncTransforms();
        }
    }

    private static IEnumerator FlushReapingPhysicsTransformsNextFrame()
    {
        yield return null;
        PendingReapingPhysicsSync = null;
        ReapingPhysicsSyncHost = null;
        Physics.SyncTransforms();
    }

    private static void CancelPendingReapingPhysicsSync()
    {
        Coroutine? pending = PendingReapingPhysicsSync;
        ZNetScene? host = ReapingPhysicsSyncHost;
        PendingReapingPhysicsSync = null;
        ReapingPhysicsSyncHost = null;
        if (pending != null && host != null)
        {
            host.StopCoroutine(pending);
        }
    }

    private static void PlayConfiguredEffectsLocal(
        Vector3 position,
        string prefabNames,
        HashSet<string> missingPrefabs,
        string effectOwner)
    {
        if (prefabNames.Length == 0 ||
            string.Equals(prefabNames, "none", StringComparison.OrdinalIgnoreCase) ||
            ZNetScene.instance == null ||
            !IsEffectWithinPlaybackRange(position))
        {
            return;
        }

        HashSet<string> played = new(StringComparer.OrdinalIgnoreCase);
        foreach (string token in prefabNames.Split(','))
        {
            string prefabName = token.Trim();
            if (prefabName.Length == 0 ||
                string.Equals(prefabName, "none", StringComparison.OrdinalIgnoreCase) ||
                !played.Add(prefabName))
            {
                continue;
            }

            GameObject prefab = ZNetScene.instance.GetPrefab(prefabName);
            if (prefab != null)
            {
                UnityEngine.Object.Instantiate(prefab, position, Quaternion.identity);
                continue;
            }

            if (missingPrefabs.Add(prefabName))
            {
                CreatureManagerPlugin.Log.LogWarning($"{effectOwner} trigger effect prefab '{prefabName}' was not found.");
            }
        }
    }

    private static bool IsEffectWithinPlaybackRange(Vector3 position)
    {
        Player? player = Player.m_localPlayer;
        if (player == null)
        {
            return false;
        }

        Vector3 difference = player.transform.position - position;
        return difference.sqrMagnitude <= EffectPlaybackRange * EffectPlaybackRange;
    }

    private static void ApplyAdaptiveReduction(Character target, HitData hit, DamageSnapshot incoming)
    {
        AdaptiveDamageType type = incoming.DominantType();
        if (type == AdaptiveDamageType.None ||
            !TryGetModifierPower(target, ModifierMask.Adaptive, AdaptivePowerKey, AdaptiveDefaultPower, out float power) ||
            !TryGetZdo(target, out ZDO zdo) ||
            zdo.GetFloat(AdaptiveUntilKey, 0f) <= GetNetworkTimeSeconds() ||
            zdo.GetInt(AdaptiveTypeKey, 0) != (int)type)
        {
            return;
        }

        ScaleDamageType(ref hit.m_damage, type, 1f - power);
    }

    private static void StoreAdaptiveMemory(Character target, DamageSnapshot incoming)
    {
        AdaptiveDamageType type = incoming.DominantType();
        if (type == AdaptiveDamageType.None ||
            !TryGetModifierPower(target, ModifierMask.Adaptive, AdaptivePowerKey, AdaptiveDefaultPower, out _) ||
            !TryGetZdo(target, out ZDO zdo))
        {
            return;
        }

        float now = GetNetworkTimeSeconds();
        if (zdo.GetInt(AdaptiveTypeKey, 0) != (int)AdaptiveDamageType.None &&
            zdo.GetFloat(AdaptiveUntilKey, 0f) > now)
        {
            return;
        }

        zdo.Set(AdaptiveTypeKey, (int)type);
        zdo.Set(AdaptiveUntilKey, now + AdaptiveDuration);
    }

    private static void ScaleDamageType(ref HitData.DamageTypes damage, AdaptiveDamageType type, float multiplier)
    {
        multiplier = Mathf.Clamp01(multiplier);
        switch (type)
        {
            case AdaptiveDamageType.Physical:
                damage.m_damage *= multiplier;
                break;
            case AdaptiveDamageType.Blunt:
                damage.m_blunt *= multiplier;
                break;
            case AdaptiveDamageType.Slash:
                damage.m_slash *= multiplier;
                break;
            case AdaptiveDamageType.Pierce:
                damage.m_pierce *= multiplier;
                break;
            case AdaptiveDamageType.Chop:
                damage.m_chop *= multiplier;
                break;
            case AdaptiveDamageType.Pickaxe:
                damage.m_pickaxe *= multiplier;
                break;
            case AdaptiveDamageType.Fire:
                damage.m_fire *= multiplier;
                break;
            case AdaptiveDamageType.Frost:
                damage.m_frost *= multiplier;
                break;
            case AdaptiveDamageType.Lightning:
                damage.m_lightning *= multiplier;
                break;
            case AdaptiveDamageType.Poison:
                damage.m_poison *= multiplier;
                break;
            case AdaptiveDamageType.Spirit:
                damage.m_spirit *= multiplier;
                break;
        }
    }

    private static bool IsProjectileHit(HitData hit)
    {
        return HitRangedField?.GetValue(hit) is bool ranged && ranged;
    }

    private static void RPC_ReflectionDamageRequest(
        Character source,
        long sender,
        long requestId,
        ZDOID targetId,
        float amount)
    {
        if (ZNet.instance == null ||
            !ZNet.instance.IsServer() ||
            ZRoutedRpc.instance == null ||
            source == null ||
            source.IsPlayer() ||
            source.m_nview == null ||
            !source.m_nview.IsValid() ||
            requestId <= 0 ||
            targetId == ZDOID.None ||
            amount <= 0f ||
            float.IsNaN(amount) ||
            float.IsInfinity(amount) ||
            !TryGetZdo(source, out ZDO sourceZdo) ||
            sourceZdo.GetOwner() != sender ||
            !TryGetModifierPower(
                source,
                ModifierMask.Reflection,
                ReflectionPowerKey,
                ReflectionDefaultPower,
                out float reflectionPower) ||
            !TryFindCharacter(targetId, out Character target) ||
            target == source ||
            target.IsDead() ||
            !IsHostileAttacker(source, target) ||
            Vector3.Distance(source.transform.position, target.transform.position) > ModifierRequestValidationRange)
        {
            return;
        }

        ZDOID sourceId = source.GetZDOID();
        if (sourceId == ZDOID.None)
        {
            return;
        }

        float maximumAmount = Mathf.Max(0f, source.GetMaxHealth()) * Mathf.Clamp01(reflectionPower);
        if (maximumAmount <= 0f || amount > maximumAmount + 0.1f)
        {
            return;
        }

        ZNetView? targetView = target.m_nview;
        if (targetView == null || !targetView.IsValid())
        {
            return;
        }

        if (!ServerReflectionRequestStates.TryGetValue(sourceId, out ServerReflectionRequestState requestState))
        {
            requestState = new ServerReflectionRequestState();
            ServerReflectionRequestStates[sourceId] = requestState;
        }

        if (requestState.RequestOwner != sender)
        {
            requestState.RequestOwner = sender;
            requestState.LastRequestId = 0;
            requestState.NextAllowedTime = 0f;
        }

        if (requestId <= requestState.LastRequestId)
        {
            return;
        }

        requestState.LastRequestId = requestId;
        float now = GetNetworkTimeSeconds();
        if (now < requestState.NextAllowedTime)
        {
            return;
        }

        requestState.NextAllowedTime = now + ReflectionServerMinimumRequestInterval;
        targetView.InvokeRPC(ReflectionDamageRpc, sourceId, Mathf.Min(amount, maximumAmount));
    }

    private static void ApplyAuthorizedReflectionDamage(
        Character target,
        long sender,
        ZDOID sourceId,
        float amount)
    {
        if (!IsTrustedServerRpc(sender) ||
            !CreatureLevelManager.IsLevelSystemEnabled() ||
            target == null ||
            target.m_nview == null ||
            !target.m_nview.IsValid() ||
            !target.m_nview.IsOwner() ||
            sourceId == ZDOID.None ||
            amount <= 0f ||
            float.IsNaN(amount) ||
            float.IsInfinity(amount) ||
            target.IsDead() ||
            target.InGodMode() ||
            target.InGhostMode())
        {
            return;
        }

        float exactDamage = Mathf.Min(amount, Mathf.Max(0f, target.GetHealth()));
        if (exactDamage > 0f)
        {
            HitData reflectionHit = new()
            {
                m_attacker = sourceId,
                m_hitType = HitData.HitType.EnemyHit,
                m_point = target.GetCenterPoint()
            };
            reflectionHit.m_damage.m_damage = exactDamage;
            target.m_lastHit = reflectionHit;

            if (DamageText.instance != null && ZRoutedRpc.instance != null)
            {
                DamageText.instance.ShowText(
                    DamageText.TextType.Normal,
                    target.GetCenterPoint(),
                    exactDamage,
                    target.IsPlayer() || target.IsTamed());
            }

            target.UseHealth(exactDamage);
            target.CheckDeath();
        }
    }

    internal static void ClearTransientDamageContext(HitData? hit = null)
    {
        if (hit != null)
        {
            PendingPlayerSpiritDamage.Remove(hit);
            PendingVampiricDamage.Remove(hit);
            PendingReflectionDamage.Remove(hit);
            FinalDeathwardConsumedHits.Remove(hit);
            PendingVortexProjectileDecisions.Remove(hit);
            if (PendingKnockbackHits.TryGetValue(hit, out KnockbackHitContext knockback))
            {
                PendingKnockbackHits.Remove(hit);
                ReleaseKnockbackReservation(knockback, hit);
            }
        }
    }

    internal static void ApplyArmorPiercing(ref float armor)
    {
        if (CurrentRpcDamageContext.ArmorPiercing <= 0f)
        {
            return;
        }

        armor *= 1f - Mathf.Clamp01(CurrentRpcDamageContext.ArmorPiercing);
        CurrentRpcDamageContext.ArmorPiercing = 0f;
    }

    internal static void ApplyAttackAnimationSpeed(CharacterAnimEvent animEvent)
    {
        if (animEvent == null ||
            animEvent.m_character == null ||
            animEvent.m_animator == null ||
            animEvent.m_character.IsPlayer() ||
            !animEvent.m_character.InAttack() ||
            !TryGetModifierHotPathState(animEvent.m_character, ModifierMask.AttackSpeed, out ModifierHotPathState state))
        {
            return;
        }

        if (animEvent.m_animator.speed > 0.001f)
        {
            animEvent.m_animator.speed = Mathf.Max(animEvent.m_animator.speed, state.AttackSpeedFactor);
        }
    }

    internal static void ApplyAttackIntervalSpeed(Humanoid character, ItemDrop.ItemData weapon, bool started)
    {
        if (!started ||
            character == null ||
            weapon?.m_shared == null ||
            ((Character)character).IsPlayer() ||
            !TryGetModifierHotPathState((Character)character, ModifierMask.AttackSpeed, out ModifierHotPathState state))
        {
            return;
        }

        float factor = state.AttackSpeedFactor;
        float intervalReduction = 1f - 1f / factor;
        weapon.m_lastAttackTime -= weapon.m_shared.m_aiAttackInterval * intervalReduction;
    }

    internal static void ApplyMinimumAttackIntervalSpeed(Humanoid character, ref float timeSinceLastAttack)
    {
        if (character == null ||
            ((Character)character).IsPlayer() ||
            !TryGetModifierHotPathState((Character)character, ModifierMask.AttackSpeed, out ModifierHotPathState state))
        {
            return;
        }

        timeSinceLastAttack *= state.AttackSpeedFactor;
    }

    private static float GetAttackSpeedFactor(float power)
    {
        return Mathf.Max(1f, 1f + power);
    }

    internal static bool TryGetSwiftMovementFactor(Character? character, out float factor)
    {
        factor = 1f;
        if (character == null ||
            character.IsPlayer() ||
            !TryGetModifierHotPathState(character, ModifierMask.Swift, out ModifierHotPathState state))
        {
            return false;
        }

        factor = state.SwiftFactor;
        return factor > 1f;
    }

    internal static BlockAttackModifierState BeginBlockAttackModifierScope(Character? attacker)
    {
        float previousMultiplier = BlockStaggerMultiplier;
        BlockStaggerMultiplier = attacker != null &&
                                 TryGetModifierPower(attacker, ModifierMask.Staggering, StaggeringPowerKey, StaggeringDefaultPower, out float power)
            ? 1f + power
            : 0f;

        Character? unflinchingAttacker = null;
        bool originalStaggerWhenBlocked = false;
        if (attacker != null &&
            TryGetModifierPower(attacker, ModifierMask.Unflinching, UnflinchingPowerKey, UnflinchingDefaultPower, out _))
        {
            unflinchingAttacker = attacker;
            originalStaggerWhenBlocked = attacker.m_staggerWhenBlocked;
            attacker.m_staggerWhenBlocked = false;
        }

        return new BlockAttackModifierState(previousMultiplier, unflinchingAttacker, originalStaggerWhenBlocked);
    }

    internal static void EndBlockAttackModifierScope(BlockAttackModifierState state)
    {
        try
        {
            if (state.UnflinchingAttacker != null)
            {
                state.UnflinchingAttacker.m_staggerWhenBlocked = state.OriginalStaggerWhenBlocked;
            }
        }
        finally
        {
            BlockStaggerMultiplier = state.PreviousStaggerMultiplier;
        }
    }

    internal static void ApplyBlockStaggerBonus(ref float damage)
    {
        if (damage > 0f && BlockStaggerMultiplier > 1f)
        {
            damage *= BlockStaggerMultiplier;
        }
    }

    internal static BlamerFleeOverrideState BeginBlamerFleeOverride(MonsterAI monsterAI)
    {
        Character? character = monsterAI?.m_character;
        if (character == null ||
            character.IsPlayer() ||
            character.IsTamed() ||
            !TryGetHotPathModifierZdo(character, ModifierMask.Blamer, out ZDO zdo) ||
            !HasBlamerKarmaRemaining(zdo) ||
            !IsBlamerFleeTargetValid(character, monsterAI!, zdo))
        {
            return default;
        }

        BlamerFleeOverrideState state = new(monsterAI!, monsterAI!.m_fleeIfLowHealth, monsterAI.m_fleeTimeSinceHurt);
        monsterAI.m_fleeIfLowHealth = ResolveBlamerFleeHealthRatio(zdo.GetFloat(BlamerFleeHealthRatioKey, BlamerDefaultFleeHealthRatio));
        monsterAI.m_fleeTimeSinceHurt = float.MaxValue;
        return state;
    }

    internal static void EndBlamerFleeOverride(BlamerFleeOverrideState state)
    {
        if (!state.IsActive || state.MonsterAI == null)
        {
            return;
        }

        state.MonsterAI.m_fleeIfLowHealth = state.FleeIfLowHealth;
        state.MonsterAI.m_fleeTimeSinceHurt = state.FleeTimeSinceHurt;
    }

    private static bool IsBlamerFleeTargetValid(Character character, MonsterAI monsterAI, ZDO zdo)
    {
        float fleeHealthRatio = ResolveBlamerFleeHealthRatio(zdo.GetFloat(BlamerFleeHealthRatioKey, BlamerDefaultFleeHealthRatio));
        if (character.GetHealthPercentage() >= fleeHealthRatio || !monsterAI.IsAlerted())
        {
            return false;
        }

        Character? target = TryGetMonsterAiTarget(monsterAI);
        return target is Player player && !player.IsDead() && IsHostileAttacker(character, player);
    }

    internal static BlinkAttackAiOverrideState? BeginBlinkAttackAiOverride(MonsterAI monsterAI)
    {
        Humanoid? humanoid = monsterAI?.m_character as Humanoid;
        if (humanoid == null)
        {
            return null;
        }

        Character character = humanoid;
        if (character.IsPlayer() ||
            !TryGetHotPathModifierZdo(character, ModifierMask.Blink, out ZDO zdo) ||
            IsBlinkAlertGraceActive(character, zdo) ||
            zdo.GetFloat(BlinkNextTimeKey, 0f) > GetNetworkTimeSeconds())
        {
            return null;
        }

        float maxRange = ResolveBlinkMaxRange(zdo.GetFloat(BlinkMaxRangeKey, BlinkDefaultMaxRange));
        Player? target = TryGetBlinkTarget(character);
        if (maxRange <= 0f || target == null || Utils.DistanceXZ(character.transform.position, target.transform.position) > maxRange)
        {
            return null;
        }

        BlinkAttackAiOverrideState state = BlinkAttackAiOverridePool.Count > 0
            ? BlinkAttackAiOverridePool.Pop()
            : new BlinkAttackAiOverrideState();
        try
        {
            foreach (ItemDrop.ItemData? item in humanoid.GetInventory().GetAllItems())
            {
                if (item == null)
                {
                    continue;
                }

                ItemDrop.ItemData.SharedData? shared = item.m_shared;
                if (shared == null || !item.IsWeapon() || shared.m_aiTargetType != ItemDrop.ItemData.AiTarget.Enemy)
                {
                    continue;
                }

                state.Override(shared, maxRange, BlinkAiMaxAngle);
            }

            if (state.Count > 0)
            {
                return state;
            }
        }
        catch
        {
            state.Restore();
            BlinkAttackAiOverridePool.Push(state);
            throw;
        }

        BlinkAttackAiOverridePool.Push(state);
        return null;
    }

    internal static void EndBlinkAttackAiOverride(BlinkAttackAiOverrideState? state)
    {
        if (state == null)
        {
            return;
        }

        state.Restore();
        BlinkAttackAiOverridePool.Push(state);
    }

    internal static void HandleBlinkAlertStateChanged(MonsterAI monsterAI, bool wasAlerted)
    {
        bool isAlerted = monsterAI.IsAlerted();
        if (wasAlerted == isAlerted)
        {
            return;
        }

        Character? character = monsterAI.m_character;
        ZNetView? nview = character?.m_nview;
        if (character == null ||
            character.IsPlayer() ||
            nview == null ||
            !nview.IsValid() ||
            !nview.IsOwner() ||
            !TryGetZdo(character, out ZDO zdo) ||
            !HasModifier(GetStoredModifierMask(zdo), ModifierMask.Blink))
        {
            return;
        }

        if (isAlerted)
        {
            zdo.Set(BlinkAlertStartTimeKey, GetNetworkTimeSeconds());
        }
        else
        {
            zdo.RemoveFloat(BlinkAlertStartTimeKey);
        }
    }

    internal static void TryBlinkOnAttackStart(Humanoid humanoid, ItemDrop.ItemData weapon, bool started)
    {
        if (humanoid == null)
        {
            return;
        }

        Character character = humanoid;
        ZNetView? nview = character.m_nview;
        if (character.IsPlayer() ||
            nview == null ||
            !nview.IsValid() ||
            !nview.IsOwner() ||
            !TryGetZdo(character, out ZDO zdo))
        {
            return;
        }

        ModifierMask mask = GetStoredModifierMask(zdo);
        if (!HasModifier(mask, ModifierMask.Blink))
        {
            return;
        }

        if (!started || weapon?.m_shared?.m_aiTargetType != ItemDrop.ItemData.AiTarget.Enemy)
        {
            return;
        }

        if (character.IsTamed())
        {
            return;
        }

        if (!CreatureLevelManager.AllowsModifierEffects(character))
        {
            return;
        }

        if (IsBlinkAlertGraceActive(character, zdo))
        {
            return;
        }

        float now = GetNetworkTimeSeconds();
        float nextTime = zdo.GetFloat(BlinkNextTimeKey, 0f);
        if (nextTime > now)
        {
            return;
        }

        Player? target = TryGetBlinkTarget(character);
        if (target == null)
        {
            return;
        }

        float maxRange = ResolveBlinkMaxRange(zdo.GetFloat(BlinkMaxRangeKey, BlinkDefaultMaxRange));
        float distance = Utils.DistanceXZ(character.transform.position, target.transform.position);
        if (maxRange <= 0f || distance > maxRange)
        {
            return;
        }

        if (!TryGetBlinkDestination(target, out Vector3 destination))
        {
            return;
        }

        float cooldown = ResolveBlinkCooldown(zdo.GetFloat(BlinkCooldownKey, BlinkDefaultCooldown));
        zdo.Set(BlinkNextTimeKey, now + cooldown);
        Vector3 origin = character.transform.position;
        string startEffect = ResolveBlinkStartEffect(zdo.GetString(BlinkStartEffectKey, BlinkDefaultStartEffect));
        PlayBlinkStartEffect(origin, startEffect);
        TeleportCharacter(character, destination, target.transform.position);
    }

    private static bool IsBlinkAlertGraceActive(Character character, ZDO zdo)
    {
        float gracePeriod = ResolveBlinkAlertGracePeriod();
        ZNetView? nview = character.m_nview;
        bool isOwner = nview != null && nview.IsValid() && nview.IsOwner();
        BaseAI? baseAI = character.GetBaseAI();
        float alertStartTime = zdo.GetFloat(BlinkAlertStartTimeKey, 0f);
        if (baseAI == null || !baseAI.IsAlerted())
        {
            if (isOwner && alertStartTime > 0f)
            {
                zdo.RemoveFloat(BlinkAlertStartTimeKey);
            }

            return gracePeriod > 0f;
        }

        float now = GetNetworkTimeSeconds();
        if (alertStartTime <= 0f)
        {
            if (!isOwner)
            {
                return gracePeriod > 0f;
            }

            alertStartTime = now;
            zdo.Set(BlinkAlertStartTimeKey, alertStartTime);
        }

        return gracePeriod > 0f && now - alertStartTime < gracePeriod;
    }

    private static float ResolveBlinkAlertGracePeriod()
    {
        return Mathf.Clamp(CreatureManagerPlugin.BlinkAlertGracePeriod?.Value ?? 0f, 0f, 10f);
    }

    private static Player? TryGetBlinkTarget(Character character)
    {
        BaseAI? baseAI = character.GetBaseAI();
        if (baseAI == null)
        {
            return null;
        }

        Character? target = TryGetMonsterAiTarget(baseAI as MonsterAI);
        if (target is not Player player)
        {
            return null;
        }

        if (player.IsDead())
        {
            return null;
        }

        if (!IsHostileAttacker(character, player))
        {
            return null;
        }

        return player;
    }

    internal static UndodgeableScopeState BeginUndodgeableAttackScope(
        Attack? attack,
        UndodgeableSourcePath sourcePath)
    {
        Character? attacker = null;
        ZNetView? sourceView = null;
        bool sourceDodgeable = false;
        try
        {
            attacker = attack?.m_character;
            sourceView = attacker?.m_nview;
            ItemDrop.ItemData? weapon = attack?.m_weapon;
            sourceDodgeable = weapon?.m_shared != null && weapon.m_shared.m_dodgeable;
        }
        catch (Exception exception)
        {
            ReportUndodgeableRuntimeFailure("begin_attack_scope", exception);
        }

        UndodgeableSourceScopeState source = BeginUndodgeableSourceScopeCore(
            attacker,
            sourceView,
            intendedTarget: null,
            sourcePath,
            sourceDodgeable,
            requireSourceOwner: true,
            executionEligible: sourcePath != UndodgeableSourcePath.None);
        return new UndodgeableScopeState(source);
    }

    internal static UndodgeableScopeState BeginUndodgeableProjectileScope(
        Projectile? projectile,
        IDestructible? destructible)
    {
        Player? intendedTarget = destructible as Player;
        Character? attacker = projectile?.m_owner;
        ZNetView? sourceView = projectile?.m_nview;
        bool sourceDodgeable = projectile?.m_dodgeable == true;
        UndodgeableSourceScopeState source = BeginUndodgeableSourceScopeCore(
            attacker,
            sourceView,
            intendedTarget,
            UndodgeableSourcePath.ProjectileTarget,
            sourceDodgeable,
            requireSourceOwner: true,
            executionEligible: intendedTarget != null);
        return new UndodgeableScopeState(source);
    }

    internal static UndodgeableScopeState BeginUndodgeableAoeScope(Aoe? aoe, Collider? collider)
    {
        Character? attacker = aoe?.m_owner;
        ZNetView? sourceView = aoe?.m_nview;
        bool sourceDodgeable = aoe?.m_dodgeable == true;
        bool useTriggers = aoe?.m_useTriggers == true;
        Player? intendedTarget = null;
        bool executionEligible = false;
        bool requireSourceOwner = !useTriggers;
        bool sourcePrevalidated = false;
        try
        {
            sourcePrevalidated = aoe != null &&
                                 sourceDodgeable &&
                                 attacker != null &&
                                 (!requireSourceOwner ||
                                  IsLocallyOwnedUndodgeableSource(attacker, sourceView)) &&
                                 HasActiveUndodgeableModifier(attacker);
            if (sourcePrevalidated)
            {
                GameObject? hitObject = collider == null ? null : Projectile.FindHitObject(collider);
                intendedTarget = hitObject?.GetComponent<Character>() as Player;
                if (intendedTarget != null)
                {
                    bool requireTargetOwner = useTriggers || sourceView == null;
                    executionEligible = !requireTargetOwner || intendedTarget.IsOwner();
                }
            }
        }
        catch (Exception exception)
        {
            ReportUndodgeableRuntimeFailure("resolve_aoe_target", exception);
        }

        UndodgeableSourceScopeState source = BeginUndodgeableSourceScopeCore(
            attacker,
            sourceView,
            intendedTarget,
            UndodgeableSourcePath.AoeTarget,
            sourceDodgeable,
            requireSourceOwner,
            executionEligible,
            sourcePrevalidated);
        return new UndodgeableScopeState(source);
    }

    internal static void EndUndodgeableScope(ref UndodgeableScopeState state)
    {
        if (!state.IsActive)
        {
            state = default;
            return;
        }

        UndodgeableScopeState activeState = state;
        state = default;
        if (activeState.Source.Changed)
        {
            CurrentUndodgeableSourceContext = activeState.Source.Previous;
        }
    }

    private static UndodgeableSourceScopeState BeginUndodgeableSourceScopeCore(
        Character? attacker,
        ZNetView? sourceView,
        Player? intendedTarget,
        UndodgeableSourcePath path,
        bool sourceDodgeable,
        bool requireSourceOwner,
        bool executionEligible,
        bool sourcePrevalidated = false)
    {
        UndodgeableSourceContext previous = CurrentUndodgeableSourceContext;
        UndodgeableSourceContext next = default;
        try
        {
            if (executionEligible &&
                sourceDodgeable &&
                attacker != null &&
                path != UndodgeableSourcePath.None &&
                (sourcePrevalidated ||
                 ((!requireSourceOwner || IsLocallyOwnedUndodgeableSource(attacker, sourceView)) &&
                  HasActiveUndodgeableModifier(attacker))))
            {
                next = new UndodgeableSourceContext(attacker, intendedTarget, path, sourceDodgeable);
            }
        }
        catch (Exception exception)
        {
            ReportUndodgeableRuntimeFailure("begin_source_scope", exception);
        }

        bool changed = previous.IsActive || next.IsActive;
        if (changed)
        {
            // An inactive nested source must shadow an active outer source as well.
            CurrentUndodgeableSourceContext = next;
        }

        return new UndodgeableSourceScopeState(previous, changed);
    }

    internal static bool ApplyUndodgeableDodgeOverride(Player? target, ref bool dodgeInvincible)
    {
        if (!dodgeInvincible || target == null)
        {
            return false;
        }

        try
        {
            UndodgeableSourceContext context = CurrentUndodgeableSourceContext;
            if (!context.IsActive ||
                (context.IntendedTarget != null && !ReferenceEquals(context.IntendedTarget, target)))
            {
                return false;
            }

            dodgeInvincible = false;
            return true;
        }
        catch (Exception exception)
        {
            ReportUndodgeableRuntimeFailure("dodge_override", exception);
            return false;
        }
    }

    internal static UndodgeableDamageScopeState BeginUndodgeableDamageScope()
    {
        UndodgeableSourceContext restore = CurrentUndodgeableSourceContext;
        if (!restore.IsActive)
        {
            return default;
        }

        // Dodge checks performed by Character.Damage patches are unrelated to the
        // attack source prefilter and must observe the real player dodge state. The
        // outer source is restored afterward for later checks such as melee attachment.
        CurrentUndodgeableSourceContext = default;
        return new UndodgeableDamageScopeState(restore, changed: true);
    }

    internal static void EndUndodgeableDamageScope(ref UndodgeableDamageScopeState state)
    {
        if (!state.Changed)
        {
            state = default;
            return;
        }

        UndodgeableSourceContext restore = state.Restore;
        state = default;
        CurrentUndodgeableSourceContext = restore;
    }

    internal static void ApplyUndodgeableBeforeDamage(Character? target, HitData? hit)
    {
        try
        {
            if (target is Player && hit != null)
            {
                Character? attacker = hit.GetAttacker();
                if (HasActiveUndodgeableModifier(attacker))
                {
                    // Character.Damage serializes this HitData immediately for RPC delivery.
                    hit.m_dodgeable = false;
                }
            }
        }
        catch (Exception exception)
        {
            ReportUndodgeableRuntimeFailure("damage_dispatch", exception);
        }
    }

    private static bool HasActiveUndodgeableModifier(Character? character)
    {
        return TryGetActiveUndodgeableModifier(character, out _);
    }

    private static bool TryGetActiveUndodgeableModifier(
        Character? character,
        out float damageReduction)
    {
        damageReduction = 0f;
        if (character == null ||
            !TryGetModifierHotPathState(
                character,
                ModifierMask.Undodgeable,
                out ModifierHotPathState state) ||
            !state.UndodgeableEffectActive)
        {
            return false;
        }

        damageReduction = state.UndodgeableDamageReduction;
        return true;
    }

    private static bool IsLocallyOwnedUndodgeableSource(Character attacker, ZNetView? sourceView)
    {
        if (sourceView == null)
        {
            return true;
        }

        if (sourceView.IsValid())
        {
            return sourceView.IsOwner();
        }

        ZNetView? attackerView = attacker.m_nview;
        return attackerView != null && attackerView.IsValid() && attackerView.IsOwner();
    }

    private static void ReportUndodgeableRuntimeFailure(string stage, Exception exception)
    {
        if (UndodgeableRuntimeFailureReported)
        {
            return;
        }

        UndodgeableRuntimeFailureReported = true;
        try
        {
            CreatureManagerPlugin.Log.LogWarning(
                $"[Undodgeable] one evaluation failed open stage={stage}; " +
                $"{exception.GetType().Name}: {exception.Message}");
        }
        catch
        {
            // Undodgeable safeguards must never interfere with combat.
        }
    }

    private static Character? TryGetMonsterAiTarget(MonsterAI? monsterAI)
    {
        if (monsterAI == null)
        {
            return null;
        }

        if (!MonsterAiTargetLookupDone)
        {
            CachedMonsterAiTargetField = typeof(MonsterAI).GetField("m_targetCreature", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            MonsterAiTargetLookupDone = true;
        }

        return CachedMonsterAiTargetField?.GetValue(monsterAI) as Character;
    }

    private static bool TryGetBlinkDestination(Player target, out Vector3 destination)
    {
        Vector3 targetPosition = target.transform.position;
        Vector2 offset2d = UnityEngine.Random.insideUnitCircle * BlinkDestinationRadius;
        destination = new Vector3(targetPosition.x + offset2d.x, targetPosition.y, targetPosition.z + offset2d.y);
        return true;
    }

    private static void TeleportCharacter(Character character, Vector3 destination, Vector3 lookAt)
    {
        Vector3 direction = lookAt - destination;
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.001f)
        {
            character.transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        }

        if (character.TryGetComponent(out Rigidbody body))
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.position = destination;
        }

        character.transform.position = destination;
    }

    private static void ApplyOutgoingModifierEffects(Character attacker, Character target, HitData hit)
    {
        if (target is Player &&
            TryGetActiveUndodgeableModifier(attacker, out float undodgeableDamageReduction))
        {
            hit.m_dodgeable = false;
            hit.m_damage.Modify(1f - undodgeableDamageReduction);
        }

        float totalDamage = Mathf.Max(0f, hit.GetTotalDamage());
        if (totalDamage <= 0f)
        {
            return;
        }

        if (TryGetModifierPower(attacker, ModifierMask.Staggering, StaggeringPowerKey, StaggeringDefaultPower, out float staggering))
        {
            hit.m_staggerMultiplier *= 1f + staggering;
        }

        if (TryGetModifierPower(attacker, ModifierMask.Fire, FirePowerKey, FireDefaultPower, out float fire))
        {
            hit.m_damage.m_fire += totalDamage * fire;
        }

        if (TryGetModifierPower(attacker, ModifierMask.Frost, FrostPowerKey, FrostDefaultPower, out float frost))
        {
            hit.m_damage.m_frost += totalDamage * frost;
        }

        if (TryGetModifierPower(attacker, ModifierMask.Lightning, LightningPowerKey, LightningDefaultPower, out float lightning))
        {
            hit.m_damage.m_lightning += totalDamage * lightning;
        }

        if (TryGetModifierPower(attacker, ModifierMask.Spirit, SpiritPowerKey, ElementalDefaultPower, out float spirit))
        {
            float spiritDamage = totalDamage * spirit;
            if (target is Player)
            {
                QueuePlayerSpiritDamage(hit, spiritDamage);
            }
            else
            {
                hit.m_damage.m_spirit += spiritDamage;
            }
        }

        if (TryGetModifierPower(attacker, ModifierMask.Vampiric, VampiricPowerKey, VampiricDefaultPower, out float vampiric))
        {
            // Queue only this direct hit; delayed status-effect ticks have no matching context.
            PendingVampiricDamage[hit] = new VampiricDamageContext(attacker, target, vampiric);
        }

        if (target is Player && TryGetModifierPower(attacker, ModifierMask.ArmorPiercing, ArmorPiercingPowerKey, ArmorPiercingDefaultPower, out float armorPiercing))
        {
            CurrentRpcDamageContext.ArmorPiercing = Mathf.Max(CurrentRpcDamageContext.ArmorPiercing, armorPiercing);
        }

        TryArmKnockback(attacker, target, hit);
    }

    internal static ApplyDamageState BeginApplyDamage(Character target, HitData hit)
    {
        bool eligibleAtEntry = target != null &&
                               hit != null &&
                               !target.IsDebugFlying() &&
                               !target.IsDead() &&
                               !target.IsTeleporting() &&
                               !target.InCutscene();
        float healthBefore = target == null ? 0f : Mathf.Max(0f, target.GetHealth());
        return new ApplyDamageState(BeginDirectDamage(target!, hit!), eligibleAtEntry, healthBefore);
    }

    internal static void CompleteApplyDamage(Character target, HitData hit, ApplyDamageState state)
    {
        if (hit != null && FinalDeathwardConsumedHits.Remove(hit))
        {
            return;
        }

        CompleteDirectDamage(target, state.DirectDamage);
        if (!state.EligibleAtEntry || target == null || hit == null)
        {
            return;
        }

        bool hasResolvedOriginalHit = GetResolvedOriginalPlayerHitDamage(target, hit) > 0.1f;
        TryApplyPendingPlayerSpiritDamage(target, hit, hasResolvedOriginalHit);
        TryApplyPlayerDebuffModifiers(target, hit, hasResolvedOriginalHit);
        CaptureDelayedDamageDeathCredit(target, hit, state.HealthBefore);
    }

    internal static DirectDamageState BeginDirectDamage(Character target, HitData hit)
    {
        if (target == null || hit == null)
        {
            return default;
        }

        Character? vampiricAttacker = null;
        float vampiricPower = 0f;
        if (PendingVampiricDamage.TryGetValue(hit, out VampiricDamageContext vampiricContext))
        {
            PendingVampiricDamage.Remove(hit);
            if (ReferenceEquals(vampiricContext.Target, target) && vampiricContext.Attacker != null && vampiricContext.Power > 0f)
            {
                vampiricAttacker = vampiricContext.Attacker;
                vampiricPower = vampiricContext.Power;
            }
        }

        Character? reflectionAttacker = null;
        float reflectionPower = 0f;
        Vector3 reflectionHitPoint = Vector3.zero;
        if (PendingReflectionDamage.TryGetValue(hit, out ReflectionDamageContext reflectionContext))
        {
            PendingReflectionDamage.Remove(hit);
            if (ReferenceEquals(reflectionContext.Target, target) && reflectionContext.Attacker != null && reflectionContext.Power > 0f)
            {
                reflectionAttacker = reflectionContext.Attacker;
                reflectionPower = reflectionContext.Power;
                reflectionHitPoint = reflectionContext.HitPoint;
            }
        }

        if (vampiricAttacker == null && reflectionAttacker == null)
        {
            return default;
        }

        return new DirectDamageState(
            vampiricAttacker,
            vampiricPower,
            reflectionAttacker,
            reflectionPower,
            reflectionHitPoint,
            Mathf.Max(0f, target.GetHealth()));
    }

    internal static void CompleteDirectDamage(Character target, DirectDamageState state)
    {
        if (target == null || !state.IsValid)
        {
            return;
        }

        float healthAfter = Mathf.Max(0f, target.GetHealth());
        float actualDamage = Mathf.Clamp(state.HealthBefore - healthAfter, 0f, state.HealthBefore);
        if (actualDamage <= 0f)
        {
            return;
        }

        if (state.HasVampiric && state.VampiricAttacker != null)
        {
            state.VampiricAttacker.Heal(actualDamage * state.VampiricPower, true);
        }

        if (state.HasReflection && state.ReflectionAttacker != null)
        {
            PlayReflectionEffects(target, state.ReflectionAttacker, state.ReflectionHitPoint);
            SendExactReflectionDamage(
                state.ReflectionAttacker,
                target,
                actualDamage * state.ReflectionPower);
        }
    }

    private static void SendExactReflectionDamage(
        Character target,
        Character source,
        float amount)
    {
        if (target == null ||
            source == null ||
            source.m_nview == null ||
            !source.m_nview.IsValid() ||
            target.IsDead() ||
            amount <= 0f ||
            ZRoutedRpc.instance == null)
        {
            return;
        }

        ZDOID targetId = target.GetZDOID();
        if (targetId == ZDOID.None)
        {
            return;
        }

        long requestId = unchecked(++NextReflectionRequestId);
        if (requestId <= 0)
        {
            requestId = NextReflectionRequestId = 1;
        }

        source.m_nview.InvokeRPC(
            ZRoutedRpc.instance.GetServerPeerID(),
            ReflectionDamageRequestRpc,
            requestId,
            targetId,
            amount);
    }

    private static void TryArmKnockback(Character attacker, Character target, HitData hit)
    {
        if (target is not Player ||
            attacker == null ||
            attacker.IsPlayer() ||
            hit == null ||
            hit.GetTotalDamage() <= 0f ||
            !IsHostileAttacker(target, attacker) ||
            !TryGetZdo(attacker, out ZDO zdo) ||
            !HasModifier(zdo, ModifierMask.Knockback) ||
            !CreatureLevelManager.AllowsModifierEffects(attacker))
        {
            return;
        }

        float minimumPushForce = ClampKnockbackPower(zdo.GetFloat(KnockbackPowerKey, KnockbackDefaultPower));
        if (minimumPushForce <= 0f)
        {
            return;
        }

        int attackerId = attacker.GetInstanceID();
        float now = GetNetworkTimeSeconds();
        if (zdo.GetFloat(KnockbackNextReadyTimeKey, 0f) > now ||
            (LocalKnockbackReadyTimes.TryGetValue(attackerId, out float localReady) && localReady > now) ||
            PendingKnockbackReservations.ContainsKey(attackerId) ||
            PendingKnockbackHits.ContainsKey(hit))
        {
            return;
        }

        LocalKnockbackReadyTimes.Remove(attackerId);
        float cooldown = ResolveKnockbackCooldown(zdo.GetFloat(KnockbackCooldownKey, KnockbackDefaultCooldown));
        hit.m_pushForce = Mathf.Max(hit.m_pushForce, minimumPushForce);
        PendingKnockbackHits[hit] = new KnockbackHitContext(attacker, cooldown);
        PendingKnockbackReservations[attackerId] = hit;
    }

    internal static void ConfirmKnockbackPush(Character target, HitData hit)
    {
        if (target is not Player ||
            hit == null ||
            hit.m_pushForce == 0f ||
            hit.m_dir == Vector3.zero ||
            !PendingKnockbackHits.TryGetValue(hit, out KnockbackHitContext context))
        {
            return;
        }

        PendingKnockbackHits.Remove(hit);
        ReleaseKnockbackReservation(context, hit);
        LocalKnockbackReadyTimes[context.AttackerId] = GetNetworkTimeSeconds() + context.Cooldown;
        if (context.Attacker != null &&
            context.Attacker.m_nview != null &&
            context.Attacker.m_nview.IsValid() &&
            ZRoutedRpc.instance != null)
        {
            ZDOID targetId = target.GetZDOID();
            if (targetId != ZDOID.None)
            {
                context.Attacker.m_nview.InvokeRPC(
                    ZRoutedRpc.instance.GetServerPeerID(),
                    KnockbackCooldownRequestRpc,
                    targetId);
            }
        }
    }

    private static void RPC_KnockbackCooldownRequest(Character attacker, long sender, ZDOID targetId)
    {
        if (ZNet.instance == null ||
            !ZNet.instance.IsServer() ||
            attacker == null ||
            attacker.IsPlayer() ||
            attacker.IsDead() ||
            attacker.m_nview == null ||
            !attacker.m_nview.IsValid() ||
            !TryGetZdo(attacker, out ZDO attackerZdo) ||
            !HasModifier(attackerZdo, ModifierMask.Knockback) ||
            !CreatureLevelManager.AllowsModifierEffects(attacker) ||
            !TryFindCharacter(targetId, out Character target) ||
            target is not Player ||
            !TryGetCharacterZdo(target, out ZDO targetZdo) ||
            targetZdo.GetOwner() != sender ||
            !IsHostileAttacker(target, attacker) ||
            Vector3.Distance(attacker.transform.position, target.transform.position) > ModifierRequestValidationRange)
        {
            return;
        }

        ZDOID attackerId = attacker.GetZDOID();
        float now = GetNetworkTimeSeconds();
        if (attackerId == ZDOID.None ||
            attackerZdo.GetFloat(KnockbackNextReadyTimeKey, 0f) > now ||
            (ServerKnockbackNextAllowedTimes.TryGetValue(attackerId, out float nextAllowed) && nextAllowed > now))
        {
            return;
        }

        float cooldown = ResolveKnockbackCooldown(
            attackerZdo.GetFloat(KnockbackCooldownKey, KnockbackDefaultCooldown));
        float nextReadyTime = now + cooldown;
        ServerKnockbackNextAllowedTimes[attackerId] = nextReadyTime;
        attacker.m_nview.InvokeRPC(KnockbackCooldownRpc, nextReadyTime);
    }

    private static void CommitAuthorizedKnockbackCooldown(Character attacker, long sender, float nextReadyTime)
    {
        if (!IsTrustedServerRpc(sender) ||
            !CreatureLevelManager.IsLevelSystemEnabled() ||
            attacker == null ||
            attacker.IsPlayer() ||
            attacker.m_nview == null ||
            !attacker.m_nview.IsValid() ||
            !attacker.m_nview.IsOwner() ||
            nextReadyTime < 0f ||
            float.IsNaN(nextReadyTime) ||
            float.IsInfinity(nextReadyTime) ||
            !TryGetZdo(attacker, out ZDO zdo))
        {
            return;
        }

        if (!HasModifier(zdo, ModifierMask.Knockback) ||
            !CreatureLevelManager.AllowsModifierEffects(attacker))
        {
            return;
        }

        if (nextReadyTime > zdo.GetFloat(KnockbackNextReadyTimeKey, 0f))
        {
            zdo.Set(KnockbackNextReadyTimeKey, nextReadyTime);
        }
    }

    private static void ReleaseKnockbackReservation(KnockbackHitContext context, HitData hit)
    {
        if (PendingKnockbackReservations.TryGetValue(context.AttackerId, out HitData reserved) && ReferenceEquals(reserved, hit))
        {
            PendingKnockbackReservations.Remove(context.AttackerId);
        }
    }

    private static void QueuePlayerSpiritDamage(HitData sourceHit, float amount)
    {
        if (sourceHit == null || amount <= 0f)
        {
            return;
        }

        if (PendingPlayerSpiritDamage.TryGetValue(sourceHit, out float pending))
        {
            PendingPlayerSpiritDamage[sourceHit] = pending + amount;
            return;
        }

        PendingPlayerSpiritDamage[sourceHit] = amount;
    }

    internal static void TryApplyPendingPlayerSpiritDamage(
        Character target,
        HitData hit,
        bool hasResolvedOriginalHit)
    {
        if (hit == null || !PendingPlayerSpiritDamage.TryGetValue(hit, out float amount))
        {
            return;
        }

        PendingPlayerSpiritDamage.Remove(hit);
        if (target is not Player player || player.IsDead() || !hasResolvedOriginalHit)
        {
            return;
        }

        player.AddSpiritDamage(amount);
    }

    internal static void TryApplyPlayerDebuffModifiers(
        Character target,
        HitData hit,
        bool hasResolvedOriginalHit)
    {
        if (!CreatureLevelManager.IsLevelSystemEnabled() ||
            target is not Player player ||
            hit == null ||
            !hasResolvedOriginalHit ||
            IsVanillaDelayedDamageTick(target, hit))
        {
            return;
        }

        Character attacker = hit.GetAttacker();
        if (attacker == null || attacker.IsPlayer() || attacker.IsTamed())
        {
            return;
        }

        TryApplyPlayerDebuff(attacker, player, ModifierMask.Exposed, ExposedChanceKey, ExposedPowerKey, ExposedDefaultPower, ExposedStatusHash, PlayerExposedPowerKey, PlayerExposedUntilKey, ExposedDurationKey, PlayerDebuffDuration, (state, value) => state.Exposed = value);
        TryApplyPlayerDebuff(attacker, player, ModifierMask.Weakened, WeakenedChanceKey, WeakenedPowerKey, WeakenedDefaultPower, WeakenedStatusHash, PlayerWeakenedPowerKey, PlayerWeakenedUntilKey, WeakenedDurationKey, PlayerDebuffDuration, (state, value) => state.Weakened = value);
        TryApplyPlayerDebuff(attacker, player, ModifierMask.Withered, WitheredChanceKey, WitheredPowerKey, WitheredDefaultPower, WitheredStatusHash, PlayerWitheredPowerKey, PlayerWitheredUntilKey, WitheredDurationKey, PlayerDebuffDuration, (state, value) => state.Withered = value);
        TryApplySplitPlayerDebuff(attacker, player, ModifierMask.Crippling, CripplingChanceKey, CripplingPowerKey, CripplingDefaultPower, CripplingJumpPowerKey, CripplingDefaultPower, CripplingStatusHash, PlayerCripplingPowerKey, PlayerCripplingJumpPowerKey, PlayerCripplingUntilKey, CripplingDurationKey, ControlDebuffDuration, (state, value) => state.Crippling = value, (state, value) => state.CripplingJump = value);
        TryApplySplitPlayerDebuff(attacker, player, ModifierMask.Disruptive, DisruptiveChanceKey, DisruptivePowerKey, DisruptiveDefaultPower, DisruptiveEitrPowerKey, DisruptiveDefaultPower, DisruptiveStatusHash, PlayerDisruptivePowerKey, PlayerDisruptiveEitrPowerKey, PlayerDisruptiveUntilKey, DisruptiveDurationKey, ControlDebuffDuration, (state, value) => state.Disruptive = value, (state, value) => state.DisruptiveEitr = value);
        TryApplyPlayerDebuff(attacker, player, ModifierMask.Corrosive, CorrosiveChanceKey, CorrosivePowerKey, CorrosiveDefaultPower, CorrosiveStatusHash, PlayerCorrosivePowerKey, PlayerCorrosiveUntilKey, CorrosiveDurationKey, PlayerDebuffDuration, (state, value) => state.Corrosive = value);
        TryDrainPlayerAdrenaline(attacker, player);
    }

    private static float GetResolvedOriginalPlayerHitDamage(Character target, HitData hit)
    {
        float directDamage = SanitizePositiveDamage(hit.GetTotalDamage());
        if (target is not Player ||
            !CurrentRpcDamageContext.DelayedDamageCaptured ||
            !ReferenceEquals(CurrentRpcDamageContext.Target, target) ||
            !ReferenceEquals(CurrentRpcDamageContext.Hit, hit))
        {
            return directDamage;
        }

        return directDamage + CurrentRpcDamageContext.ResolvedDelayedDamage;
    }

    private static float SanitizePositiveDamage(float damage)
    {
        return float.IsNaN(damage) || float.IsInfinity(damage) || damage <= 0f ? 0f : damage;
    }

    private static bool IsVanillaDelayedDamageTick(Character target, HitData hit)
    {
        return ReferenceEquals(CurrentDelayedDamageTickContext.Target, target) &&
               CurrentDelayedDamageTickContext.HitType == hit.m_hitType &&
               (hit.m_hitType == HitData.HitType.Poisoned ||
                hit.m_hitType == HitData.HitType.Burning);
    }

    internal static void RecordPoisonDamageSource(SE_Poison status, bool accepted)
    {
        if (!accepted ||
            status == null ||
            status.m_character == null)
        {
            return;
        }

        Character target = status.m_character;
        DelayedDamageSourceLedger ledger = GetDelayedDamageSourceLedger(target);
        ledger.PoisonStatus = status;
        ledger.Poison = GetCurrentDelayedDamageAttribution(target);
    }

    internal static void RecordFireDamageSource(
        SE_Burning status,
        bool poolWasEmpty,
        bool accepted)
    {
        RecordAccumulatingDelayedDamageSource(status, poolWasEmpty, accepted, spirit: false);
    }

    internal static void RecordSpiritDamageSource(
        SE_Burning status,
        bool poolWasEmpty,
        bool accepted)
    {
        RecordAccumulatingDelayedDamageSource(status, poolWasEmpty, accepted, spirit: true);
    }

    private static void RecordAccumulatingDelayedDamageSource(
        SE_Burning status,
        bool poolWasEmpty,
        bool accepted,
        bool spirit)
    {
        if (!accepted ||
            status == null ||
            status.m_character == null)
        {
            return;
        }

        Character target = status.m_character;
        DelayedDamageSourceLedger ledger = GetDelayedDamageSourceLedger(target);
        DelayedDamageAttribution incoming = GetCurrentDelayedDamageAttribution(target);
        if (spirit)
        {
            bool sameStatus = ReferenceEquals(ledger.SpiritStatus, status);
            ledger.Spirit = poolWasEmpty
                ? incoming
                : MergeDelayedDamageAttribution(
                    sameStatus ? ledger.Spirit : default,
                    incoming);
            ledger.SpiritStatus = status;
            return;
        }

        bool sameFireStatus = ReferenceEquals(ledger.FireStatus, status);
        ledger.Fire = poolWasEmpty
            ? incoming
            : MergeDelayedDamageAttribution(
                sameFireStatus ? ledger.Fire : default,
                incoming);
        ledger.FireStatus = status;
    }

    private static DelayedDamageAttribution MergeDelayedDamageAttribution(
        DelayedDamageAttribution current,
        DelayedDamageAttribution incoming)
    {
        if (current.Kind == DelayedDamageAttributionKind.Exact &&
            incoming.Kind == DelayedDamageAttributionKind.Exact &&
            current.Source == incoming.Source)
        {
            // The ZDOID is the source identity. A remote player Character may resolve on only
            // some contributing hits; keep the exact source and let the server validate its type.
            return DelayedDamageAttribution.FromSource(
                current.Source,
                current.SourceWasPlayer || incoming.SourceWasPlayer);
        }

        if (current.Kind == DelayedDamageAttributionKind.Unattributed &&
            incoming.Kind == DelayedDamageAttributionKind.Unattributed)
        {
            return current;
        }

        return new DelayedDamageAttribution(
            DelayedDamageAttributionKind.Ambiguous,
            ZDOID.None);
    }

    private static DelayedDamageSourceLedger GetDelayedDamageSourceLedger(Character target)
    {
        int id = target.GetInstanceID();
        if (!DelayedDamageSourceLedgers.TryGetValue(id, out DelayedDamageSourceLedger ledger) ||
            !ReferenceEquals(ledger.Target, target))
        {
            ledger = new DelayedDamageSourceLedger { Target = target };
            DelayedDamageSourceLedgers[id] = ledger;
        }

        return ledger;
    }

    private static DelayedDamageAttribution GetCurrentDelayedDamageAttribution(Character target)
    {
        if (!ReferenceEquals(CurrentRpcDamageContext.Target, target) ||
            CurrentRpcDamageContext.Hit == null)
        {
            return DelayedDamageAttribution.FromSource(ZDOID.None, sourceWasPlayer: false);
        }

        HitData sourceHit = CurrentRpcDamageContext.Hit;
        Character? source = sourceHit.GetAttacker();
        return DelayedDamageAttribution.FromSource(
            sourceHit.m_attacker,
            source != null && source.IsPlayer());
    }

    internal static DelayedDamageTickScopeState BeginPoisonDamageTick(SE_Poison status)
    {
        DelayedDamageTickContext previous = CurrentDelayedDamageTickContext;
        DelayedDamageAttribution attribution = default;
        Character? target = status?.m_character;
        if (target != null &&
            DelayedDamageSourceLedgers.TryGetValue(target.GetInstanceID(), out DelayedDamageSourceLedger ledger) &&
            ReferenceEquals(ledger.Target, target) &&
            ReferenceEquals(ledger.PoisonStatus, status))
        {
            attribution = ledger.Poison;
        }

        CurrentDelayedDamageTickContext = new DelayedDamageTickContext
        {
            Target = target,
            HitType = HitData.HitType.Poisoned,
            Attribution = attribution
        };
        return new DelayedDamageTickScopeState(previous);
    }

    internal static DelayedDamageTickScopeState BeginBurningDamageTick(SE_Burning status)
    {
        DelayedDamageTickContext previous = CurrentDelayedDamageTickContext;
        Character? target = status?.m_character;
        DelayedDamageAttribution fire = default;
        DelayedDamageAttribution spirit = default;
        if (target != null &&
            DelayedDamageSourceLedgers.TryGetValue(target.GetInstanceID(), out DelayedDamageSourceLedger ledger) &&
            ReferenceEquals(ledger.Target, target))
        {
            if (ReferenceEquals(ledger.FireStatus, status))
            {
                fire = ledger.Fire;
            }

            if (ReferenceEquals(ledger.SpiritStatus, status))
            {
                spirit = ledger.Spirit;
            }
        }

        bool hasFire = status != null && status.m_fireDamagePerHit > 0f;
        bool hasSpirit = status != null && status.m_spiritDamagePerHit > 0f;
        CurrentDelayedDamageTickContext = new DelayedDamageTickContext
        {
            Target = target,
            HitType = HitData.HitType.Burning,
            Attribution = CombineDelayedDamageTickAttribution(fire, hasFire, spirit, hasSpirit)
        };
        return new DelayedDamageTickScopeState(previous);
    }

    internal static void EndDelayedDamageTick(DelayedDamageTickScopeState state)
    {
        if (state.Changed)
        {
            CurrentDelayedDamageTickContext = state.Previous;
        }
    }

    private static DelayedDamageAttribution CombineDelayedDamageTickAttribution(
        DelayedDamageAttribution fire,
        bool hasFire,
        DelayedDamageAttribution spirit,
        bool hasSpirit)
    {
        if (!hasFire)
        {
            return hasSpirit ? spirit : default;
        }

        if (!hasSpirit)
        {
            return fire;
        }

        if (fire.Kind == DelayedDamageAttributionKind.Exact &&
            spirit.Kind == DelayedDamageAttributionKind.Exact &&
            fire.Source == spirit.Source)
        {
            return DelayedDamageAttribution.FromSource(
                fire.Source,
                fire.SourceWasPlayer || spirit.SourceWasPlayer);
        }

        if (fire.Kind == DelayedDamageAttributionKind.Unattributed &&
            spirit.Kind == DelayedDamageAttributionKind.Unattributed)
        {
            return fire;
        }

        return new DelayedDamageAttribution(
            DelayedDamageAttributionKind.Ambiguous,
            ZDOID.None);
    }

    private static void CaptureDelayedDamageDeathCredit(
        Character target,
        HitData hit,
        float healthBefore)
    {
        if (healthBefore <= 0f ||
            target.GetHealth() > 0f ||
            !ReferenceEquals(target.m_lastHit, hit))
        {
            return;
        }

        int id = target.GetInstanceID();
        if (!ReferenceEquals(CurrentDelayedDamageTickContext.Target, target) ||
            CurrentDelayedDamageTickContext.HitType != hit.m_hitType)
        {
            PendingDelayedDamageDeathCredits.Remove(id);
            return;
        }

        // This is the first health > 0 -> health <= 0 transition. Later ticks in the same
        // frame may overwrite m_lastHit, but they must not steal or erase the lethal source.
        DelayedDamageAttribution attribution = CurrentDelayedDamageTickContext.Attribution;
        PendingDelayedDamageDeathCredits[id] = new DelayedDamageDeathCredit(
            target,
            attribution.IsExact ? attribution.Source : ZDOID.None,
            attribution.IsExact && attribution.SourceWasPlayer);
    }

    internal static void ClearRecoveredDelayedDamageDeathCredit(Character character)
    {
        if (character != null && character.GetHealth() > 0f)
        {
            PendingDelayedDamageDeathCredits.Remove(character.GetInstanceID());
        }
    }

    internal static void ApplyPlayerHealingDebuff(Character character, ref float amount)
    {
        if (!CreatureLevelManager.IsLevelSystemEnabled() ||
            character is not Player player ||
            amount <= 0f ||
            !TryGetActivePlayerDebuffPower(player, WitheredStatusHash, PlayerWitheredPowerKey, PlayerWitheredUntilKey, state => state.Withered, WitheredDefaultPower, out float power))
        {
            return;
        }

        amount *= 1f - power;
    }

    internal static void RegisterStatusEffects(ObjectDB objectDb)
    {
        if (objectDb == null || objectDb.m_StatusEffects == null)
        {
            return;
        }

        RegisterPlayerDebuffStatusEffect(objectDb, ExposedStatusName, ExposedStatusHash, "$cm_modifier_exposed_name", "$cm_status_exposed_tooltip", GetExposedSprite());
        RegisterPlayerDebuffStatusEffect(objectDb, WeakenedStatusName, WeakenedStatusHash, "$cm_modifier_weakened_name", "$cm_status_weakened_tooltip", GetWeakenedSprite());
        RegisterPlayerDebuffStatusEffect(objectDb, WitheredStatusName, WitheredStatusHash, "$cm_modifier_withered_name", "$cm_status_withered_tooltip", GetWitheredSprite());
        RegisterPlayerDebuffStatusEffect(objectDb, CripplingStatusName, CripplingStatusHash, "$cm_modifier_crippling_name", "$cm_status_crippling_tooltip", GetCripplingSprite(), ControlDebuffDuration);
        RegisterPlayerDebuffStatusEffect(objectDb, DisruptiveStatusName, DisruptiveStatusHash, "$cm_modifier_disruptive_name", "$cm_status_disruptive_tooltip", GetDisruptiveSprite(), ControlDebuffDuration);
        RegisterPlayerDebuffStatusEffect(objectDb, AdrenalineDrainStatusName, AdrenalineDrainStatusHash, "$cm_modifier_adrenaline_drain_name", "$cm_status_adrenaline_drain_tooltip", GetAdrenalineDrainSprite(), AdrenalineDrainDefaultDuration);
        RegisterPlayerDebuffStatusEffect(objectDb, CorrosiveStatusName, CorrosiveStatusHash, "$cm_modifier_corrosive_name", "$cm_status_corrosive_tooltip", GetCorrosiveSprite());
    }

    private static void RegisterPlayerDebuffStatusEffect(ObjectDB objectDb, string internalName, int hash, string displayName, string tooltip, Sprite icon, float duration = PlayerDebuffDuration)
    {
        if (objectDb.GetStatusEffect(hash) != null)
        {
            return;
        }

        SE_Stats status = ScriptableObject.CreateInstance<SE_Stats>();
        status.name = internalName;
        status.m_name = displayName;
        status.m_tooltip = tooltip;
        status.m_ttl = duration;
        status.m_icon = icon;
        objectDb.m_StatusEffects.Add(status);
    }

    private static void ApplyPlayerIncomingDebuffs(Player player, HitData hit)
    {
        if (hit.GetTotalDamage() <= 0f ||
            !TryGetActivePlayerDebuffPower(player, ExposedStatusHash, PlayerExposedPowerKey, PlayerExposedUntilKey, state => state.Exposed, ExposedDefaultPower, out float power))
        {
            return;
        }

        hit.m_damage.Modify(1f + power);
    }

    private static void ApplyPlayerOutgoingDebuffs(Player player, HitData hit)
    {
        if (hit.GetTotalDamage() <= 0f ||
            !TryGetActivePlayerDebuffPower(player, WeakenedStatusHash, PlayerWeakenedPowerKey, PlayerWeakenedUntilKey, state => state.Weakened, WeakenedDefaultPower, out float power))
        {
            return;
        }

        hit.m_damage.Modify(1f - power);
    }

    internal static bool ShouldUpdatePlayerControlDebuffs(Player player)
    {
        if (player == null ||
            player.m_nview == null ||
            !player.m_nview.IsValid() ||
            !player.m_nview.IsOwner())
        {
            return false;
        }

        int id = player.GetInstanceID();
        if (ActivePlayerRuntimeDebuffs.ContainsKey(id) || PlayerControlDebuffWakeIds.Contains(id))
        {
            return true;
        }

        float now = Time.unscaledTime;
        if (NextInactivePlayerDebuffProbeTimes.TryGetValue(id, out float nextProbe) && now < nextProbe)
        {
            return false;
        }

        NextInactivePlayerDebuffProbeTimes[id] = now + InactivePlayerDebuffProbeInterval;
        return true;
    }

    internal static void UpdatePlayerControlDebuffs(Player player)
    {
        if (player == null)
        {
            return;
        }

        int id = player.GetInstanceID();
        if (!CreatureLevelManager.IsLevelSystemEnabled())
        {
            if (ActivePlayerRuntimeDebuffs.TryGetValue(id, out PlayerRuntimeDebuffs disabledRuntime))
            {
                RestoreCrippling(player, disabledRuntime);
                ClearCorrosiveTracking(disabledRuntime);
                ActivePlayerRuntimeDebuffs.Remove(id);
            }

            PlayerControlDebuffWakeIds.Remove(id);
            NextInactivePlayerDebuffProbeTimes[id] = Time.unscaledTime + InactivePlayerDebuffProbeInterval;

            return;
        }

        bool hasCrippling = TryGetActivePlayerDebuffPowers(
            player,
            CripplingStatusHash,
            PlayerCripplingPowerKey,
            PlayerCripplingJumpPowerKey,
            PlayerCripplingUntilKey,
            state => state.Crippling,
            state => state.CripplingJump,
            CripplingDefaultPower,
            CripplingDefaultPower,
            out float cripplingMovement,
            out float cripplingJump);
        bool hasDisruptive = TryGetActivePlayerDebuffPowers(
            player,
            DisruptiveStatusHash,
            PlayerDisruptivePowerKey,
            PlayerDisruptiveEitrPowerKey,
            PlayerDisruptiveUntilKey,
            state => state.Disruptive,
            state => state.DisruptiveEitr,
            DisruptiveDefaultPower,
            DisruptiveDefaultPower,
            out float disruptiveStamina,
            out float disruptiveEitr);
        float corrosivePower = 0f;
        bool hasCorrosive = ReferenceEquals(player, Player.m_localPlayer) && TryGetActivePlayerDebuffPower(
            player,
            CorrosiveStatusHash,
            PlayerCorrosivePowerKey,
            PlayerCorrosiveUntilKey,
            state => state.Corrosive,
            CorrosiveDefaultPower,
            out corrosivePower);
        if (!hasCrippling && !hasDisruptive && !hasCorrosive && !ActivePlayerRuntimeDebuffs.TryGetValue(id, out _))
        {
            PlayerControlDebuffWakeIds.Remove(id);
            NextInactivePlayerDebuffProbeTimes[id] = Time.unscaledTime + InactivePlayerDebuffProbeInterval;
            return;
        }

        if (!ActivePlayerRuntimeDebuffs.TryGetValue(id, out PlayerRuntimeDebuffs runtime))
        {
            runtime = new PlayerRuntimeDebuffs();
            ActivePlayerRuntimeDebuffs[id] = runtime;
        }

        if (hasCrippling)
        {
            ApplyCrippling(player, runtime, cripplingMovement, cripplingJump);
        }
        else
        {
            RestoreCrippling(player, runtime);
        }

        if (hasDisruptive)
        {
            ApplyDisruptive(player, runtime, disruptiveStamina, disruptiveEitr);
        }
        else
        {
            runtime.LastStamina = -1f;
            runtime.LastEitr = -1f;
        }

        if (hasCorrosive)
        {
            ApplyCorrosiveDurability(player, runtime, corrosivePower);
        }
        else
        {
            ClearCorrosiveTracking(runtime);
        }

        if (!hasCrippling && !hasDisruptive && !hasCorrosive && !runtime.MovementApplied)
        {
            ActivePlayerRuntimeDebuffs.Remove(id);
            PlayerControlDebuffWakeIds.Remove(id);
            NextInactivePlayerDebuffProbeTimes[id] = Time.unscaledTime + InactivePlayerDebuffProbeInterval;
        }
    }

    private static void ApplyCrippling(Player player, PlayerRuntimeDebuffs runtime, float movementPower, float jumpPower)
    {
        movementPower = Mathf.Clamp01(movementPower);
        jumpPower = Mathf.Clamp01(jumpPower);
        if (runtime.MovementApplied &&
            Mathf.Approximately(runtime.MovementPower, movementPower) &&
            Mathf.Approximately(runtime.JumpPower, jumpPower))
        {
            return;
        }

        RestoreCrippling(player, runtime);
        runtime.OriginalCrouchSpeed = player.m_crouchSpeed;
        runtime.OriginalWalkSpeed = player.m_walkSpeed;
        runtime.OriginalSpeed = player.m_speed;
        runtime.OriginalRunSpeed = player.m_runSpeed;
        runtime.OriginalJumpForce = player.m_jumpForce;
        runtime.OriginalJumpForceForward = player.m_jumpForceForward;
        runtime.MovementPower = movementPower;
        runtime.JumpPower = jumpPower;
        runtime.MovementApplied = true;

        float movementMultiplier = 1f - movementPower;
        player.m_crouchSpeed *= movementMultiplier;
        player.m_walkSpeed *= movementMultiplier;
        player.m_speed *= movementMultiplier;
        player.m_runSpeed *= movementMultiplier;
        float jumpMultiplier = 1f - jumpPower;
        player.m_jumpForce *= jumpMultiplier;
        player.m_jumpForceForward *= jumpMultiplier;
    }

    private static void RestoreCrippling(Player player, PlayerRuntimeDebuffs runtime)
    {
        if (!runtime.MovementApplied)
        {
            return;
        }

        player.m_crouchSpeed = runtime.OriginalCrouchSpeed;
        player.m_walkSpeed = runtime.OriginalWalkSpeed;
        player.m_speed = runtime.OriginalSpeed;
        player.m_runSpeed = runtime.OriginalRunSpeed;
        player.m_jumpForce = runtime.OriginalJumpForce;
        player.m_jumpForceForward = runtime.OriginalJumpForceForward;
        runtime.MovementApplied = false;
        runtime.MovementPower = 0f;
        runtime.JumpPower = 0f;
    }

    private static void ApplyDisruptive(Player player, PlayerRuntimeDebuffs runtime, float staminaPower, float eitrPower)
    {
        staminaPower = Mathf.Clamp01(staminaPower);
        eitrPower = Mathf.Clamp01(eitrPower);
        float stamina = player.GetStamina();
        if (runtime.LastStamina >= 0f && stamina > runtime.LastStamina)
        {
            player.UseStamina((stamina - runtime.LastStamina) * staminaPower);
            stamina = player.GetStamina();
        }

        runtime.LastStamina = stamina;

        float eitr = player.GetEitr();
        if (runtime.LastEitr >= 0f && eitr > runtime.LastEitr)
        {
            player.UseEitr((eitr - runtime.LastEitr) * eitrPower);
            eitr = player.GetEitr();
        }

        runtime.LastEitr = eitr;
    }

    private static void ApplyCorrosiveDurability(Player player, PlayerRuntimeDebuffs runtime, float power)
    {
        Inventory inventory = player.GetInventory();
        if (inventory == null)
        {
            ClearCorrosiveTracking(runtime);
            return;
        }

        power = Mathf.Clamp01(power);
        List<ItemDrop.ItemData> currentItems = runtime.CurrentDurabilityItems;
        Dictionary<ItemDrop.ItemData, float> snapshots = runtime.DurabilitySnapshots;
        currentItems.Clear();

        foreach (ItemDrop.ItemData? item in inventory.GetAllItems())
        {
            if (item == null || !IsCorrosiveDurabilityTarget(item))
            {
                continue;
            }

            currentItems.Add(item);
            float currentDurability = item.m_durability;
            if (snapshots.TryGetValue(item, out float previousDurability) && currentDurability < previousDurability && power > 0f)
            {
                float extraLoss = (previousDurability - currentDurability) * power;
                item.m_durability = Mathf.Max(0f, currentDurability - extraLoss);
                currentDurability = item.m_durability;
            }

            snapshots[item] = currentDurability;
        }

        List<ItemDrop.ItemData> removedItems = runtime.RemovedDurabilityItems;
        removedItems.Clear();
        foreach (ItemDrop.ItemData item in snapshots.Keys)
        {
            if (!currentItems.Contains(item))
            {
                removedItems.Add(item);
            }
        }

        foreach (ItemDrop.ItemData item in removedItems)
        {
            snapshots.Remove(item);
        }
    }

    private static bool IsCorrosiveDurabilityTarget(ItemDrop.ItemData item)
    {
        ItemDrop.ItemData.SharedData? shared = item.m_shared;
        if (!item.m_equipped || shared == null || !shared.m_useDurability)
        {
            return false;
        }

        ItemDrop.ItemData.ItemType itemType = shared.m_itemType;
        return item.IsWeapon() ||
               itemType == ItemDrop.ItemData.ItemType.Shield ||
               itemType == ItemDrop.ItemData.ItemType.Helmet ||
               itemType == ItemDrop.ItemData.ItemType.Chest ||
               itemType == ItemDrop.ItemData.ItemType.Legs ||
               itemType == ItemDrop.ItemData.ItemType.Shoulder;
    }

    private static void ClearCorrosiveTracking(PlayerRuntimeDebuffs runtime)
    {
        runtime.DurabilitySnapshots.Clear();
        runtime.CurrentDurabilityItems.Clear();
        runtime.RemovedDurabilityItems.Clear();
    }

    private static void TryDrainPlayerAdrenaline(Character attacker, Player player)
    {
        ZNetView? nview = player.m_nview;
        if (nview == null ||
            !nview.IsValid() ||
            !nview.IsOwner() ||
            !TryGetProcModifierState(attacker, ModifierMask.AdrenalineDrain, AdrenalineDrainChanceKey, out ZDO zdo, out float chance) ||
            UnityEngine.Random.Range(0f, 100f) >= chance)
        {
            return;
        }

        float power = Mathf.Clamp01(zdo.GetFloat(AdrenalineDrainPowerKey, AdrenalineDrainDefaultPower));
        float amount = Mathf.Max(0f, player.GetAdrenaline()) * power;
        if (amount > 0f)
        {
            player.AddAdrenaline(-amount);
        }

        float gainReduction = Mathf.Clamp01(zdo.GetFloat(AdrenalineDrainGainReductionKey, AdrenalineDrainDefaultGainReduction));
        float duration = ResolvePlayerDebuffDuration(zdo.GetFloat(AdrenalineDrainDurationKey, AdrenalineDrainDefaultDuration), AdrenalineDrainDefaultDuration);
        if (gainReduction <= 0f)
        {
            return;
        }

        StatusEffect status = player.GetSEMan().AddStatusEffect(AdrenalineDrainStatusHash, true, 0, 0f);
        if (status is SE_Stats stats)
        {
            stats.m_ttl = duration;
            stats.m_adrenalineModifier = -gainReduction;
        }
    }

    private static void TryApplyPlayerDebuff(
        Character attacker,
        Player player,
        ModifierMask modifier,
        string chanceKey,
        string powerKey,
        float fallbackPower,
        int statusHash,
        string playerPowerKey,
        string playerUntilKey,
        string durationKey,
        float fallbackDuration,
        Action<PlayerDebuffPowers, float> powerSetter)
    {
        if (!TryGetProcModifierState(attacker, modifier, chanceKey, out ZDO zdo, out float chance) ||
            UnityEngine.Random.Range(0f, 100f) >= chance)
        {
            return;
        }

        float power = Mathf.Clamp01(zdo.GetFloat(powerKey, fallbackPower));
        if (power <= 0f)
        {
            return;
        }

        float duration = ResolvePlayerDebuffDuration(zdo.GetFloat(durationKey, fallbackDuration), fallbackDuration);
        StatusEffect status = player.GetSEMan().AddStatusEffect(statusHash, true, 0, 0f);
        if (status == null)
        {
            return;
        }

        status.m_ttl = duration;
        int id = player.GetInstanceID();
        WakePlayerControlDebuffUpdate(id, statusHash);
        if (!ActivePlayerDebuffs.TryGetValue(id, out PlayerDebuffPowers state))
        {
            state = new PlayerDebuffPowers();
            ActivePlayerDebuffs[id] = state;
        }

        powerSetter(state, power);
        StorePlayerDebuff(player, playerPowerKey, playerUntilKey, power, duration);
    }

    private static void TryApplySplitPlayerDebuff(
        Character attacker,
        Player player,
        ModifierMask modifier,
        string chanceKey,
        string primaryPowerKey,
        float fallbackPrimaryPower,
        string secondaryPowerKey,
        float fallbackSecondaryPower,
        int statusHash,
        string playerPrimaryPowerKey,
        string playerSecondaryPowerKey,
        string playerUntilKey,
        string durationKey,
        float fallbackDuration,
        Action<PlayerDebuffPowers, float> primarySetter,
        Action<PlayerDebuffPowers, float> secondarySetter)
    {
        if (!TryGetProcModifierState(attacker, modifier, chanceKey, out ZDO zdo, out float chance) ||
            UnityEngine.Random.Range(0f, 100f) >= chance)
        {
            return;
        }

        float primaryPower = Mathf.Clamp01(zdo.GetFloat(primaryPowerKey, fallbackPrimaryPower));
        float secondaryPower = Mathf.Clamp01(zdo.GetFloat(secondaryPowerKey, fallbackSecondaryPower));
        if (primaryPower <= 0f && secondaryPower <= 0f)
        {
            return;
        }

        float duration = ResolvePlayerDebuffDuration(zdo.GetFloat(durationKey, fallbackDuration), fallbackDuration);
        StatusEffect status = player.GetSEMan().AddStatusEffect(statusHash, true, 0, 0f);
        if (status == null)
        {
            return;
        }

        status.m_ttl = duration;
        int id = player.GetInstanceID();
        WakePlayerControlDebuffUpdate(id, statusHash);
        if (!ActivePlayerDebuffs.TryGetValue(id, out PlayerDebuffPowers state))
        {
            state = new PlayerDebuffPowers();
            ActivePlayerDebuffs[id] = state;
        }

        primarySetter(state, primaryPower);
        secondarySetter(state, secondaryPower);
        StorePlayerDebuff(player, playerPrimaryPowerKey, playerUntilKey, primaryPower, duration, playerSecondaryPowerKey, secondaryPower);
    }

    private static void WakePlayerControlDebuffUpdate(int playerId, int statusHash)
    {
        if (statusHash != CripplingStatusHash && statusHash != DisruptiveStatusHash && statusHash != CorrosiveStatusHash)
        {
            return;
        }

        PlayerControlDebuffWakeIds.Add(playerId);
        NextInactivePlayerDebuffProbeTimes.Remove(playerId);
    }

    private static bool TryGetProcModifierState(Character character, ModifierMask modifier, string chanceKey, out ZDO zdo, out float chance)
    {
        zdo = null!;
        chance = 0f;
        if (!TryGetZdo(character, out zdo) ||
            !HasModifier(zdo, modifier) ||
            !CreatureLevelManager.AllowsModifierEffects(character))
        {
            return false;
        }

        chance = ClampChance(zdo.GetFloat(chanceKey, 0f));
        return chance > 0f;
    }

    private static bool TryGetActivePlayerDebuffPower(Player player, int statusHash, string powerKey, string untilKey, Func<PlayerDebuffPowers, float> selector, float fallbackPower, out float power)
    {
        power = 0f;
        if (TryGetStoredPlayerDebuff(player, powerKey, untilKey, out power))
        {
            return true;
        }

        SEMan seMan = player.GetSEMan();
        if (seMan == null || !seMan.HaveStatusEffect(statusHash))
        {
            return false;
        }

        if (!ActivePlayerDebuffs.TryGetValue(player.GetInstanceID(), out PlayerDebuffPowers state))
        {
            power = fallbackPower;
            return power > 0f;
        }

        power = Mathf.Clamp01(selector(state));
        if (power <= 0f)
        {
            power = fallbackPower;
        }

        return power > 0f;
    }

    private static bool TryGetActivePlayerDebuffPowers(
        Player player,
        int statusHash,
        string primaryPowerKey,
        string secondaryPowerKey,
        string untilKey,
        Func<PlayerDebuffPowers, float> primarySelector,
        Func<PlayerDebuffPowers, float> secondarySelector,
        float fallbackPrimaryPower,
        float fallbackSecondaryPower,
        out float primaryPower,
        out float secondaryPower)
    {
        primaryPower = 0f;
        secondaryPower = 0f;
        if (TryGetStoredPlayerDebuffState(player, untilKey, out ZDO zdo))
        {
            primaryPower = Mathf.Clamp01(zdo.GetFloat(primaryPowerKey, fallbackPrimaryPower));
            secondaryPower = Mathf.Clamp01(zdo.GetFloat(secondaryPowerKey, fallbackSecondaryPower));
            return primaryPower > 0f || secondaryPower > 0f;
        }

        SEMan seMan = player.GetSEMan();
        if (seMan == null || !seMan.HaveStatusEffect(statusHash))
        {
            return false;
        }

        if (!ActivePlayerDebuffs.TryGetValue(player.GetInstanceID(), out PlayerDebuffPowers state))
        {
            primaryPower = fallbackPrimaryPower;
            secondaryPower = fallbackSecondaryPower;
            return primaryPower > 0f || secondaryPower > 0f;
        }

        primaryPower = Mathf.Clamp01(primarySelector(state));
        secondaryPower = Mathf.Clamp01(secondarySelector(state));
        return primaryPower > 0f || secondaryPower > 0f;
    }

    private static void StorePlayerDebuff(
        Player player,
        string powerKey,
        string untilKey,
        float power,
        float duration,
        string? secondaryPowerKey = null,
        float secondaryPower = 0f)
    {
        ZNetView? nview = player.m_nview;
        if (nview == null || !nview.IsValid())
        {
            return;
        }

        ZDO zdo = nview.GetZDO();
        if (zdo == null)
        {
            return;
        }

        zdo.Set(powerKey, Mathf.Clamp01(power));
        if (secondaryPowerKey != null)
        {
            zdo.Set(secondaryPowerKey, Mathf.Clamp01(secondaryPower));
        }

        zdo.Set(untilKey, GetNetworkTimeSeconds() + Mathf.Max(0.1f, duration));
    }

    private static bool TryGetStoredPlayerDebuff(Player player, string powerKey, string untilKey, out float power)
    {
        power = 0f;
        if (!TryGetStoredPlayerDebuffState(player, untilKey, out ZDO zdo))
        {
            return false;
        }

        power = Mathf.Clamp01(zdo.GetFloat(powerKey, 0f));
        return power > 0f;
    }

    private static bool TryGetStoredPlayerDebuffState(Player player, string untilKey, out ZDO zdo)
    {
        zdo = null!;
        ZNetView? nview = player.m_nview;
        if (nview == null || !nview.IsValid())
        {
            return false;
        }

        zdo = nview.GetZDO();
        return zdo != null && zdo.GetFloat(untilKey, 0f) > GetNetworkTimeSeconds();
    }

    private static float GetNetworkTimeSeconds()
    {
        return ZNet.instance != null ? (float)ZNet.instance.GetTimeSeconds() : Time.time;
    }

    internal static float ResolveFinalDeathwardDamage(Character target, HitData hit, float finalDamage)
    {
        if (target == null ||
            hit == null ||
            finalDamage <= 0.1f ||
            float.IsNaN(finalDamage) ||
            float.IsInfinity(finalDamage))
        {
            return finalDamage;
        }

        HitData.DamageTypes originalDamage = hit.m_damage;
        float originalHealth = float.NaN;
        ZDO? committedZdo = null;
        int originalActivationCount = 0;
        float originalNextReadyTime = 0f;
        bool commitStarted = false;
        try
        {
            ZNetView? nview = target.m_nview;
            if (nview != null && nview.IsValid() && !nview.IsOwner())
            {
                return finalDamage;
            }

            if (!TryGetDeathwardHealth(target, out float healthRatio))
            {
                return finalDamage;
            }

            float currentHealth = target.GetHealth();
            float maxHealth = target.GetMaxHealth();
            if (target.InGodMode() ||
                target.InGhostMode() ||
                currentHealth <= 0f ||
                float.IsNaN(currentHealth) ||
                float.IsInfinity(currentHealth) ||
                maxHealth <= 0f ||
                float.IsNaN(maxHealth) ||
                float.IsInfinity(maxHealth) ||
                currentHealth - finalDamage > 0f)
            {
                return finalDamage;
            }

            if (!TryGetZdo(target, out ZDO zdo))
            {
                return finalDamage;
            }

            originalActivationCount = zdo.GetInt(DeathwardActivationCountKey, 0);
            int activationCount = Math.Max(0, originalActivationCount);
            int maxActivations = ResolveDeathwardMaxActivations(zdo.GetInt(DeathwardMaxActivationsKey, DeathwardDefaultMaxActivations));
            if (activationCount >= maxActivations)
            {
                return finalDamage;
            }

            float cooldown = ResolveDeathwardCooldown(zdo.GetFloat(DeathwardCooldownKey, DeathwardDefaultCooldown));
            originalHealth = currentHealth;
            committedZdo = zdo;
            originalNextReadyTime = zdo.GetFloat(DeathwardNextReadyTimeKey, 0f);
            commitStarted = true;
            zdo.Set(DeathwardActivationCountKey, activationCount + 1);
            zdo.Set(DeathwardNextReadyTimeKey, GetNetworkTimeSeconds() + cooldown);
            InvalidateModifierHudState(target);
            hit.m_damage.Modify(0f);
            target.SetHealth(Mathf.Max(1f, maxHealth * healthRatio));
            FinalDeathwardConsumedHits.Add(hit);
            PlayDeathwardTriggerEffects(target.GetCenterPoint());
            return 0f;
        }
        catch (Exception exception)
        {
            FinalDeathwardConsumedHits.Remove(hit);
            if (commitStarted)
            {
                TryRollbackDeathwardCommit(
                    target,
                    hit,
                    originalDamage,
                    originalHealth,
                    committedZdo,
                    originalActivationCount,
                    originalNextReadyTime);
            }

            ReportDeathwardRuntimeFailure(exception);
            return finalDamage;
        }
    }

    private static void TryRollbackDeathwardCommit(
        Character target,
        HitData hit,
        HitData.DamageTypes originalDamage,
        float originalHealth,
        ZDO? zdo,
        int originalActivationCount,
        float originalNextReadyTime)
    {
        try
        {
            hit.m_damage = originalDamage;
        }
        catch
        {
            // Best-effort rollback only; the original damage flow must continue.
        }

        try
        {
            if (!float.IsNaN(originalHealth) && !float.IsInfinity(originalHealth))
            {
                target.SetHealth(originalHealth);
            }
        }
        catch
        {
            // Best-effort rollback only; the original damage flow must continue.
        }

        try
        {
            if (zdo != null)
            {
                zdo.Set(DeathwardActivationCountKey, originalActivationCount);
                zdo.Set(DeathwardNextReadyTimeKey, originalNextReadyTime);
                InvalidateModifierHudState(target);
            }
        }
        catch
        {
            // Best-effort rollback only; the original damage flow must continue.
        }
    }

    private static void ReportDeathwardRuntimeFailure(Exception exception)
    {
        if (DeathwardRuntimeFailureReported)
        {
            return;
        }

        DeathwardRuntimeFailureReported = true;
        try
        {
            CreatureManagerPlugin.Log.LogWarning(
                $"[Deathward] final-damage evaluation failed open; {exception.GetType().Name}: {exception.Message}");
        }
        catch
        {
            // Deathward safeguards must never interfere with combat.
        }
    }

    internal static void UpdatePassiveModifiers(Character character)
    {
        if (!CreatureLevelManager.IsLevelSystemEnabled() || character == null || character.IsPlayer())
        {
            return;
        }

        if (!TryGetZdo(character, out ZDO zdo) || !zdo.GetBool(AppliedKey, false))
        {
            UntrackRuntimeModifiers(character);
            return;
        }

        ZNetView? nview = character.m_nview;
        if (nview == null || !nview.IsValid() || !nview.IsOwner())
        {
            return;
        }

        ApplyRuntimeModifierStats(character, zdo);
        if (!CreatureLevelManager.AllowsModifierEffects(character))
        {
            return;
        }

        float now = Time.time;
        ModifierMask mask = GetStoredModifierMask(zdo);
        PassiveModifierSchedule schedule = GetPassiveModifierSchedule(character, zdo, now);
        if (HasModifier(mask, ModifierMask.Regenerating) &&
            zdo.GetFloat(RegeneratingPowerKey, 0f) > 0f &&
            now >= schedule.NextRegeneration)
        {
            schedule.NextRegeneration = now + 1f;
            float regen = character.GetMaxHealth() * Mathf.Clamp01(zdo.GetFloat(RegeneratingPowerKey, RegeneratingDefaultPower));
            if (regen > 0f)
            {
                character.Heal(regen, false);
            }
        }

        if (HasModifier(mask, ModifierMask.Chameleon) &&
            now >= schedule.NextChameleon)
        {
            UpdateChameleon(character, zdo, now, schedule);
        }

        if (HasModifier(mask, ModifierMask.Blamer) &&
            now >= schedule.NextBlamer)
        {
            UpdateBlamer(character, zdo, now, schedule);
        }

        TrackRuntimeModifiers(character, zdo);
    }

    private static void InitializeChameleonState(Character character, ZDO zdo)
    {
        ChameleonDamageType selected = SelectChameleonDamageType(character, ChameleonDamageType.None);
        zdo.Set(ChameleonTypeKey, (int)selected);
        float interval = ResolveChameleonInterval(zdo.GetFloat(ChameleonIntervalKey, ChameleonDefaultInterval));
        GetPassiveModifierSchedule(character, zdo, Time.time).NextChameleon = Time.time + interval;
    }

    private static void UpdateChameleon(
        Character character,
        ZDO zdo,
        float now,
        PassiveModifierSchedule schedule)
    {
        float interval = ResolveChameleonInterval(zdo.GetFloat(ChameleonIntervalKey, ChameleonDefaultInterval));
        schedule.NextChameleon = now + interval;
        BaseAI? baseAI = character.GetBaseAI();
        if (baseAI == null || !baseAI.IsAlerted())
        {
            return;
        }

        ChameleonDamageType current = GetChameleonDamageType(zdo);
        ChameleonDamageType selected = SelectChameleonDamageType(character, current);
        if (selected != current)
        {
            zdo.Set(ChameleonTypeKey, (int)selected);
            InvalidateModifierHudState(character);
        }
    }

    private static void UpdateBlamer(
        Character character,
        ZDO zdo,
        float now,
        PassiveModifierSchedule schedule)
    {
        schedule.NextBlamer = now + BlamerTickInterval;
        if (character.IsTamed() ||
            CreatureKarmaManager.IsKarmaSummonedCreature(character) ||
            character.GetBaseAI() is not MonsterAI monsterAI ||
            !IsBlamerFleeTargetValid(character, monsterAI, zdo))
        {
            SetBlamerActive(character, zdo, false);
            return;
        }

        float karmaPerSecond = ResolveBlamerKarmaPerSecond(zdo.GetFloat(BlamerKarmaPerSecondKey, BlamerDefaultKarmaPerSecond));
        float cap = ResolveBlamerMaxKarmaGain(zdo.GetFloat(BlamerMaxKarmaGainKey, BlamerDefaultMaxKarmaGain));
        float accumulated = GetBlamerAccumulatedKarma(zdo);
        float amount = cap > 0f ? Mathf.Min(karmaPerSecond, Mathf.Max(0f, cap - accumulated)) : karmaPerSecond;
        if (amount <= 0f)
        {
            if (cap > 0f && accumulated >= cap)
            {
                ExhaustBlamer(character, zdo);
            }
            else
            {
                SetBlamerActive(character, zdo, false);
            }

            return;
        }

        // Flee behavior and its HUD marker describe the creature's current state. They must
        // not depend on a round trip to the authoritative Karma server.
        SetBlamerActive(character, zdo, true);
        BlamerKarmaAddResult addResult = TryAddBlamerKarma(character, zdo, amount, out float confirmedAccumulated);
        if (addResult == BlamerKarmaAddResult.Failed)
        {
            return;
        }

        if (addResult == BlamerKarmaAddResult.Pending)
        {
            return;
        }

        CommitBlamerKarma(character, zdo, confirmedAccumulated, true);
    }

    private static BlamerKarmaAddResult TryAddBlamerKarma(
        Character character,
        ZDO zdo,
        float amount,
        out float confirmedAccumulated)
    {
        float current = GetBlamerAccumulatedKarma(zdo);
        confirmedAccumulated = current;
        if (ZNet.instance == null)
        {
            if (!CreatureKarmaManager.TryAddBlamerKarma(character.transform.position, amount))
            {
                return BlamerKarmaAddResult.Failed;
            }

            confirmedAccumulated = current + amount;
            return BlamerKarmaAddResult.Added;
        }

        if (ZNet.instance.IsServer())
        {
            bool granted = TryGrantServerBlamerKarma(
                character,
                zdo,
                out confirmedAccumulated,
                out ServerBlamerKarmaState state);
            state.RequestOwner = zdo.GetOwner();
            state.HasCachedResponse = false;
            return granted || confirmedAccumulated > current
                ? BlamerKarmaAddResult.Added
                : BlamerKarmaAddResult.Failed;
        }

        ZNetView? nview = character.m_nview;
        if (nview == null ||
            !nview.IsValid() ||
            !nview.IsOwner() ||
            ZRoutedRpc.instance == null)
        {
            return BlamerKarmaAddResult.Failed;
        }

        if (character.GetBaseAI() is not MonsterAI monsterAI ||
            TryGetMonsterAiTarget(monsterAI) is not Player target ||
            target.IsDead())
        {
            return BlamerKarmaAddResult.Failed;
        }

        ZDOID targetId = target.GetZDOID();
        if (targetId == ZDOID.None)
        {
            return BlamerKarmaAddResult.Failed;
        }

        int characterId = character.GetInstanceID();
        float now = Time.unscaledTime;
        if (PendingBlamerKarmaRequests.TryGetValue(characterId, out PendingBlamerKarmaRequest pending))
        {
            if (pending.TargetId != targetId)
            {
                PendingBlamerKarmaRequests.Remove(characterId);
            }
            else
            {
                if (now - pending.SentAt < BlamerKarmaRequestTimeout)
                {
                    return BlamerKarmaAddResult.Pending;
                }

                pending.SentAt = now;
                return SendBlamerKarmaRequest(nview, pending)
                    ? BlamerKarmaAddResult.Pending
                    : BlamerKarmaAddResult.Failed;
            }
        }

        long requestId = unchecked(++NextBlamerKarmaRequestId);
        if (requestId <= 0)
        {
            requestId = NextBlamerKarmaRequestId = 1;
        }

        PendingBlamerKarmaRequests[characterId] = new PendingBlamerKarmaRequest
        {
            RequestId = requestId,
            SentAt = now,
            TargetId = targetId
        };

        return SendBlamerKarmaRequest(nview, PendingBlamerKarmaRequests[characterId])
            ? BlamerKarmaAddResult.Pending
            : BlamerKarmaAddResult.Failed;
    }

    private static bool SendBlamerKarmaRequest(ZNetView nview, PendingBlamerKarmaRequest pending)
    {
        try
        {
            nview.InvokeRPC(
                ZRoutedRpc.instance.GetServerPeerID(),
                BlamerKarmaRequestRpc,
                pending.RequestId,
                pending.TargetId);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void RPC_BlamerKarmaRequest(
        Character character,
        long sender,
        long requestId,
        ZDOID targetId)
    {
        if (requestId <= 0 ||
            ZNet.instance == null ||
            !ZNet.instance.IsServer() ||
            character.m_nview == null ||
            !character.m_nview.IsValid() ||
            !TryGetZdo(character, out ZDO zdo))
        {
            return;
        }

        bool accepted = false;
        float observed = GetBlamerAccumulatedKarma(zdo);

        if (zdo.GetOwner() != sender)
        {
            LogBlamerRequestRejection(
                character,
                zdo,
                sender,
                $"ZDO owner {zdo.GetOwner()} does not match the request sender");
            SendBlamerKarmaResponse(character, sender, requestId, false, Mathf.Max(0f, observed));
            return;
        }

        ServerBlamerKarmaState state = GetServerBlamerKarmaState(character, zdo);
        if (state.RequestOwner != sender)
        {
            state.RequestOwner = sender;
            state.HasCachedResponse = false;
        }

        if (state.HasCachedResponse && state.LastRequestId == requestId)
        {
            SendBlamerKarmaResponse(
                character,
                sender,
                requestId,
                state.LastAccepted,
                state.LastResponseAccumulated);
            return;
        }

        if (state.HasCachedResponse && requestId < state.LastRequestId)
        {
            SendBlamerKarmaResponse(character, sender, requestId, false, state.Accumulated);
            return;
        }

        if (IsServerBlamerRequestValid(character, zdo, targetId, out string rejectionReason))
        {
            accepted = TryGrantServerBlamerKarma(character, zdo, out _, out state);
            if (!accepted && Time.unscaledTime >= state.NextAllowedTime)
            {
                LogBlamerRequestRejection(character, zdo, sender, "the authoritative Karma grant was unavailable");
            }
        }
        else
        {
            LogBlamerRequestRejection(character, zdo, sender, rejectionReason);
        }

        state.LastRequestId = requestId;
        state.HasCachedResponse = true;
        state.LastAccepted = accepted;
        state.LastResponseAccumulated = state.Accumulated;
        SendBlamerKarmaResponse(character, sender, requestId, accepted, state.Accumulated);
    }

    private static bool IsServerBlamerRequestValid(
        Character character,
        ZDO zdo,
        ZDOID targetId,
        out string rejectionReason)
    {
        rejectionReason = "unknown validation failure";
        float fleeHealthRatio = ResolveBlamerFleeHealthRatio(
            zdo.GetFloat(BlamerFleeHealthRatioKey, BlamerDefaultFleeHealthRatio));
        if (!zdo.GetBool(AppliedKey, false) ||
            !HasModifier(GetStoredModifierMask(zdo), ModifierMask.Blamer))
        {
            rejectionReason = "the Blamer modifier is not present in the authoritative ZDO";
            return false;
        }

        if (fleeHealthRatio <= 0f)
        {
            rejectionReason = "the configured flee-health ratio is zero";
            return false;
        }

        if (character.IsDead() || character.IsTamed() || character.GetBaseAI() is not MonsterAI)
        {
            rejectionReason = "the creature is dead, tamed, or has no MonsterAI";
            return false;
        }

        if (CreatureKarmaManager.IsKarmaSummonedCreature(character) ||
            !CreatureLevelManager.AllowsModifierEffects(character))
        {
            rejectionReason = "modifier effects are disabled for this creature";
            return false;
        }

        float maxHealth = Mathf.Max(0.01f, character.GetMaxHealth());
        float syncedHealth = zdo.GetFloat(ZDOVars.s_health, character.GetHealth());
        float syncedHealthRatio = Mathf.Clamp01(syncedHealth / maxHealth);
        if (syncedHealthRatio >= fleeHealthRatio)
        {
            rejectionReason = $"server health {syncedHealthRatio:P0} is not below {fleeHealthRatio:P0}";
            return false;
        }

        if (!zdo.GetBool(ZDOVars.s_alert) || !zdo.GetBool(ZDOVars.s_haveTargetHash))
        {
            rejectionReason = "the server ZDO is not alerted or has no target";
            return false;
        }

        if (targetId == ZDOID.None || ZNetScene.instance == null)
        {
            rejectionReason = "the player target id or server scene is unavailable";
            return false;
        }

        GameObject targetObject = ZNetScene.instance.FindInstance(targetId);
        Player? target = targetObject != null ? targetObject.GetComponent<Player>() : null;
        if (target == null || target.IsDead())
        {
            rejectionReason = "the player target is missing or dead on the server";
            return false;
        }

        if (!IsHostileAttacker(character, target))
        {
            rejectionReason = "the target is not hostile to the creature";
            return false;
        }

        float distance = Vector3.Distance(zdo.GetPosition(), target.transform.position);
        if (distance > BlamerServerTargetValidationRange)
        {
            rejectionReason = $"the player target is {distance:0.#}m away";
            return false;
        }

        rejectionReason = "";
        return true;
    }

    private static void LogBlamerRequestRejection(
        Character character,
        ZDO zdo,
        long sender,
        string reason)
    {
        ZDOID characterId = zdo.m_uid;
        float now = Time.unscaledTime;
        if (characterId == ZDOID.None ||
            now < NextBlamerGlobalRejectionLogTime ||
            (NextBlamerRejectionLogTimes.TryGetValue(characterId, out float nextLogTime) && now < nextLogTime))
        {
            return;
        }

        NextBlamerRejectionLogTimes[characterId] = now + BlamerRejectionLogInterval;
        NextBlamerGlobalRejectionLogTime = now + BlamerGlobalRejectionLogInterval;
        CreatureManagerPlugin.Log.LogWarning(
            $"Blamer Karma request rejected for '{Utils.GetPrefabName(character.gameObject)}' " +
            $"({characterId}) from owner {sender}: {reason}.");
    }

    private static ServerBlamerKarmaState GetServerBlamerKarmaState(Character character, ZDO zdo)
    {
        ZDOID characterId = character.GetZDOID();
        if (!ServerBlamerKarmaStates.TryGetValue(characterId, out ServerBlamerKarmaState state))
        {
            state = new ServerBlamerKarmaState();
            ServerBlamerKarmaStates[characterId] = state;
        }

        float observed = GetBlamerAccumulatedKarma(zdo);
        state.Accumulated = Mathf.Max(state.Accumulated, observed);

        return state;
    }

    private static bool TryGrantServerBlamerKarma(
        Character character,
        ZDO zdo,
        out float accumulated,
        out ServerBlamerKarmaState state)
    {
        state = GetServerBlamerKarmaState(character, zdo);
        accumulated = state.Accumulated;
        float now = Time.unscaledTime;
        if (now < state.NextAllowedTime)
        {
            return false;
        }

        float power = ResolveBlamerKarmaPerSecond(zdo.GetFloat(BlamerKarmaPerSecondKey, BlamerDefaultKarmaPerSecond));
        float cap = ResolveBlamerMaxKarmaGain(zdo.GetFloat(BlamerMaxKarmaGainKey, BlamerDefaultMaxKarmaGain));
        float grant = cap > 0f
            ? Mathf.Min(power, Mathf.Max(0f, cap - state.Accumulated))
            : power;
        if (grant <= 0f || float.IsNaN(grant) || float.IsInfinity(grant))
        {
            return false;
        }

        float nextAccumulated = cap > 0f
            ? Mathf.Min(cap, state.Accumulated + grant)
            : state.Accumulated + grant;
        if (float.IsNaN(nextAccumulated) || float.IsInfinity(nextAccumulated))
        {
            return false;
        }

        Vector3 position = zdo.GetPosition();
        if (!IsFinite(position) || !CreatureKarmaManager.TryAddBlamerKarma(position, grant))
        {
            return false;
        }

        state.Accumulated = nextAccumulated;
        state.NextAllowedTime = now + BlamerServerMinimumRequestInterval;
        accumulated = state.Accumulated;
        return true;
    }

    private static bool IsFinite(Vector3 value)
    {
        return !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
               !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
               !float.IsNaN(value.z) && !float.IsInfinity(value.z);
    }

    private static void SendBlamerKarmaResponse(
        Character character,
        long target,
        long requestId,
        bool accepted,
        float accumulated)
    {
        if (character.m_nview != null && character.m_nview.IsValid())
        {
            try
            {
                character.m_nview.InvokeRPC(
                    target,
                    BlamerKarmaResponseRpc,
                    requestId,
                    accepted,
                    accumulated);
            }
            catch
            {
                // The owner retries the same request id and receives the cached response.
            }
        }
    }

    private static void RPC_BlamerKarmaResponse(
        Character character,
        long sender,
        long requestId,
        bool accepted,
        float accumulated)
    {
        if (!IsTrustedServerRpc(sender))
        {
            return;
        }

        int characterId = character.GetInstanceID();
        if (!PendingBlamerKarmaRequests.TryGetValue(characterId, out PendingBlamerKarmaRequest pending) ||
            pending.RequestId != requestId)
        {
            return;
        }

        PendingBlamerKarmaRequests.Remove(characterId);
        ZNetView? nview = character.m_nview;
        if (nview == null ||
            !nview.IsValid() ||
            !nview.IsOwner() ||
            !TryGetZdo(character, out ZDO zdo) ||
            !zdo.GetBool(AppliedKey, false) ||
            !HasModifier(GetStoredModifierMask(zdo), ModifierMask.Blamer))
        {
            return;
        }

        bool conditionsStillValid = !character.IsTamed() &&
                                    !CreatureKarmaManager.IsKarmaSummonedCreature(character) &&
                                    character.GetBaseAI() is MonsterAI monsterAI &&
                                    IsBlamerFleeTargetValid(character, monsterAI, zdo);
        CommitBlamerKarma(character, zdo, accumulated, conditionsStillValid);
    }

    private static void CommitBlamerKarma(
        Character character,
        ZDO zdo,
        float accumulated,
        bool active)
    {
        if (float.IsNaN(accumulated) || float.IsInfinity(accumulated))
        {
            return;
        }

        float cap = ResolveBlamerMaxKarmaGain(zdo.GetFloat(BlamerMaxKarmaGainKey, BlamerDefaultMaxKarmaGain));
        float current = GetBlamerAccumulatedKarma(zdo);
        float nextAccumulated = Mathf.Max(current, Mathf.Max(0f, accumulated));
        if (cap > 0f)
        {
            nextAccumulated = Mathf.Min(cap, nextAccumulated);
        }

        if (!Mathf.Approximately(current, nextAccumulated))
        {
            zdo.Set(BlamerAccumulatedKarmaKey, nextAccumulated);
        }

        if (cap > 0f && nextAccumulated >= cap)
        {
            ExhaustBlamer(character, zdo);
            return;
        }

        SetBlamerActive(character, zdo, active);
    }

    private static bool HasBlamerKarmaRemaining(ZDO zdo)
    {
        float cap = ResolveBlamerMaxKarmaGain(zdo.GetFloat(BlamerMaxKarmaGainKey, BlamerDefaultMaxKarmaGain));
        return cap <= 0f || GetBlamerAccumulatedKarma(zdo) < cap;
    }

    private static float GetBlamerAccumulatedKarma(ZDO zdo)
    {
        float accumulated = zdo.GetFloat(BlamerAccumulatedKarmaKey, 0f);
        return float.IsNaN(accumulated) || float.IsInfinity(accumulated)
            ? 0f
            : Mathf.Max(0f, accumulated);
    }

    private static void SetBlamerActive(Character character, ZDO zdo, bool active)
    {
        if (zdo.GetBool(BlamerActiveKey, false) == active)
        {
            return;
        }

        zdo.Set(BlamerActiveKey, active);
        InvalidateModifierHudState(character);
    }

    private static void ExhaustBlamer(Character character, ZDO zdo)
    {
        ModifierMask stored = GetStoredModifierMask(zdo);
        if (!HasModifier(stored, ModifierMask.Blamer))
        {
            return;
        }

        SetStoredModifierMask(zdo, stored & ~ModifierMask.Blamer);
        zdo.RemoveFloat(BlamerKarmaPerSecondKey);
        zdo.RemoveFloat(BlamerMaxKarmaGainKey);
        zdo.RemoveFloat(BlamerFleeHealthRatioKey);
        zdo.RemoveFloat(BlamerAccumulatedKarmaKey);
        zdo.RemoveInt(BlamerActiveKey);
        RefreshModifierHotPathState(character, zdo);
        InvalidateModifierHudState(character);
    }

    private static bool HasEligibleChameleonType(Character character)
    {
        if (character == null)
        {
            return false;
        }

        foreach (ChameleonDamageType type in ChameleonDamageTypes)
        {
            if (GetChameleonDamageModifier(character.m_damageModifiers, type) != HitData.DamageModifier.Immune)
            {
                return true;
            }
        }

        return false;
    }

    private static ChameleonDamageType SelectChameleonDamageType(Character character, ChameleonDamageType previous)
    {
        List<ChameleonDamageType> eligible = new(ChameleonDamageTypes.Length);
        foreach (ChameleonDamageType type in ChameleonDamageTypes)
        {
            if (GetChameleonDamageModifier(character.m_damageModifiers, type) != HitData.DamageModifier.Immune)
            {
                eligible.Add(type);
            }
        }

        if (eligible.Count == 0)
        {
            return ChameleonDamageType.None;
        }

        if (eligible.Count > 1)
        {
            eligible.Remove(previous);
        }

        return eligible[UnityEngine.Random.Range(0, eligible.Count)];
    }

    private static ChameleonDamageType GetChameleonDamageType(ZDO zdo)
    {
        int value = zdo.GetInt(ChameleonTypeKey, 0);
        return value >= (int)ChameleonDamageType.Blunt && value <= (int)ChameleonDamageType.Spirit
            ? (ChameleonDamageType)value
            : ChameleonDamageType.None;
    }

    private static HitData.DamageModifier GetChameleonDamageModifier(
        HitData.DamageModifiers modifiers,
        ChameleonDamageType type)
    {
        return type switch
        {
            ChameleonDamageType.Blunt => modifiers.m_blunt,
            ChameleonDamageType.Pierce => modifiers.m_pierce,
            ChameleonDamageType.Slash => modifiers.m_slash,
            ChameleonDamageType.Fire => modifiers.m_fire,
            ChameleonDamageType.Poison => modifiers.m_poison,
            ChameleonDamageType.Lightning => modifiers.m_lightning,
            ChameleonDamageType.Frost => modifiers.m_frost,
            ChameleonDamageType.Spirit => modifiers.m_spirit,
            _ => HitData.DamageModifier.Normal
        };
    }

    private static void SetChameleonDamageModifier(
        ref HitData.DamageModifiers modifiers,
        ChameleonDamageType type,
        HitData.DamageModifier value)
    {
        switch (type)
        {
            case ChameleonDamageType.Blunt: modifiers.m_blunt = value; break;
            case ChameleonDamageType.Pierce: modifiers.m_pierce = value; break;
            case ChameleonDamageType.Slash: modifiers.m_slash = value; break;
            case ChameleonDamageType.Fire: modifiers.m_fire = value; break;
            case ChameleonDamageType.Poison: modifiers.m_poison = value; break;
            case ChameleonDamageType.Lightning: modifiers.m_lightning = value; break;
            case ChameleonDamageType.Frost: modifiers.m_frost = value; break;
            case ChameleonDamageType.Spirit: modifiers.m_spirit = value; break;
        }
    }

    internal static void HandleDeath(Character character, FinalDeathAttribution attribution)
    {
        if (!CreatureLevelManager.IsLevelSystemEnabled() || character == null || character.IsPlayer())
        {
            return;
        }

        UntrackRuntimeModifiers(character);
        ZNetView? nview = character.m_nview;
        if (nview != null && nview.IsValid() && !nview.IsOwner())
        {
            return;
        }

        Character? finalAttacker = ResolveFinalDeathAttributionCharacter(attribution);
        ClearDelayedDamageTracking(character);
        if (attribution.SourceWasPlayer)
        {
            ApplyReapingForNearbyDeaths(character);
        }
        else
        {
            TryApplyDirectReapingGain(finalAttacker, character);
        }

        if (!TryGetModifierPower(character, ModifierMask.ToxicDeath, ToxicDeathPowerKey, ToxicDeathDefaultPower, out float power))
        {
            return;
        }

        Vector3 origin = character.transform.position;
        if (!TryGetZdo(character, out ZDO modifierZdo))
        {
            return;
        }

        float radius = Mathf.Max(0f, modifierZdo.GetFloat(ToxicDeathRadiusKey, ToxicDeathDefaultRadius));
        string triggerEffect = ResolveTriggerEffect(modifierZdo.GetString(ToxicDeathTriggerEffectKey, ToxicDeathDefaultTriggerEffect), ToxicDeathDefaultTriggerEffect);
        PlayBlinkStartEffect(origin, triggerEffect);

        foreach (Player player in Player.GetAllPlayers())
        {
            if (player == null || player.IsDead() || Vector3.Distance(player.transform.position, origin) > radius)
            {
                continue;
            }

            HitData poisonHit = new()
            {
                m_damage =
                {
                    m_poison = player.GetMaxHealth() * power
                },
                m_point = player.transform.position,
                m_dir = (player.transform.position - origin).normalized
            };
            player.Damage(poisonHit);
        }
    }

    internal static void HandlePlayerDeath(Player player)
    {
        if (!CreatureLevelManager.IsLevelSystemEnabled() || player == null)
        {
            return;
        }

        FinalDeathAttribution attribution = CaptureFinalDeathAttribution(player);
        Character? finalAttacker = ResolveFinalDeathAttributionCharacter(attribution);
        ClearDelayedDamageTracking(player);
        TryApplyDirectReapingGain(finalAttacker, player);
    }

    internal static void NotifyPlayerRespawn(Player player)
    {
        ZNetView? nview = player?.m_nview;
        if (player == null ||
            nview == null ||
            !nview.IsValid() ||
            !nview.IsOwner() ||
            ZRoutedRpc.instance == null)
        {
            return;
        }

        nview.InvokeRPC(
            ZRoutedRpc.instance.GetServerPeerID(),
            ReapingRespawnRequestRpc);
    }

    internal static FinalDeathAttribution CaptureFinalDeathAttribution(Character dead)
    {
        if (dead == null)
        {
            return default;
        }

        int id = dead.GetInstanceID();
        if (PendingDelayedDamageDeathCredits.TryGetValue(id, out DelayedDamageDeathCredit credit) &&
            ReferenceEquals(credit.Target, dead))
        {
            if (credit.Source == ZDOID.None)
            {
                return default;
            }

            Character? resolvedSource = credit.SourceWasPlayer
                ? null
                : TryFindCharacter(credit.Source, out Character delayedSource)
                    ? delayedSource
                    : null;
            return new FinalDeathAttribution(
                credit.Source,
                DeathAttributionKind.Delayed,
                credit.SourceWasPlayer,
                resolvedSource);
        }

        Character? directAttacker = dead.m_lastHit?.GetAttacker();
        ZDOID directSource = dead.m_lastHit?.m_attacker ?? ZDOID.None;
        if (directSource == ZDOID.None && directAttacker != null)
        {
            directSource = directAttacker.GetZDOID();
        }

        return directSource == ZDOID.None
            ? default
            : new FinalDeathAttribution(
                directSource,
                DeathAttributionKind.Direct,
                directAttacker != null && directAttacker.IsPlayer(),
                directAttacker);
    }

    private static Character? ResolveFinalDeathAttributionCharacter(FinalDeathAttribution attribution)
    {
        if (attribution.ResolvedSource != null)
        {
            return attribution.ResolvedSource;
        }

        if (!attribution.HasSource || attribution.SourceWasPlayer)
        {
            return null;
        }

        return TryFindCharacter(attribution.Source, out Character source) ? source : null;
    }

    private static void ClearDelayedDamageTracking(Character character)
    {
        int id = character.GetInstanceID();
        DelayedDamageSourceLedgers.Remove(id);
        PendingDelayedDamageDeathCredits.Remove(id);
    }

    private static bool TryApplyDirectReapingGain(Character? attacker, Character dead)
    {
        if (attacker == null || dead == null || attacker == dead || attacker.IsPlayer() || attacker.IsDead() ||
            !TryGetReapingSettings(attacker, out _))
        {
            return false;
        }

        Vector3 deathPosition = dead.transform.position;
        float squaredDistance = (attacker.transform.position - deathPosition).sqrMagnitude;
        if (squaredDistance > ReapingRadius * ReapingRadius)
        {
            return false;
        }

        return RequestReapingGain(attacker, dead, deathPosition);
    }

    private static bool RequestReapingGain(Character reaper, Character dead, Vector3 deathPosition)
    {
        ZNetView? nview = reaper?.m_nview;
        if (reaper == null ||
            dead == null ||
            !IsFinite(deathPosition) ||
            !IsFinite(reaper.transform.position) ||
            nview == null ||
            !nview.IsValid() ||
            !TryGetReapingSettings(reaper, out ReapingSettings settings))
        {
            return false;
        }

        ZDOID deadId = dead.GetZDOID();
        if (deadId == ZDOID.None)
        {
            return false;
        }

        if (ZNet.instance == null || ZRoutedRpc.instance == null)
        {
            ZDO zdo = nview.GetZDO();
            return nview.IsOwner() && zdo != null && ApplyReapingGain(reaper, zdo, settings);
        }

        nview.InvokeRPC(
            ZRoutedRpc.instance.GetServerPeerID(),
            ReapingDirectKillRequestRpc,
            deadId,
            deathPosition);
        return true;
    }

    private static void RPC_ReapingDirectKillRequest(
        Character reaper,
        long sender,
        ZDOID deadId,
        Vector3 deathPosition)
    {
        if (ZNet.instance == null ||
            !ZNet.instance.IsServer() ||
            ZDOMan.instance == null ||
            reaper == null ||
            reaper.IsPlayer() ||
            reaper.IsDead() ||
            reaper.m_nview == null ||
            !reaper.m_nview.IsValid() ||
            deadId == ZDOID.None ||
            !IsFinite(deathPosition) ||
            !IsFinite(reaper.transform.position) ||
            !TryGetReapingSettings(reaper, out _) ||
            (reaper.transform.position - deathPosition).sqrMagnitude > ReapingRadius * ReapingRadius)
        {
            return;
        }

        ZDO deadZdo = ZDOMan.instance.GetZDO(deadId);
        if (deadZdo == null ||
            deadZdo.GetOwner() != sender ||
            !IsFinite(deadZdo.GetPosition()) ||
            (deadZdo.GetPosition() - deathPosition).sqrMagnitude >
            ReapingDeathPositionTolerance * ReapingDeathPositionTolerance)
        {
            return;
        }

        ZDOID reaperId = reaper.GetZDOID();
        ReapingRequestKey request = new(reaperId, deadId);
        if (reaperId == ZDOID.None ||
            (ServerAuthorizedReapingDeaths.TryGetValue(reaperId, out HashSet<ZDOID> authorizedDeaths) &&
             authorizedDeaths.Contains(deadId)) ||
            !ServerPendingReapingRequests.Add(request))
        {
            return;
        }

        ZNet.instance.StartCoroutine(
            AuthorizeReapingAfterDeathSync(reaper, sender, deadId, deathPosition, request));
    }

    private static void RPC_ReapingRespawnRequest(Character character, long sender)
    {
        if (ZNet.instance == null ||
            !ZNet.instance.IsServer() ||
            character is not Player ||
            character.m_nview == null ||
            !character.m_nview.IsValid() ||
            !TryGetCharacterZdo(character, out ZDO zdo) ||
            zdo.GetOwner() != sender)
        {
            return;
        }

        ZDOID characterId = character.GetZDOID();
        if (characterId == ZDOID.None || !ServerPendingReapingRespawns.Add(characterId))
        {
            return;
        }

        ZNet.instance.StartCoroutine(
            AuthorizeReapingRespawnAfterSync(character, sender, characterId));
    }

    private static IEnumerator AuthorizeReapingRespawnAfterSync(
        Character character,
        long sender,
        ZDOID characterId)
    {
        float deadline = Time.realtimeSinceStartup + ReapingDeathSyncTimeout;
        try
        {
            while (Time.realtimeSinceStartup <= deadline)
            {
                if (character == null ||
                    character.m_nview == null ||
                    !character.m_nview.IsValid() ||
                    !TryGetCharacterZdo(character, out ZDO zdo) ||
                    zdo.GetOwner() != sender)
                {
                    yield break;
                }

                float health = zdo.GetFloat(ZDOVars.s_health, float.NaN);
                if (!zdo.GetBool(ZDOVars.s_dead, false) &&
                    !float.IsNaN(health) &&
                    !float.IsInfinity(health) &&
                    health > 0f)
                {
                    ClearReapingDeathAuthorization(characterId);
                    yield break;
                }

                yield return null;
            }
        }
        finally
        {
            ServerPendingReapingRespawns.Remove(characterId);
        }
    }

    private static IEnumerator AuthorizeReapingAfterDeathSync(
        Character reaper,
        long sender,
        ZDOID deadId,
        Vector3 deathPosition,
        ReapingRequestKey request)
    {
        if (!IsFinite(deathPosition))
        {
            ServerPendingReapingRequests.Remove(request);
            yield break;
        }

        float deadline = Time.realtimeSinceStartup + ReapingDeathSyncTimeout;
        try
        {
            while (Time.realtimeSinceStartup <= deadline)
            {
                if (reaper == null ||
                    reaper.m_nview == null ||
                    !reaper.m_nview.IsValid() ||
                    !IsFinite(reaper.transform.position) ||
                    ZDOMan.instance == null)
                {
                    yield break;
                }

                ZDO deadZdo = ZDOMan.instance.GetZDO(deadId);
                if (deadZdo == null ||
                    deadZdo.GetOwner() != sender ||
                    !IsFinite(deadZdo.GetPosition()) ||
                    (deadZdo.GetPosition() - deathPosition).sqrMagnitude >
                    ReapingDeathPositionTolerance * ReapingDeathPositionTolerance)
                {
                    yield break;
                }

                bool observedDead = deadZdo.GetBool(ZDOVars.s_dead, false) ||
                                    deadZdo.GetFloat(ZDOVars.s_health, float.PositiveInfinity) <= 0f;
                if (!observedDead && TryFindCharacter(deadId, out Character deadCharacter))
                {
                    observedDead = deadCharacter.IsDead() || deadCharacter.GetHealth() <= 0f;
                }

                if (observedDead)
                {
                    AuthorizeReapingGain(reaper, deadId, deathPosition);
                    yield break;
                }

                yield return null;
            }
        }
        finally
        {
            ServerPendingReapingRequests.Remove(request);
        }
    }

    private static void AuthorizeReapingGain(Character reaper, ZDOID deadId, Vector3 deathPosition)
    {
        if (reaper == null ||
            reaper.IsPlayer() ||
            reaper.IsDead() ||
            reaper.m_nview == null ||
            !reaper.m_nview.IsValid() ||
            !IsFinite(deathPosition) ||
            !IsFinite(reaper.transform.position) ||
            !TryGetReapingSettings(reaper, out _) ||
            (reaper.transform.position - deathPosition).sqrMagnitude > ReapingRadius * ReapingRadius)
        {
            return;
        }

        ZDOID reaperId = reaper.GetZDOID();
        if (reaperId == ZDOID.None ||
            !GetReapingDeathLedger(reaperId).Add(deadId))
        {
            return;
        }

        if (reaper.m_nview.IsOwner() && ZRoutedRpc.instance != null)
        {
            ApplyAuthorizedReapingGain(
                reaper,
                ZRoutedRpc.instance.GetServerPeerID(),
                deadId,
                deathPosition);
            return;
        }

        reaper.m_nview.InvokeRPC(ReapingDirectKillRpc, deadId, deathPosition);
    }

    private static void ApplyAuthorizedReapingGain(
        Character reaper,
        long sender,
        ZDOID deadId,
        Vector3 deathPosition)
    {
        ZNetView? nview = reaper?.m_nview;
        if (!IsTrustedServerRpc(sender) ||
            reaper == null ||
            reaper.IsPlayer() ||
            reaper.IsDead() ||
            nview == null ||
            !nview.IsValid() ||
            !nview.IsOwner() ||
            deadId == ZDOID.None ||
            !IsFinite(deathPosition) ||
            !IsFinite(reaper.transform.position) ||
            !TryGetReapingSettings(reaper, out ReapingSettings settings) ||
            (reaper.transform.position - deathPosition).sqrMagnitude > ReapingRadius * ReapingRadius)
        {
            return;
        }

        ZDO zdo = nview.GetZDO();
        if (zdo != null)
        {
            ApplyReapingGain(reaper, zdo, settings);
        }
    }

    private static HashSet<ZDOID> GetReapingDeathLedger(ZDOID reaperId)
    {
        if (!ServerAuthorizedReapingDeaths.TryGetValue(reaperId, out HashSet<ZDOID> ledger))
        {
            ledger = new HashSet<ZDOID>();
            ServerAuthorizedReapingDeaths[reaperId] = ledger;
        }

        return ledger;
    }

    private static void ClearReapingDeathAuthorization(ZDOID deadId)
    {
        foreach (HashSet<ZDOID> ledger in ServerAuthorizedReapingDeaths.Values)
        {
            ledger.Remove(deadId);
        }
    }

    private static void ApplyReapingForNearbyDeaths(Character dead)
    {
        if (ActiveReapingModifierCharacters.Count == 0)
        {
            return;
        }

        Vector3 origin = dead.transform.position;
        int overlapCount = GetNearbyReapingOverlapCount(origin);
        ReapingStaleCandidateIds.Clear();
        try
        {
            for (int index = 0; index < overlapCount; index++)
            {
                Collider collider = ReapingOverlapBuffer[index];
                if (collider == null)
                {
                    continue;
                }

                Character colliderCharacter = collider.GetComponentInParent<Character>();
                if (colliderCharacter == null)
                {
                    continue;
                }

                int candidateId = colliderCharacter.GetInstanceID();
                if (!ReapingNearbyCandidateIds.Add(candidateId) ||
                    !ActiveReapingModifierCharacters.TryGetValue(candidateId, out Character candidate))
                {
                    continue;
                }

                ProcessNearbyReapingCandidate(candidate, candidateId, dead, origin);
            }

            foreach (KeyValuePair<int, Character> fallback in UnqueryableReapingModifierCharacters)
            {
                int candidateId = fallback.Key;
                if (!ReapingNearbyCandidateIds.Add(candidateId) ||
                    !ActiveReapingModifierCharacters.TryGetValue(candidateId, out Character candidate))
                {
                    continue;
                }

                ProcessNearbyReapingCandidate(candidate, candidateId, dead, origin);
            }
        }
        finally
        {
            foreach (int staleId in ReapingStaleCandidateIds)
            {
                ActiveReapingModifierCharacters.Remove(staleId);
                UnqueryableReapingModifierCharacters.Remove(staleId);
            }

            ReapingStaleCandidateIds.Clear();
            Array.Clear(ReapingOverlapBuffer, 0, overlapCount);
            ReapingNearbyCandidateIds.Clear();
        }
    }

    private static void ProcessNearbyReapingCandidate(
        Character candidate,
        int candidateId,
        Character dead,
        Vector3 origin)
    {
        if (candidate == null || candidate == dead || candidate.IsPlayer() || candidate.IsDead())
        {
            ReapingStaleCandidateIds.Add(candidateId);
            return;
        }

        float squaredDistance = (candidate.transform.position - origin).sqrMagnitude;
        if (squaredDistance > ReapingRadius * ReapingRadius)
        {
            return;
        }

        if (!CreatureLevelManager.AllowsModifierEffects(candidate))
        {
            return;
        }

        if (!TryGetReapingSettings(candidate, out _))
        {
            return;
        }

        ZNetView? nview = candidate.m_nview;
        if (nview == null || !nview.IsValid())
        {
            return;
        }

        RequestReapingGain(candidate, dead, origin);
    }

    private static int GetNearbyReapingOverlapCount(Vector3 origin)
    {
        while (true)
        {
            int count = Physics.OverlapSphereNonAlloc(
                origin,
                ReapingRadius,
                ReapingOverlapBuffer,
                GetReapingCharacterLayerMask(),
                QueryTriggerInteraction.Collide);
            if (count < ReapingOverlapBuffer.Length)
            {
                return count;
            }

            ReapingOverlapBuffer = new Collider[checked(ReapingOverlapBuffer.Length * 2)];
        }
    }

    private static int GetReapingCharacterLayerMask()
    {
        int mask = Character.s_characterLayerMask;
        if (Character.s_characterLayer >= 0)
        {
            mask |= 1 << Character.s_characterLayer;
        }

        if (Character.s_characterNetLayer >= 0)
        {
            mask |= 1 << Character.s_characterNetLayer;
        }

        if (Character.s_characterGhostLayer >= 0)
        {
            mask |= 1 << Character.s_characterGhostLayer;
        }

        return mask;
    }

    private static bool ApplyReapingGain(
        Character character,
        ZDO zdo,
        ReapingSettings settings)
    {
        if (zdo == null || !settings.HasAnyGain)
        {
            return false;
        }

        float baseMaxHealth = EnsureReapingBaseMaxHealth(character, zdo);
        if (baseMaxHealth <= 0f)
        {
            return false;
        }

        float healthBefore = character.GetHealth();
        int healActivationCount = Math.Max(0, zdo.GetInt(ReapingHealActivationCountKey, 0));
        float currentHealthBonus = Mathf.Max(0f, zdo.GetFloat(ReapingBonusHealthKey, 0f));
        float currentDamageBonus = Mathf.Max(0f, zdo.GetFloat(ReapingDamageBonusKey, 0f));
        float currentScaleBonus = Mathf.Max(0f, zdo.GetFloat(ReapingScaleBonusKey, 0f));
        float maxHealthBonus = baseMaxHealth * settings.MaxHealthCap;
        bool scaleAllowed = !CreatureLevelManager.IsDungeonCreature(character);
        bool healRemaining = settings.HealPerKill > 0f && healActivationCount < settings.HealMaxActivations;
        bool maxHealthRemaining = settings.MaxHealthPerKill > 0f && maxHealthBonus > currentHealthBonus;
        bool damageRemaining = settings.DamagePerKill > 0f && settings.DamageCap > currentDamageBonus;
        bool scaleRemaining = scaleAllowed && settings.ScalePerKill > 0f && settings.ScaleCap > currentScaleBonus;
        if (!healRemaining && !maxHealthRemaining && !damageRemaining && !scaleRemaining)
        {
            return false;
        }

        float maxHealthGain = Mathf.Min(
            baseMaxHealth * settings.MaxHealthPerKill,
            Mathf.Max(0f, maxHealthBonus - currentHealthBonus));
        if (maxHealthGain > 0f)
        {
            currentHealthBonus += maxHealthGain;
            zdo.Set(ReapingBonusHealthKey, currentHealthBonus);
        }

        float targetMaxHealth = baseMaxHealth + Mathf.Min(currentHealthBonus, maxHealthBonus);
        if (!Mathf.Approximately(character.GetMaxHealth(), targetMaxHealth))
        {
            character.SetMaxHealth(targetMaxHealth);
        }

        float requestedHeal = healRemaining ? baseMaxHealth * settings.HealPerKill : 0f;
        float actualHeal = Mathf.Min(requestedHeal, Mathf.Max(0f, targetMaxHealth - healthBefore));
        if (actualHeal > 0f)
        {
            healActivationCount++;
            zdo.Set(ReapingHealActivationCountKey, healActivationCount);
            character.SetHealth(healthBefore + actualHeal);
            ShowReapingHealText(character, actualHeal);
        }

        float damageBonusAfter = currentDamageBonus;
        if (settings.DamagePerKill > 0f && settings.DamageCap > currentDamageBonus)
        {
            damageBonusAfter = Mathf.Min(settings.DamageCap, currentDamageBonus + settings.DamagePerKill);
            zdo.Set(ReapingDamageBonusKey, damageBonusAfter);
        }

        float scaleBonusAfter = currentScaleBonus;
        if (scaleAllowed && settings.ScalePerKill > 0f && settings.ScaleCap > currentScaleBonus)
        {
            EnsureReapingBaseScale(character, zdo);
            scaleBonusAfter = Mathf.Min(settings.ScaleCap, currentScaleBonus + settings.ScalePerKill);
            zdo.Set(ReapingScaleBonusKey, scaleBonusAfter);
        }

        bool scaleChanged = ApplyStoredReapingScale(character, zdo);
        bool changed = maxHealthGain > 0f || actualHeal > 0f ||
                       damageBonusAfter > currentDamageBonus || scaleBonusAfter > currentScaleBonus;
        if (!changed)
        {
            return false;
        }

        BroadcastReapingFeedback(character, scaleChanged);
        return true;
    }

    private static void ApplyStoredReapingHealth(Character character, ZDO zdo)
    {
        if (zdo == null || !HasModifier(zdo, ModifierMask.Reaping))
        {
            return;
        }

        float baseMaxHealth = zdo.GetFloat(ReapingBaseMaxHealthKey, 0f);
        float bonusHealth = Mathf.Max(0f, zdo.GetFloat(ReapingBonusHealthKey, 0f));
        if (baseMaxHealth <= 0f || bonusHealth <= 0f)
        {
            return;
        }

        float maxHealthCap = Mathf.Max(0f, zdo.GetFloat(ReapingMaxHealthCapKey, ReapingDefaultMaxHealthCap));
        float targetMaxHealth = baseMaxHealth + Mathf.Min(bonusHealth, baseMaxHealth * maxHealthCap);
        if (!Mathf.Approximately(character.GetMaxHealth(), targetMaxHealth))
        {
            character.SetMaxHealth(targetMaxHealth);
        }
    }

    private static bool ApplyStoredReapingScale(Character character, ZDO zdo)
    {
        if (zdo == null || !HasModifier(zdo, ModifierMask.Reaping))
        {
            return false;
        }

        float scaleBonus = Mathf.Max(0f, zdo.GetFloat(ReapingScaleBonusKey, 0f));
        Vector3 baseScale = zdo.GetVec3(ReapingBaseScaleKey, Vector3.zero);
        if (scaleBonus <= 0f || baseScale == Vector3.zero)
        {
            return false;
        }

        if (CreatureLevelManager.IsDungeonCreature(character))
        {
            return false;
        }

        Vector3 targetScale = baseScale * (1f + scaleBonus);
        if (Approximately(character.transform.localScale, targetScale))
        {
            return false;
        }

        character.transform.localScale = targetScale;
        if (character.m_nview != null && character.m_nview.IsValid() && character.m_nview.IsOwner())
        {
            zdo.Set(ZDOVars.s_scaleHash, targetScale);
        }

        ScheduleReapingPhysicsSync();
        return true;
    }

    private static float EnsureReapingBaseMaxHealth(Character character, ZDO zdo)
    {
        float baseMaxHealth = zdo.GetFloat(ReapingBaseMaxHealthKey, 0f);
        if (baseMaxHealth > 0f)
        {
            return baseMaxHealth;
        }

        baseMaxHealth = character.GetMaxHealth();
        if (baseMaxHealth > 0f)
        {
            zdo.Set(ReapingBaseMaxHealthKey, baseMaxHealth);
        }

        return baseMaxHealth;
    }

    private static Vector3 EnsureReapingBaseScale(Character character, ZDO zdo)
    {
        Vector3 baseScale = zdo.GetVec3(ReapingBaseScaleKey, Vector3.zero);
        if (baseScale != Vector3.zero)
        {
            return baseScale;
        }

        baseScale = character.transform.localScale;
        if (baseScale != Vector3.zero)
        {
            zdo.Set(ReapingBaseScaleKey, baseScale);
        }

        return baseScale;
    }

    private static bool Approximately(Vector3 left, Vector3 right)
    {
        return Mathf.Approximately(left.x, right.x) &&
               Mathf.Approximately(left.y, right.y) &&
               Mathf.Approximately(left.z, right.z);
    }

    private static void ShowReapingHealText(Character character, float actualHeal)
    {
        if (actualHeal <= 0f || ZRoutedRpc.instance == null)
        {
            return;
        }

        Vector3 position = character.GetTopPoint();
        if (DamageText.instance != null)
        {
            DamageText.instance.ShowText(DamageText.TextType.Heal, position, actualHeal, false);
            return;
        }

        ZPackage package = new();
        package.Write((int)DamageText.TextType.Heal);
        package.Write(position);
        package.Write(actualHeal.ToString("0.#", CultureInfo.InvariantCulture));
        package.Write(false);
        ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, "RPC_DamageText", package);
    }

    internal static bool TryGetOmenEnforcerChance(ZDO zdo, out float chance)
    {
        chance = 0f;
        if (zdo == null ||
            !zdo.GetBool(AppliedKey, false) ||
            !HasModifier(GetStoredModifierMask(zdo), ModifierMask.Omen))
        {
            return false;
        }

        chance = Mathf.Clamp01(zdo.GetFloat(OmenPowerKey, OmenDefaultPower));
        return chance > 0f;
    }

    private static void ApplyRuntimeModifierStats(Character character, ZDO zdo)
    {
        TrackRuntimeModifiers(character, zdo);
        ApplyStoredReapingHealth(character, zdo);
        ApplyStoredReapingScale(character, zdo);
    }

    internal static void RefreshStoredReapingScale(Character character)
    {
        if (character == null || character.IsPlayer() ||
            !TryGetZdo(character, out ZDO zdo) ||
            !zdo.GetBool(AppliedKey, false))
        {
            return;
        }

        ZNetView? nview = character.m_nview;
        if (nview != null && nview.IsValid() && nview.IsOwner())
        {
            if (EnsureInheritedReapingBases(character, zdo))
            {
                ApplyStoredReapingHealth(character, zdo);
            }
        }

        ApplyStoredReapingScale(character, zdo);
    }

    private static bool EnsureInheritedReapingBases(Character character, ZDO zdo)
    {
        if (!HasModifier(zdo, ModifierMask.Reaping))
        {
            return false;
        }

        bool healthBaseInitialized = false;
        int id = character.GetInstanceID();
        if (PendingReapingHealthBonusRatios.TryGetValue(id, out float healthBonusRatio))
        {
            float targetBaseMaxHealth = character.GetMaxHealth();
            if (targetBaseMaxHealth > 0f)
            {
                zdo.Set(ReapingBaseMaxHealthKey, targetBaseMaxHealth);
                zdo.Set(ReapingBonusHealthKey, targetBaseMaxHealth * Mathf.Max(0f, healthBonusRatio));
                PendingReapingHealthBonusRatios.Remove(id);
                healthBaseInitialized = true;
            }
        }

        if (zdo.GetFloat(ReapingBonusHealthKey, 0f) > 0f && zdo.GetFloat(ReapingBaseMaxHealthKey, 0f) <= 0f)
        {
            healthBaseInitialized = EnsureReapingBaseMaxHealth(character, zdo) > 0f;
        }

        if (zdo.GetFloat(ReapingScaleBonusKey, 0f) > 0f && zdo.GetVec3(ReapingBaseScaleKey, Vector3.zero) == Vector3.zero)
        {
            EnsureReapingBaseScale(character, zdo);
        }

        return healthBaseInitialized;
    }

    internal static void UpdateEnemyHud(Character character, bool isMount)
    {
        if (character == null || character.IsPlayer() || EnemyHud.instance == null)
        {
            return;
        }

        if (!EnemyHud.instance.m_huds.TryGetValue(character, out var hud))
        {
            return;
        }

        if (isMount)
        {
            UpdateBlamerAlertIcon(hud, false);
            return;
        }

        if (hud.m_gui == null)
        {
            return;
        }

        if (hud.m_name != null && CreatureKarmaManager.TryGetDisplayName(character, out string displayName))
        {
            hud.m_name.text = displayName;
        }

        UpdateResistanceHud(character, hud);

        if (!CreatureLevelManager.IsLevelSystemEnabled())
        {
            UpdateBlamerAlertIcon(hud, false);
            HideManagedHudContent(hud);
            return;
        }

        if (!TryGetHudModifierState(
                character,
                hud.m_gui,
                out ModifierMask visibleModifiers,
                out float armoredReduction,
                out float enragedBonus,
                out bool blamerActive,
                out bool refreshed) &&
            refreshed &&
            character.m_nview != null &&
            character.m_nview.IsValid() &&
            character.m_nview.IsOwner())
        {
            TryRollModifiers(character);
            TryGetHudModifierState(
                character,
                hud.m_gui,
                out visibleModifiers,
                out armoredReduction,
                out enragedBonus,
                out blamerActive,
                out _);
        }

        UpdateBlamerAlertIcon(hud, blamerActive);

        bool hasModifiers = visibleModifiers != ModifierMask.None;
        int stars = Math.Max(0, character.GetLevel() - 1);
        if (character.IsBoss() || CreatureKarmaManager.IsBossHudOnly(character))
        {
            UpdateBossHud(hud, stars, hasModifiers, armoredReduction, enragedBonus, visibleModifiers);
            return;
        }

        SetBossContentActive(hud, false);
        HideVanillaLevelBlocks(hud);
        if (stars <= 0 && !hasModifiers)
        {
            SetLevelContentActive(hud, false);
            return;
        }

        RectTransform? content = EnsureLevelContent(hud, stars > 0);
        if (content == null)
        {
            return;
        }

        content.gameObject.SetActive(true);
        UpdateStarBadge(content, hud.m_level2, hud.m_level3, hud.m_name, stars, showIndividualStars: stars <= 2);
        RectTransform iconContainer = EnsureIconContainer(content);
        UpdateModifierIcons(iconContainer, visibleModifiers, armoredReduction, enragedBonus);
    }

    private static void UpdateBlamerAlertIcon(EnemyHud.HudData hud, bool visible)
    {
        RectTransform? alerted = hud.m_alerted;
        if (alerted == null)
        {
            return;
        }

        if (!BlamerAlertIconStates.TryGetValue(alerted, out BlamerAlertIconState state))
        {
            if (!visible)
            {
                return;
            }

            state = new BlamerAlertIconState();
            BlamerAlertIconStates.Add(alerted, state);
        }

        if (state.Icon == null)
        {
            if (!visible)
            {
                return;
            }

            Transform? existing = alerted.Find(BlamerActiveIconName);
            Image? icon = existing != null ? existing.GetComponent<Image>() : null;
            if (icon == null)
            {
                GameObject iconObject = new(BlamerActiveIconName, typeof(RectTransform), typeof(Image));
                RectTransform iconRect = (RectTransform)iconObject.transform;
                iconRect.SetParent(alerted, false);
                icon = iconObject.GetComponent<Image>();
            }

            RectTransform rect = (RectTransform)icon.transform;
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, BlamerActiveIconGap);
            rect.localScale = Vector3.one;
            icon.sprite = GetBlamerSprite();
            icon.color = Color.white;
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            icon.enabled = true;
            state.Icon = icon;
            state.VisibilityInitialized = false;
        }

        float iconSize = ModifierIconSize * BlamerActiveIconScale;
        Vector2 desiredSize = new(iconSize, iconSize);
        RectTransform activeIconRect = state.Icon.rectTransform;
        if (activeIconRect.sizeDelta != desiredSize)
        {
            activeIconRect.sizeDelta = desiredSize;
        }

        if (state.VisibilityInitialized && state.Visible == visible)
        {
            return;
        }

        state.Icon.gameObject.SetActive(visible);
        state.VisibilityInitialized = true;
        state.Visible = visible;
    }

    private static void HideVanillaLevelBlocks(EnemyHud.HudData hud)
    {
        if (hud.m_level2 != null)
        {
            hud.m_level2.gameObject.SetActive(false);
        }

        if (hud.m_level3 != null)
        {
            hud.m_level3.gameObject.SetActive(false);
        }
    }

    private static void UpdateBossHud(
        EnemyHud.HudData hud,
        int stars,
        bool hasModifiers,
        float armoredReduction,
        float enragedBonus,
        ModifierMask visibleModifiers)
    {
        SetLevelContentActive(hud, false);
        HideVanillaLevelBlocks(hud);
        if (stars <= 0 && !hasModifiers)
        {
            SetBossContentActive(hud, false);
            return;
        }

        RectTransform? content = EnsureBossLevelContent(hud, stars > 0);
        if (content == null)
        {
            return;
        }

        content.gameObject.SetActive(true);
        UpdateStarBadge(
            content,
            hud.m_level2,
            hud.m_level3,
            hud.m_name,
            stars,
            showIndividualStars: stars <= 2);
        RectTransform iconContainer = EnsureIconContainer(content);
        UpdateModifierIcons(iconContainer, visibleModifiers, armoredReduction, enragedBonus);
    }

    private static void HideManagedHudContent(EnemyHud.HudData hud)
    {
        SetLevelContentActive(hud, false);
        SetBossContentActive(hud, false);
    }

    private static void UpdateResistanceHud(Character character, EnemyHud.HudData hud)
    {
        if (hud.m_gui == null)
        {
            return;
        }

        float range = Mathf.Clamp(CreatureManagerPlugin.NormalCreatureNameplateRange.Value, 10f, 50f);
        if (CreatureManagerPlugin.ShowSneakHoverResistances?.Value != CreatureManagerPlugin.Toggle.On ||
            character.IsTamed() ||
            !ShouldShowResistanceHud(character, range) ||
            !TryBuildResistanceText(GetDisplayedDamageModifiers(character), out string resistanceText, out int lineCount))
        {
            SetResistanceTextActive(hud, false);
            return;
        }

        TextMeshProUGUI? text = EnsureResistanceText(hud);
        if (text == null)
        {
            return;
        }

        text.text = resistanceText;
        PositionResistanceText(text.rectTransform, hud, lineCount);
        text.gameObject.SetActive(true);
    }

    private static HitData.DamageModifiers GetDisplayedDamageModifiers(Character character)
    {
        HitData.DamageModifiers modifiers = character.m_damageModifiers;
        if (character.GetBaseAI() is not BaseAI baseAI ||
            !baseAI.IsAlerted() ||
            !TryGetZdo(character, out ZDO zdo) ||
            !HasModifier(zdo, ModifierMask.Chameleon) ||
            !CreatureLevelManager.AllowsModifierEffects(character))
        {
            return modifiers;
        }

        ChameleonDamageType type = GetChameleonDamageType(zdo);
        if (type != ChameleonDamageType.None)
        {
            SetChameleonDamageModifier(ref modifiers, type, HitData.DamageModifier.Immune);
        }

        return modifiers;
    }

    private static bool TryBuildResistanceText(HitData.DamageModifiers modifiers, out string text, out int lineCount)
    {
        StringBuilder builder = new();
        lineCount = 0;
        foreach ((string key, HitData.DamageModifier value) in EnumerateDamageModifiers(modifiers))
        {
            if (value == HitData.DamageModifier.Normal || value == HitData.DamageModifier.Ignore)
            {
                continue;
            }

            if (builder.Length > 0)
            {
                builder.AppendLine();
            }

            builder.Append(CreatureLocalization.Localize($"cm_damage_{key}", key));
            builder.Append(": ");
            builder.Append(GetLocalizedDamageModifier(value));
            lineCount++;
        }

        text = builder.ToString();
        return lineCount > 0;
    }

    private static string GetLocalizedDamageModifier(HitData.DamageModifier modifier)
    {
        return modifier switch
        {
            HitData.DamageModifier.Weak => CreatureLocalization.Localize("cm_resistance_weak", "Weak"),
            HitData.DamageModifier.VeryWeak => CreatureLocalization.Localize("cm_resistance_very_weak", "VeryWeak"),
            HitData.DamageModifier.Resistant => CreatureLocalization.Localize("cm_resistance_resistant", "Resistant"),
            HitData.DamageModifier.VeryResistant => CreatureLocalization.Localize("cm_resistance_very_resistant", "VeryResistant"),
            HitData.DamageModifier.Immune => CreatureLocalization.Localize("cm_resistance_immune", "Immune"),
            _ => modifier.ToString()
        };
    }

    private static IEnumerable<(string Key, HitData.DamageModifier Value)> EnumerateDamageModifiers(HitData.DamageModifiers modifiers)
    {
        yield return ("blunt", modifiers.m_blunt);
        yield return ("slash", modifiers.m_slash);
        yield return ("pierce", modifiers.m_pierce);
        yield return ("chop", modifiers.m_chop);
        yield return ("pickaxe", modifiers.m_pickaxe);
        yield return ("fire", modifiers.m_fire);
        yield return ("frost", modifiers.m_frost);
        yield return ("lightning", modifiers.m_lightning);
        yield return ("poison", modifiers.m_poison);
        yield return ("spirit", modifiers.m_spirit);
    }

    private static bool ShouldShowResistanceHud(Character character, float range)
    {
        Player? player = Player.m_localPlayer;
        if (player == null || !IsPlayerSneakingForFrame(player))
        {
            return false;
        }

        if (Vector3.Distance(player.transform.position, character.transform.position) > range)
        {
            return false;
        }

        Character? hovered = GetHoveredCharacterForFrame(range);
        return hovered == character;
    }

    private static bool IsPlayerSneakingForFrame(Player player)
    {
        int frame = Time.frameCount;
        if (CachedSneakFrame == frame)
        {
            return CachedSneakState;
        }

        CachedSneakFrame = frame;
        CachedSneakState = IsPlayerSneaking(player);
        return CachedSneakState;
    }

    private static bool IsPlayerSneaking(Player player)
    {
        if (!SneakMemberLookupDone)
        {
            Type playerType = typeof(Player);
            foreach (string methodName in SneakMethodNames)
            {
                MethodInfo? method = playerType.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (method != null && method.ReturnType == typeof(bool) && method.GetParameters().Length == 0)
                {
                    CachedSneakMethod = method;
                    break;
                }
            }

            if (CachedSneakMethod == null)
            {
                foreach (string fieldName in SneakFieldNames)
                {
                    FieldInfo? field = playerType.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (field != null && field.FieldType == typeof(bool))
                    {
                        CachedSneakField = field;
                        break;
                    }
                }
            }

            SneakMemberLookupDone = true;
        }

        try
        {
            if (CachedSneakMethod != null)
            {
                return CachedSneakMethod.Invoke(player, Array.Empty<object>()) is true;
            }

            if (CachedSneakField != null)
            {
                return CachedSneakField.GetValue(player) is true;
            }
        }
        catch
        {
            return false;
        }

        return false;
    }

    private static Character? GetHoveredCharacter(float range)
    {
        if (GameCamera.instance == null)
        {
            return null;
        }

        if (HoverRaycastMask < 0)
        {
            HoverRaycastMask = LayerMask.GetMask(
                "item",
                "piece",
                "piece_nonsolid",
                "Default",
                "static_solid",
                "Default_small",
                "character",
                "character_net",
                "terrain",
                "vehicle",
                "character_trigger");
        }

        Transform cameraTransform = GameCamera.instance.transform;
        RaycastHit[] hits = Physics.RaycastAll(cameraTransform.position, cameraTransform.forward, range, HoverRaycastMask);
        Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == null || hit.collider.GetComponent<EffectArea>() != null)
            {
                continue;
            }

            Character? character = hit.collider.GetComponentInParent<Character>();
            if (character == null || character.IsPlayer())
            {
                continue;
            }

            return character;
        }

        return null;
    }

    private static Character? GetHoveredCharacterForFrame(float range)
    {
        int frame = Time.frameCount;
        if (CachedHoverFrame == frame && Mathf.Approximately(CachedHoverRange, range))
        {
            return CachedHoverValid ? CachedHoveredCharacter : null;
        }

        CachedHoverFrame = frame;
        CachedHoverRange = range;
        CachedHoveredCharacter = GetHoveredCharacter(range);
        CachedHoverValid = CachedHoveredCharacter != null;
        return CachedHoveredCharacter;
    }

    private static TextMeshProUGUI? EnsureResistanceText(EnemyHud.HudData hud)
    {
        RectTransform? parent = GetResistanceHudParent(hud);
        if (parent == null)
        {
            return null;
        }

        HudContentState state = HudContentStates.GetValue(parent, _ => new HudContentState());
        if (!state.ResistanceTextSearched)
        {
            state.ResistanceTextSearched = true;
            Transform existing = parent.Find(ResistanceTextName);
            if (existing != null)
            {
                state.ResistanceText = existing.GetComponent<TextMeshProUGUI>();
                ConfigureResistanceText(state.ResistanceText, hud.m_name);
            }
        }

        if (state.ResistanceText != null)
        {
            return state.ResistanceText;
        }

        GameObject textObject = new(ResistanceTextName, typeof(RectTransform));
        textObject.SetActive(false);
        RectTransform rect = (RectTransform)textObject.transform;
        rect.SetParent(parent, false);
        rect.SetAsLastSibling();

        TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
        ConfigureResistanceText(text, hud.m_name);

        Outline outline = textObject.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
        outline.effectDistance = new Vector2(1f, -1f);
        outline.useGraphicAlpha = true;
        textObject.SetActive(true);
        state.ResistanceText = text;
        return text;
    }

    private static void ConfigureResistanceText(TextMeshProUGUI text, TextMeshProUGUI? nameText)
    {
        ApplyHudFont(text, nameText);

        text.raycastTarget = false;
        text.enableAutoSizing = false;
        text.fontSize = 12f;
        text.alignment = TextAlignmentOptions.Top;
        text.color = new Color(0.86f, 0.95f, 1f, 0.95f);
        text.richText = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
    }

    private static RectTransform? GetResistanceHudParent(EnemyHud.HudData hud)
    {
        return GetHudContentParent(hud);
    }

    private static void PositionResistanceText(RectTransform rect, EnemyHud.HudData hud, int lineCount)
    {
        RectTransform? parent = rect.parent as RectTransform;
        float height = Mathf.Max(ResistanceLineHeight, ResistanceLineHeight * Mathf.Max(1, lineCount));
        float contentHeight = GetActiveLevelContentHeight(parent);
        RectTransform? healthRect = GetHealthRoot(hud.m_healthFast);
        if (parent != null && healthRect != null && healthRect.parent == parent)
        {
            float width = Mathf.Max(ResistanceTextMinWidth, Mathf.Max(healthRect.rect.width, healthRect.sizeDelta.x));
            float healthHeight = Mathf.Max(1f, Mathf.Max(healthRect.rect.height, healthRect.sizeDelta.y));
            float healthBottom = healthRect.anchoredPosition.y - healthRect.pivot.y * healthHeight;
            ApplyRectLayout(
                rect,
                healthRect.anchorMin,
                healthRect.anchorMax,
                new Vector2(0.5f, 1f),
                new Vector2(
                    healthRect.anchoredPosition.x,
                    healthBottom - LevelContentBelowHealthGap - contentHeight - ResistanceTextGap),
                new Vector2(width, height));
            return;
        }

        RectTransform? nameRect = hud.m_name != null ? hud.m_name.rectTransform : null;
        if (parent != null && nameRect != null && nameRect.parent == parent)
        {
            float nameHeight = Mathf.Max(1f, Mathf.Max(nameRect.rect.height, nameRect.sizeDelta.y));
            float nameBottom = nameRect.anchoredPosition.y - nameRect.pivot.y * nameHeight;
            ApplyRectLayout(
                rect,
                nameRect.anchorMin,
                nameRect.anchorMax,
                new Vector2(0.5f, 1f),
                new Vector2(
                    nameRect.anchoredPosition.x,
                    nameBottom - contentHeight - ResistanceTextGap),
                new Vector2(Mathf.Max(ResistanceTextMinWidth, Mathf.Max(nameRect.rect.width, nameRect.sizeDelta.x)), height));
            return;
        }

        ApplyRectLayout(
            rect,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 1f),
            new Vector2(0f, -contentHeight - ResistanceTextGap),
            new Vector2(ResistanceTextMinWidth, height));
    }

    private static float GetActiveLevelContentHeight(RectTransform? parent)
    {
        if (parent == null)
        {
            return LevelContentHeight;
        }

        Transform? content = parent.Find(LevelContentName);
        if (content == null || !content.gameObject.activeSelf)
        {
            content = parent.Find(BossLevelContentName);
        }

        if (content == null || !content.gameObject.activeSelf)
        {
            return LevelContentHeight;
        }

        RectTransform contentRect = (RectTransform)content;
        float height = Mathf.Max(LevelContentHeight, Mathf.Max(contentRect.rect.height, contentRect.sizeDelta.y));
        RectTransform? starGroup = content.Find(StarGroupName) as RectTransform;
        if (starGroup != null && starGroup.gameObject.activeSelf)
        {
            height = Mathf.Max(height, Mathf.Max(starGroup.rect.height, starGroup.sizeDelta.y));
        }

        RectTransform? iconContainer = content.Find(IconContainerName) as RectTransform;
        if (iconContainer != null &&
            HudIconStates.TryGetValue(iconContainer, out HudIconState iconState) &&
            iconState.Visible != ModifierMask.None)
        {
            height = Mathf.Max(height, Mathf.Max(iconContainer.rect.height, iconContainer.sizeDelta.y));
        }

        return height;
    }

    private static void SetResistanceTextActive(EnemyHud.HudData hud, bool active)
    {
        RectTransform? parent = GetResistanceHudParent(hud);
        if (parent == null)
        {
            return;
        }

        HudContentState state = HudContentStates.GetValue(parent, _ => new HudContentState());
        if (!state.ResistanceTextSearched)
        {
            state.ResistanceTextSearched = true;
            Transform existing = parent.Find(ResistanceTextName);
            state.ResistanceText = existing != null ? existing.GetComponent<TextMeshProUGUI>() : null;
        }

        if (state.ResistanceText != null && state.ResistanceText.gameObject.activeSelf != active)
        {
            state.ResistanceText.gameObject.SetActive(active);
        }
    }

    private static void SetBossContentActive(EnemyHud.HudData hud, bool active)
    {
        RectTransform? parent = GetHudContentParent(hud);
        if (parent == null)
        {
            return;
        }

        HudContentState state = HudContentStates.GetValue(parent, _ => new HudContentState());
        if (!state.BossContentSearched)
        {
            state.BossContentSearched = true;
            state.BossContent = parent.Find(BossLevelContentName) as RectTransform;
        }

        if (state.BossContent != null && state.BossContent.gameObject.activeSelf != active)
        {
            state.BossContent.gameObject.SetActive(active);
        }
    }

    private static void SetLevelContentActive(EnemyHud.HudData hud, bool active)
    {
        RectTransform? parent = GetHudContentParent(hud);
        if (parent == null)
        {
            return;
        }

        HudContentState state = HudContentStates.GetValue(parent, _ => new HudContentState());
        if (!state.LevelContentSearched)
        {
            state.LevelContentSearched = true;
            state.LevelContent = parent.Find(LevelContentName) as RectTransform;
        }

        if (state.LevelContent != null && state.LevelContent.gameObject.activeSelf != active)
        {
            state.LevelContent.gameObject.SetActive(active);
        }
    }

    private static bool TryFindVanillaStarSlot(RectTransform? block, ref RectTransform? sourceBlock, ref Vector2 sourceCenter, ref float bestScore)
    {
        if (block == null)
        {
            return false;
        }

        Image[] images = block.GetComponentsInChildren<Image>(true);
        bool found = false;
        foreach (Image image in images)
        {
            if (!image.gameObject.name.StartsWith("star", StringComparison.OrdinalIgnoreCase) || image.sprite == null)
            {
                continue;
            }

            float score = GetStarImageScore(image.color);
            if (score <= bestScore)
            {
                continue;
            }

            sourceBlock = block;
            sourceCenter = GetRelativeCenter(block, image.rectTransform);
            bestScore = score;
            found = true;
        }

        return found;
    }

    private static float GetStarImageScore(Color color)
    {
        float brightest = Mathf.Max(color.r, Mathf.Max(color.g, color.b));
        float darkest = Mathf.Min(color.r, Mathf.Min(color.g, color.b));
        float yellowBias = Mathf.Max(0f, color.r + color.g - color.b);
        return color.a * (brightest + brightest - darkest + yellowBias);
    }

    private static RectTransform? EnsureLevelContent(EnemyHud.HudData hud, bool showStars)
    {
        RectTransform? parent = GetHudContentParent(hud);
        if (parent == null)
        {
            return null;
        }

        HudContentState state = HudContentStates.GetValue(parent, _ => new HudContentState());
        if (!state.LevelContentSearched)
        {
            state.LevelContentSearched = true;
            state.LevelContent = parent.Find(LevelContentName) as RectTransform;
        }

        if (state.LevelContent != null)
        {
            PositionLevelContent(state.LevelContent, hud, showStars);
            return state.LevelContent;
        }

        GameObject contentObject = new(LevelContentName, typeof(RectTransform), typeof(HorizontalLayoutGroup));
        RectTransform content = (RectTransform)contentObject.transform;
        content.SetParent(parent, false);
        content.SetAsLastSibling();
        PositionLevelContent(content, hud, showStars);
        ConfigureContentLayout(content);

        EnsureStarGroup(content);
        EnsureStarNumber(content, hud.m_name);
        EnsureIconContainer(content);
        state.LevelContent = content;
        return content;
    }

    private static RectTransform? EnsureBossLevelContent(EnemyHud.HudData hud, bool showStars)
    {
        RectTransform? parent = GetHudContentParent(hud);
        if (parent == null)
        {
            return null;
        }

        HudContentState state = HudContentStates.GetValue(parent, _ => new HudContentState());
        if (!state.BossContentSearched)
        {
            state.BossContentSearched = true;
            state.BossContent = parent.Find(BossLevelContentName) as RectTransform;
        }

        if (state.BossContent != null)
        {
            PositionBossContent(state.BossContent, hud, showStars);
            return state.BossContent;
        }

        GameObject contentObject = new(BossLevelContentName, typeof(RectTransform), typeof(HorizontalLayoutGroup));
        RectTransform content = (RectTransform)contentObject.transform;
        content.SetParent(parent, false);
        content.SetAsLastSibling();
        PositionBossContent(content, hud, showStars);

        ConfigureContentLayout(content);

        EnsureStarGroup(content);
        EnsureStarNumber(content, hud.m_name);
        EnsureIconContainer(content);
        state.BossContent = content;
        return content;
    }

    private static void ConfigureContentLayout(RectTransform content)
    {
        HorizontalLayoutGroup layout = content.GetComponent<HorizontalLayoutGroup>();
        if (layout == null)
        {
            return;
        }

        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.spacing = 0f;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        int leftPadding = -Mathf.RoundToInt(StarIconSize * HudEdgeOpticalBleedRatio);
        if (layout.padding.left != leftPadding ||
            layout.padding.right != 0 ||
            layout.padding.top != 0 ||
            layout.padding.bottom != 0)
        {
            // Procedural and vanilla stars retain transparent sprite padding, so bleed the block outward optically.
            layout.padding = new RectOffset(leftPadding, 0, 0, 0);
        }
    }

    private static RectTransform? GetHudContentParent(EnemyHud.HudData hud)
    {
        RectTransform? healthRoot = GetHealthRoot(hud.m_healthFast);
        if (healthRoot != null && healthRoot.parent is RectTransform healthParent)
        {
            return healthParent;
        }

        if (hud.m_level2 != null && hud.m_level2.parent is RectTransform level2Parent)
        {
            return level2Parent;
        }

        if (hud.m_level3 != null && hud.m_level3.parent is RectTransform level3Parent)
        {
            return level3Parent;
        }

        if (hud.m_name != null && hud.m_name.rectTransform.parent is RectTransform nameParent)
        {
            return nameParent;
        }

        return hud.m_gui != null && hud.m_gui.transform is RectTransform guiRect
            ? guiRect
            : null;
    }

    private static RectTransform? GetHealthRoot(GuiBar? healthFast)
    {
        if (healthFast == null)
        {
            return null;
        }

        RectTransform? healthBar = healthFast.transform as RectTransform;
        return healthBar != null ? healthBar.parent as RectTransform : null;
    }

    private static void PositionLevelContent(RectTransform content, EnemyHud.HudData hud, bool showStars)
    {
        PositionHudContent(content, hud, LevelContentWidth, LevelContentBelowHealthGap, GetLevelContentHeight(showStars));
    }

    private static void PositionBossContent(RectTransform content, EnemyHud.HudData hud, bool showStars)
    {
        PositionHudContent(content, hud, BossContentWidth, BossContentBelowHealthGap, GetLevelContentHeight(showStars));
    }

    private static void PositionHudContent(
        RectTransform content,
        EnemyHud.HudData hud,
        float fallbackWidth,
        float belowReferenceGap,
        float contentHeight)
    {
        RectTransform? parent = content.parent as RectTransform;
        RectTransform? healthRect = GetHealthRoot(hud.m_healthFast);
        if (parent != null && healthRect != null && healthRect.parent == parent)
        {
            PositionBelowReference(
                content,
                healthRect,
                belowReferenceGap,
                minimumWidth: 1f,
                contentHeight: contentHeight);
            return;
        }

        RectTransform? levelReference = hud.m_level2 != null && hud.m_level2.parent == parent
            ? hud.m_level2
            : hud.m_level3 != null && hud.m_level3.parent == parent ? hud.m_level3 : null;
        if (levelReference != null)
        {
            float width = Mathf.Max(fallbackWidth, Mathf.Max(levelReference.rect.width, levelReference.sizeDelta.x));
            ApplyRectLayout(
                content,
                levelReference.anchorMin,
                levelReference.anchorMax,
                levelReference.pivot,
                levelReference.anchoredPosition,
                new Vector2(width, contentHeight));
            return;
        }

        RectTransform? nameRect = hud.m_name != null ? hud.m_name.rectTransform : null;
        if (parent != null && nameRect != null && nameRect.parent == parent)
        {
            PositionBelowReference(content, nameRect, belowReferenceGap, fallbackWidth, contentHeight);
            return;
        }

        ApplyRectLayout(
            content,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            new Vector2(fallbackWidth, contentHeight));
    }

    private static void PositionBelowReference(
        RectTransform content,
        RectTransform reference,
        float gap,
        float minimumWidth,
        float contentHeight)
    {
        float referenceWidth = Mathf.Max(1f, Mathf.Max(reference.rect.width, reference.sizeDelta.x));
        float referenceHeight = Mathf.Max(1f, Mathf.Max(reference.rect.height, reference.sizeDelta.y));
        float width = Mathf.Max(minimumWidth, referenceWidth);
        float referenceBottom = reference.anchoredPosition.y - reference.pivot.y * referenceHeight;
        float contentY = referenceBottom - gap - (1f - reference.pivot.y) * contentHeight;

        ApplyRectLayout(
            content,
            reference.anchorMin,
            reference.anchorMax,
            reference.pivot,
            new Vector2(reference.anchoredPosition.x, contentY),
            new Vector2(width, contentHeight));
    }

    private static void ApplyRectLayout(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta)
    {
        SetRectValue(rect, anchorMin, anchorMax, pivot, anchoredPosition);
        if (rect.sizeDelta != sizeDelta)
        {
            rect.sizeDelta = sizeDelta;
        }
    }

    private static void SetRectValue(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition)
    {
        if (rect.anchorMin != anchorMin) rect.anchorMin = anchorMin;
        if (rect.anchorMax != anchorMax) rect.anchorMax = anchorMax;
        if (rect.pivot != pivot) rect.pivot = pivot;
        if (rect.anchoredPosition != anchoredPosition) rect.anchoredPosition = anchoredPosition;
    }

    private static void UpdateStarBadge(
        RectTransform content,
        RectTransform? level2,
        RectTransform? level3,
        TextMeshProUGUI? nameText,
        int stars,
        bool showIndividualStars = false)
    {
        RectTransform starGroup = EnsureStarGroup(content);
        TextMeshProUGUI number = EnsureStarNumber(content, nameText);
        int iconCount = showIndividualStars ? Mathf.Clamp(stars, 1, 2) : 1;
        bool visible = stars > 0 && EnsureVanillaStarLayers(starGroup, level2, level3, iconCount);
        starGroup.gameObject.SetActive(visible);
        bool showNumber = visible && !showIndividualStars;
        number.gameObject.SetActive(showNumber);
        if (!showNumber)
        {
            return;
        }

        if (int.TryParse(number.text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int displayedStars) &&
            displayedStars == stars)
        {
            return;
        }

        number.text = stars.ToString(CultureInfo.InvariantCulture);
        ConfigureStarNumber(number);
    }

    private static RectTransform EnsureStarGroup(RectTransform content)
    {
        Transform existing = content.Find(StarGroupName);
        if (existing != null)
        {
            return (RectTransform)existing;
        }

        GameObject starObject = new(StarGroupName, typeof(RectTransform), typeof(LayoutElement));
        RectTransform rect = (RectTransform)starObject.transform;
        rect.SetParent(content, false);
        float starSize = StarIconSize;
        rect.sizeDelta = new Vector2(starSize, starSize);

        LayoutElement layout = starObject.GetComponent<LayoutElement>();
        layout.minWidth = starSize;
        layout.preferredWidth = starSize;
        layout.minHeight = starSize;
        layout.preferredHeight = starSize;
        return rect;
    }

    private static bool EnsureVanillaStarLayers(
        RectTransform starGroup,
        RectTransform? level2,
        RectTransform? level3,
        int iconCount)
    {
        iconCount = Mathf.Clamp(iconCount, 1, 2);
        RectTransform? firstSlot = starGroup.Find(StarSlotNamePrefix + "1") as RectTransform;
        if (firstSlot == null)
        {
            RectTransform? sourceBlock = null;
            Vector2 sourceCenter = Vector2.zero;
            float score = -1f;
            TryFindVanillaStarSlot(level2, ref sourceBlock, ref sourceCenter, ref score);
            TryFindVanillaStarSlot(level3, ref sourceBlock, ref sourceCenter, ref score);
            TryFindTemplateVanillaStarSlot(ref sourceBlock, ref sourceCenter, ref score);
            firstSlot = CreateStarSlot(starGroup, 1);
            if (sourceBlock != null)
            {
                foreach (Image image in sourceBlock.GetComponentsInChildren<Image>(true))
                {
                    if (!image.gameObject.name.StartsWith("star", StringComparison.OrdinalIgnoreCase) || image.sprite == null)
                    {
                        continue;
                    }

                    Vector2 center = GetRelativeCenter(sourceBlock, image.rectTransform);
                    if (Vector2.Distance(center, sourceCenter) <= StarLayerTolerance)
                    {
                        CopyStarLayer(firstSlot, sourceBlock, sourceCenter, image);
                    }
                }
            }

            if (firstSlot.childCount == 0)
            {
                CreateFallbackStarLayer(firstSlot);
            }
        }

        RectTransform? secondSlot = starGroup.Find(StarSlotNamePrefix + "2") as RectTransform;
        if (iconCount == 2 && secondSlot == null)
        {
            GameObject clone = UnityEngine.Object.Instantiate(firstSlot.gameObject, starGroup, false);
            clone.name = StarSlotNamePrefix + "2";
            secondSlot = clone.transform as RectTransform;
        }

        ConfigureStarSlots(starGroup, firstSlot, secondSlot, iconCount);
        return true;
    }

    private static RectTransform CreateStarSlot(RectTransform starGroup, int index)
    {
        GameObject slotObject = new(StarSlotNamePrefix + index, typeof(RectTransform));
        RectTransform slot = (RectTransform)slotObject.transform;
        slot.SetParent(starGroup, false);
        slot.anchorMin = new Vector2(0.5f, 0.5f);
        slot.anchorMax = new Vector2(0.5f, 0.5f);
        slot.pivot = new Vector2(0.5f, 0.5f);
        slot.sizeDelta = new Vector2(StarLayerBaseSize, StarLayerBaseSize);
        return slot;
    }

    private static void CreateFallbackStarLayer(RectTransform slot)
    {
        GameObject layerObject = new("CreatureManager_FallbackStar", typeof(RectTransform), typeof(Image));
        RectTransform rect = (RectTransform)layerObject.transform;
        rect.SetParent(slot, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(StarLayerBaseSize, StarLayerBaseSize);

        Image image = layerObject.GetComponent<Image>();
        image.sprite = GetFallbackStarSprite();
        image.color = Color.white;
        image.preserveAspect = true;
        image.raycastTarget = false;
    }

    private static void ConfigureStarSlots(
        RectTransform starGroup,
        RectTransform firstSlot,
        RectTransform? secondSlot,
        int iconCount)
    {
        float starSize = StarIconSize;
        float starScale = starSize / StarLayerBaseSize;
        float width = starSize * iconCount;
        bool geometryChanged = false;
        Vector2 groupSize = new(width, starSize);
        if (starGroup.sizeDelta != groupSize)
        {
            starGroup.sizeDelta = groupSize;
            geometryChanged = true;
        }

        LayoutElement? layout = starGroup.GetComponent<LayoutElement>();
        if (layout != null &&
            (!Mathf.Approximately(layout.minWidth, width) ||
             !Mathf.Approximately(layout.preferredWidth, width) ||
             !Mathf.Approximately(layout.minHeight, starSize) ||
             !Mathf.Approximately(layout.preferredHeight, starSize)))
        {
            layout.minWidth = width;
            layout.preferredWidth = width;
            layout.minHeight = starSize;
            layout.preferredHeight = starSize;
            geometryChanged = true;
        }

        firstSlot.gameObject.SetActive(true);
        geometryChanged |= ConfigureStarSlot(
            firstSlot,
            iconCount == 2 ? new Vector2(-starSize * 0.5f, 0f) : Vector2.zero,
            starScale);

        if (secondSlot != null)
        {
            secondSlot.gameObject.SetActive(iconCount == 2);
            geometryChanged |= ConfigureStarSlot(secondSlot, new Vector2(starSize * 0.5f, 0f), starScale);
        }

        if (geometryChanged)
        {
            LayoutRebuilder.MarkLayoutForRebuild(starGroup);
            if (starGroup.parent is RectTransform content)
            {
                LayoutRebuilder.MarkLayoutForRebuild(content);
            }
        }
    }

    private static bool ConfigureStarSlot(RectTransform slot, Vector2 position, float scale)
    {
        bool changed = false;
        Vector2 baseSize = new(StarLayerBaseSize, StarLayerBaseSize);
        if (slot.sizeDelta != baseSize)
        {
            slot.sizeDelta = baseSize;
            changed = true;
        }

        if (slot.anchoredPosition != position)
        {
            slot.anchoredPosition = position;
            changed = true;
        }

        Vector3 desiredScale = new(scale, scale, 1f);
        if (slot.localScale != desiredScale)
        {
            slot.localScale = desiredScale;
            changed = true;
        }

        return changed;
    }

    private static void TryFindTemplateVanillaStarSlot(ref RectTransform? sourceBlock, ref Vector2 sourceCenter, ref float score)
    {
        GameObject? template = EnemyHud.instance != null ? EnemyHud.instance.m_baseHud : null;
        if (template == null)
        {
            return;
        }

        TryFindVanillaStarSlot(template.transform.Find("level_2") as RectTransform, ref sourceBlock, ref sourceCenter, ref score);
        TryFindVanillaStarSlot(template.transform.Find("level_3") as RectTransform, ref sourceBlock, ref sourceCenter, ref score);
    }

    private static void CopyStarLayer(RectTransform starGroup, RectTransform sourceBlock, Vector2 sourceCenter, Image source)
    {
        GameObject layerObject = new($"CreatureManager_{source.gameObject.name}", typeof(RectTransform), typeof(Image));
        RectTransform rect = (RectTransform)layerObject.transform;
        rect.SetParent(starGroup, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = source.rectTransform.pivot;

        Vector2 center = GetRelativeCenter(sourceBlock, source.rectTransform);
        rect.anchoredPosition = center - sourceCenter;
        rect.sizeDelta = new Vector2(
            Mathf.Max(1f, source.rectTransform.rect.width),
            Mathf.Max(1f, source.rectTransform.rect.height));
        rect.localScale = source.rectTransform.localScale;

        Image image = layerObject.GetComponent<Image>();
        image.sprite = source.sprite;
        image.color = source.color;
        image.material = source.material;
        image.type = source.type;
        image.preserveAspect = source.preserveAspect;
        image.fillCenter = source.fillCenter;
        image.fillMethod = source.fillMethod;
        image.fillAmount = source.fillAmount;
        image.fillClockwise = source.fillClockwise;
        image.fillOrigin = source.fillOrigin;
        image.raycastTarget = false;
    }

    private static Vector2 GetRelativeCenter(RectTransform root, RectTransform rect)
    {
        Vector3[] corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        Vector3 center = (corners[0] + corners[2]) * 0.5f;
        Vector3 local = root.InverseTransformPoint(center);
        return new Vector2(local.x, local.y);
    }

    private static TextMeshProUGUI EnsureStarNumber(RectTransform content, TextMeshProUGUI? nameText)
    {
        Transform existing = content.Find(StarNumberName);
        if (existing != null)
        {
            return existing.GetComponent<TextMeshProUGUI>();
        }

        GameObject numberObject = new(StarNumberName, typeof(RectTransform), typeof(LayoutElement));
        numberObject.SetActive(false);
        RectTransform rect = (RectTransform)numberObject.transform;
        rect.SetParent(content, false);

        TextMeshProUGUI number = numberObject.AddComponent<TextMeshProUGUI>();
        ApplyHudFont(number, nameText);

        number.raycastTarget = false;
        number.enableAutoSizing = false;
        number.alignment = TextAlignmentOptions.MidlineLeft;
        number.color = new Color(1f, 0.74f, 0.24f, 1f);
        number.textWrappingMode = TextWrappingModes.NoWrap;
        number.overflowMode = TextOverflowModes.Overflow;

        ConfigureStarNumber(number);
        numberObject.SetActive(true);
        return number;
    }

    private static void ConfigureStarNumber(TextMeshProUGUI number)
    {
        float starSize = StarIconSize;
        float scale = starSize / StarLayerBaseSize;
        float fontSize = StarNumberBaseFontSize * scale;
        if (!Mathf.Approximately(number.fontSize, fontSize))
        {
            number.fontSize = fontSize;
        }

        int characterCount = Mathf.Max(1, number.text?.Length ?? 0);
        float estimatedTextWidth = characterCount * fontSize * 0.65f;
        float width = Mathf.Max(StarNumberBaseWidth * scale, estimatedTextWidth + 2f * scale);
        float height = Mathf.Max(LevelContentHeight, starSize);
        Vector2 desiredSize = new(width, height);
        RectTransform rect = number.rectTransform;
        bool geometryChanged = false;
        if (rect.sizeDelta != desiredSize)
        {
            rect.sizeDelta = desiredSize;
            geometryChanged = true;
        }

        LayoutElement layout = number.GetComponent<LayoutElement>();
        if (!Mathf.Approximately(layout.minWidth, width) ||
            !Mathf.Approximately(layout.preferredWidth, width) ||
            !Mathf.Approximately(layout.minHeight, height) ||
            !Mathf.Approximately(layout.preferredHeight, height))
        {
            layout.minWidth = width;
            layout.preferredWidth = width;
            layout.minHeight = height;
            layout.preferredHeight = height;
            geometryChanged = true;
        }

        if (geometryChanged && rect.parent is RectTransform content)
        {
            LayoutRebuilder.MarkLayoutForRebuild(content);
        }
    }

    private static float GetLevelContentHeight(bool showStars)
    {
        return showStars ? Mathf.Max(LevelContentHeight, StarIconSize) : LevelContentHeight;
    }

    private static int GetModifierIconContainerWidth()
    {
        return Mathf.CeilToInt(ModifierIconSize * MaxActiveModifiers + IconSpacing * (MaxActiveModifiers - 1));
    }

    private static RectTransform EnsureIconContainer(RectTransform row)
    {
        Transform existing = row.Find(IconContainerName);
        if (existing != null)
        {
            RectTransform existingContainer = (RectTransform)existing;
            EnsureModifierSlots(existingContainer);
            return existingContainer;
        }

        GameObject containerObject = new(IconContainerName, typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        RectTransform container = (RectTransform)containerObject.transform;
        container.SetParent(row, false);
        container.SetAsLastSibling();

        ConfigureIconContainer(container);
        EnsureModifierSlots(container);
        return container;
    }

    private static void ConfigureIconContainer(RectTransform container)
    {
        int width = GetModifierIconContainerWidth();
        int height = ModifierIconSize;
        float rightBleed = height * HudEdgeOpticalBleedRatio;
        SetRectValue(container, Vector2.one, Vector2.one, Vector2.one, new Vector2(rightBleed, 0f));

        LayoutElement layoutElement = container.GetComponent<LayoutElement>();
        // Keep the right-aligned modifier block independent so it may overlap without moving the left star block.
        layoutElement.ignoreLayout = true;

        Vector2 desiredSize = new(width, height);
        bool geometryChanged = false;
        if (container.sizeDelta != desiredSize)
        {
            container.sizeDelta = desiredSize;
            geometryChanged = true;
        }

        HorizontalLayoutGroup layout = container.GetComponent<HorizontalLayoutGroup>();
        if (layout.childAlignment != TextAnchor.MiddleRight) layout.childAlignment = TextAnchor.MiddleRight;
        if (!Mathf.Approximately(layout.spacing, IconSpacing)) layout.spacing = IconSpacing;
        if (layout.childControlWidth) layout.childControlWidth = false;
        if (layout.childControlHeight) layout.childControlHeight = false;
        if (layout.childForceExpandWidth) layout.childForceExpandWidth = false;
        if (layout.childForceExpandHeight) layout.childForceExpandHeight = false;
        if (layout.padding.left != 0 || layout.padding.right != 0 || layout.padding.top != 0 || layout.padding.bottom != 0)
        {
            layout.padding = new RectOffset(0, 0, 0, 0);
        }

        if (!Mathf.Approximately(layoutElement.minWidth, width) ||
            !Mathf.Approximately(layoutElement.preferredWidth, width) ||
            !Mathf.Approximately(layoutElement.minHeight, height) ||
            !Mathf.Approximately(layoutElement.preferredHeight, height))
        {
            layoutElement.minWidth = width;
            layoutElement.preferredWidth = width;
            layoutElement.minHeight = height;
            layoutElement.preferredHeight = height;
            geometryChanged = true;
        }

        if (geometryChanged)
        {
            LayoutRebuilder.MarkLayoutForRebuild(container);
        }
    }

    internal static void UpdateEnemyHuds(EnemyHud enemyHud)
    {
        if (enemyHud == null)
        {
            return;
        }

        foreach (var hud in enemyHud.m_huds)
        {
            UpdateEnemyHud(hud.Key, hud.Value.m_isMount);
        }
    }

    private static bool TryGetHudModifierState(
        Character character,
        GameObject hudGui,
        out ModifierMask visible,
        out float armoredReduction,
        out float enragedBonus,
        out bool blamerActive,
        out bool refreshed)
    {
        int id = character.GetInstanceID();
        float now = Time.unscaledTime;
        if (ModifierHudRefreshStates.TryGetValue(id, out ModifierHudRefreshState cached) &&
            ReferenceEquals(cached.Character, character) &&
            ReferenceEquals(cached.HudGui, hudGui) &&
            now < cached.NextRefreshTime)
        {
            visible = cached.Visible;
            armoredReduction = cached.ArmoredReduction;
            enragedBonus = cached.EnragedBonus;
            blamerActive = cached.BlamerActive;
            refreshed = false;
            return cached.Result;
        }

        refreshed = true;
        bool result = ComputeHudModifierState(character, out visible, out armoredReduction, out enragedBonus, out blamerActive);
        if (!ModifierHudRefreshStates.TryGetValue(id, out cached) || !ReferenceEquals(cached.Character, character))
        {
            cached = new ModifierHudRefreshState { Character = character };
            ModifierHudRefreshStates[id] = cached;
        }

        cached.HudGui = hudGui;
        cached.NextRefreshTime = now + HudModifierRefreshInterval;
        cached.Result = result;
        cached.Visible = visible;
        cached.ArmoredReduction = armoredReduction;
        cached.EnragedBonus = enragedBonus;
        cached.BlamerActive = blamerActive;
        return result;
    }

    private static bool ComputeHudModifierState(
        Character character,
        out ModifierMask visible,
        out float armoredReduction,
        out float enragedBonus,
        out bool blamerActive)
    {
        visible = ModifierMask.None;
        armoredReduction = 0f;
        enragedBonus = 0f;
        blamerActive = false;
        if (!TryGetZdo(character, out ZDO zdo))
        {
            return false;
        }

        if (!zdo.GetBool(AppliedKey, false))
        {
            return false;
        }

        ModifierMask stored = GetStoredModifierMask(zdo);
        if (stored != ModifierMask.None && !CreatureLevelManager.AllowsModifierEffects(character))
        {
            return true;
        }

        if (HasModifier(stored, ModifierMask.Armored))
        {
            armoredReduction = Mathf.Clamp01(zdo.GetFloat(ArmoredReductionKey, ArmoredDefaultPower));
        }

        if (HasModifier(stored, ModifierMask.Enraged))
        {
            enragedBonus = Mathf.Clamp01(zdo.GetFloat(EnragedBonusKey, EnragedDefaultPower));
        }

        blamerActive = HasModifier(stored, ModifierMask.Blamer) &&
                       zdo.GetBool(BlamerActiveKey, false) &&
                       HasBlamerKarmaRemaining(zdo);

        foreach (ModifierSpec spec in ModifierSpecs)
        {
            if (IsModifierVisible(stored, zdo, spec))
            {
                visible |= spec.Mask;
            }
        }

        return true;
    }

    private static bool IsModifierVisible(ModifierMask stored, ZDO zdo, ModifierSpec spec)
    {
        if (!HasModifier(stored, spec.Mask))
        {
            return false;
        }

        if (spec.Mask == ModifierMask.Deathward && !IsDeathwardReady(zdo))
        {
            return false;
        }

        if (spec.Mask == ModifierMask.Reaping)
        {
            return HasStoredReapingSettings(zdo);
        }

        if (spec.Mask == ModifierMask.Chameleon)
        {
            return GetChameleonDamageType(zdo) != ChameleonDamageType.None;
        }

        if (spec.Mask == ModifierMask.Undodgeable)
        {
            // A zero reduction still leaves the dodge-invulnerability bypass active.
            return true;
        }

        if (spec.Mask == ModifierMask.Crippling)
        {
            return zdo.GetFloat(spec.ProcChanceKey!, 0f) > 0f &&
                   (zdo.GetFloat(spec.PowerKey, 0f) > 0f || zdo.GetFloat(CripplingJumpPowerKey, 0f) > 0f);
        }

        if (spec.Mask == ModifierMask.Disruptive)
        {
            return zdo.GetFloat(spec.ProcChanceKey!, 0f) > 0f &&
                   (zdo.GetFloat(spec.PowerKey, 0f) > 0f || zdo.GetFloat(DisruptiveEitrPowerKey, 0f) > 0f);
        }

        return spec.ProcChanceKey == null
            ? zdo.GetFloat(spec.PowerKey, 0f) > 0f
            : zdo.GetFloat(spec.ProcChanceKey, 0f) > 0f && zdo.GetFloat(spec.PowerKey, 0f) > 0f;
    }

    private static bool TryGetArmoredReduction(Character character, out float reduction)
    {
        return TryGetModifierPower(
            character,
            ModifierMask.Armored,
            ArmoredReductionKey,
            ArmoredDefaultPower,
            out reduction);
    }

    private static bool TryGetEnragedBonus(Character character, out float bonus)
    {
        return TryGetModifierPower(
            character,
            ModifierMask.Enraged,
            EnragedBonusKey,
            EnragedDefaultPower,
            out bonus);
    }

    private static bool TryGetReapingDamageBonus(Character character, out float bonus)
    {
        bonus = 0f;
        if (!TryGetZdo(character, out ZDO zdo) ||
            !HasModifier(zdo, ModifierMask.Reaping) ||
            !CreatureLevelManager.AllowsModifierEffects(character))
        {
            return false;
        }

        bonus = Mathf.Max(0f, zdo.GetFloat(ReapingDamageBonusKey, 0f));
        return bonus > 0f;
    }

    private static bool TryGetReapingSettings(Character character, out ReapingSettings settings)
    {
        settings = default;
        if (!TryGetZdo(character, out ZDO zdo) ||
            !HasModifier(zdo, ModifierMask.Reaping) ||
            !CreatureLevelManager.AllowsModifierEffects(character))
        {
            return false;
        }

        settings = new ReapingSettings(
            Mathf.Clamp01(zdo.GetFloat(ReapingPowerKey, ReapingDefaultPower)),
            zdo.GetInt(ReapingHealMaxActivationsKey, ReapingDefaultHealMaxActivations),
            zdo.GetFloat(ReapingMaxHealthPerKillKey, ReapingDefaultMaxHealthPerKill),
            zdo.GetFloat(ReapingMaxHealthCapKey, ReapingDefaultMaxHealthCap),
            zdo.GetFloat(ReapingDamagePerKillKey, ReapingDefaultDamagePerKill),
            zdo.GetFloat(ReapingDamageCapKey, ReapingDefaultDamageCap),
            zdo.GetFloat(ReapingScalePerKillKey, ReapingDefaultScalePerKill),
            zdo.GetFloat(ReapingScaleCapKey, ReapingDefaultScaleCap));
        return settings.HasAnyGain;
    }

    private static bool HasStoredReapingSettings(ZDO zdo)
    {
        return (zdo.GetFloat(ReapingPowerKey, 0f) > 0f && zdo.GetInt(ReapingHealMaxActivationsKey, 0) > 0) ||
               (zdo.GetFloat(ReapingMaxHealthPerKillKey, 0f) > 0f && zdo.GetFloat(ReapingMaxHealthCapKey, 0f) > 0f) ||
               (zdo.GetFloat(ReapingDamagePerKillKey, 0f) > 0f && zdo.GetFloat(ReapingDamageCapKey, 0f) > 0f) ||
               (zdo.GetFloat(ReapingScalePerKillKey, 0f) > 0f && zdo.GetFloat(ReapingScaleCapKey, 0f) > 0f);
    }

    private static bool TryGetDeathwardHealth(Character character, out float healthRatio)
    {
        healthRatio = 0f;
        if (!TryGetZdo(character, out ZDO zdo) ||
            !HasModifier(zdo, ModifierMask.Deathward) ||
            !CreatureLevelManager.AllowsModifierEffects(character) ||
            !IsDeathwardReady(zdo))
        {
            return false;
        }

        healthRatio = Mathf.Clamp01(zdo.GetFloat(DeathwardHealthKey, DeathwardDefaultPower));
        return healthRatio > 0f;
    }

    private static bool IsDeathwardReady(ZDO zdo)
    {
        int activationCount = Math.Max(0, zdo.GetInt(DeathwardActivationCountKey, 0));
        int maxActivations = ResolveDeathwardMaxActivations(zdo.GetInt(DeathwardMaxActivationsKey, DeathwardDefaultMaxActivations));
        return activationCount < maxActivations &&
               zdo.GetFloat(DeathwardNextReadyTimeKey, 0f) <= GetNetworkTimeSeconds();
    }

    private static bool TryGetModifierHotPathState(
        Character character,
        ModifierMask modifier,
        out ModifierHotPathState state)
    {
        state = null!;
        if (character == null || character.IsPlayer())
        {
            return false;
        }

        int id = character.GetInstanceID();
        float now = Time.unscaledTime;
        if (!ModifierHotPathStates.TryGetValue(id, out state) || !ReferenceEquals(state.Character, character))
        {
            if (!TryGetZdo(character, out ZDO initialZdo) || !initialZdo.GetBool(AppliedKey, false))
            {
                ModifierHotPathStates.Remove(id);
                state = null!;
                return false;
            }

            state = new ModifierHotPathState { Character = character };
            ModifierHotPathStates[id] = state;
            UpdateModifierHotPathState(state, initialZdo, now);
        }
        else if (now >= state.NextValidationTime)
        {
            if (!TryGetZdo(character, out ZDO validationZdo) || !validationZdo.GetBool(AppliedKey, false))
            {
                ModifierHotPathStates.Remove(id);
                state = null!;
                return false;
            }

            UpdateModifierHotPathState(state, validationZdo, now);
        }

        if (!HasModifier(state.Mask, modifier))
        {
            return false;
        }

        int frame = Time.frameCount;
        if (state.EligibilityFrame != frame)
        {
            state.EligibilityFrame = frame;
            state.EffectsAllowed = CreatureLevelManager.AllowsModifierEffects(character);
        }

        return state.EffectsAllowed;
    }

    private static bool TryGetHotPathModifierZdo(Character character, ModifierMask modifier, out ZDO zdo)
    {
        zdo = null!;
        if (!TryGetModifierHotPathState(character, modifier, out _))
        {
            return false;
        }

        if (TryGetZdo(character, out zdo))
        {
            return true;
        }

        ModifierHotPathStates.Remove(character.GetInstanceID());
        return false;
    }

    private static void RefreshModifierHotPathState(Character character, ZDO zdo)
    {
        if (character == null || zdo == null || !zdo.GetBool(AppliedKey, false))
        {
            return;
        }

        int id = character.GetInstanceID();
        if (!ModifierHotPathStates.TryGetValue(id, out ModifierHotPathState state) ||
            !ReferenceEquals(state.Character, character))
        {
            state = new ModifierHotPathState { Character = character };
            ModifierHotPathStates[id] = state;
        }

        UpdateModifierHotPathState(state, zdo, Time.unscaledTime);
    }

    private static void UpdateModifierHotPathState(ModifierHotPathState state, ZDO zdo, float now)
    {
        state.Mask = GetStoredModifierMask(zdo);
        state.SwiftFactor = HasModifier(state.Mask, ModifierMask.Swift)
            ? Mathf.Max(1f, 1f + Mathf.Clamp01(zdo.GetFloat(SwiftPowerKey, SwiftDefaultPower)))
            : 1f;
        state.AttackSpeedFactor = HasModifier(state.Mask, ModifierMask.AttackSpeed)
            ? GetAttackSpeedFactor(Mathf.Clamp01(zdo.GetFloat(AttackSpeedPowerKey, AttackSpeedDefaultPower)))
            : 1f;
        state.UndodgeableEffectActive = HasModifier(state.Mask, ModifierMask.Undodgeable);
        state.UndodgeableDamageReduction = state.UndodgeableEffectActive
            ? ClampUndodgeableDamageReduction(zdo.GetFloat(
                UndodgeableDamageReductionKey,
                UndodgeableDefaultDamageReduction))
            : 0f;
        state.EligibilityFrame = -1;
        state.NextValidationTime = now + ModifierHotPathValidationInterval;
    }

    private static void InvalidateModifierCaches(Character character)
    {
        if (character == null)
        {
            return;
        }

        int id = character.GetInstanceID();
        ModifierHotPathStates.Remove(id);
        ModifierHudRefreshStates.Remove(id);
        PendingReapingHealthBonusRatios.Remove(id);
    }

    private static void InvalidateModifierHudState(Character character)
    {
        if (character != null)
        {
            ModifierHudRefreshStates.Remove(character.GetInstanceID());
        }
    }

    private static bool TryGetModifierPower(Character character, ModifierMask modifier, string key, float fallback, out float power)
    {
        power = 0f;
        if (!TryGetZdo(character, out ZDO zdo) ||
            !HasModifier(zdo, modifier) ||
            !CreatureLevelManager.AllowsModifierEffects(character))
        {
            return false;
        }

        power = Mathf.Clamp01(zdo.GetFloat(key, fallback));
        return power > 0f;
    }

    private static bool HasModifier(ZDO zdo, ModifierMask modifier)
    {
        return HasModifier(GetStoredModifierMask(zdo), modifier);
    }

    private static bool HasModifier(ModifierMask stored, ModifierMask modifier)
    {
        return (stored & modifier) != 0;
    }

    private static bool HasAnyActiveModifier(Character character)
    {
        if (!TryGetZdo(character, out ZDO zdo) || GetStoredModifierMask(zdo) == ModifierMask.None)
        {
            return false;
        }

        return CreatureLevelManager.AllowsModifierEffects(character);
    }

    private static ModifierMask GetStoredModifierMask(ZDO zdo)
    {
        return (ModifierMask)zdo.GetLong(Mask64Key, 0L);
    }

    private static void SetStoredModifierMask(ZDO zdo, ModifierMask mask)
    {
        zdo.Set(Mask64Key, (long)mask);
    }

    private static bool TryFindCharacter(ZDOID id, out Character character)
    {
        character = null!;
        if (id == ZDOID.None || ZNetScene.instance == null)
        {
            return false;
        }

        GameObject instance = ZNetScene.instance.FindInstance(id);
        character = instance != null ? instance.GetComponent<Character>() : null!;
        return character != null;
    }

    private static bool TryGetCharacterZdo(Character character, out ZDO zdo)
    {
        zdo = null!;
        if (character == null)
        {
            return false;
        }

        ZNetView? nview = character.m_nview;
        if (nview == null || !nview.IsValid())
        {
            return false;
        }

        zdo = nview.GetZDO();
        return zdo != null;
    }

    private static bool TryGetZdo(Character character, out ZDO zdo)
    {
        zdo = null!;
        return character != null &&
               !character.IsPlayer() &&
               TryGetCharacterZdo(character, out zdo);
    }

    private static void EnsureModifierSlots(RectTransform row)
    {
        HudIconState state = GetHudIconState(row);
        bool slotsReady = state.SlotsInitialized;
        if (slotsReady)
        {
            for (int index = 0; index < state.Slots.Length; index++)
            {
                if (state.Slots[index] != null)
                {
                    continue;
                }

                slotsReady = false;
                break;
            }
        }

        if (slotsReady)
        {
            return;
        }

        for (int index = 0; index < ModifierGroupOrder.Length; index++)
        {
            state.Slots[index] = EnsureModifierSlot(row, ModifierGroupOrder[index], index, ModifierIconSize);
        }

        state.Initialized = false;
        state.SlotsInitialized = true;
    }

    private static void UpdateModifierIcons(RectTransform row, ModifierMask visible, float armoredReduction, float enragedBonus)
    {
        int armoredKey = Mathf.RoundToInt(armoredReduction * 10000f);
        int enragedKey = Mathf.RoundToInt(enragedBonus * 10000f);
        CreatureManagerPlugin.ModifierIconLayout layout = CreatureManagerPlugin.ModifierHudIconLayout?.Value ??
                                                          CreatureManagerPlugin.ModifierIconLayout.FixedCategorySlots;
        HudIconState state = GetHudIconState(row);
        if (state.Matches(visible, armoredKey, enragedKey, layout))
        {
            return;
        }

        ModifierSpec?[] slots = BuildHudModifierSlots(visible, armoredReduction, enragedBonus, layout);
        state.Set(visible, armoredKey, enragedKey, layout);
        bool layoutChanged = false;
        bool packEmptySlots = layout == CreatureManagerPlugin.ModifierIconLayout.RightPacked;
        for (int slotIndex = 0; slotIndex < slots.Length; slotIndex++)
        {
            layoutChanged |= UpdateModifierSlot(row, slotIndex, slots[slotIndex], packEmptySlots);
        }

        if (layoutChanged)
        {
            LayoutRebuilder.MarkLayoutForRebuild(row);
        }
    }

    private static HudIconState GetHudIconState(RectTransform row)
    {
        return HudIconStates.GetValue(row, _ => new HudIconState());
    }

    private static Image EnsureModifierSlot(RectTransform row, ModifierGroup group, int siblingIndex, int iconSize)
    {
        string name = GetModifierSlotName(group);
        Transform existing = row.Find(name);
        if (existing != null)
        {
            existing.SetSiblingIndex(siblingIndex);
            existing.gameObject.SetActive(true);
            Image existingImage = existing.GetComponent<Image>();
            ConfigureModifierSlot(existingImage, iconSize);
            return existingImage;
        }

        GameObject icon = new(name, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        RectTransform rect = (RectTransform)icon.transform;
        rect.SetParent(row, false);

        Image image = icon.GetComponent<Image>();
        image.raycastTarget = false;
        image.preserveAspect = true;
        image.enabled = false;
        rect.SetSiblingIndex(siblingIndex);
        ConfigureModifierSlot(image, iconSize);
        return image;
    }

    private static void ConfigureModifierSlot(Image image, int size)
    {
        Vector2 desiredSize = new(size, size);
        RectTransform rect = image.rectTransform;
        bool geometryChanged = false;
        if (rect.sizeDelta != desiredSize)
        {
            rect.sizeDelta = desiredSize;
            geometryChanged = true;
        }

        LayoutElement layout = image.GetComponent<LayoutElement>();
        if (!Mathf.Approximately(layout.preferredWidth, size) ||
            !Mathf.Approximately(layout.preferredHeight, size) ||
            !Mathf.Approximately(layout.minWidth, size) ||
            !Mathf.Approximately(layout.minHeight, size))
        {
            layout.preferredWidth = size;
            layout.preferredHeight = size;
            layout.minWidth = size;
            layout.minHeight = size;
            geometryChanged = true;
        }

        if (geometryChanged && rect.parent is RectTransform container)
        {
            LayoutRebuilder.MarkLayoutForRebuild(container);
        }
    }

    private static ModifierSpec?[] BuildHudModifierSlots(
        ModifierMask visible,
        float armoredReduction,
        float enragedBonus,
        CreatureManagerPlugin.ModifierIconLayout layout)
    {
        ModifierSpec?[] slots = new ModifierSpec?[MaxActiveModifiers];
        if (layout == CreatureManagerPlugin.ModifierIconLayout.RightPacked)
        {
            int nextSlot = 0;
            foreach (ModifierSpec spec in ModifierSpecs)
            {
                if (!IsModifierIconVisible(spec, visible, armoredReduction, enragedBonus))
                {
                    continue;
                }

                slots[nextSlot++] = spec;
                if (nextSlot >= slots.Length)
                {
                    break;
                }
            }

            return slots;
        }

        ModifierSpec?[] extras = new ModifierSpec?[MaxActiveModifiers];
        int extraCount = 0;
        foreach (ModifierSpec spec in ModifierSpecs)
        {
            if (!IsModifierIconVisible(spec, visible, armoredReduction, enragedBonus))
            {
                continue;
            }

            int categorySlot = (int)spec.Group;
            if (slots[categorySlot] == null)
            {
                slots[categorySlot] = spec;
            }
            else if (extraCount < extras.Length)
            {
                // Forced modifier sets may contain several entries from one category.
                extras[extraCount++] = spec;
            }
        }

        int extraIndex = 0;
        for (int slotIndex = 0; slotIndex < slots.Length && extraIndex < extraCount; slotIndex++)
        {
            if (slots[slotIndex] == null)
            {
                slots[slotIndex] = extras[extraIndex++];
            }
        }

        return slots;
    }

    private static bool IsModifierIconVisible(
        ModifierSpec spec,
        ModifierMask visible,
        float armoredReduction,
        float enragedBonus)
    {
        return HasModifier(visible, spec.Mask) &&
               (spec.Mask != ModifierMask.Armored || armoredReduction > 0f) &&
               (spec.Mask != ModifierMask.Enraged || enragedBonus > 0f);
    }

    private static bool UpdateModifierSlot(
        RectTransform row,
        int slotIndex,
        ModifierSpec? spec,
        bool packEmptySlots)
    {
        HudIconState state = GetHudIconState(row);
        ModifierGroup slotGroup = ModifierGroupOrder[slotIndex];
        Image image = state.Slots[slotIndex] ?? EnsureModifierSlot(row, slotGroup, slotIndex, ModifierIconSize);
        if (!image.gameObject.activeSelf)
        {
            image.gameObject.SetActive(true);
        }

        image.enabled = spec != null;
        image.sprite = spec?.Sprite();
        image.color = Color.white;
        LayoutElement? layoutElement = image.GetComponent<LayoutElement>();
        bool ignoreLayout = packEmptySlots && spec == null;
        if (layoutElement == null || layoutElement.ignoreLayout == ignoreLayout)
        {
            return false;
        }

        layoutElement.ignoreLayout = ignoreLayout;
        return true;
    }

    private static string GetModifierSlotName(ModifierGroup group)
    {
        return group switch
        {
            ModifierGroup.Offense => OffenseIconSlotName,
            ModifierGroup.Defense => DefenseIconSlotName,
            ModifierGroup.Affliction => AfflictionIconSlotName,
            ModifierGroup.Special => SpecialIconSlotName,
            _ => throw new ArgumentOutOfRangeException(nameof(group), group, null)
        };
    }

    private static Sprite GetArmoredSprite()
    {
        ArmoredSprite ??= CreateSolidIcon(
            "CreatureManager_ArmoredIcon",
            IsCuirassPixel,
            new Color(0.08f, 0.28f, 0.68f, 1f));
        return ArmoredSprite!;
    }

    private static Sprite GetEnragedSprite()
    {
        EnragedSprite ??= CreateSolidIcon("CreatureManager_EnragedIcon", IsSwordPixel, new Color(1f, 0.23f, 0.12f, 1f));
        return EnragedSprite!;
    }

    private static Sprite GetDeathwardSprite()
    {
        DeathwardSprite ??= CreateThreeToneIcon(
            "CreatureManager_DeathwardIcon",
            GetDeathwardTone,
            new Color(0.36f, 0.12f, 0.62f, 1f),
            new Color(0.94f, 0.85f, 1f, 1f),
            new Color(0.18f, 0.04f, 0.32f, 1f));
        return DeathwardSprite!;
    }

    private static Sprite GetSwiftSprite()
    {
        SwiftSprite ??= CreateSolidIcon("CreatureManager_SwiftIcon", IsChevronPixel, new Color(0.25f, 0.95f, 0.55f, 1f));
        return SwiftSprite!;
    }

    private static Sprite GetRegeneratingSprite()
    {
        RegeneratingSprite ??= CreateSolidIcon("CreatureManager_RegeneratingIcon", IsCrossPixel, new Color(0.2f, 0.95f, 0.25f, 1f));
        return RegeneratingSprite!;
    }

    private static Sprite GetVampiricSprite()
    {
        VampiricSprite ??= CreateThreeToneIcon(
            "CreatureManager_VampiricIcon",
            GetVampiricTone,
            new Color(0.96f, 0.97f, 1f, 1f),
            new Color(0.92f, 0.94f, 1f, 1f),
            new Color(0.98f, 0.03f, 0.07f, 1f));
        return VampiricSprite!;
    }

    private static Sprite GetFireSprite()
    {
        FireSprite ??= CreateTwoToneIcon(
            "CreatureManager_FireIcon",
            IsFlamePixel,
            new Color(1f, 0.2f, 0.02f, 1f),
            new Color(1f, 0.82f, 0.08f, 1f));
        return FireSprite!;
    }

    private static Sprite GetFrostSprite()
    {
        FrostSprite ??= CreateSolidIcon("CreatureManager_FrostIcon", IsSnowPixel, new Color(0.35f, 0.85f, 1f, 1f));
        return FrostSprite!;
    }

    private static Sprite GetLightningSprite()
    {
        LightningSprite ??= CreateSolidIcon("CreatureManager_LightningIcon", IsBoltPixel, new Color(1f, 0.9f, 0.08f, 1f));
        return LightningSprite!;
    }

    private static Sprite GetSpiritSprite()
    {
        SpiritSprite ??= CreateThreeToneIcon(
            "CreatureManager_SpiritIcon",
            GetSpiritTone,
            new Color(1f, 0.68f, 0.05f, 1f),
            new Color(1f, 0.95f, 0.6f, 1f),
            new Color(0.85f, 0.15f, 0.08f, 1f));
        return SpiritSprite!;
    }

    private static Sprite GetToxicDeathSprite()
    {
        ToxicDeathSprite ??= CreateSolidIcon("CreatureManager_ToxicDeathIcon", IsSkullPixel, new Color(0.45f, 1f, 0.18f, 1f));
        return ToxicDeathSprite!;
    }

    private static Sprite GetArmorPiercingSprite()
    {
        ArmorPiercingSprite ??= CreateThreeToneIcon(
            "CreatureManager_ArmorPiercingIcon",
            GetArmorPiercingTone,
            new Color(0.48f, 0.75f, 1f, 1f),
            new Color(0.2f, 0.25f, 0.55f, 1f),
            new Color(1f, 0.52f, 0.08f, 1f));
        return ArmorPiercingSprite!;
    }

    private static Sprite GetStaggeringSprite()
    {
        StaggeringSprite ??= CreateThreeToneIcon(
            "CreatureManager_StaggeringIcon",
            GetStaggeringTone,
            new Color(1f, 0.55f, 0.02f, 1f),
            new Color(1f, 0.86f, 0.12f, 1f),
            new Color(1f, 0.95f, 0.28f, 1f));
        return StaggeringSprite!;
    }

    private static Sprite GetUndodgeableSprite()
    {
        UndodgeableSprite ??= CreateTwoToneIcon(
            "CreatureManager_UndodgeableIcon",
            IsUndodgeablePixel,
            new Color(1f, 0.96f, 0.9f, 1f),
            new Color(1f, 0.12f, 0.08f, 1f));
        return UndodgeableSprite!;
    }

    private static Sprite GetAttackSpeedSprite()
    {
        AttackSpeedSprite ??= CreateSolidIcon("CreatureManager_AttackSpeedIcon", IsHourglassPixel, new Color(1f, 0.62f, 0.08f, 1f));
        return AttackSpeedSprite!;
    }

    private static Sprite GetExposedSprite()
    {
        ExposedSprite ??= CreateTwoToneIcon(
            "CreatureManager_ExposedIcon",
            IsBrokenShieldPixel,
            new Color(0.42f, 0.82f, 0.98f, 1f),
            new Color(0.72f, 0.96f, 1f, 1f));
        return ExposedSprite!;
    }

    private static Sprite GetWeakenedSprite()
    {
        WeakenedSprite ??= CreateThreeToneIcon(
            "CreatureManager_WeakenedIcon",
            GetWeakenedTone,
            new Color(0.82f, 0.84f, 0.85f, 1f),
            new Color(0.32f, 0.11f, 0.15f, 1f),
            new Color(0.96f, 0.68f, 0.24f, 1f));
        return WeakenedSprite!;
    }

    private static Sprite GetWitheredSprite()
    {
        WitheredSprite ??= CreateSolidIcon("CreatureManager_WitheredIcon", IsWiltedLeafPixel, new Color(0.66f, 0.78f, 0.22f, 1f));
        return WitheredSprite!;
    }

    private static Sprite GetReflectionSprite()
    {
        ReflectionSprite ??= CreateThreeToneIcon(
            "CreatureManager_ReflectionIcon",
            GetReflectionTone,
            new Color(0.56f, 0.84f, 0.92f, 1f),
            new Color(0.32f, 0.68f, 1f, 1f),
            new Color(0.96f, 1f, 1f, 1f));
        return ReflectionSprite!;
    }

    private static Sprite GetVortexSprite()
    {
        VortexSprite ??= CreateTwoToneIcon(
            "CreatureManager_VortexIcon",
            IsSpiralPixel,
            new Color(0.58f, 0.72f, 0.82f, 1f),
            new Color(0.36f, 0.55f, 0.68f, 1f));
        return VortexSprite!;
    }

    private static Sprite GetCripplingSprite()
    {
        CripplingSprite ??= CreateSolidIcon("CreatureManager_CripplingIcon", IsSnarePixel, new Color(0.78f, 0.45f, 1f, 1f));
        return CripplingSprite!;
    }

    private static Sprite GetDisruptiveSprite()
    {
        DisruptiveSprite ??= CreateSolidIcon("CreatureManager_DisruptiveIcon", IsInterferencePixel, new Color(0.14f, 1f, 0.86f, 1f));
        return DisruptiveSprite!;
    }

    private static Sprite GetAdrenalineDrainSprite()
    {
        AdrenalineDrainSprite ??= CreateSolidIcon("CreatureManager_AdrenalineDrainIcon", IsAdrenalineDrainPixel, new Color(1f, 0.35f, 0.42f, 1f));
        return AdrenalineDrainSprite!;
    }

    private static Sprite GetCorrosiveSprite()
    {
        CorrosiveSprite ??= CreateThreeToneIcon(
            "CreatureManager_CorrosiveIcon",
            GetCorrosiveTone,
            new Color(0.72f, 0.78f, 0.81f, 1f),
            new Color(0.34f, 0.43f, 0.48f, 1f),
            new Color(0.95f, 0.32f, 0.06f, 1f));
        return CorrosiveSprite!;
    }

    private static Sprite GetAdaptiveSprite()
    {
        AdaptiveSprite ??= CreateTwoToneIcon(
            "CreatureManager_AdaptiveIcon",
            IsAdaptivePixel,
            new Color(0.7f, 1f, 0.28f, 1f),
            new Color(0.92f, 1f, 0.58f, 1f));
        return AdaptiveSprite!;
    }

    private static Sprite GetUnflinchingSprite()
    {
        UnflinchingSprite ??= CreateTwoToneIcon(
            "CreatureManager_UnflinchingIcon",
            IsUnflinchingPixel,
            new Color(0.92f, 0.82f, 0.55f, 1f),
            new Color(1f, 0.62f, 0.11f, 1f));
        return UnflinchingSprite!;
    }

    private static Sprite GetChameleonSprite()
    {
        ChameleonSprite ??= CreateTwoToneIcon(
            "CreatureManager_ChameleonIcon",
            IsChameleonPixel,
            new Color(0.1f, 0.78f, 0.62f, 1f),
            new Color(0.82f, 1f, 0.18f, 1f));
        return ChameleonSprite!;
    }

    private static Sprite GetOmenSprite()
    {
        OmenSprite ??= CreateTwoToneIcon(
            "CreatureManager_OmenIcon",
            IsEyePixel,
            new Color(0.78f, 0.49f, 1f, 1f),
            new Color(1f, 0.188f, 0.157f, 1f));
        return OmenSprite!;
    }

    private static Sprite GetReapingSprite()
    {
        ReapingSprite ??= CreateThreeToneIcon(
            "CreatureManager_ReapingIcon",
            GetReapingTone,
            new Color(0.36f, 0.08f, 0.16f, 1f),
            new Color(0.48f, 0.28f, 0.13f, 1f),
            new Color(0.82f, 0.88f, 0.93f, 1f));
        return ReapingSprite!;
    }

    private static Sprite GetBlinkSprite()
    {
        BlinkSprite ??= CreateTwoToneIcon(
            "CreatureManager_BlinkIcon",
            IsBlinkPixel,
            new Color(0.28f, 0.88f, 1f, 1f),
            new Color(0.58f, 0.36f, 1f, 1f));
        return BlinkSprite!;
    }

    private static Sprite GetKnockbackSprite()
    {
        KnockbackSprite ??= CreateSolidIcon("CreatureManager_JuggernautIcon", IsAnchorPixel, new Color(0.82f, 0.72f, 0.48f, 1f));
        return KnockbackSprite!;
    }

    private static Sprite GetBlamerSprite()
    {
        BlamerSprite ??= CreateThreeToneIcon(
            "CreatureManager_BlamerIcon",
            GetBlamerTone,
            new Color(0.78f, 0.52f, 0.15f, 1f),
            new Color(0.34f, 0.22f, 0.07f, 1f),
            new Color(1f, 0.78f, 0.22f, 1f));
        return BlamerSprite!;
    }

    private static Sprite GetFallbackStarSprite()
    {
        FallbackStarSprite ??= CreateSolidIcon(
            "CreatureManager_FallbackStarSprite",
            IsFallbackStarPixel,
            new Color(1f, 0.78f, 0.22f, 1f));
        return FallbackStarSprite!;
    }

    private static Sprite CreateSolidIcon(string name, IconShape shape, Color filled)
    {
        return CreateTwoToneIcon(name, shape, filled, filled);
    }

    private static Sprite CreateTwoToneIcon(string name, IconShape shape, Color filled, Color accent)
    {
        Texture2D texture = CreateTexture(name);
        Color clear = new(0f, 0f, 0f, 0f);

        for (int y = 0; y < texture.height; y++)
        {
            for (int x = 0; x < texture.width; x++)
            {
                bool active = shape(x, y, out bool useAccent);
                texture.SetPixel(x, y, active ? useAccent ? accent : filled : clear);
            }
        }

        texture.Apply(updateMipmaps: false, makeNoLongerReadable: true);
        return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), texture.width);
    }

    private static Sprite CreateThreeToneIcon(
        string name,
        IconToneShape shape,
        Color primary,
        Color secondary,
        Color accent)
    {
        Texture2D texture = CreateTexture(name);
        Color clear = new(0f, 0f, 0f, 0f);

        for (int y = 0; y < texture.height; y++)
        {
            for (int x = 0; x < texture.width; x++)
            {
                Color color = shape(x, y) switch
                {
                    IconTone.Primary => primary,
                    IconTone.Secondary => secondary,
                    IconTone.Accent => accent,
                    _ => clear
                };
                texture.SetPixel(x, y, color);
            }
        }

        texture.Apply(updateMipmaps: false, makeNoLongerReadable: true);
        return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), texture.width);
    }

    private static Texture2D CreateTexture(string name)
    {
        Texture2D texture = new(64, 64, TextureFormat.RGBA32, mipChain: false)
        {
            name = name,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        return texture;
    }

    private static readonly Vector2[] CuirassOuterPolygon =
    {
        new(10f, 58f), new(23f, 61f), new(41f, 61f), new(54f, 58f), new(52f, 48f),
        new(57f, 39f), new(52f, 32f), new(49f, 11f), new(32f, 5f), new(15f, 11f),
        new(12f, 32f), new(7f, 39f), new(12f, 48f)
    };
    private static readonly Vector2[] VampiricLeftOuterFangPolygon =
    {
        new(7f, 49f), new(20f, 49f), new(22f, 46f), new(21f, 34f), new(18f, 12f),
        new(14f, 21f), new(9f, 36f)
    };
    private static readonly Vector2[] VampiricRightOuterFangPolygon =
    {
        new(44f, 49f), new(57f, 49f), new(55f, 36f), new(50f, 12f), new(46f, 21f),
        new(43f, 34f), new(42f, 46f)
    };
    private static readonly Vector2[] VampiricLeftInnerToothPolygon =
    {
        new(23f, 49f), new(31f, 49f), new(31f, 31f), new(29f, 27f), new(25f, 27f), new(23f, 31f)
    };
    private static readonly Vector2[] VampiricRightInnerToothPolygon =
    {
        new(33f, 49f), new(41f, 49f), new(41f, 31f), new(39f, 27f), new(35f, 27f), new(33f, 31f)
    };
    private static readonly Vector2[] VampiricDropPolygon =
    {
        new(32f, 23f), new(27f, 14f), new(28f, 8f), new(32f, 5f), new(36f, 8f), new(37f, 14f)
    };
    private static readonly Vector2[] FlameOuterPolygon =
    {
        new(34f, 61f), new(40f, 52f), new(43f, 44f), new(49f, 35f), new(53f, 26f),
        new(51f, 16f), new(45f, 8f), new(36f, 4f), new(27f, 5f), new(18f, 10f),
        new(12f, 18f), new(9f, 28f), new(11f, 39f), new(17f, 49f), new(25f, 56f)
    };
    private static readonly Vector2[] FlameInnerPolygon =
    {
        new(32f, 40f), new(38f, 32f), new(41f, 24f), new(39f, 16f),
        new(33f, 11f), new(27f, 14f), new(23f, 21f), new(24f, 29f)
    };
    private static readonly Vector2[] BoltPolygon =
    {
        new(36f, 58f), new(16f, 29f), new(29f, 29f), new(24f, 6f), new(49f, 38f), new(35f, 38f)
    };
    private static readonly Vector2[] StaggeringCenterStarPolygon =
    {
        new(30f, 57f), new(35f, 41f), new(51f, 36f), new(35f, 31f),
        new(30f, 15f), new(25f, 31f), new(10f, 36f), new(25f, 41f)
    };
    private static readonly Vector2[] StaggeringSmallStarPolygon =
    {
        new(14f, 61f), new(17f, 54f), new(24f, 51f), new(17f, 48f),
        new(14f, 41f), new(11f, 48f), new(4f, 51f), new(11f, 54f)
    };
    private static readonly Vector2[] AdaptiveCorePolygon =
    {
        new(31.5f, 40f), new(39f, 35.5f), new(39f, 27.5f),
        new(31.5f, 23f), new(24f, 27.5f), new(24f, 35.5f)
    };
    private static readonly Vector2[] ReapingHoodPolygon =
    {
        new(32f, 55f), new(23f, 46f), new(21f, 37f), new(26f, 31f),
        new(38f, 31f), new(43f, 38f), new(40f, 48f)
    };
    private static readonly Vector2[] ReapingRobePolygon =
    {
        new(25f, 34f), new(39f, 34f), new(43f, 25f), new(40f, 18f),
        new(46f, 7f), new(18f, 7f), new(24f, 20f), new(21f, 28f)
    };
    private static readonly Vector2[] ReapingBladePolygon =
    {
        new(9f, 49f), new(14f, 59f), new(28f, 62f), new(43f, 60f), new(57f, 52f),
        new(49f, 53f), new(38f, 55f), new(28f, 55f), new(18f, 52f), new(13f, 44f)
    };
    private static readonly Vector2[] BrokenShieldPolygon =
    {
        new(11f, 53f), new(53f, 53f), new(53f, 32f), new(49f, 20f), new(41f, 12f),
        new(31.5f, 6f), new(23f, 12f), new(15f, 20f), new(11f, 32f)
    };
    private static readonly Vector2[] ArmorPiercingShieldPolygon =
    {
        new(13f, 53f), new(48f, 56f), new(54f, 48f), new(53f, 31f), new(48f, 19f),
        new(34f, 7f), new(20f, 13f), new(12f, 26f), new(10f, 42f)
    };
    private static readonly Vector2[] ArmorPiercingCrackPolygon =
    {
        new(29f, 48f), new(35f, 42f), new(33f, 38f), new(40f, 35f), new(36f, 31f),
        new(43f, 27f), new(38f, 22f), new(34f, 24f), new(29f, 18f), new(26f, 24f),
        new(22f, 21f), new(23f, 28f), new(18f, 31f), new(24f, 35f), new(21f, 39f),
        new(27f, 41f), new(26f, 46f)
    };
    private static readonly Vector2[] ArmorPiercingArrowHeadPolygon =
    {
        new(4f, 4f), new(8f, 22f), new(13f, 17f), new(20f, 20f), new(17f, 13f), new(22f, 8f)
    };
    private static readonly Vector2[] UndodgeableOuterPolygon =
    {
        new(32f, 45f), new(45f, 32f), new(32f, 19f), new(19f, 32f)
    };
    private static readonly Vector2[] UndodgeableInnerPolygon =
    {
        new(32f, 38f), new(38f, 32f), new(32f, 26f), new(26f, 32f)
    };
    private static readonly Vector2[] WeakenedAttachedBladePolygon =
    {
        new(21f, 36f), new(29f, 39f), new(42f, 19f), new(39f, 14f),
        new(36f, 16f), new(32f, 12f), new(27f, 20f)
    };
    private static readonly Vector2[] WeakenedTipBladePolygon =
    {
        new(44f, 25f), new(53f, 33f), new(61f, 51f), new(48f, 46f), new(41f, 37f), new(47f, 33f)
    };
    private static readonly Vector2[] WeakenedAttachedGoldEdgePolygon =
    {
        new(19f, 36f), new(22f, 35f), new(29f, 18f), new(33f, 11f), new(31f, 9f), new(26f, 17f)
    };
    private static readonly Vector2[] WeakenedTipGoldEdgePolygon =
    {
        new(43f, 24f), new(53f, 32f), new(62f, 51f), new(59f, 46f), new(51f, 34f), new(46f, 27f)
    };
    private static readonly Vector2[] WeakenedGuardPolygon =
    {
        new(7f, 34f), new(14f, 35f), new(22f, 39f), new(31f, 47f),
        new(32f, 51f), new(27f, 47f), new(20f, 42f), new(12f, 38f)
    };
    private static readonly Vector2[] WiltedLeafLeftPolygon =
    {
        new(31f, 33f), new(9f, 48f), new(26f, 55f)
    };
    private static readonly Vector2[] WiltedLeafRightPolygon =
    {
        new(34f, 27f), new(55f, 40f), new(39f, 49f)
    };
    private static readonly Vector2[] ReflectionShieldPolygon =
    {
        new(34f, 55f), new(54f, 50f), new(54f, 32f), new(50f, 20f),
        new(41f, 11f), new(33f, 16f), new(28f, 26f), new(27f, 40f)
    };
    private static readonly Vector2[] ReflectionArrowHeadPolygon =
    {
        new(6f, 5f), new(10f, 21f), new(21f, 10f)
    };
    private static readonly Vector2[] AdrenalineDrainArrowPolygon =
    {
        new(37f, 25f), new(61f, 25f), new(49f, 10f)
    };
    private static readonly Vector2[] CorrosiveOuterPolygon =
    {
        new(14f, 54f), new(43f, 54f), new(56f, 34f), new(47f, 11f), new(18f, 10f), new(6f, 31f)
    };
    private static readonly Vector2[] BlinkArrowPolygon =
    {
        new(43f, 32f), new(30f, 22f), new(30f, 42f)
    };
    private static readonly Vector2[] AnchorLeftFlukePolygon =
    {
        new(6f, 15f), new(22f, 12f), new(18f, 3f)
    };
    private static readonly Vector2[] AnchorUpperFlukePolygon =
    {
        new(50f, 59f), new(54f, 41f), new(63f, 43f)
    };
    private static readonly Vector2[] BlamerBellPolygon =
    {
        new(32f, 57f), new(24f, 55f), new(19f, 50f), new(17f, 43f), new(17f, 31f),
        new(15f, 24f), new(11f, 18f), new(10f, 14f), new(13f, 11f), new(51f, 11f),
        new(54f, 14f), new(53f, 18f), new(49f, 24f), new(47f, 31f), new(47f, 43f),
        new(45f, 50f), new(40f, 55f)
    };

    private static bool IsCuirassPixel(int x, int y, out bool isAccent)
    {
        isAccent = false;
        Vector2 point = new(x + 0.5f, y + 0.5f);
        bool outer = IsPointInPolygon(point, CuirassOuterPolygon);
        bool neck = Vector2.Distance(point, new Vector2(32f, 62f)) <= 10.5f;
        bool leftArmhole = Vector2.Distance(point, new Vector2(4f, 49f)) <= 11.5f;
        bool rightArmhole = Vector2.Distance(point, new Vector2(60f, 49f)) <= 11.5f;
        return outer && !neck && !leftArmhole && !rightArmhole;
    }

    private static bool IsSwordPixel(int x, int y, out bool isBorder)
    {
        isBorder = false;
        Vector2 point = new(x + 0.5f, y + 0.5f);

        if (IsSwordBladePixel(point, out isBorder))
        {
            return true;
        }

        if (IsSwordGuardPixel(point, out isBorder))
        {
            return true;
        }

        return IsSwordGripPixel(point, out isBorder);
    }

    private static IconTone GetDeathwardTone(int x, int y)
    {
        Vector2 point = new(x + 0.5f, y + 0.5f);
        bool body = point.x >= 15f && point.x <= 49f && point.y >= 14f && point.y <= 43f;
        bool arch = Vector2.Distance(point, new Vector2(32f, 43f)) <= 17f && point.y >= 43f;
        bool tombstone = body || arch;
        bool cross = tombstone &&
                     (Mathf.Abs(point.x - 32f) <= 3.5f && point.y >= 23f && point.y <= 48f ||
                      Mathf.Abs(point.y - 37f) <= 3.5f && point.x >= 23f && point.x <= 41f);
        bool baseStone = point.x >= 10f && point.x <= 54f && point.y >= 7f && point.y <= 14f;

        if (cross)
        {
            return IconTone.Secondary;
        }

        if (baseStone)
        {
            return IconTone.Accent;
        }

        return tombstone ? IconTone.Primary : IconTone.Clear;
    }

    private static bool IsChevronPixel(int x, int y, out bool isBorder)
    {
        isBorder = false;
        Vector2 point = new(x + 0.5f, y + 0.5f);
        bool first = IsThickLine(point, new Vector2(10f, 16f), new Vector2(31f, 32f), 5f) ||
                     IsThickLine(point, new Vector2(31f, 32f), new Vector2(10f, 48f), 5f);
        bool second = IsThickLine(point, new Vector2(28f, 16f), new Vector2(49f, 32f), 5f) ||
                      IsThickLine(point, new Vector2(49f, 32f), new Vector2(28f, 48f), 5f);
        return first || second;
    }

    private static bool IsCrossPixel(int x, int y, out bool isBorder)
    {
        isBorder = false;
        return (x >= 25 && x <= 38 && y >= 9 && y <= 55) ||
               (x >= 9 && x <= 55 && y >= 25 && y <= 38);
    }

    private static IconTone GetVampiricTone(int x, int y)
    {
        Vector2 point = new(x + 0.5f, y + 0.5f);
        bool outerFangs = IsPointInPolygon(point, VampiricLeftOuterFangPolygon) ||
                          IsPointInPolygon(point, VampiricRightOuterFangPolygon);
        bool innerTeeth = IsPointInPolygon(point, VampiricLeftInnerToothPolygon) ||
                          IsPointInPolygon(point, VampiricRightInnerToothPolygon);
        bool drop = IsPointInPolygon(point, VampiricDropPolygon);

        if (drop)
        {
            return IconTone.Accent;
        }

        if (innerTeeth)
        {
            return IconTone.Secondary;
        }

        return outerFangs ? IconTone.Primary : IconTone.Clear;
    }

    private static bool IsFlamePixel(int x, int y, out bool isBorder)
    {
        Vector2 point = new(x + 0.5f, y + 0.5f);
        bool outer = IsPointInPolygon(point, FlameOuterPolygon);
        bool inner = IsPointInPolygon(point, FlameInnerPolygon);
        isBorder = outer && inner;
        return outer;
    }

    private static bool IsSnowPixel(int x, int y, out bool isBorder)
    {
        isBorder = false;
        Vector2 point = new(x + 0.5f, y + 0.5f);
        return IsThickLine(point, new Vector2(10f, 32f), new Vector2(54f, 32f), 2.5f) ||
               IsThickLine(point, new Vector2(32f, 10f), new Vector2(32f, 54f), 2.5f) ||
               IsThickLine(point, new Vector2(16f, 16f), new Vector2(48f, 48f), 2.5f) ||
               IsThickLine(point, new Vector2(16f, 48f), new Vector2(48f, 16f), 2.5f);
    }

    private static bool IsBoltPixel(int x, int y, out bool isBorder)
    {
        isBorder = false;
        Vector2 point = new(x + 0.5f, y + 0.5f);
        return IsPointInPolygon(point, BoltPolygon);
    }

    private static bool IsSkullPixel(int x, int y, out bool isBorder)
    {
        isBorder = false;
        Vector2 point = new(x + 0.5f, y + 0.5f);
        bool head = Vector2.Distance(point, new Vector2(31.5f, 36f)) <= 20f;
        bool jaw = x >= 21 && x <= 42 && y >= 10 && y <= 28;
        bool eyeLeft = Vector2.Distance(point, new Vector2(24f, 39f)) <= 4f;
        bool eyeRight = Vector2.Distance(point, new Vector2(39f, 39f)) <= 4f;
        bool nose = Mathf.Abs(point.x - 31.5f) + Mathf.Abs(point.y - 28f) <= 5f;
        return (head || jaw) && !eyeLeft && !eyeRight && !nose;
    }

    private static IconTone GetStaggeringTone(int x, int y)
    {
        Vector2 point = new(x + 0.5f, y + 0.5f);
        const float radians = 20f * Mathf.Deg2Rad;
        float dx = point.x - 32f;
        float dy = point.y - 31f;
        float orbitX = dx * Mathf.Cos(radians) + dy * Mathf.Sin(radians);
        float orbitY = -dx * Mathf.Sin(radians) + dy * Mathf.Cos(radians);
        float outer = orbitX * orbitX / (29f * 29f) + orbitY * orbitY / (17f * 17f);
        float inner = orbitX * orbitX / (23f * 23f) + orbitY * orbitY / (11f * 11f);
        bool orbit = outer <= 1f && inner >= 1f;
        bool centerStar = IsPointInPolygon(point, StaggeringCenterStarPolygon);
        bool smallStar = IsPointInPolygon(point, StaggeringSmallStarPolygon);

        if (centerStar)
        {
            return IconTone.Accent;
        }

        if (smallStar)
        {
            return IconTone.Secondary;
        }

        return orbit ? IconTone.Primary : IconTone.Clear;
    }

    private static bool IsHourglassPixel(int x, int y, out bool isBorder)
    {
        isBorder = false;
        Vector2 point = new(x + 0.5f, y + 0.5f);
        bool top = IsThickLine(point, new Vector2(18f, 52f), new Vector2(46f, 52f), 3f);
        bool bottom = IsThickLine(point, new Vector2(18f, 12f), new Vector2(46f, 12f), 3f);
        bool left = IsThickLine(point, new Vector2(20f, 49f), new Vector2(31.5f, 32f), 3.5f) ||
                    IsThickLine(point, new Vector2(31.5f, 32f), new Vector2(20f, 15f), 3.5f);
        bool right = IsThickLine(point, new Vector2(44f, 49f), new Vector2(31.5f, 32f), 3.5f) ||
                     IsThickLine(point, new Vector2(31.5f, 32f), new Vector2(44f, 15f), 3.5f);
        bool sand = Mathf.Abs(point.x - 31.5f) <= 5f && point.y >= 27f && point.y <= 37f;
        return top || bottom || left || right || sand;
    }

    private static bool IsSpiralPixel(int x, int y, out bool isBorder)
    {
        Vector2 point = new(x + 0.5f, y + 0.5f);
        Vector2 center = new(31.5f, 31.5f);
        Vector2 delta = point - center;
        float radius = delta.magnitude;
        const float innerRadius = 5f;
        const float outerRadius = 28f;
        if (radius < innerRadius || radius > outerRadius)
        {
            isBorder = false;
            return false;
        }

        float angle = -Mathf.Atan2(delta.y, delta.x);
        float radialProgress = Mathf.InverseLerp(innerRadius, outerRadius, radius);
        const float sectorAngle = Mathf.PI * 2f / 5f;
        float bladeCenter = Mathf.Lerp(0.08f, 0.83f, radialProgress);
        float bladeOffset = Mathf.Repeat(angle - bladeCenter + sectorAngle * 0.5f, sectorAngle) - sectorAngle * 0.5f;
        float halfWidth = 0.035f + 0.38f * Mathf.Pow(Mathf.Sin(Mathf.PI * radialProgress), 0.7f);
        bool blade = Mathf.Abs(bladeOffset) <= halfWidth;
        isBorder = blade && bladeOffset < -halfWidth * 0.42f;
        return blade;
    }

    private static bool IsSnarePixel(int x, int y, out bool isBorder)
    {
        isBorder = false;
        Vector2 point = new(x + 0.5f, y + 0.5f);
        bool shackle = Vector2.Distance(point, new Vector2(31.5f, 38f)) <= 17f &&
                       Vector2.Distance(point, new Vector2(31.5f, 38f)) >= 10f &&
                       point.y >= 28f;
        bool chain = IsThickLine(point, new Vector2(20f, 24f), new Vector2(44f, 14f), 3f) ||
                     IsThickLine(point, new Vector2(26f, 17f), new Vector2(38f, 28f), 3f);
        bool foot = IsThickLine(point, new Vector2(19f, 10f), new Vector2(49f, 10f), 3f);
        return shackle || chain || foot;
    }

    private static bool IsAdaptivePixel(int x, int y, out bool isCore)
    {
        Vector2 point = new(x + 0.5f, y + 0.5f);
        Vector2 center = new(31.5f, 31.5f);
        Vector2 delta = point - center;
        float radius = delta.magnitude;
        float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
        float localAngle = Mathf.Repeat(angle + 360f, 120f);
        bool arrowBody = radius is >= 19f and <= 24.5f && localAngle is >= 8f and <= 86f;
        bool arrowHead = IsAdaptiveArrowHead(point, center, 88f) ||
                         IsAdaptiveArrowHead(point, center, 208f) ||
                         IsAdaptiveArrowHead(point, center, 328f);

        isCore = IsPointInPolygon(point, AdaptiveCorePolygon);
        return arrowBody || arrowHead || isCore;
    }

    private static bool IsAdaptiveArrowHead(Vector2 point, Vector2 center, float angleDegrees)
    {
        float radians = angleDegrees * Mathf.Deg2Rad;
        Vector2 radial = new(Mathf.Cos(radians), Mathf.Sin(radians));
        Vector2 tangent = new(-radial.y, radial.x);
        Vector2 relative = point - (center + radial * 21.75f);
        float forward = Vector2.Dot(relative, tangent);
        if (forward < -3.5f || forward > 8f)
        {
            return false;
        }

        float halfWidth = Mathf.Lerp(6.5f, 0f, (forward + 3.5f) / 11.5f);
        return Mathf.Abs(Vector2.Dot(relative, radial)) <= halfWidth;
    }

    private static bool IsEyePixel(int x, int y, out bool isPupil)
    {
        Vector2 point = new(x + 0.5f, y + 0.5f);
        Vector2 center = new(31.5f, 31.5f);
        float dx = Mathf.Abs(point.x - center.x);
        float dy = Mathf.Abs(point.y - center.y);
        bool eye = dx / 27f + dy / 18f <= 1f;
        bool pupil = Vector2.Distance(point, center) <= 8.5f;
        bool border = eye && dx / 20f + dy / 11f >= 1f;
        isPupil = pupil;
        return border || pupil;
    }

    private static IconTone GetReapingTone(int x, int y)
    {
        Vector2 point = new(x + 0.5f, y + 0.5f);
        bool hood = IsPointInPolygon(point, ReapingHoodPolygon);
        bool robe = IsPointInPolygon(point, ReapingRobePolygon);
        bool faceOpening = Vector2.Distance(point, new Vector2(32f, 41.5f)) <= 5.2f;
        bool figure = (hood || robe) && !faceOpening;

        bool handle = IsThickLine(point, new Vector2(13f, 49f), new Vector2(22f, 35f), 2.4f) ||
                      IsThickLine(point, new Vector2(22f, 35f), new Vector2(49f, 7f), 2.4f);
        bool blade = IsPointInPolygon(point, ReapingBladePolygon);

        if (blade)
        {
            return IconTone.Accent;
        }

        if (handle)
        {
            return IconTone.Secondary;
        }

        return figure ? IconTone.Primary : IconTone.Clear;
    }

    private static IconTone GetSpiritTone(int x, int y)
    {
        Vector2 point = new(x + 0.5f, y + 4.5f);
        float loopX = (point.x - 32f) / 10f;
        float loopY = (point.y - 46f) / 13f;
        float innerX = (point.x - 32f) / 4.5f;
        float innerY = (point.y - 46f) / 7f;
        bool loop = loopX * loopX + loopY * loopY <= 1f && innerX * innerX + innerY * innerY >= 1f;
        bool stem = Mathf.Abs(point.x - 32f) <= 3.5f && point.y >= 9f && point.y <= 38f;
        bool cross = Mathf.Abs(point.y - 31f) <= 3.5f && point.x >= 17f && point.x <= 47f;
        bool ankh = loop || stem || cross;
        bool jewel = Vector2.Distance(point, new Vector2(32f, 31f)) <= 3.2f;

        if (jewel)
        {
            return IconTone.Accent;
        }

        if (ankh)
        {
            return IconTone.Primary;
        }

        return IconTone.Clear;
    }

    private static bool IsBrokenShieldPixel(int x, int y, out bool isRightHalf)
    {
        Vector2 point = new(x + 0.5f, y + 0.5f);
        bool crack = IsThickLine(point, new Vector2(31f, 5f), new Vector2(36f, 14f), 3.2f) ||
                     IsThickLine(point, new Vector2(36f, 14f), new Vector2(28f, 25f), 3.2f) ||
                     IsThickLine(point, new Vector2(28f, 25f), new Vector2(36f, 35f), 3.2f) ||
                     IsThickLine(point, new Vector2(36f, 35f), new Vector2(27f, 45f), 3.2f) ||
                     IsThickLine(point, new Vector2(27f, 45f), new Vector2(34f, 55f), 3.2f);
        isRightHalf = point.x > GetBrokenShieldCrackX(point.y);
        return IsPointInPolygon(point, BrokenShieldPolygon) && !crack;
    }

    private static float GetBrokenShieldCrackX(float y)
    {
        if (y <= 14f) return Mathf.Lerp(31f, 36f, Mathf.InverseLerp(5f, 14f, y));
        if (y <= 25f) return Mathf.Lerp(36f, 28f, Mathf.InverseLerp(14f, 25f, y));
        if (y <= 35f) return Mathf.Lerp(28f, 36f, Mathf.InverseLerp(25f, 35f, y));
        if (y <= 45f) return Mathf.Lerp(36f, 27f, Mathf.InverseLerp(35f, 45f, y));
        return Mathf.Lerp(27f, 34f, Mathf.InverseLerp(45f, 55f, y));
    }

    private static IconTone GetArmorPiercingTone(int x, int y)
    {
        Vector2 point = new(x + 0.5f, y + 0.5f);
        bool shield = IsPointInPolygon(point, ArmorPiercingShieldPolygon);
        bool crack = shield && IsPointInPolygon(point, ArmorPiercingCrackPolygon);
        bool arrowShaft = IsThickLine(point, new Vector2(30f, 30f), new Vector2(56f, 56f), 4.8f);
        bool arrowHead = IsPointInPolygon(point, ArmorPiercingArrowHeadPolygon);

        if (arrowShaft || arrowHead)
        {
            return IconTone.Accent;
        }

        if (crack)
        {
            return IconTone.Secondary;
        }

        if (shield)
        {
            return IconTone.Primary;
        }

        return IconTone.Clear;
    }

    private static bool IsUndodgeablePixel(int x, int y, out bool isMiddleDiamond)
    {
        Vector2 point = new(x + 0.5f, y + 0.5f);
        bool corners =
            IsThickLine(point, new Vector2(8f, 54f), new Vector2(23f, 54f), 2.5f) ||
            IsThickLine(point, new Vector2(8f, 54f), new Vector2(8f, 39f), 2.5f) ||
            IsThickLine(point, new Vector2(41f, 54f), new Vector2(56f, 54f), 2.5f) ||
            IsThickLine(point, new Vector2(56f, 54f), new Vector2(56f, 39f), 2.5f) ||
            IsThickLine(point, new Vector2(8f, 10f), new Vector2(23f, 10f), 2.5f) ||
            IsThickLine(point, new Vector2(8f, 10f), new Vector2(8f, 25f), 2.5f) ||
            IsThickLine(point, new Vector2(41f, 10f), new Vector2(56f, 10f), 2.5f) ||
            IsThickLine(point, new Vector2(56f, 10f), new Vector2(56f, 25f), 2.5f);
        bool outer = IsPointInPolygon(point, UndodgeableOuterPolygon);
        bool inner = IsPointInPolygon(point, UndodgeableInnerPolygon);
        bool diamond = outer && !inner;
        bool center = Vector2.Distance(point, new Vector2(32f, 32f)) <= 3.5f;
        isMiddleDiamond = diamond;
        return corners || diamond || center;
    }

    private static IconTone GetWeakenedTone(int x, int y)
    {
        Vector2 point = new(x + 0.5f, y + 0.5f);
        bool attachedBlade = IsPointInPolygon(point, WeakenedAttachedBladePolygon);
        bool tipBlade = IsPointInPolygon(point, WeakenedTipBladePolygon);
        bool attachedGoldEdge = IsPointInPolygon(point, WeakenedAttachedGoldEdgePolygon);
        bool tipGoldEdge = IsPointInPolygon(point, WeakenedTipGoldEdgePolygon);
        bool grip = IsThickLine(point, new Vector2(14f, 53f), new Vector2(23f, 42f), 4.2f);
        bool guard = IsPointInPolygon(point, WeakenedGuardPolygon);
        bool pommel = Vector2.Distance(point, new Vector2(11f, 57f)) <= 5f;

        if (grip || guard)
        {
            return IconTone.Secondary;
        }

        if (pommel || attachedGoldEdge || tipGoldEdge)
        {
            return IconTone.Accent;
        }

        return attachedBlade || tipBlade ? IconTone.Primary : IconTone.Clear;
    }

    private static bool IsWiltedLeafPixel(int x, int y, out bool isBorder)
    {
        Vector2 point = new(x + 0.5f, y + 0.5f);
        bool stem = IsThickLine(point, new Vector2(27f, 8f), new Vector2(38f, 56f), 2.6f);
        bool leafLeft = IsPointInPolygon(point, WiltedLeafLeftPolygon);
        bool leafRight = IsPointInPolygon(point, WiltedLeafRightPolygon);
        bool droop = IsThickLine(point, new Vector2(35f, 20f), new Vector2(50f, 12f), 3f);
        isBorder = stem || droop;
        return stem || leafLeft || leafRight || droop;
    }

    private static IconTone GetReflectionTone(int x, int y)
    {
        Vector2 point = new(x + 0.5f, y + 0.5f);
        bool shield = IsPointInPolygon(point, ReflectionShieldPolygon);
        bool reflectedPath = IsThickLine(point, new Vector2(7f, 54f), new Vector2(34f, 34f), 3.4f) ||
                             IsThickLine(point, new Vector2(34f, 31f), new Vector2(10f, 8f), 3.6f) ||
                             IsPointInPolygon(point, ReflectionArrowHeadPolygon);
        bool impact = IsFivePointStarPixel(point, new Vector2(34f, 33f), 10f, 4.3f);

        if (impact)
        {
            return IconTone.Accent;
        }

        if (reflectedPath)
        {
            return IconTone.Secondary;
        }

        return shield ? IconTone.Primary : IconTone.Clear;
    }

    private static bool IsInterferencePixel(int x, int y, out bool isBorder)
    {
        isBorder = false;
        Vector2 point = new(x + 0.5f, y + 0.5f);
        bool wave1 = point.x >= 8f &&
                     point.x <= 56f &&
                     Mathf.Abs(point.y - (22f + 5f * Mathf.Sin((point.x - 8f) * 0.22f))) <= 2.3f;
        bool wave2 = point.x >= 8f &&
                     point.x <= 56f &&
                     Mathf.Abs(point.y - (36f + 5f * Mathf.Sin((point.x - 8f) * 0.22f + Mathf.PI))) <= 2.3f;
        return wave1 || wave2;
    }

    private static bool IsAdrenalineDrainPixel(int x, int y, out bool isBorder)
    {
        isBorder = false;
        Vector2 point = new(x + 0.5f, y + 0.5f);
        bool pulse = IsThickLine(point, new Vector2(6f, 40f), new Vector2(18f, 40f), 2.5f) ||
                     IsThickLine(point, new Vector2(18f, 40f), new Vector2(24f, 50f), 2.5f) ||
                     IsThickLine(point, new Vector2(24f, 50f), new Vector2(31f, 28f), 2.5f) ||
                     IsThickLine(point, new Vector2(31f, 28f), new Vector2(38f, 40f), 2.5f) ||
                     IsThickLine(point, new Vector2(38f, 40f), new Vector2(49f, 40f), 2.5f);
        bool shaft = IsThickLine(point, new Vector2(49f, 52f), new Vector2(49f, 20f), 3f);
        bool arrow = IsPointInPolygon(point, AdrenalineDrainArrowPolygon);
        return pulse || shaft || arrow;
    }

    private static IconTone GetCorrosiveTone(int x, int y)
    {
        Vector2 point = new(x + 0.5f, y + 0.5f);
        bool outer = IsPointInPolygon(point, CorrosiveOuterPolygon);
        bool centerHole = Vector2.Distance(point, new Vector2(31f, 32f)) <= 13f;
        bool brokenEdge = Vector2.Distance(point, new Vector2(54f, 33f)) <= 6.5f ||
                          Vector2.Distance(point, new Vector2(48f, 50f)) <= 4.5f ||
                          Vector2.Distance(point, new Vector2(49f, 14f)) <= 4.5f;
        bool body = outer && !centerHole && !brokenEdge;
        if (!body)
        {
            return IconTone.Clear;
        }

        bool oxide = Vector2.Distance(point, new Vector2(43f, 47f)) <= 2.5f ||
                     Vector2.Distance(point, new Vector2(48f, 40f)) <= 2.5f ||
                     Vector2.Distance(point, new Vector2(43f, 20f)) <= 2.5f ||
                     Vector2.Distance(point, new Vector2(47f, 16f)) <= 2.2f;
        if (oxide)
        {
            return IconTone.Secondary;
        }

        bool rust = Vector2.Distance(point, new Vector2(44f, 44f)) <= 9f ||
                    Vector2.Distance(point, new Vector2(45f, 19f)) <= 8f ||
                    point.x >= 49f;
        if (rust)
        {
            return IconTone.Accent;
        }

        return IconTone.Primary;
    }

    private static bool IsBlinkPixel(int x, int y, out bool isPortal)
    {
        Vector2 point = new(x + 0.5f, y + 0.5f);
        Vector2 center = new(32f, 32f);
        float radius = Vector2.Distance(point, center);
        bool portal = radius >= 17f && radius <= 23f && point.x >= 14f;
        bool dash = IsThickLine(point, new Vector2(8f, 32f), new Vector2(35f, 32f), 3.5f);
        bool arrow = IsPointInPolygon(point, BlinkArrowPolygon);
        isPortal = portal;
        return portal || dash || arrow;
    }

    private static bool IsThickLine(Vector2 point, Vector2 start, Vector2 end, float width)
    {
        Vector2 axis = end - start;
        float length = axis.magnitude;
        if (length <= 0f)
        {
            return false;
        }

        Vector2 direction = axis / length;
        Vector2 delta = point - start;
        float along = Vector2.Dot(delta, direction);
        if (along < 0f || along > length)
        {
            return false;
        }

        float distance = Mathf.Abs(Vector2.Dot(delta, new Vector2(-direction.y, direction.x)));
        return distance <= width;
    }

    private static bool IsUnflinchingPixel(int x, int y, out bool isStar)
    {
        Vector2 point = new(x + 0.5f, y + 0.5f);
        Vector2 center = new(31.5f, 32f);
        Vector2 ellipsePoint = new(point.x - center.x, (point.y - center.y) * 1.5f);
        float ellipseRadius = ellipsePoint.magnitude;
        float angle = Mathf.Atan2(ellipsePoint.y, ellipsePoint.x) * Mathf.Rad2Deg;
        bool upperArc = ellipseRadius is >= 20.5f and <= 27.5f && angle is >= 18f and <= 162f;
        bool lowerArc = ellipseRadius is >= 20.5f and <= 27.5f && angle is >= -162f and <= -18f;
        bool stars = IsFivePointStarPixel(point, new Vector2(18f, 21f), 10f, 4.5f) ||
                     IsFivePointStarPixel(point, new Vector2(45f, 46f), 9f, 4f);
        isStar = stars;
        return upperArc || lowerArc || stars;
    }

    private static bool IsFivePointStarPixel(Vector2 point, Vector2 center, float outerRadius, float innerRadius)
    {
        bool inside = false;
        Vector2 previous = GetFivePointStarVertex(center, outerRadius, innerRadius, 9);
        for (int index = 0; index < 10; index++)
        {
            Vector2 current = GetFivePointStarVertex(center, outerRadius, innerRadius, index);
            if ((current.y > point.y) != (previous.y > point.y) &&
                point.x < (previous.x - current.x) * (point.y - current.y) /
                (previous.y - current.y) + current.x)
            {
                inside = !inside;
            }

            previous = current;
        }

        return inside;
    }

    private static bool IsFallbackStarPixel(int x, int y, out bool isAccent)
    {
        isAccent = false;
        return IsFivePointStarPixel(new Vector2(x + 0.5f, y + 0.5f), new Vector2(32f, 32f), 27f, 12f);
    }

    private static Vector2 GetFivePointStarVertex(
        Vector2 center,
        float outerRadius,
        float innerRadius,
        int index)
    {
        float radius = index % 2 == 0 ? outerRadius : innerRadius;
        float angle = Mathf.PI * 0.5f + index * Mathf.PI / 5f;
        return center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
    }

    private static bool IsAnchorPixel(int x, int y, out bool isBorder)
    {
        isBorder = false;
        Vector2 point = new(x + 0.5f, y + 0.5f);
        Vector2 ringCenter = new(13f, 51f);
        float ringRadius = Vector2.Distance(point, ringCenter);
        bool ring = ringRadius is >= 5.5f and <= 10f;
        bool shaft = IsThickLine(point, new Vector2(18f, 45f), new Vector2(46f, 17f), 3.8f);
        bool crossbar = IsThickLine(point, new Vector2(15f, 29f), new Vector2(35f, 49f), 3.6f);
        bool curvedArms = IsThickLine(point, new Vector2(17f, 9f), new Vector2(28f, 6f), 4f) ||
                          IsThickLine(point, new Vector2(28f, 6f), new Vector2(40f, 7f), 4f) ||
                          IsThickLine(point, new Vector2(40f, 7f), new Vector2(50f, 14f), 4f) ||
                          IsThickLine(point, new Vector2(50f, 14f), new Vector2(56f, 25f), 4f) ||
                          IsThickLine(point, new Vector2(56f, 25f), new Vector2(58f, 38f), 4f) ||
                          IsThickLine(point, new Vector2(58f, 38f), new Vector2(56f, 47f), 4f);
        bool leftFluke = IsPointInPolygon(point, AnchorLeftFlukePolygon);
        bool upperFluke = IsPointInPolygon(point, AnchorUpperFlukePolygon);
        return ring || shaft || crossbar || curvedArms || leftFluke || upperFluke;
    }

    private static bool IsChameleonPixel(int x, int y, out bool isAccent)
    {
        Vector2 point = new(x + 0.5f, y + 0.5f);
        Vector2 tailCenter = new(17f, 31f);
        float tailRadius = Vector2.Distance(point, tailCenter);
        float tailAngle = Mathf.Atan2(point.y - tailCenter.y, point.x - tailCenter.x);
        float tailTarget = 4f + Mathf.Repeat(tailAngle + Mathf.PI, Mathf.PI * 2f) / (Mathf.PI * 2f) * 10f;
        bool tail = tailRadius is >= 3f and <= 16f && Mathf.Abs(tailRadius - tailTarget) <= 2.8f;
        bool body = IsThickLine(point, new Vector2(21f, 33f), new Vector2(43f, 37f), 5.5f);
        bool head = Vector2.Distance(point, new Vector2(49f, 39f)) <= 8f;
        bool frontLeg = IsThickLine(point, new Vector2(39f, 34f), new Vector2(47f, 23f), 2.5f);
        bool backLeg = IsThickLine(point, new Vector2(28f, 32f), new Vector2(22f, 21f), 2.5f);
        bool eye = Vector2.Distance(point, new Vector2(51f, 42f)) <= 2.6f;
        isAccent = eye || tail;
        return tail || body || head || frontLeg || backLeg || eye;
    }

    private static IconTone GetBlamerTone(int x, int y)
    {
        const float scale = 0.82f;
        Vector2 point = new(
            32f + (x + 0.5f - 32f) / scale,
            32f + (y + 0.5f - 32f) / scale);
        bool bell = IsPointInPolygon(point, BlamerBellPolygon) ||
                    point.x is >= 29f and <= 35f && point.y is >= 55f and <= 63f;
        bool rim = point.x is >= 11f and <= 53f && point.y is >= 10f and <= 15f;
        bool clapper = Vector2.Distance(point, new Vector2(32f, 7f)) <= 5f;
        float waveY = point.y - 36f;
        float waveX = Mathf.Abs(point.x - 32f);
        float innerWave = waveX * waveX / (26f * 26f) + waveY * waveY / (23f * 23f);
        float outerWave = waveX * waveX / (34f * 34f) + waveY * waveY / (30f * 30f);
        bool waves = waveX >= 21f && innerWave is >= 0.82f and <= 1.14f ||
                     waveX >= 28f && outerWave is >= 0.82f and <= 1.14f;

        if (waves)
        {
            return IconTone.Accent;
        }

        if (rim || clapper)
        {
            return IconTone.Secondary;
        }

        return bell ? IconTone.Primary : IconTone.Clear;
    }

    private static bool IsPointInPolygon(Vector2 point, IReadOnlyList<Vector2> polygon)
    {
        bool inside = false;
        for (int current = 0, previous = polygon.Count - 1; current < polygon.Count; previous = current++)
        {
            Vector2 a = polygon[current];
            Vector2 b = polygon[previous];
            if ((a.y > point.y) != (b.y > point.y) &&
                point.x < (b.x - a.x) * (point.y - a.y) / (b.y - a.y) + a.x)
            {
                inside = !inside;
            }
        }

        return inside;
    }

    private static bool IsSwordBladePixel(Vector2 point, out bool isBorder)
    {
        isBorder = false;
        Vector2 start = new(18f, 16f);
        Vector2 tip = new(53f, 51f);
        Vector2 axis = tip - start;
        float length = axis.magnitude;
        Vector2 direction = axis / length;
        Vector2 normal = new(-direction.y, direction.x);
        Vector2 delta = point - start;
        float along = Vector2.Dot(delta, direction);
        if (along < 0f || along > length)
        {
            return false;
        }

        float width = along > length - 8f
            ? Mathf.Lerp(5f, 0.5f, (along - (length - 8f)) / 8f)
            : 5f;
        float side = Mathf.Abs(Vector2.Dot(delta, normal));
        if (side > width)
        {
            return false;
        }

        isBorder = side > width - 1.6f || along > length - 4f;
        return true;
    }

    private static bool IsSwordGuardPixel(Vector2 point, out bool isBorder)
    {
        isBorder = false;
        Vector2 center = new(18f, 16f);
        Vector2 axis = new Vector2(1f, 1f).normalized;
        Vector2 normal = new(-axis.y, axis.x);
        Vector2 delta = point - center;
        float along = Vector2.Dot(delta, normal);
        float across = Vector2.Dot(delta, axis);
        if (Mathf.Abs(along) > 13f || Mathf.Abs(across) > 2.6f)
        {
            return false;
        }

        isBorder = Mathf.Abs(along) > 10.8f || Mathf.Abs(across) > 1.2f;
        return true;
    }

    private static bool IsSwordGripPixel(Vector2 point, out bool isBorder)
    {
        isBorder = false;
        Vector2 start = new(7f, 5f);
        Vector2 end = new(18f, 16f);
        Vector2 axis = end - start;
        float length = axis.magnitude;
        Vector2 direction = axis / length;
        Vector2 normal = new(-direction.y, direction.x);
        Vector2 delta = point - start;
        float along = Vector2.Dot(delta, direction);
        if (along < 0f || along > length)
        {
            return false;
        }

        float side = Mathf.Abs(Vector2.Dot(delta, normal));
        if (side > 3.4f)
        {
            return false;
        }

        isBorder = side > 1.8f || along < 1.8f;
        return true;
    }
}
