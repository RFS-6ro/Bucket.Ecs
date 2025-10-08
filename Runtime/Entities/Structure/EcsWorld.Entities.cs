using UnsafeCollections.Collections.Native;
using System.Collections.Generic;
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

    public sealed partial class EcsWorld
    {
        private unsafe BitSet* _isAlive;
        private Archetype[] _archetypes;

        private Dictionary<ulong, ArchetypeId> _hashToArchetype;
        private Dictionary<ArchetypeId, ulong> _archetypeToHash;

        private ArchetypeId _emptyArchetypeId;

        public unsafe int ArchetypesCount { [Inline(256)] get => BitSet.CountSet(_isAlive); }

        [Inline(256)]
        private unsafe void InitArchetypes()
        {
            _isAlive = BitSet.Allocate(Config.ExpectedAmountOfArchetypes);
            _archetypes = new Archetype[Config.ExpectedAmountOfArchetypes];
            _hashToArchetype = new Dictionary<ulong, ArchetypeId>();
            _archetypeToHash = new Dictionary<ArchetypeId, ulong>();

            BitSet* emptyComponentsMask = BitSet.Allocate(RUNTIME_REFERENCES.UnmanagedComponentsCount);
            _emptyArchetypeId = GetOrCreateArchetype(emptyComponentsMask);
            BitSet.Free(emptyComponentsMask);
        }

        [Inline(256)]
        public EntityBuilder GetEntityBuilder() => new EntityBuilder(this);

        [Inline(256)]
        internal unsafe ArchetypeId GetOrCreateArchetype(BitSet* componentsMask)
        {
            // componentsMask is read only here, meaning that it has to be allocated and released outside.
            ulong hash = BitSet.GetHashCode(componentsMask);
            if (_hashToArchetype.TryGetValue(hash, out var archetypeId))
            {
                return archetypeId;
            }

            if (BitSet.GetFirstClearBit(_isAlive, out int id) == false)
            {
                id = _archetypes.Length;
                int newSize = id << 1;
                Array.Resize<Archetype>(ref _archetypes, newSize);
                _isAlive = BitSet.Resize(_isAlive, newSize);
            }

            _archetypes[id] = new Archetype((ArchetypeId)id, this, _chunksDataAllocator);
            BitSet.Set(_isAlive, id);

            ref var archetype = ref _archetypes[id];
            archetype.FillBitMask(componentsMask);
            OnArchetypeCreated(archetype.Id, componentsMask);

            _hashToArchetype.Add(hash, (ArchetypeId)id);
            _archetypeToHash.Add((ArchetypeId)id, hash);

            return (ArchetypeId)id;
        }

        [Inline(256)]
        internal ref Archetype GetArchetype(ArchetypeId archetypeId)
        {
            return ref _archetypes[(int)archetypeId];
        }

        [Inline(256)]
        internal unsafe bool IsAlive(ArchetypeId archetypeId)
        {
            return BitSet.IsSet(_isAlive, (int)archetypeId);
        }

        [Inline(256)]
        public EntityAddress CreateEntity()
        {
            return CreateEntity(_emptyArchetypeId);
        }

        [Inline(256)]
        internal unsafe EntityAddress CreateEntity(ArchetypeId archetypeId)
        {
            // BAssert.True(BitSet.IsSet(_isAlive, (int)archetypeId));
            ref var archetype = ref _archetypes[(int)archetypeId];
            var entityId = new EntityId(_entityIdFactory.GetNewId());
            BAssert.True(entityId.IsValid, "Entity id factory created an invalid entity id.");
            return archetype.AddEntity(entityId);
        }
        
        [Inline(256)]
        internal unsafe EntityContext CreateEntityContext(in EntityAddress address)
        {
            ref var archetype = ref _archetypes[(int)address.archetype];
            ref var chunk = ref archetype.GetChunk(address.chunkIndex);
            var unmanagedComponentsStorage = chunk.GetUnmanagedComponentsStorage();
            return new EntityContext
            (
                this,
                in chunk.Get(address.entityIndex),
                address,
                ref unmanagedComponentsStorage,
                chunk.ManagedComponents,
                chunk.GetMigrationTable()
            );
        }

        [Inline(256)]
        public unsafe void DestroyEntity(in EntityAddress address)
        {
            BAssert.True(BitSet.IsSet(_isAlive, (int)address.archetype));
            ref var archetype = ref _archetypes[(int)address.archetype];
            archetype.RemoveEntity(in address);
        }

        [Inline(256)]
        internal unsafe void RecycleArchetype(ArchetypeId id)
        {
            if (id == _emptyArchetypeId) return; // Do not recycle empty archetype

            if (_archetypeToHash.TryGetValue(id, out var hash))
            {
                _hashToArchetype.Remove(hash);
                _archetypeToHash.Remove(id);
            }

            _archetypes[(int)id].Dispose();
            BitSet.Clear(_isAlive, (int)id);
            OnArchetypeRecycled(id);
        }

        [Inline(256)]
        private unsafe void ReleaseArchetypes()
        {
            for (int i = 0; i < _archetypes.Length; i++)
            {
                _archetypes[i].Dispose();
            }

            if (_isAlive != null)
            {
                BitSet.Free(_isAlive);
                _isAlive = null;
            }
        }

        [Inline(256)]
        public unsafe BitSet.SetBitEnumerator GetEnumerator()
        {
            return BitSet.GetSetEnumerator(_isAlive);
        }

        public unsafe struct Enumerator
        {
            private readonly Archetype[] _archetypes;
            private readonly BitSet* _isAlive;
            private readonly int _aliveArchetypesCount;
            private readonly int _count;
            private int _idx;

            internal Enumerator(Archetype[] archetypes, BitSet* isAlive, int aliveArchetypesCount)
            {
                _archetypes = archetypes;
                _count = _archetypes.Length;
                _isAlive = isAlive;
                _aliveArchetypesCount = aliveArchetypesCount;
                _idx = -1;
            }

            public ref Archetype Current
            {
                [Inline(256)]
                get
                {
                    return ref _archetypes[_idx];
                }
            }

            [Inline(256)]
            public bool MoveNext()
            {
                do
                {
                    // TODO: if _archetypes will have only few alive entries, but the gap between them will be huge - iteration here will freeze a bit. Maybe add a separate bitset alive iterator?
                    if (_isAlive == null) break;
                    if (++_idx >= _aliveArchetypesCount) break;
                    if (++_idx >= _count) break;
                    
                    if (BitSet.IsSet(_isAlive, _idx))
                    {
                        if (_archetypes[_idx].IsCreated) return true;
                    }

                } while (_idx < _count);

                return false;
            }
        }
    }
}
