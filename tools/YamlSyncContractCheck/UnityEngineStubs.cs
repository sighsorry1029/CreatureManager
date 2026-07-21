namespace UnityEngine
{
    internal readonly struct Vector3
    {
        internal Vector3(float x, float y, float z)
        {
        }

        internal static Vector3 one => new(1f, 1f, 1f);
    }

    internal static class Mathf
    {
        internal static float Clamp(float value, float minimum, float maximum) => Math.Clamp(value, minimum, maximum);
        internal static float Clamp01(float value) => Clamp(value, 0f, 1f);
        internal static float Max(float left, float right) => Math.Max(left, right);
    }
}

internal sealed class Attack
{
    internal enum AttackType
    {
        Horizontal,
        Vertical,
        Projectile,
        Area
    }
}

internal sealed class HitData
{
    internal enum DamageModifier
    {
        Normal,
        Resistant,
        Weak,
        Immune,
        Ignore,
        VeryResistant,
        VeryWeak
    }
}

namespace CreatureManager
{
    internal static class CreatureManagerPlugin
    {
        internal static ContractLogger Log { get; } = new();
    }

    internal sealed class ContractLogger
    {
        internal void LogError(object value)
        {
        }

        internal void LogWarning(object value)
        {
        }
    }

    internal static class CreatureModifierManager
    {
        internal static bool IsKnownModifier(string modifier) => true;
    }
}
