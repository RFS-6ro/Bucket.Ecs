// ----------------------------------------------------------------------------
// The Proprietary or MIT License
// Copyright (c) 20224-2024 RFS_6ro <rfs6ro@gmail.com>
// ----------------------------------------------------------------------------

using BucketEcs.Collections;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;

namespace BucketEcs
{
    public class EcsWorld
    {
        public EcsWorld()
        {
            _isInitialized = false;

            _collectionPool = new EntitiesCollectionPool();
            _entityBuilders = new RecycleArray<EntityBuilder>(100); // _ CONFIG
            _entityRepositories = new RecycleArray<EntityRepository>(100); // _ CONFIG
            _hashedEntityRepositories = new Dictionary<int, EntityRepositoryId>(100); // _ CONFIG

            _pools = new SafeGrowingArray<IEcsPool>(100); // _ CONFIG

            _allFilters = new List<EcsFilter>(100); // _ CONFIG

            _masks = new RecycleArray<ComponentBitMask>(100); // _ CONFIG
            _emptyComponentBitMask = GetNewComponentBitMask();

            _raisedEvents = new BitArray(100); // _ CONFIG
        }
 
        public static int LastRegisteredComponentTypeId = -1;

        private bool _isInitialized;

        //Entities
        private long _lastEntity;
        private readonly EntitiesCollectionPool _collectionPool;
        private readonly RecycleArray<EntityBuilder> _entityBuilders;
        private readonly RecycleArray<EntityRepository> _entityRepositories;
        private readonly Dictionary<int, EntityRepositoryId> _hashedEntityRepositories;

        public RecycleArray<EntityRepository> EntityRepositories => _entityRepositories;

        //Components
        private SafeGrowingArray<IEcsPool> _pools;

        //Filters
        private readonly List<EcsFilter> _allFilters;

        //Masks
        private ComponentBitMask _emptyComponentBitMask;
        private readonly RecycleArray<ComponentBitMask> _masks;

        //Events
        private readonly BitArray _raisedEvents;

        /*V3*/ // [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void InitializeWorld()
        {
            _isInitialized = true;
        }

        #region Entities
        public EntityRepositoryId CreateEntity()
        {
            if (_isInitialized == false) 
            {
                throw new Exception("World Should Be Initialized"); 
            }

            return CreateEntity(_emptyComponentBitMask);
        }

        public EntityRepositoryId CreateEntity(ComponentBitMask mask)
        {
            if (_isInitialized == false) 
            {
                throw new Exception("World Should Be Initialized"); 
            }

            Entity entity = GenerateEntity();

            EntityRepository entityRepository = GetOrCreateEntityRepository(mask);

            entityRepository.AddEntity(entity);

            return entityRepository.Id;
        }

        public void CreateEntity(EntityRepository entityRepository)
        {
            if (_isInitialized == false) 
            {
                throw new Exception("World Should Be Initialized"); 
            }

            Entity entity = GenerateEntity();

            entityRepository.AddEntity(entity);
        }

        /*V3*/ // [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Entity GenerateEntity()
        {
            Interlocked.Increment(ref _lastEntity);

            return (Entity)(ulong)_lastEntity;
        }

        /*V3*/ // [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public EntityRepository GetEntityRepository(EntityRepositoryId entityRepositoryId)
        {
            return _entityRepositories[(int)entityRepositoryId];
        }

        /*V3*/ // [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool HasEntityRepositoryById(EntityRepositoryId entityRepositoryId)
        {
            return _entityRepositories.IsAlive((int)entityRepositoryId);
        }

        /*V3*/ // [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool HasEntityRepositoryByHash(int hash)
        {
            return _hashedEntityRepositories.TryGetValue(hash, out _);
        }

        /*V3*/ // [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public EntityRepository GetEntityRepositoryByHash(int hash)
        {
            bool exists = _hashedEntityRepositories.TryGetValue(hash, out EntityRepositoryId entityRepositoryId);

            if (exists) return _entityRepositories[(int)entityRepositoryId];
            else return null;
        }

        /*V3*/ // [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public EntityRepository GetOrCreateEntityRepository(ComponentBitMask bitMask)
        {
            int hash = bitMask.GetHashCode();
            bool exists = _hashedEntityRepositories.TryGetValue(hash, out EntityRepositoryId entityRepositoryId);

            if (exists == false)
            {
                return CreateEntityRepository(bitMask);
            }

            return _entityRepositories[(int)entityRepositoryId];
        }

        /*V3*/ // [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public EntityRepository CreateEntityRepository(ComponentBitMask bitMask)
        {
            int hash = bitMask.GetHashCode();
            EntityRepositoryId entityRepositoryId = (EntityRepositoryId)_entityRepositories.Allocate();

            if (_entityRepositories[(int)entityRepositoryId] == null)
            {
                _entityRepositories[(int)entityRepositoryId] = new EntityRepository(this, entityRepositoryId, _collectionPool);
            }

            //create Components repository
            foreach (ComponentId bit in bitMask.IncludeIterator)
            {
                var pool = GetEcsPoolById(bit);
                pool.CreateStorage(entityRepositoryId);
            }

            _entityRepositories[(int)entityRepositoryId].SetBitMask(bitMask);
            _entityRepositories[(int)entityRepositoryId].SetInUse();
            
            _hashedEntityRepositories.Add(hash, entityRepositoryId);

            NotifyFiltersOnEntityRepositoryCreate(entityRepositoryId, bitMask);

            return _entityRepositories[(int)entityRepositoryId];
        }

