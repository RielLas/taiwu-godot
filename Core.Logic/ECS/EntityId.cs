using System;

namespace TaiwuGu.Entities
{
    /// <summary>
    /// Type-safe immutable handle to any simulation entity inside WorldStore.
    /// </summary>
    [Serializable]
    public readonly record struct EntityId(ulong Value)
    {
        public static readonly EntityId None = new(0);
        public bool IsValid => Value != 0;

        public static implicit operator ulong(EntityId id) => id.Value;
        public override string ToString() => $"Entity({Value})";
    }
}
