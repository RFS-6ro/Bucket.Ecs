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

    public readonly struct EntityId : IComparable<EntityId>, IEquatable<EntityId>
    {
        public static EntityId Invalid { [Inline(256)] get => new EntityId(0UL); }

        public readonly ulong Id;

        public bool IsValid { [Inline(256)] get => Id != 0UL; }

        public EntityId(ulong id)
        {
            Id = id;
        }

        [Inline(256)]
        public override bool Equals(object obj)
        {
            if (obj == null) return false;

            if (obj is not EntityId entityId) return false;
            
            return this.Equals(entityId);
        }

        [Inline(256)]
        public bool Equals(EntityId other)
        {
            return Id.Equals(other.Id);
        }

        [Inline(256)]
        public int CompareTo(EntityId other)
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
        public static bool operator ==(EntityId a, EntityId b)
        {
            return a.Equals(b);
        }

        [Inline(256)]
        public static bool operator !=(EntityId a, EntityId b)
        {
            return a.Equals(b) == false;
        }

        [Inline(256)]
        public static implicit operator ulong(EntityId entityId)
        {
            return entityId.Id;
        }

        [Inline(256)]
        public static implicit operator EntityId(ulong id)
        {
            return new EntityId(id);
        }
    }
}