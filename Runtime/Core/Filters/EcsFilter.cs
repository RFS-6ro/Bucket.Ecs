// ----------------------------------------------------------------------------
// The Proprietary or MIT License
// Copyright (c) 20224-2024 RFS_6ro <rfs6ro@gmail.com>
// ----------------------------------------------------------------------------

using BucketEcs.Collections;
using System.Runtime.CompilerServices;

namespace BucketEcs
{
    public class EcsFilter 
    {
        public EcsFilter(EcsWorld world, ComponentBitMask mask)
        {
            _filteredRepositories = new BitArray(100); // _ CONFIG
            _maxEntityRepositoryId = -1;
            _world = world;
            _mask = mask;
        }
 
        private readonly EcsWorld _world;
        private readonly ComponentBitMask _mask;
        private readonly BitArray _filteredRepositories;

        private int _maxEntityRepositoryId;

        /*V3*/ // [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void TryAddEntityRepository(EntityRepositoryId id, ComponentBitMask repositoryAttachedComponentsMask)
        {
            bool result = _mask.IsIncluding(repositoryAttachedComponentsMask);
            if (result == false) return;

            int index = (int)id;
            if (_maxEntityRepositoryId < index) _maxEntityRepositoryId = index;
            _filteredRepositories.Set(index);
        }

        /*V3*/ // [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void TryDelEntityRepository(EntityRepositoryId id)
        {
            int index = (int)id;
            if (_maxEntityRepositoryId < index) _maxEntityRepositoryId = index;
            _filteredRepositories.Reset(index);
        }

        public ulong GetEntitiesCount() 
        {
            ulong sum = 0;
            for (int i = 0; i < _maxEntityRepositoryId; i++)
            {
                bool isIncluded = _filteredRepositories[i];
                if (isIncluded == false) continue;

                EntityRepositoryId id = (EntityRepositoryId)i;
                EntityRepository repository = _world.GetEntityRepository(id);
                sum += repository.AllEntitiesCount;
            }
            return sum;
        }

        /*V3*/ // [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Enumerator GetEnumerator()
        {
            return new Enumerator(_filteredRepositories, _maxEntityRepositoryId);
        }

        public struct Enumerator
        {
            public Enumerator(BitArray repositories, int maxId) 
            {
                _repositories = repositories;

                _count = maxId + 1;

                _idx = -1;
            }
 
            private readonly BitArray _repositories;
            private readonly int _count;
            private int _idx;

            public EntityRepositoryId Current 
            {
                /*V3*/ // [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => (EntityRepositoryId)_idx;
            }

            /*V3*/ // [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() 
            {
                do
                {
                    if (++_idx >= _count) return false;
                    
                    bool isIncluded = _repositories[_idx];

                    if (isIncluded) return true;

                } while (_idx < _count);

                return false;
            }
        }
    }
}
