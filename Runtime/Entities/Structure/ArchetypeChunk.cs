using System;
using UnsafeCollections;
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

    public unsafe struct ArchetypeChunk : IDisposable
    {
        private DynamicComponentsStorage _managedComponentsStorage;
        private UnmanagedComponentsStorage _unmanagedComponentsStorage;
#if B_ENABLE_ENTITY_PIN
        private ManagedDynamicComponentStorage<Metadata> _metadata;
#endif

        private BitSet* _markedToRemove;

        private short _count;
        private short _capacity;
        private UnsafeArray* _entities;
        private Entity* _entitiesRaw;
        private UnsafeArray* _migrationTable;
        private ChunkIndex _chunkIndex;
        private ArchetypeId _id;

        public bool IsCreated { [Inline(256)] get => _entities != null; }
        public bool IsFull { [Inline(256)] get => _count == _capacity; }
        public bool IsEmpty { [Inline(256)] get => _count == 0; }

        public int Count { [Inline(256)] get => _count; }

        public ChunkIndex Index { [Inline(256)] get => _chunkIndex; }

        internal DynamicComponentsStorage ManagedComponents { [Inline(256)] get => _managedComponentsStorage; }

        public ArchetypeChunk(EcsWorld world, ArchetypeId id, ChunkIndex chunkIndex, UnsafePoolAllocator chunksDataAllocator, int entitiesCount, int componentsSummarizedSize, UnsafeArray* indexToOffsetMap)
        {
            BAssert.IsNotEmpty(entitiesCount);
            
            _id = id;
            _chunkIndex = chunkIndex;
            _capacity = (short)entitiesCount;
            _count = 0;
            _entities = chunksDataAllocator.GetEntityArray(id, chunkIndex, _capacity).GetInternal();
            _entitiesRaw = UnsafeArray.GetPtr<Entity>(_entities, 0);
            _migrationTable = UnsafeArray.Allocate<EntityMigrationData>(_capacity);
            _markedToRemove = BitSet.Allocate(_capacity);
            // ProcessAddNewNodeComponentsStorageCallback(chunkIndex);

            _managedComponentsStorage = new DynamicComponentsStorage(world, _capacity);
            _unmanagedComponentsStorage = new UnmanagedComponentsStorage(id, chunkIndex, chunksDataAllocator, _capacity, componentsSummarizedSize, indexToOffsetMap);
            
#if B_ENABLE_ENTITY_PIN
            _metadata = new ManagedDynamicComponentStorage<Metadata>(_capacity);
#endif
        }
        
        [Inline(256)]
        public EntityIndexInChunk Add(in Entity entity)
        {
            // BAssert.False(IsFull);
            EntityIndexInChunk entityIndex = (EntityIndexInChunk)_count;
            *(Entity*)(_entitiesRaw + _count) = entity;
            ++_count;

            // ProcessAddEntityStorageCallback(chunkIndex, entityIndex);

#if B_ENABLE_ENTITY_PIN
            if (_metadata.Has((short)entityIndex))
            {
                _metadata.GetRef((short)entityIndex).EntityPin.Address = new EntityAddress()
                {
                    archetype = _id,
                    chunkIndex = _chunkIndex,
                    entityIndex = entityIndex
                };
            }
#endif
            
            return entityIndex;
        }

        [Inline(256)]
        public ref Entity Get(EntityIndexInChunk index)
        {
            BAssert.IndexInRange((short)index, _count);
            return ref *((Entity*)(_entitiesRaw + (short)index));
        }

        [Inline(256)]
        public void MarkToRemove(EntityIndexInChunk entityIndex)
        {
            BAssert.IndexInRange((short)entityIndex, _count);
            BitSet.Set(_markedToRemove, (short)entityIndex);
        }

        [Inline(256)]
        public int RemoveMarkedEntities()
        {
            int removed = 0;
            foreach ((int bit, bool ismarked) in BitSet.GetReverseEnumerator(_markedToRemove))
            {
                if (ismarked)
                {
                    ++removed;
                    Remove((EntityIndexInChunk) bit);
                    // throw new System.NotImplementedException("right now iteration is from start to end. That will lead to incorrect addresses on removal. I need to invert iteration!");
                }
            }
            return removed;
        }

        [Inline(256)]
        public void Remove(EntityIndexInChunk entityIndex)
        {
            short index = (short)entityIndex;
            BAssert.IndexInRange(index, _count);

            BitSet.Clear(_markedToRemove, index);

            ManagedComponents.DelAll(index);

            --_count;
            *(_entitiesRaw + index) = *(_entitiesRaw + _count);
            
#if B_ENABLE_ENTITY_PIN
            ref var oldMetadata = ref _metadata.Swap(replacedEntityIndex, index); // TODO: a place where we'll lose metadata. Handle!
            if (_metadata.Has(index))
            {
                _metadata.GetRef(index).EntityPin.Address = new EntityAddress()
                {
                    archetype = _id,
                    chunkIndex = _chunkIndex,
                    entityIndex = entityIndex
                };
            }
#endif
            
            // ProcessRemoveEntityStorageCallback(chunkIndex, entityIndex);
        }

        [Inline(256)]
        public void CombineWith(ref ArchetypeChunk other)
        {
            _managedComponentsStorage.CombineWith(_count, other._managedComponentsStorage, other._count);
            _unmanagedComponentsStorage.CombineWith(_count, other._unmanagedComponentsStorage, other._count);
            Memory.ArrayCopy<Entity>(other._entitiesRaw, 0, _entitiesRaw, _count, other._count);
            _count += other._count;
        }

        [Inline(256)]
        public UnmanagedComponentsStorage GetUnmanagedComponentsStorage()
        {
            return _unmanagedComponentsStorage;
        }

        [Inline(256)]
        public UnmanagedChunkData GetUnmanagedChunkData(byte* context, short contextTypeIndex)
        {
            return new UnmanagedChunkData
            (
                _entities,
                Count,
                _unmanagedComponentsStorage,
                archetypeId: _id,
                chunkIndex: _chunkIndex,
                _migrationTable,
                context,
                contextTypeIndex
            );
        }

        [Inline(256)]
        public UnsafeArray* GetEntities()
        {
            return _entities;
        }

        [Inline(256)]
        public UnsafeArray* GetMigrationTable()
        {
            return _migrationTable;
        }

        [Inline(256)]
        public void Dispose()
        {
            if (_entities != null)
            {
                _entities = null;
                _entitiesRaw = null;
            }

            if (_migrationTable != null)
            {
                UnsafeArray.Free(_migrationTable);
                _migrationTable = null;
            }
            
            if (_markedToRemove != null)
            {
                BitSet.Free(_markedToRemove);
                _markedToRemove = null;
            }

            _unmanagedComponentsStorage.Dispose();

            _managedComponentsStorage?.Dispose();
#if B_ENABLE_ENTITY_PIN
            _metadata.Dispose();
#endif
            _chunkIndex = ChunkIndex.INVALID;
            _id = ArchetypeId.INVALID;
        }
    }
}
