using UnsafeCollections.Collections.Native;
using UnsafeCollections.Collections.Unsafe;

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

    public unsafe struct UnmanagedChunkData
    {
        public enum UnsafeAccessMode { None, AllowRead, AllowWrite }

        [AllowUnsafePtr] private readonly EntityId* _entities;
        internal UnmanagedComponentsStorage _unmanagedComponentsStorage;
        private readonly ArchetypeId _archetypeId;
        private readonly ChunkIndex _chunkIndex;
        [AllowUnsafePtr] private readonly UnsafeArray* _migrationTable;
        [AllowUnsafePtr] private readonly byte* _context;
        private readonly short _contextTypeIndex;

        private readonly short _count;
        private readonly short _capacity;

        private UnsafeAccessMode _unsafeAccessMode;

        [AllowUnsafePtr] internal BitSet* _dependenciesBits;
        [AllowUnsafePtr] internal BitSet* _filterIncludeBits;

        public short Count { [Inline(256)] get => _count; }
        public short Capacity { [Inline(256)] get => _capacity; }

        internal ArchetypeId ArchetypeId { [Inline(256)] get => _archetypeId; }
        internal ChunkIndex ChunkIndex { [Inline(256)] get => _chunkIndex; }

        internal UnmanagedChunkData(UnsafeArray* entities,
            int entitiesCount,
            UnmanagedComponentsStorage unmanagedComponentsStorage,
            ArchetypeId archetypeId,
            ChunkIndex chunkIndex,
            UnsafeArray* migrationTable,
            byte* context,
            short contextTypeIndex)
        {
            this = default;
            _entities = UnsafeArray.GetPtr<EntityId>(entities, 0);
            _unmanagedComponentsStorage = unmanagedComponentsStorage;
            _archetypeId = archetypeId;
            _chunkIndex = chunkIndex;
            _count = (short)entitiesCount;
            _capacity = (short)UnsafeArray.GetLength(entities);
            _migrationTable = migrationTable;
            _context = context;
            _contextTypeIndex = contextTypeIndex;
        }

        [Inline(256)]
        public void SetUnsafeAccessMode(UnsafeAccessMode newMode)
        {
            _unsafeAccessMode = newMode;
        }

        [Inline(256)]
        public EntityId GetEntityId(short index)
        {
            BAssert.IndexInRange(index, _count);
            return *(_entities + index);
        }

        [Inline(256)]
        public TContext GetSystemContext<TContext>()
            where TContext : unmanaged, IMultiThreadSystemContext
        {
            if (_contextTypeIndex == -1 || _context == null) BExceptionThrower.InvalidMemoryAccess("System can't access context.");

            short contextTypeIndex = EcsMultiThreadSystemContextDescriptor<TContext>.TypeIndex;
            if (_contextTypeIndex != contextTypeIndex) BExceptionThrower.InvalidMemoryAccess("System is trying to access context of another system.");

            return *(TContext*)_context;
        }

        [Inline(256)]
        public TComponent Read<TComponent>(short index)
            where TComponent : unmanaged, IEcsUnmanagedComponent
        {
            short componentIndex = EcsUnmanagedComponentDescriptor<TComponent>.TypeIndex;

            BAssert.IndexInRange(index, _count);
            if (_unsafeAccessMode != UnsafeAccessMode.None)
            {
                BAssert.CanAccess(BitSet.IsSet(_dependenciesBits, componentIndex * Config.FilterBitsPerDependency), "System should declare Read interest over component to perform READ operation");
            }

            return _unmanagedComponentsStorage.Read<TComponent>(index, componentIndex);
        }

        [WriteAccess]
        [Inline(256)]
        public ref TComponent Ref<TComponent>(short index)
            where TComponent : unmanaged, IEcsUnmanagedComponent
        {
            short componentIndex = EcsUnmanagedComponentDescriptor<TComponent>.TypeIndex;

            BAssert.IndexInRange(index, _count);
            if (_unsafeAccessMode == UnsafeAccessMode.AllowWrite)
            {
                BAssert.CanAccess(BitSet.IsSet(_dependenciesBits, componentIndex * Config.FilterBitsPerDependency + 1), "System should declare ReadWrite interest over component to perform REF operation");
            }

            return ref _unmanagedComponentsStorage.ReadRef<TComponent>(index, componentIndex);
        }

        [WriteAccess]
        [Inline(256)]
        public void Write<TComponent>(short index, in TComponent component)
            where TComponent : unmanaged, IEcsUnmanagedComponent
        {
            short componentIndex = EcsUnmanagedComponentDescriptor<TComponent>.TypeIndex;

            BAssert.IndexInRange(index, _count);
            if (_unsafeAccessMode == UnsafeAccessMode.AllowWrite)
            {
                BAssert.CanAccess(BitSet.IsSet(_dependenciesBits, componentIndex * Config.FilterBitsPerDependency + 1), "System should declare ReadWrite interest over component to perform WRITE operation");
            }

            _unmanagedComponentsStorage.Write<TComponent>(index, componentIndex, in component);
        }

        [WriteAccess]
        [Inline(256)]
        public void Add<TComponent>(short index)
            where TComponent : unmanaged, IEcsUnmanagedComponent
        {
            short componentIndex = EcsUnmanagedComponentDescriptor<TComponent>.TypeIndex;

            BAssert.IndexInRange(index, _count);
            if (_unmanagedComponentsStorage.Has<TComponent>(index, componentIndex))
            {
                BExceptionThrower.ComponentAlreadyAttached("Entity already has component");
            }

            ref EntityMigrationData migrationData = ref GetOrCreateMigrationData(index);
            migrationData.Add<TComponent>();
        }

        [WriteAccess]
        [Inline(256)]
        public void Add<TComponent>(short index, TComponent component)
            where TComponent : unmanaged, IEcsUnmanagedComponent
        {
            short componentIndex = EcsUnmanagedComponentDescriptor<TComponent>.TypeIndex;

            BAssert.IndexInRange(index, _count);
            if (_unmanagedComponentsStorage.Has<TComponent>(index, componentIndex))
            {
                BExceptionThrower.ComponentAlreadyAttached("Entity already has component");
            }

            ref EntityMigrationData migrationData = ref GetOrCreateMigrationData(index);
            migrationData.Add<TComponent>(component);
        }

        [WriteAccess]
        [Inline(256)]
        public void Del<TComponent>(short index)
            where TComponent : unmanaged, IEcsUnmanagedComponent
        {
            short componentIndex = EcsUnmanagedComponentDescriptor<TComponent>.TypeIndex;

            BAssert.IndexInRange(index, _count);
            BAssert.CanAccess(BitSet.IsSet(_dependenciesBits, componentIndex * Config.FilterBitsPerDependency + 1), "System should declare ReadWrite interest over component to perform DEL operation");

            ref EntityMigrationData migrationData = ref GetOrCreateMigrationData(index);
            migrationData.Del<TComponent>();
        }

        [WriteAccess]
        [Inline(256)]
        private ref EntityMigrationData GetOrCreateMigrationData(short index)
        {
            if (UnsafeArray.Get<EntityMigrationData>(_migrationTable, index).IsCreated == false)
            {
                UnsafeArray.GetRef<EntityMigrationData>(_migrationTable, index).Create();
            }

            return ref UnsafeArray.GetRef<EntityMigrationData>(_migrationTable, index);
        }
    }
}
