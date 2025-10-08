using System;
using System.Net.Http.Headers;
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

    public unsafe struct EcsFilter : IDisposable
    {
        private EcsWorld _world;
        private BitSet* _filterIncludeBits;
        private BitSet* _filterExcludeBits;
        private BitSet* _filterIncludeUnmanagedBits;
        private BitSet* _filterExcludeUnmanagedBits;
        private UnsafeList* _matchingArchetypes;

        internal UnsafeList* MatchingArchetypes => _matchingArchetypes;

        private bool IsValid { [Inline(256)] get => _filterIncludeBits != null && _filterExcludeBits != null && _filterIncludeUnmanagedBits != null && _filterExcludeUnmanagedBits != null; }

        public EcsFilter(EcsWorld world)
        {
            _world = world;
            _filterIncludeBits = BitSet.Allocate(RUNTIME_REFERENCES.ComponentsCount);
            _filterExcludeBits = BitSet.Allocate(RUNTIME_REFERENCES.ComponentsCount);
            _filterIncludeUnmanagedBits = BitSet.Allocate(RUNTIME_REFERENCES.UnmanagedComponentsCount);
            _filterExcludeUnmanagedBits = BitSet.Allocate(RUNTIME_REFERENCES.UnmanagedComponentsCount);
            _matchingArchetypes = UnsafeList.Allocate<int>(Config.ExpectedAmountOfArchetypesInFilter);
            // _matchingArchetypes = new DynamicNativeBitSet(Config.ExpectedAmountOfArchetypesInFilter, autoResize: true);
        }

        [Inline(256)]
        internal Mask GetMask()
        {
            return new Mask(in this, _filterIncludeBits, _filterExcludeBits, _filterIncludeUnmanagedBits, _filterExcludeUnmanagedBits);
        }

        [Inline(256)]
        internal void TryAddArchetype(ArchetypeId id, BitSet* componentsMask)
        {
            foreach ((int bit, bool set) in BitSet.GetEnumerator(componentsMask))
            {
                if (BitSet.IsSet(_filterExcludeUnmanagedBits, bit) && set) return;
                if (BitSet.IsSet(_filterIncludeUnmanagedBits, bit) && !set) return;
            }
            UnsafeList.Add(_matchingArchetypes, (int)id);
        }
        
        [Inline(256)]
        internal void RemoveArchetype(ArchetypeId id)
        {
            UnsafeList.RemoveUnordered(_matchingArchetypes, (int)id);
            // _matchingArchetypes.SetValue(false, (int)id);
        }

        [Inline(256)]
        private void UpdateArchetypes()
        {
            UnsafeList.Clear(_matchingArchetypes);
            // _matchingArchetypes.Reset();
            _world.RefreshArchetypesForFilter(ref this);
        }

        [Inline(256)]
        public void Dispose()
        {
            if (_filterIncludeBits != null)
            {
                BitSet.Free(_filterIncludeBits);
                _filterIncludeBits = null;
            }
            if (_filterExcludeBits != null)
            {
                BitSet.Free(_filterExcludeBits);
                _filterExcludeBits = null;
            }
            if (_filterIncludeUnmanagedBits != null)
            {
                BitSet.Free(_filterIncludeUnmanagedBits);
                _filterIncludeUnmanagedBits = null;
            }
            if (_filterExcludeUnmanagedBits != null)
            {
                BitSet.Free(_filterExcludeUnmanagedBits);
                _filterExcludeUnmanagedBits = null;
            }
            if (_matchingArchetypes != null)
            {
                UnsafeList.Free(_matchingArchetypes);
                _matchingArchetypes = null;
            }
        }

        [Inline(256)]
        public ulong Count()
        {
            ulong counter = 0;
            foreach (var _ in this)
            {
                ++counter;
            }
            return counter;
        }

        [Inline(256)]
        public EntityAddress First()
        {
            foreach (var entity in this)
            {
                return entity;
            }
            BExceptionThrower.FilterIsEmpty();
            return default;
        }

        [Inline(256)]
        public EntityAddress Single()
        {
            EntityAddress singleEntity = default;
            foreach (var entity in this)
            {
                if (singleEntity.IsValid)
                {
                    BExceptionThrower.EntityIsNotSingle();
                }
                singleEntity = entity;
            }

            if (singleEntity.IsValid)
            {
                return singleEntity;
            }

            BExceptionThrower.FilterIsEmpty();
            return default;
        }
        
        [Inline(256)]
        public Enumerator GetEnumerator()
        {
            return new Enumerator(_world, _matchingArchetypes, _filterIncludeBits, _filterExcludeBits, null);
        }
        
        [Inline(256)]
        public Enumerator GetQueryEnumerator(EcsQuery query) // TODO: probably make the query mandatory, not optional. it can be cached though!
        {
            return new Enumerator(_world, _matchingArchetypes, _filterIncludeBits, _filterExcludeBits, query);
        }

        [Inline(256)]
        public ChunksEnumerator ForEachChunk() => new ChunksEnumerator(_world, _matchingArchetypes);

        public unsafe ref struct Enumerator
        {
            private readonly EcsWorld _world;
            private readonly UnsafeList* _matchingArchetypes;

            private BitSet* _filterIncludeBits;
            private BitSet* _filterExcludeBits;

            private readonly EcsQuery _query;

            private int _archetypeIndex;
            private readonly int _archetypesCount;
            private ArchetypeChunk[] _chunks;
            private BitSet* _isChunkAlive;
            private int _chunkIndex;
            private UnmanagedChunkData _chunkData;
            private DynamicComponentsStorage _managedComponents;
            private EntityId* _chunkEntitiesPtr;
            private BitSet* _matchedEntities;
            private bool _allMatch;
            private short _entitiesCount;
            private short _entityIndex;

            private EntityAddress _current;

            public Enumerator(EcsWorld world, UnsafeList* matchingArchetypes, BitSet* filterIncludeBits, BitSet* filterExcludeBits, EcsQuery query) 
            {
                this = default;
                _world = world;
                _query = query;
                _matchingArchetypes = matchingArchetypes;
                _filterIncludeBits = filterIncludeBits;
                _filterExcludeBits = filterExcludeBits;
                _archetypesCount = UnsafeList.GetCount(_matchingArchetypes);
                _archetypeIndex = -1;
                _matchedEntities = BitSet.Allocate(Config.ChunkEntitiesCount);

                if (_query != null) _query._world = _world;
            }

            public EntityAddress Current
            {
                [Inline(256)]
                get
                {
                    if (_chunkEntitiesPtr == null)
                    {
                        BExceptionThrower.FilterIsEmpty();
                    }

                    return _current;
                }
            }

            [Inline(256)]
            public bool MoveNext()
            {
                // Outer infinite loop: we advance step by step
                while (true)
                {
                    if (_chunkEntitiesPtr == null)
                    {
                        // STEP 1: Archetype iteration
                        // If we don’t have chunks loaded, move to the next archetype
                        if (_chunks == null)
                        {
                            ++_archetypeIndex;

                            // If no more archetypes -> enumeration finished
                            if (_archetypeIndex >= _archetypesCount)
                            {
                                Dispose();
                                return false;
                            }

                            // Load archetype info:
                            _current.archetype = UnsafeList.Get<ArchetypeId>(_matchingArchetypes, _archetypeIndex);
                            BitSetPtrWrapper isAlive;
                            ref var archetype = ref _world.GetArchetype(_current.archetype);
                            (_chunks, isAlive) = archetype.GetChunks();
                            _isChunkAlive = isAlive.GetInternal();

                            if (BitSet.GetSize(_matchedEntities) < archetype.EntitiesCapacity)
                            {
                                _matchedEntities = BitSet.Resize(_matchedEntities, archetype.EntitiesCapacity);
                            }

                            _chunkIndex = -1;
                        }

                        // STEP 2: Chunk iteration
                        // Advance chunk index
                        ++_chunkIndex;

                        // If no more chunks in this archetype -> reset and go to next archetype
                        if (_chunkIndex >= _chunks.Length || _isChunkAlive == null)
                        {
                            _chunks = null;
                            _isChunkAlive = null;
                            continue; // restart loop, next archetype
                        }

                        // If chunk is not alive -> skip this chunk, move to next
                        if (BitSet.IsSet(_isChunkAlive, _chunkIndex) == false)
                        {
                            _chunks = null;
                            _isChunkAlive = null;
                            continue; // try next chunk
                        }

                        ref var chunk = ref _chunks[_chunkIndex];
                        UnsafeArray* entitiesInChunk = chunk.GetEntities();

                        // If chunk has no entities -> skip, move to next chunk
                        if (entitiesInChunk == null)
                        {
                            _chunkEntitiesPtr = null;
                            continue;
                        }

                        // Initialize current chunk:
                        _chunkData = chunk.GetUnmanagedChunkData(null, -1);
                        if (_query != null) _query._unmanagedComponentsStorage = _chunkData._unmanagedComponentsStorage;
                        _chunkEntitiesPtr = UnsafeArray.GetPtr<Entity>(entitiesInChunk, 0);
                        if (_query != null) _query._entities = _chunkEntitiesPtr;
                        _entitiesCount = (short)chunk.Count;
                        _managedComponents = chunk.ManagedComponents;
                        if (_query != null) _query._managedComponents = _managedComponents;
                        _allMatch = _managedComponents.GetEntitiesMatch(_matchedEntities, _entitiesCount, _filterIncludeBits, _filterExcludeBits);
                        if (_query != null) _query._migrationTable = chunk.GetMigrationTable();
                        _current.chunkIndex = chunk.Index;

                        // Reset entity index
                        _entityIndex = -1;
                    }

                    // STEP 3: Entity iteration
                    // Advance entity index
                    ++_entityIndex;

                    // If no more entities in chunk -> dispose and move to next chunk
                    if (_entityIndex >= _entitiesCount)
                    {
                        _chunkEntitiesPtr = null;
                        continue; // restart loop, next chunk
                    }

                    // If not all-match, check bitset if entity matches filter
                    if (_allMatch == false && BitSet.IsSet(_matchedEntities, _entityIndex) == false)
                    {
                        continue; // skip this entity, move to next entity
                    }

                    break;
                }

                _current.entityIndex = (EntityIndexInChunk)_entityIndex;

                return true;
            }

            private void Dispose()
            {
                if (_matchedEntities != null)
                {
                    BitSet.Free(_matchedEntities);
                    _matchedEntities = null;
                }
            }
        }

        public struct ChunksEnumerator
        {
            private readonly EcsWorld _world;
            private readonly UnsafeList* _matchingArchetypes;

            public ChunksEnumerator(EcsWorld world, UnsafeList* matchingArchetypes)
            {
                _world = world;
                _matchingArchetypes = matchingArchetypes;
            }

            public ChunkEnumerator GetEnumerator()
            {
                return new ChunkEnumerator(_world, _matchingArchetypes, null);
            }
        }

        public unsafe ref struct ChunkEnumerator
        {
            private readonly EcsWorld _world;
            private readonly UnsafeList* _matchingArchetypes;

            private readonly EcsQuery _query;

            private int _archetypeIndex;
            private readonly int _archetypesCount;
            private ArchetypeChunk[] _chunks;
            private BitSet* _isChunkAlive;
            private short _chunkIndex;

            public ChunkEnumerator(EcsWorld world, UnsafeList* matchingArchetypes, EcsQuery query) 
            {
                // this = default;
                _world = world;
                _query = query;
                _matchingArchetypes = matchingArchetypes;
                _archetypesCount = UnsafeList.GetCount(_matchingArchetypes);
                _archetypeIndex = -1;

                _chunks = null;
                _isChunkAlive = null;
                _chunkIndex = -1;

                if (_query != null) _query._world = _world;
            }

            public UnmanagedChunkData Current
            {
                [Inline(256)]
                get
                {
                    if (_chunks == null)
                    {
                        BExceptionThrower.FilterIsEmpty();
                    }

                    return _chunks[_chunkIndex].GetUnmanagedChunkData(null, -1);
                }
            }

            [Inline(256)]
            public bool MoveNext()
            {
                // Outer infinite loop: we advance step by step
                while (true)
                {
                    if (_chunks == null)
                    {
                        ++_archetypeIndex;

                        // If no more archetypes -> enumeration finished
                        if (_archetypeIndex >= _archetypesCount) return false;

                        // Load archetype info:
                        ArchetypeId archetypeId = UnsafeList.Get<ArchetypeId>(_matchingArchetypes, _archetypeIndex);
                        BitSetPtrWrapper isAlive;
                        ref var archetype = ref _world.GetArchetype(archetypeId);
                        (_chunks, isAlive) = archetype.GetChunks();
                        _isChunkAlive = isAlive.GetInternal();
                        _chunkIndex = -1;
                    }

                    // STEP 2: Chunk iteration
                    // Advance chunk index
                    ++_chunkIndex;

                    // If no more chunks in this archetype -> reset and go to next archetype
                    if (_chunkIndex >= _chunks.Length || _isChunkAlive == null)
                    {
                        _chunks = null;
                        _isChunkAlive = null;
                        continue; // restart loop, next archetype
                    }

                    // If chunk is not alive -> skip this chunk, move to next
                    if (BitSet.IsSet(_isChunkAlive, _chunkIndex) == false)
                    {
                        _chunks = null;
                        _isChunkAlive = null;
                        continue; // try next chunk
                    }

                    ref var chunk = ref _chunks[_chunkIndex];

                    // Initialize current chunk:
                    if (_query != null) _query._unmanagedComponentsStorage = chunk.GetUnmanagedComponentsStorage();
                    if (_query != null) _query._managedComponents = chunk.ManagedComponents;
                    if (_query != null) _query._migrationTable = chunk.GetMigrationTable();

                    break;
                }

                return true;
            }
        }

        public readonly ref struct Mask
        {
            private readonly EcsFilter _parent;
            private readonly BitSet* _includeBits;
            private readonly BitSet* _excludeBits;
            private readonly BitSet* _includeUnmanagedBits;
            private readonly BitSet* _excludeUnmanagedBits;

            private bool IsValid { [Inline(256)] get => _includeBits != null && _excludeBits != null && _includeUnmanagedBits != null && _excludeUnmanagedBits != null; }
            
            public Mask(in EcsFilter parent, BitSet* includeBits, BitSet* excludeBits, BitSet* includeUnmanagedBits, BitSet* excludeUnmanagedBits)
            {
                _parent = parent;
                _includeBits = includeBits;
                _excludeBits = excludeBits;
                _includeUnmanagedBits = includeUnmanagedBits;
                _excludeUnmanagedBits = excludeUnmanagedBits;
            }
            
            [Inline(256)]
            public Mask With<TComponent>()
                where TComponent : struct, IEcsComponentBase
            {
                if (IsValid == false) BExceptionThrower.ObjectIsDisposed();
                
                short index = EcsComponentDescriptor<TComponent>.TypeIndex;

                BitSet.Set(_includeBits, index);
                BitSet.Clear(_excludeBits, index);
                
                return this;
            }
            
            [Inline(256)]
            public Mask WithUnmanaged<TComponent>()
                where TComponent : unmanaged, IEcsUnmanagedComponent
            {
                if (IsValid == false) BExceptionThrower.ObjectIsDisposed();
                
                short index = EcsUnmanagedComponentDescriptor<TComponent>.TypeIndex;

                BitSet.Set(_includeUnmanagedBits, index);
                BitSet.Clear(_excludeUnmanagedBits, index);
                
                return this;
            }
            
            [Inline(256)]
            public Mask Without<TComponent>()
                where TComponent : struct, IEcsComponentBase
            {
                if (IsValid == false) BExceptionThrower.ObjectIsDisposed();
                
                short index = EcsComponentDescriptor<TComponent>.TypeIndex;

                BitSet.Clear(_includeBits, index);
                BitSet.Set(_excludeBits, index);
                
                return this;
            }
            
            [Inline(256)]
            public Mask WithoutUnmanaged<TComponent>()
                where TComponent : unmanaged, IEcsUnmanagedComponent
            {
                if (IsValid == false) BExceptionThrower.ObjectIsDisposed();
                
                short index = EcsUnmanagedComponentDescriptor<TComponent>.TypeIndex;

                BitSet.Clear(_includeUnmanagedBits, index);
                BitSet.Set(_excludeUnmanagedBits, index);
                
                return this;
            }
            
            [Inline(256)]
            public Mask WithAspect<TAspect>()
                where TAspect : unmanaged, IEcsAspect
            {
                (new TAspect()).Define(this);
                return this;
            }

            [Inline(256)]
            public EcsFilter Build()
            {
                _parent.UpdateArchetypes();
                return _parent;
            }
        }
    }
}