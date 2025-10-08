using System;
using System.Collections.Generic;
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

    public enum ArchetypeId : short
    {
        INVALID = -1
    }
    public enum ChunkIndex : short
    {
        INVALID = -1
    }
    public enum EntityIndexInChunk : short
    {
        INVALID = -1
    }

    public unsafe partial struct Archetype : IDisposable
    {
        private readonly EcsWorld _world;
        private readonly UnsafePoolAllocator _chunksDataAllocator;
        private ArchetypeId _id;
        private BitSet* _isAlive;
        private ArchetypeChunk[] _chunks;
        private BitSet* _componentsMask; // Filters in threaded systems should use this bit array to get archetypes that match
        private int _componentsMaskSize;
        private UnsafeArray* _indexToOffsetMap;
        private int* _indexToOffsetMapRaw;

        private UnsafeList* _notFullChunksIndexes;
        private int _componentsSummarizedSize;
        private int _entitiesCapacity;

        private bool _needRebalance;
        
        struct ChunkRef
        {
            public int Count;
            public ChunkIndex Idx;
        }
        // Cached property for reuse across frames
        private List<ChunkRef> _cachedSortedChunks;
        
        private ulong _allEntitiesCount;
        
        private bool _isInitialized;

        internal ArchetypeId Id { [Inline(256)] get => _id; }

        public int EntitiesCapacity { [Inline(256)] get => _entitiesCapacity; }

        internal BitSet* ComponentsMask { [Inline(256)] get => _componentsMask; }

        internal bool IsCreated { [Inline(256)] get => _isInitialized; }

        internal Archetype(ArchetypeId id, EcsWorld world, UnsafePoolAllocator chunksDataAllocator)
        {
            _id = id;
            _world = world;
            _allEntitiesCount = 0UL;
            _componentsSummarizedSize = 0;
            _entitiesCapacity = Config.ChunkEntitiesCount;
            _indexToOffsetMap = null;
            _indexToOffsetMapRaw = null;
            _componentsMaskSize = RUNTIME_REFERENCES.UnmanagedComponentsCount;
            _componentsMask = BitSet.Allocate(_componentsMaskSize);
            _notFullChunksIndexes = UnsafeList.Allocate<short>(Config.ExpectedChunkAmountInArchetype);
            _isAlive = BitSet.Allocate(Config.ExpectedChunkAmountInArchetype);
            _chunks = new ArchetypeChunk[Config.ExpectedChunkAmountInArchetype];
            _isInitialized = false;
            _chunksDataAllocator = chunksDataAllocator;
            _needRebalance = true;
            _cachedSortedChunks = new List<ChunkRef>(Config.ExpectedChunkAmountInArchetype);
        }

        [Inline(256)]
        internal void FillBitMask(BitSet* other)
        {
            BAssert.True(BitSet.GetSize(other) == _componentsMaskSize);

            _indexToOffsetMap = UnsafeArray.Allocate<int>(_componentsMaskSize);
            _indexToOffsetMapRaw = UnsafeArray.GetPtr<int>(_indexToOffsetMap, 0);
            _componentsSummarizedSize = 0;
            int indexOffset = 0;
            int notSetIndexOffset = -1;
            foreach ((int bit, bool set) in BitSet.GetEnumerator(other))
            {
                BitSet.Set(_componentsMask, bit, set);

                *(_indexToOffsetMapRaw + bit) = set ? indexOffset : notSetIndexOffset;
                if (set)
                {
                    indexOffset += UnmanagedComponentIndexRegistry.Sizeof((short)bit);
                    _componentsSummarizedSize += UnmanagedComponentIndexRegistry.Sizeof((short)bit);
                }
            }

            _entitiesCapacity = Config.MemoryMode == ChunkMemoryMode.FixNumberOfEntities
                ? Config.ChunkEntitiesCount
                : Config.ChunkMemorySize / (_componentsSummarizedSize + sizeof(EntityId)); // sum entity size to fit it in cache

            BAssert.IsNotEmpty(_entitiesCapacity);

            _isInitialized = true;
        }
        
        [Inline(256)]
        internal EntityAddress AddEntity(in EntityId entity)
        {
            BAssert.True(_isInitialized);
            // TODO: asserts

            short chunkIndex;
            short notFullChunksCount = (short)UnsafeList.GetCount(_notFullChunksIndexes);
            bool allChunksAreFull = notFullChunksCount == 0;
            if (allChunksAreFull)
            {
                chunkIndex = CreateEmptyChunk();
            }
            else
            {
                chunkIndex = UnsafeList.Get<short>(_notFullChunksIndexes, notFullChunksCount - 1);
            }

            EntityIndexInChunk entityIndex = _chunks[chunkIndex].Add(in entity);
            _allEntitiesCount++;

            if (_chunks[chunkIndex].IsFull)
            {
                UnsafeList.Remove(_notFullChunksIndexes, chunkIndex);
            }

            _needRebalance = true;
            
            return new EntityAddress
            (
                _id,
                (ChunkIndex)chunkIndex,
                entityIndex
            );
        }

        [Inline(256)]
        private short CreateEmptyChunk()
        {
            BAssert.True(_isInitialized);
            
            if (BitSet.GetFirstClearBit(_isAlive, out int index) == false)
            {
                index = _chunks.Length;
                short newSize = (short)(_chunks.Length << 1);
                // BLogger.Warning("ArchetypeChunk was resized! Iterations might miss the changes!");
                // BLogger.Warning($"_chunks.Length: {_chunks.Length}, _allEntitiesCount: {_allEntitiesCount}, _notFullChunksIndexes: {UnsafeList.GetCount(_notFullChunksIndexes)}, _entitiesCapacity: {_entitiesCapacity}");
                Array.Resize<ArchetypeChunk>(ref _chunks, newSize);
                _isAlive = BitSet.Resize(_isAlive, newSize);
            }

            _chunks[index] = new ArchetypeChunk(
                _world, 
                _id, 
                (ChunkIndex)index, 
                _chunksDataAllocator,
                _entitiesCapacity, 
                _componentsSummarizedSize, 
                _indexToOffsetMap
            );
            BitSet.Set(_isAlive, index);
            UnsafeList.Add(_notFullChunksIndexes, index);

            return (short)index;
        }
        
        [Inline(256)]
        internal void MarkEntityToRemove(in EntityAddress address)
        {
            BAssert.True(_isInitialized);
            BAssert.True(BitSet.IsSet(_isAlive, (short)address.chunkIndex));
            // TODO: asserts
            
            short chunkIndex = (short)address.chunkIndex;
            ref ArchetypeChunk chunk = ref _chunks[chunkIndex];

            _chunks[chunkIndex].MarkToRemove(address.entityIndex);
        }
        
        [Inline(256)]
        internal void RemoveMarkedEntities()
        {
            BAssert.True(_isInitialized);
            // TODO: asserts

            foreach (ref ArchetypeChunk chunk in this)
            {
                _allEntitiesCount -= (ulong)chunk.RemoveMarkedEntities();

                if (_allEntitiesCount != 0)
                {
                    _needRebalance = true;
                }

                if (chunk.IsEmpty)
                {
                    UnsafeList.Remove(_notFullChunksIndexes, (short)chunk.Index);

                    ReleaseChunk((short)chunk.Index);
                }
            }
            
            if (_allEntitiesCount == 0UL)
            {
                _world.RecycleArchetype(Id);
            }
        }
        
        [Inline(256)]
        internal void RemoveEntity(in EntityAddress address)
        {
            BAssert.True(_isInitialized);
            BAssert.True(BitSet.IsSet(_isAlive, (short)address.chunkIndex));
            // TODO: asserts
            
            short chunkIndex = (short)address.chunkIndex;
            ref ArchetypeChunk chunk = ref _chunks[chunkIndex];

            if (chunk.IsFull)
            {
                UnsafeList.Add(_notFullChunksIndexes, chunkIndex);
            }
            
            _chunks[chunkIndex].Remove(address.entityIndex);
            _allEntitiesCount--;

            if (chunk.IsEmpty)
            {
                UnsafeList.Remove(_notFullChunksIndexes, chunkIndex);

                ReleaseChunk(chunkIndex);
            }
            
            if (_allEntitiesCount == 0UL)
            {
                _world.RecycleArchetype(Id);
            }
            else
            {
                _needRebalance = true;
            }
        }
        
        [Inline(256)]
        internal ref readonly EntityId GetEntity(in EntityAddress address)
        {
            BAssert.True(_isInitialized);
            // TODO: asserts

            return ref _chunks[(short)address.chunkIndex].Get(address.entityIndex);
        }

        [Inline(256)]
        internal void Rebalance()
        {
            // rebalance happens after the entities are removed from archetype, so there's a chance archetype will be recycled already
            if (_isInitialized == false) return;

            if (_needRebalance == false) return;
            
            _cachedSortedChunks.Clear();
            foreach (ref var chunk in this)
            {
                if (chunk.IsFull) continue;
                _cachedSortedChunks.Add(new() { Count = chunk.Count, Idx = chunk.Index });
            }
            _cachedSortedChunks.Sort((a, b) => a.Count.CompareTo(b.Count));
            _needRebalance = false;
            
            int left = 0;
            int right = _cachedSortedChunks.Count - 1;
            
            // 1) Greedy combine from both ends
            while (left <= right)
            {
                // Take largest available
                var targetChunkIndex = (short)_cachedSortedChunks[right--].Idx;
                ref var targetChunk = ref _chunks[targetChunkIndex];
                
                int remaining = _entitiesCapacity - targetChunk.Count;
                
                // 2) Fill target with smallest chunks
                while (left <= right && _cachedSortedChunks[left].Count <= remaining)
                {
                    var fillerChunkIndex = (short)_cachedSortedChunks[left++].Idx;
                    
                    ref var fillerChunk = ref _chunks[fillerChunkIndex];
                    remaining -= fillerChunk.Count;
                    
                    targetChunk.CombineWith(ref fillerChunk);

                    UnsafeList.Remove(_notFullChunksIndexes, fillerChunkIndex);
                    ReleaseChunk(fillerChunkIndex);

                    if (targetChunk.IsFull)
                    {
                        UnsafeList.Remove(_notFullChunksIndexes, targetChunkIndex);
                    }
                }
            }
        }

        [Inline(256)]
        internal void ReleaseChunk(int chunkIndex)
        {
            BAssert.True(_isInitialized);
            _chunksDataAllocator.Release(Id, (ChunkIndex)chunkIndex);
            _chunks[chunkIndex].Dispose();
            BitSet.Clear(_isAlive, chunkIndex);
        }

        [Inline(256)]
        public void Dispose()
        {
            _isInitialized = false;

            if (_indexToOffsetMap != null)
            {
                UnsafeArray.Free(_indexToOffsetMap);
                _indexToOffsetMap = null;
                _indexToOffsetMapRaw = null;
            }

            if (_notFullChunksIndexes != null)
            {
                UnsafeList.Free(_notFullChunksIndexes);
                _notFullChunksIndexes = null;
            }
            
            if (_chunks != null)
            {
                _chunksDataAllocator.ReleaseAll(Id);
                for (int i = 0; i < _chunks.Length; i++)
                {
                    _chunks[i].Dispose();
                }
                _chunks = null;
            }

            if (_isAlive != null)
            {
                BitSet.Free(_isAlive);
                _isAlive = null;
            }
            
            if (_componentsMask != null)
            {
                BitSet.Free(_componentsMask);
                _componentsMask = null;
            }

            _id = ArchetypeId.INVALID;
        }

        [Inline(256)]
        internal ref ArchetypeChunk GetChunk(ChunkIndex chunkIndex)
        {
            return ref _chunks[(int)chunkIndex];
        }

        [Inline(256)]
        internal (ArchetypeChunk[] chunks, BitSetPtrWrapper isAlive) GetChunks()
        {
            return (_chunks, new BitSetPtrWrapper(_isAlive));
        }

        [Inline(256)]
        public Enumerator GetEnumerator()
        {
            BAssert.True(_isInitialized);
            return new Enumerator(_chunks, _isAlive);
        }

        public struct Enumerator
        {
            private readonly ArchetypeChunk[] _chunks;
            private readonly BitSet* _isAlive;
            private readonly int _count;
            private int _idx;

            internal Enumerator(ArchetypeChunk[] chunks, BitSet* isAlive)
            {
                _chunks = chunks;
                _count = _chunks.Length;
                _isAlive = isAlive;

                _idx = -1;
            }

            public ref ArchetypeChunk Current
            {
                [Inline(256)]
                get
                {
                    return ref _chunks[_idx];
                }
            }

            [Inline(256)]
            public bool MoveNext()
            {
                do
                {
                    if (_isAlive == null) break;
                    if (++_idx >= _count) break;
                    
                    bool isAlive = BitSet.IsSet(_isAlive, _idx);

                    if (isAlive)
                    {
                        if (_chunks[_idx].IsCreated) return true;
                    }

                } while (_idx < _count);

                return false;
            }
        }
    }
}
