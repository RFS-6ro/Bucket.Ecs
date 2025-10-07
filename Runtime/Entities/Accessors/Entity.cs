using System;

namespace Bucket.Ecs.v3
{
#if UNITY
    using AllowUnsafePtr = Unity.Collections.LowLevel.Unsafe.NativeDisableUnsafePtrRestrictionAttribute;
    using WriteAccess = Unity.Collections.LowLevel.Unsafe.WriteAccessRequiredAttribute;
#else
    using AllowUnsafePtr = UnityAttribute;
    using WriteAccess = UnityAttribute;
#endif
    using Inline = System.Runtime.CompilerServices.MethodImplAttribute;

    public readonly struct Entity : IComparable<Entity>, IEquatable<Entity>
    {
        public static Entity Invalid { [Inline(256)] get => new Entity(0UL); }

        public readonly ulong Id;

        public bool IsValid { [Inline(256)] get => Id != 0UL; }

        public Entity(ulong id)
        {
            Id = id;
        }

        [Inline(256)]
        public override bool Equals(object obj)
        {
            if (obj == null) return false;

            if (obj is not Entity entityId) return false;
            
            return this.Equals(entityId);
        }

        [Inline(256)]
        public bool Equals(Entity other)
        {
            return Id.Equals(other.Id);
        }

        [Inline(256)]
        public int CompareTo(Entity other)
        {
            return Id.CompareTo(other.Id);
        }

        [Inline(256)]
        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }

        [Inline(256)]
        public override string ToString()
        {
            return Id.ToString();
        }

        // operators
        [Inline(256)]
        public static bool operator ==(Entity a, Entity b)
        {
            return a.Equals(b);
        }

        [Inline(256)]
        public static bool operator !=(Entity a, Entity b)
        {
            return a.Equals(b) == false;
        }

        [Inline(256)]
        public static implicit operator ulong(Entity entityId)
        {
            return entityId.Id;
        }

        [Inline(256)]
        public static implicit operator Entity(ulong id)
        {
            return new Entity(id);
        }
    }
}