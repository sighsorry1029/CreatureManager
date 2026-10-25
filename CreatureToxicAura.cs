using System.Collections.Generic;
using UnityEngine;

namespace CreatureManager;

// Damage is scheduled by CreatureModifierManager on the creature owner. This component
// only maintains one local visual per loaded creature, including on non-owner clients.
internal sealed class CreatureToxicAura : MonoBehaviour
{
    internal const float TickInterval = 1f;
    private const float ExtraRadius = 3f;
    private const float VerticalPadding = 2f;
    private const float VisualCheckInterval = 0.5f;
    private static readonly Dictionary<int, CreatureToxicAura> Visuals = new();
    private static int SolidMask;
    private Character? character;
    private CapsuleCollider? body;
    private GameObject? effect;
    private int id;
    private float nextCheck;

    internal static void Track(Character character, bool hasAura)
    {
        int id = character.GetInstanceID();
        if (!hasAura || character.IsDead())
        {
            Forget(id);
            return;
        }

        if (ZNet.instance != null && ZNet.instance.IsDedicated()) return;
        if (Visuals.TryGetValue(id, out CreatureToxicAura visual) && visual != null) return;
        visual = character.gameObject.AddComponent<CreatureToxicAura>();
        visual.character = character;
        visual.body = character.GetCollider();
        visual.id = id;
        Visuals[id] = visual;
    }

    internal static void Forget(int id)
    {
        if (!Visuals.TryGetValue(id, out CreatureToxicAura visual)) return;
        Visuals.Remove(id);
        if (visual == null) return;
        visual.ClearEffect();
        Destroy(visual);
    }

    internal static void Reset()
    {
        foreach (CreatureToxicAura visual in Visuals.Values)
        {
            if (visual == null) continue;
            visual.ClearEffect();
            Destroy(visual);
        }
        Visuals.Clear();
    }

    private void LateUpdate()
    {
        if (Time.time >= nextCheck)
        {
            nextCheck = Time.time + VisualCheckInterval;
            Player? player = Player.m_localPlayer;
            if (character == null || !character.isActiveAndEnabled || body == null || !body.enabled || body.isTrigger ||
                player == null || (player.transform.position - character.transform.position).sqrMagnitude > 10000f ||
                !CreatureModifierManager.TryGetToxicAuraPower(character, out _) ||
                !TryGetRadius(body.bounds, out float radius))
            {
                ClearEffect();
                return;
            }

            if (effect == null) effect = CreaturePrefabRegistry.CreateToxicAuraVisual();
            if (effect != null)
            {
                // The root is not parented to the creature: do not multiply its size twice.
                effect.transform.localScale = new Vector3(radius, 0.5f, radius);
            }
        }

        if (effect != null && body != null)
        {
            Bounds bounds = body.bounds;
            effect.transform.position = new Vector3(bounds.center.x, bounds.min.y + 0.1f, bounds.center.z);
        }
    }

    private void OnDisable() => ClearEffect();

    private void OnDestroy()
    {
        ClearEffect();
        if (Visuals.TryGetValue(id, out CreatureToxicAura visual) && ReferenceEquals(visual, this))
            Visuals.Remove(id);
    }

    private void ClearEffect()
    {
        if (effect != null)
        {
            effect.SetActive(false);
            Destroy(effect);
            effect = null;
        }
    }

    internal static bool TryGetRadius(Bounds bounds, out float radius)
    {
        radius = Mathf.Max(bounds.extents.x, bounds.extents.z) + ExtraRadius;
        return IsFinite(bounds.center) && IsFinite(bounds.extents) &&
               bounds.extents.x > 0f && bounds.extents.y > 0f && bounds.extents.z > 0f &&
               !float.IsNaN(radius) && !float.IsInfinity(radius);
    }

    internal static bool IsWithinReach(Bounds source, Bounds target, float radius)
    {
        if (!IsFinite(target.center) || !IsFinite(target.extents)) return false;
        Vector3 delta = target.center - source.center;
        return delta.x * delta.x + delta.z * delta.z <= radius * radius &&
               target.min.y <= source.max.y + VerticalPadding && target.max.y >= source.min.y - VerticalPadding;
    }

    private static bool IsFinite(Vector3 value) =>
        !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
        !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
        !float.IsNaN(value.z) && !float.IsInfinity(value.z);

    // Called only from the owner-gated passive scheduler, at most once per second.
    internal static void ApplyPoison(Character source, float power)
    {
        CapsuleCollider? collider = source.GetCollider();
        if (!source.isActiveAndEnabled || collider == null || !collider.enabled || collider.isTrigger) return;
        Bounds bounds = collider.bounds;
        if (!TryGetRadius(bounds, out float radius)) return;
        if (SolidMask == 0)
            SolidMask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "terrain", "blocker", "vehicle");
        if (SolidMask == 0) return;

        bool interior = Character.InInterior(source.transform.position);
        foreach (Player player in Player.GetAllPlayers())
        {
            if (player == null || player.IsDead() || player.IsTeleporting() ||
                Character.InInterior(player.transform.position) != interior) continue;
            CapsuleCollider? target = player.GetCollider();
            if (target == null || !target.enabled) continue;
            Bounds targetBounds = target.bounds;
            if (!IsWithinReach(bounds, targetBounds, radius) || !BaseAI.IsEnemy(source, player)) continue;

            // Cast near the player's height, not from a giant boss's head over a wall.
            Vector3 from = bounds.center;
            from.y = Mathf.Clamp(targetBounds.center.y, bounds.min.y + 0.05f, bounds.max.y - 0.05f);
            if (Physics.Linecast(from, targetBounds.center, SolidMask, QueryTriggerInteraction.Ignore)) continue;

            HitData hit = new()
            {
                m_damage = { m_poison = player.GetMaxHealth() * power },
                m_point = targetBounds.center,
                m_dir = (targetBounds.center - from).normalized
            };
            // Keep the old max-health poison contract: no attacker-based outgoing damage
            // multipliers. Damage still applies the victim's normal resistance/poison rules.
            player.Damage(hit);
        }
    }
}
