// ----------------------------------------------------------------------------
// The Proprietary or MIT License
// Copyright (c) 20224-2024 RFS_6ro <rfs6ro@gmail.com>
// ----------------------------------------------------------------------------

using BucketEcs.Collections;
using System.Collections.Generic;

namespace BucketEcs
{
    public readonly ref struct MigrationHandler
    {
        public MigrationHandler
        (
            EcsWorld world,
            ComponentBitMask mask, 
            EntityIndex entityIndex, 
            ChunkIndex entityContainer,
            EntityRepository currentEntityRepository,
            List<EntityRepository> entityRepositoriesInUseContainer,
            RecycleArray<RemoveEntityContext> entitiesToRemoveContainer
        )
        {
            _mask = mask;
            _world = world;
            _entityIndex = entityIndex;
            _entityContainer = entityContainer;
            _currentEntityRepository = currentEntityRepository;
            _entitiesToRemoveContainer = entitiesToRemoveContainer;
            _entityRepositoriesInUseContainer = entityRepositoriesInUseContainer;
        }

        private readonly EcsWorld _world;
        private readonly ComponentBitMask _mask;
        private readonly EntityIndex _entityIndex;
        private readonly ChunkIndex _entityContainer;
        private readonly EntityRepository _currentEntityRepository;
        private readonly List<EntityRepository> _entityRepositoriesInUseContainer;
        private readonly RecycleArray<RemoveEntityContext> _entitiesToRemoveContainer;

        /*V3*/ // [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public MigrationHandler Add<TAdd>()
            where TAdd : struct, IEcsComponent
        {
            ComponentId componentId = EcsComponentDescriptor<TAdd>.TypeIndex;
            if (_mask.IsComponentIncluded(componentId))
            {
                throw new System.Exception($"Entity already has a component of type {typeof(TAdd)}");
            }
            if (_world.HasEcsPoolById(componentId) == false)
            {
                _ = _world.GetEcsPool<TAdd>();
            }
            _mask.With(componentId);
            return this;
        }

        /*V3*/ // [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public MigrationHandler Del<TDel>()
            where TDel : struct, IEcsComponent
        {
            ComponentId componentId = EcsComponentDescriptor<TDel>.TypeIndex;
            if (_world.HasEcsPoolById(componentId) == false)
            {
                _ = _world.GetEcsPool<TDel>();
            }
            _mask.Without(componentId);
            return this;
        }

        /*V3*/ // [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public MigratedEntityRef Finish()
        {
            int hash = _mask.GetHashCode();
            EntityRepository newEntityRepository = null;

            // Typically, a system is adding one or two components in it. We can easily iterate over an array with  2 elements lenght.
            // Also, we are caching that access only the first time we access the repository. that means for a 4k elements chunk we'll save a lot of time.
            for (int i = 0; i < _entityRepositoriesInUseContainer.Count; i++)
            {
                EntityRepository repository = _entityRepositoriesInUseContainer[i];

                if (repository.IsRecycled()) throw new System.Exception("here");
                if (hash == repository.BitMask.GetHashCode())
                {
                    newEntityRepository = repository;
                    break;
                }
            }

            bool entityRepositoryExists = newEntityRepository != null;
            if (entityRepositoryExists)
            {
                _mask.Recycle();
            }
            else
            {
                newEntityRepository = _world.CreateEntityRepository(_mask);
                _entityRepositoriesInUseContainer.Add(newEntityRepository);
            }

            Entity migratingEntity = _currentEntityRepository.GetEntity(_entityContainer, _entityIndex);
            (ChunkIndex newEntityContainer, EntityIndex newEntityIndex) = newEntityRepository.AddEntity(migratingEntity);

            int migrationContextIndex = _entitiesToRemoveContainer.Allocate();
            _entitiesToRemoveContainer[migrationContextIndex] = new RemoveEntityContext
            (
                _currentEntityRepository,
                _entityContainer,
                _entityIndex
            );

            _currentEntityRepository.MigrateEntity(newEntityRepository, (_entityContainer, _entityIndex), (newEntityContainer, newEntityIndex));

            return new MigratedEntityRef
            (
                _world,
                newEntityRepository.BitMask,
                newEntityIndex,
                newEntityContainer,
                newEntityRepository.Id
            );
        }
    }

    public readonly ref struct MigratedEntityRef
    {
        public MigratedEntityRef
        (
            EcsWorld world,
            ComponentBitMask mask, 
            EntityIndex entityIndex, 
            ChunkIndex entityContainer,
            EntityRepositoryId entityRepositoryId
        )
        {
            _mask = mask;
            _world = world;
            _entityIndex = entityIndex;
            _entityContainer = entityContainer;
            _entityRepositoryId = entityRepositoryId;
        }

        private readonly EcsWorld _world;
        private readonly ComponentBitMask _mask;
        private readonly EntityIndex _entityIndex;
        private readonly ChunkIndex _entityContainer;
        private readonly EntityRepositoryId _entityRepositoryId;

        /*V3*/ // [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref T RW<T>() where T : struct, IEcsComponent
        {
            ComponentId componentId = EcsComponentDescriptor<T>.TypeIndex;
            if (_mask.IsComponentIncluded(componentId) == false)
            {
                throw new System.Exception($"Trying to access not attached component of type {typeof(T)}");
            }

            var pool = _world.GetEcsPoolRaw<T>();
            if (pool.HasStorage(_entityRepositoryId) == false)
            {
                pool.CreateStorage(_entityRepositoryId);
            }

            var typedPool = (EcsPool<T>)pool;

            var storage = typedPool.GetStorage(_entityRepositoryId);
            ref ComponentStorage<T>.ComponentsContainer container = ref storage.GetContainer(_entityContainer);
            if (container.components == null)
            {
                storage.AddNewContainerCallback(_entityContainer);
            }
            container.components[(int)_entityIndex] = default;
            return ref container.components[(int)_entityIndex];
        }
    }
}
