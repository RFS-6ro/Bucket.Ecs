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

    public struct EntityAddress
    {
        public ArchetypeId archetype;
        public ChunkIndex chunkIndex;
        public EntityIndexInChunk entityIndex;

        [Inline(256)]
        public static EntityAddress Invalid() => new EntityAddress(ArchetypeId.INVALID, ChunkIndex.INVALID, EntityIndexInChunk.INVALID);

        public bool IsValid { [Inline(256)] get => archetype != ArchetypeId.INVALID && chunkIndex != ChunkIndex.INVALID && entityIndex != EntityIndexInChunk.INVALID; }

        public EntityAddress(ArchetypeId archetype, ChunkIndex chunkIndex, EntityIndexInChunk entityIndex)
        {
            this.archetype = archetype;
            this.chunkIndex = chunkIndex;
            this.entityIndex = entityIndex;
        }
    }
}
