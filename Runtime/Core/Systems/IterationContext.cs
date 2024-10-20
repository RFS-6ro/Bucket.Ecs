// ----------------------------------------------------------------------------
// The Proprietary or MIT License
// Copyright (c) 20224-2024 RFS_6ro <rfs6ro@gmail.com>
// ----------------------------------------------------------------------------

using BucketEcs.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace BucketEcs
{
    using Entities = RecycleArray<Entity>;

    public readonly struct RemoveEntityContext
    {
        internal RemoveEntityContext
        (
            EntityRepositoryId entityRepositoryId,
            ChunkIndex entityContainer,
            EntityIndex entityIndex
        )
        {
            this.entityRepositoryId = entityRepositoryId;
            this.entityContainer = entityContainer;
            this.entityIndex = entityIndex;
        }

        internal readonly EntityRepositoryId entityRepositoryId;
        internal readonly ChunkIndex entityContainer;
        internal readonly EntityIndex entityIndex;
    }

    public readonly struct ChunkContext
    {
        internal ChunkContext
        (
            EntityRepositoryId entityRepositoryId, 
            ChunkIndex entityContainer
        )
        {
            this.entityRepositoryId = entityRepositoryId;
            this.entityContainer = entityContainer;
        }

        internal readonly EntityRepositoryId entityRepositoryId;
        internal readonly ChunkIndex entityContainer;
    }
    public readonly struct IterationContext
    {
        public IterationContext
        (
            List<EntityRepository> entityRepositoriesInUseContainer,
            RecycleArray<RemoveEntityContext> entitiesToRemoveContainer,
            Entities entities,
            EcsWorld world,

            EntityRepositoryId entityRepositoryId, 
            ChunkIndex entityContainer
        )
        {
            _entityRepositoriesInUseContainer = entityRepositoriesInUseContainer;
            _entitiesToRemoveContainer = entitiesToRemoveContainer;
            _entities = entities;
            _world = world;

            this.entityRepositoryId = entityRepositoryId;
            this.entityContainer = entityContainer;
        }
 
        private readonly List<EntityRepository> _entityRepositoriesInUseContainer;
        private readonly RecycleArray<RemoveEntityContext> _entitiesToRemoveContainer;
        private readonly Entities _entities;
        private readonly EcsWorld _world;

        public readonly EntityRepositoryId entityRepositoryId;
        public readonly ChunkIndex entityContainer;

        public int EntitiesCountInContext => _entities.Count;
        
        /*V3*/ // [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Has<T>() where T : struct, IEcsComponent
        {
            return _world.GetEcsPoolRaw<T>().HasStorage(entityRepositoryId);
        }

        /*V3*/ // [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public StorageAccess<T> Access<T>() where T : struct, IEcsComponent
        {
            return new StorageAccess<T>
            (
                _world.GetEcsPool<T>().GetStorage(entityRepositoryId), 
                entityContainer
            );
        }

        //TODO: Right now we can't add more, than one component to entity in a system. API here is going to change in the nearest future.
        public ref T Add<T>(EntityIndex entityIndex) where T : struct, IEcsComponent
        {
            ComponentId componentId = EcsComponentDescriptor<T>.TypeIndex;
            var currentEntityRepository = _world.GetEntityRepository(entityRepositoryId);
            var currentBitMask = currentEntityRepository.BitMask;
            bool success = currentBitMask.TryAdd(componentId, out var newMask);

            if (success == false) throw new System.Exception("currentBitMask.TryAdd Is Not Valid");

            int hash = newMask.GetHashCode();
            EntityRepository newEntityRepository = null;

            // Typically, a system is adding one or two components in it. We can easily iterate over an array with  2 elements lenght.
            // Also, we are caching that access only the first time we access the repository. that means for a 4k elements chunk we'll save a lot of time.
            foreach (var repository in _entityRepositoriesInUseContainer)
            {
                if (hash == repository.BitMask.GetHashCode())
                {
                    newEntityRepository = repository;
                    break;
                }
            }

            bool entityRepositoryExists = newEntityRepository != null;
            if (entityRepositoryExists)
            {
                if (newEntityRepository.IsRecycled()) 
                {
                    throw new System.Exception("newEntityRepository IsRecycled");
                }
                newMask.Recycle();
            }
            else
            {
                newEntityRepository = _world.CreateEntityRepository(newMask);
                _entityRepositoriesInUseContainer.Add(newEntityRepository);
            }

            Entity migratingEntity = currentEntityRepository.GetEntity(entityContainer, entityIndex);
            (ChunkIndex newEntityContainer, EntityIndex newEntityIndex) = newEntityRepository.AddEntity(migratingEntity);

            int migrationContextIndex = _entitiesToRemoveContainer.Allocate();
            _entitiesToRemoveContainer[migrationContextIndex] = new RemoveEntityContext
            (
                entityRepositoryId,
                entityContainer,
                entityIndex
            );

            currentEntityRepository.MigrateEntity(newEntityRepository, (entityContainer, entityIndex), (newEntityContainer, newEntityIndex));

            var pool = _world.GetEcsPoolRaw<T>();
            if (pool.HasStorage(newEntityRepository.Id) == false)
            {
                pool.CreateStorage(newEntityRepository.Id);
            }

            var typedPool = (EcsPool<T>)pool;

            var storage = typedPool.GetStorage(newEntityRepository.Id);
            ref var container = ref storage.GetContainer(newEntityContainer);
            return ref container.components[(int)newEntityIndex];
        }

        /*V3*/ // [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Enumerator GetEnumerator() => new Enumerator(_entities);

        public struct Enumerator
        {
            public Enumerator(Entities entities) 
            {
                _count = entities.Count;
                _entities = entities;
                _idx = -1;
            }
 
            private readonly Entities _entities;
            private readonly int _count;
            private int _idx;

            public EntityIndex Current 
            {
                /*V3*/ // [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => (EntityIndex)_idx;
            }

            /*V3*/ // [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() 
            {
                do
                {
                    if (++_idx >= _count) return false;
                    
                    bool isAlive = _entities.IsAlive(_idx);

                    if (isAlive) return true;

                } while (_idx < _count);

                return false;
            }
        }
    }
}
