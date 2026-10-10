using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace CreatureManager;

// Owner-local decisions only. The game's existing UpdateTarget cadence, attack gate and
// subsequent target validation/abandonment still run; no target IDs or timers are persisted.
internal static class CreatureTargeting
{
    private static readonly Dictionary<Character, TargetState> States = new();
    private static readonly Func<BaseAI, Vector3, bool> HavePath =
        AccessTools.MethodDelegate<Func<BaseAI, Vector3, bool>>(
            AccessTools.DeclaredMethod(typeof(BaseAI), "HavePath", new[] { typeof(Vector3) }));

    private sealed class TargetState
    {
        internal ZDO Zdo = null!;
        internal long Owner;
        internal ushort OwnerRevision;
        internal float NextAttempt;
        internal Player? HeldPlayer;
        internal float HoldUntil;
    }

    // Character and view are loaded from the original protected BaseAI fields inside
    // MonsterAI.UpdateTarget by the transpiler, rather than accessed through publicized C#.
    internal static Character? SelectTarget(Character? vanillaTarget, MonsterAI ai, Character creature, ZNetView view)
    {
        if (CreatureManagerPlugin.PlayerTargetSwitching.Value != CreatureManagerPlugin.Toggle.On ||
            CreatureManagerPlugin.PlayerTargetSwitchChance.Value <= 0)
        {
            return vanillaTarget;
        }

        Character current = ai.GetTargetCreature();
        if (view == null || !view.IsValid() || !view.IsOwner() || creature.IsDead() || creature.IsTamed() ||
            !ai.IsAlerted() || current == null || current.IsDead() || !BaseAI.IsEnemy(creature, current))
        {
            States.Remove(creature);
            return vanillaTarget;
        }

        ZDO zdo = view.GetZDO();
        if (CreatureLevelManager.IsFrozenKingPhaseTwo(zdo))
        {
            States.Remove(creature);
            return vanillaTarget;
        }

        float now = Time.time;
        long owner = zdo.GetOwner();
        if (!States.TryGetValue(creature, out TargetState state) || !ReferenceEquals(state.Zdo, zdo) ||
            state.Owner != owner || state.OwnerRevision != zdo.OwnerRevision)
        {
            States[creature] = new TargetState
            {
                Zdo = zdo, Owner = owner, OwnerRevision = zdo.OwnerRevision,
                NextAttempt = now + CreatureManagerPlugin.PlayerTargetSwitchInterval.Value
            };
            return vanillaTarget;
        }

        if (state.HeldPlayer != null)
        {
            // Never restore a target that vanilla or another mod already cleared/replaced.
            if (state.HeldPlayer == current && now < state.HoldUntil &&
                CanSelect(ai, creature, state.HeldPlayer) && HasPath(ai, state.HeldPlayer))
            {
                return state.HeldPlayer;
            }

            state.HeldPlayer = null;
        }

        if (now < state.NextAttempt)
        {
            return vanillaTarget;
        }

        // A failed roll, empty candidate set or unreachable selection consumes this attempt.
        state.NextAttempt = now + CreatureManagerPlugin.PlayerTargetSwitchInterval.Value;
        int chance = CreatureManagerPlugin.PlayerTargetSwitchChance.Value;
        if (chance < 100 && UnityEngine.Random.value * 100f >= chance)
        {
            return vanillaTarget;
        }

        Player? selected = null;
        int count = 0;
        foreach (Player player in Player.GetAllPlayers())
        {
            if (player == current || !CanSelect(ai, creature, player)) continue;
            // Reservoir selection avoids sorting, temporary lists and a nearest-player bias.
            if (UnityEngine.Random.Range(0, ++count) == 0) selected = player;
        }

        // Only the selected player's path is queried, not a path for every candidate.
        if (selected == null || !HasPath(ai, selected)) return vanillaTarget;
        state.HeldPlayer = selected;
        state.HoldUntil = now + CreatureManagerPlugin.PlayerTargetHoldDuration.Value;
        return selected;
    }

    private static bool CanSelect(MonsterAI ai, Character creature, Player player)
    {
        return player != null && !player.IsDead() && !player.m_aiSkipTarget &&
               !player.InGhostMode() && !player.InDebugFlyMode() &&
               !(player == Player.m_localPlayer && CinematicsManager.IsPlaying()) &&
               BaseAI.IsEnemy(creature, player) && !(ai.m_skipLavaTargets && player.AboveOrInLava()) &&
               ai.CanSenseTarget(player);
    }

    private static bool HasPath(MonsterAI ai, Player player)
    {
        // Original HavePath preserves flying behavior and the creature's navigation agent.
        return HavePath(ai, player.transform.position);
    }

    internal static void ForgetCharacter(Character character) => States.Remove(character);

    internal static void ResetRuntimeState() => States.Clear();
}