        /*V3*/ // [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void TryRecycleEntityRepository(EntityRepository repository)
        {
            if (repository.IsRecycled()) return;

            bool repositoryIsEmpty = repository.AllEntitiesCount == 0UL;
            if (repositoryIsEmpty == false) return;

            NotifyFiltersOnEntityRepositoryRelease(repository.Id);

            // recycle here
            int hash = repository.BitMask.GetHashCode();
            EntityRepositoryId entityRepositoryId = repository.Id;

            //clear Components repository 
            foreach (var bit in repository.BitMask.IncludeIterator)
            {
                var pool = GetEcsPoolById(bit);
                pool.ReleaseStorage(entityRepositoryId);
            }

            repository.BitMask.Recycle();

            repository.Clear();
            repository.SetRecycled();

            _hashedEntityRepositories.Remove(hash);
            _entityRepositories.Recycle((int)entityRepositoryId);
        }

        #endregion

        #region Entities Builder
        public EntityBuilder GetEntityBuilder()
        {
            if (_isInitialized == false) 
            {
                throw new Exception("World Should Be Initialized"); 
            }
            
            int index = _entityBuilders.Allocate();
            if (_entityBuilders[index] == null) 
            {
                _entityBuilders[index] = new EntityBuilder(this, index);
            }

            return _entityBuilders[index].SetMask(GetNewComponentBitMask());
        }

        /*V3*/ // [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RecycleEntityBuilder(int builderIndex)
        {
            _entityBuilders.Recycle(builderIndex);
        }
        #endregion

        #region Components
        /*V3*/ // [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool HasEcsPoolById(ComponentId componentId)
        {
            if (_pools.Capacity >= (int)componentId) 
            {
                return _pools[(int)componentId] != null;
            }
            else return false;
        }

        /*V3*/ // [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IEcsPool GetEcsPoolById(ComponentId componentId)
        {
            if (_pools.Capacity >= (int)componentId) 
            {
                if (_pools[(int)componentId] != null) return _pools[(int)componentId];
                else throw new Exception("Component pools were not created yet.");
            }
            else return null;
        }

        /*V3*/ // [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IEcsPool GetEcsPoolRaw<T>() where T : struct, IEcsComponent
        {
            ComponentId componentId = EcsComponentDescriptor<T>.TypeIndex;

            if (_pools.Capacity >= (int)componentId && _pools[(int)componentId] != null) return _pools[(int)componentId];

            return GetEcsPool<T>();
        }

        public EcsPool<T> GetEcsPool<T>() where T : struct, IEcsComponent
        {
            ComponentId componentId = EcsComponentDescriptor<T>.TypeIndex;

            if (_pools.Capacity >= (int)componentId && _pools[(ushort)componentId] != null) return (EcsPool<T>)_pools[(ushort)componentId];
            
            if (_isInitialized) //TODO: check, maybe it can?
            {
                throw new Exception("World Was Already Initialized And Cannot Register New Components"); 
            }
            
            EcsPool<T> pool = new EcsPool<T>(componentId);
            _pools[(ushort)componentId] = pool;

            return pool;
        }
        #endregion

        #region Events

        /*V3*/ // [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RaiseEventSingleton(int eventId)
        {
            _raisedEvents.Set(eventId);
        }

        /*V3*/ // [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool HasEventSingleton(int eventId)
        {
            _raisedEvents.EnsureCapacity(eventId);
            return _raisedEvents[eventId];
        }

        /*V3*/ // [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SuspendEventSingleton(int eventId)
        {
            _raisedEvents.Reset(eventId);
        }

        #endregion

        #region Filters

        /*V3*/ // [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void NotifyFiltersOnEntityRepositoryCreate(EntityRepositoryId entityRepositoryId, ComponentBitMask bitMask)
        {
            foreach (var filter in _allFilters)
            {
                filter.TryAddEntityRepository(entityRepositoryId, bitMask);
            }
        }

        /*V3*/ // [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void NotifyFiltersOnEntityRepositoryRelease(EntityRepositoryId entityRepositoryId)
        {
            foreach (var filter in _allFilters)
            {
                filter.TryDelEntityRepository(entityRepositoryId);
            }
        }

        /*V3*/ // [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public EcsFilter GetOrCreateFilter(ComponentBitMask componentBitMask)
        {
            var filter = new EcsFilter(this, componentBitMask);

            foreach (ref var repository in _entityRepositories)
            {
                filter.TryAddEntityRepository(repository.Id, repository.BitMask);
            }
            
            _allFilters.Add(filter);

            return filter;
        }

        #endregion

        #region Bit Mask
        /*V3*/ // [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ComponentBitMask GetNewComponentBitMask()
        {
            int index = _masks.Allocate();
            if (_masks[index] == null) 
            {
                _masks[index] = new ComponentBitMask(this, index);
            }
            return _masks[index];
        }

        /*V3*/ // [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RecycleBitMask(int maskIndex)
        {
            _masks.Recycle(maskIndex);
        }
        #endregion
    }
}
