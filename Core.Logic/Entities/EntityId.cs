using System;
using System.Collections.Generic;

namespace TaiwuGu.Entities
{
    /// <summary>
    /// Type-safe, lightweight unique identifier for any game entity (Player, NPC, Caravan, Gu, etc.)
    /// </summary>
    public readonly struct EntityId : IEquatable<EntityId>, IComparable<EntityId>
    {
        public readonly ulong Value;

        public EntityId(ulong value) => Value = value;

        public static readonly EntityId None = new EntityId(0);
        public bool IsValid => Value > 0;

        public bool Equals(EntityId other) => Value == other.Value;
        public override bool Equals(object? obj) => obj is EntityId other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public int CompareTo(EntityId other) => Value.CompareTo(other.Value);
        public override string ToString() => $"Entity({Value})";

        public static bool operator ==(EntityId left, EntityId right) => left.Value == right.Value;
        public static bool operator !=(EntityId left, EntityId right) => left.Value != right.Value;

        public static implicit operator ulong(EntityId id) => id.Value;
        public static explicit operator EntityId(ulong val) => new EntityId(val);
    }
}
