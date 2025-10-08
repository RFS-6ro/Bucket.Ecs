using System;
using System.Collections.Generic;
using System.Linq;
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

    public unsafe sealed class UnsafePoolAllocator : IDisposable
    {
        private readonly List<UnsafeArrayPtrWrapper> _allAllocated = new List<UnsafeArrayPtrWrapper>();

        // free lists
        private readonly List<UnsafeArrayPtrWrapper> _freeEntities = new();
        private readonly List<UnsafeArrayPtrWrapper> _freeBytes = new();

        // used arrays: archetype → chunk → ChunkArrays
        private readonly List<List<ChunkArrays>> _used = new();

        public int FreeChunks => _freeEntities.Count;
        public int AllocatedChunks
        {
            get
            {
                int count = 0;
                foreach (var item in _used)
                {
                    count += item.Count((x) => x.EntityArray.IsCreated);
                }
                return count;
            }
        }

        struct ChunkArrays
        {
            public UnsafeArrayPtrWrapper EntityArray;
            public UnsafeArrayPtrWrapper ByteArray;
        }

        [Inline(256)]
        public UnsafeArrayPtrWrapper GetEntityArray(ArchetypeId id, ChunkIndex chunkIndex, int size)
        {
            short archetypeId = (short)id;
            short chunkId = (short)chunkIndex;

            var arr = GetFromPoolOrAllocate(_freeEntities, size, (s) => new (UnsafeArray.Allocate<EntityId>(s)));
            EnsureCapacity(id, chunkIndex);
            var arrays = _used[archetypeId][chunkId];
            arrays.EntityArray = arr;
            _used[archetypeId][chunkId] = arrays;
            return arr;
        }

        [Inline(256)]
        public UnsafeArrayPtrWrapper GetByteArray(ArchetypeId id, ChunkIndex chunkIndex, int size)
        {
            short archetypeId = (short)id;
            short chunkId = (short)chunkIndex;

            var arr = GetFromPoolOrAllocate(_freeBytes, size, (s) => new (UnsafeArray.Allocate<byte>(s)));
            EnsureCapacity(id, chunkIndex);
            var arrays = _used[archetypeId][chunkId];
            arrays.ByteArray = arr;
            _used[archetypeId][chunkId] = arrays;
            return arr;
        }

        [Inline(256)]
        private UnsafeArrayPtrWrapper GetFromPoolOrAllocate(
            List<UnsafeArrayPtrWrapper> pool,
            int size,
            Func<int, UnsafeArrayPtrWrapper> allocator)
        {
            for (int i = pool.Count - 1; i >= 0; --i)
            {
                var array = pool[i];
                if (array.Length >= size)
                {
                    pool.RemoveAt(i);
                    return array;
                }
            }

            var arr = allocator(size);
            _allAllocated.Add(arr);
            return arr;
        }

        [Inline(256)]
        private void EnsureCapacity(ArchetypeId id, ChunkIndex chunkIndex)
        {
            short archetypeId = (short)id;
            short chunkId = (short)chunkIndex;

            while (_used.Count <= archetypeId)
                _used.Add(new List<ChunkArrays>());

            while (_used[archetypeId].Count <= chunkId)
                _used[archetypeId].Add(default);
        }

        [Inline(256)]
        public void Release(ArchetypeId id, ChunkIndex chunkIndex)
        {
            short archetypeId = (short)id;
            short chunkId = (short)chunkIndex;
            if (archetypeId < _used.Count && chunkId < _used[archetypeId].Count)
            {
                var arrays = _used[archetypeId][chunkId];

                if (arrays.EntityArray.IsCreated)
                {
                    _freeEntities.Add(arrays.EntityArray);
                }

                if (arrays.ByteArray.IsCreated)
                {
                    _freeBytes.Add(arrays.ByteArray);
                }

                _used[archetypeId][chunkId] = default; // reset slot
            }
        }

        [Inline(256)]
        public void ReleaseAll(ArchetypeId id)
        {
            short archetypeId = (short)id;
            if (archetypeId < _used.Count)
            {
                var chunks = _used[archetypeId];
                for (int chunkId = 0; chunkId < chunks.Count; chunkId++)
                {
                    var arrays = chunks[chunkId];

                    if (arrays.EntityArray.IsCreated)
                    {
                        _freeEntities.Add(arrays.EntityArray);
                    }

                    if (arrays.ByteArray.IsCreated)
                    {
                        _freeBytes.Add(arrays.ByteArray);
                    }
                }
                chunks.Clear();
            }
        }

        [Inline(256)]
        public void Dispose()
        {
            foreach (var array in _allAllocated)
            {
                array.Dispose();
            }
            _allAllocated.Clear();
            _freeEntities.Clear();
            _freeBytes.Clear();
            _used.Clear();
        }
    }
}
