// ----------------------------------------------------------------------------
// The Proprietary or MIT License
// Copyright (c) 20224-2024 RFS_6ro <rfs6ro@gmail.com>
// ----------------------------------------------------------------------------

using BucketEcs.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace BucketEcs
{
    public enum ComponentId : ushort { }

    public interface IEcsPool
    {
        ComponentId Id { get; }

        void CreateStorage(EntityRepositoryId entityRepositoryId);
        bool HasStorage(EntityRepositoryId entityRepositoryId);
        IComponentStorage GetStorageRaw(EntityRepositoryId entityRepositoryId);
        void ReleaseStorage(EntityRepositoryId entityRepositoryId);
    }

    public class EcsPool<T> : IEcsPool
        where T : struct, IEcsComponent
    {
        public EcsPool(ComponentId id)
        {
            _id = id;
            _collectionPool = new CollectionPool();

            _activeStorages = new BitArray(100);
            _storages = new SafeGrowingArray<ComponentStorage<T>>(100); // _ CONFIG
        }
        
        private ComponentId _id;
        private CollectionPool _collectionPool;

        private BitArray _activeStorages;
        private SafeGrowingArray<ComponentStorage<T>> _storages;

        public ComponentId Id
        {
            /*V3*/ // [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _id;
        }

        void IEcsPool.CreateStorage(EntityRepositoryId entityRepositoryId)
        {
            int index = (int)entityRepositoryId;
            _activeStorages.EnsureCapacity(index);

            if (_activeStorages[index]) return;

            _activeStorages.Set(index);

            var storage = _storages[index];
            if (storage == null)
            {
                _storages[index] = new ComponentStorage<T>(_collectionPool, entityRepositoryId, _id);
            }
        }

        /*V3*/ // [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool IEcsPool.HasStorage(EntityRepositoryId entityRepositoryId)
        {
            return _activeStorages[(int)entityRepositoryId];
        }

        /*V3*/ // [MethodImpl(MethodImplOptions.AggressiveInlining)]
        IComponentStorage IEcsPool.GetStorageRaw(EntityRepositoryId entityRepositoryId)
        {
            return GetStorage(entityRepositoryId);
        }

        /*V3*/ // [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ComponentStorage<T> GetStorage(EntityRepositoryId entityRepositoryId)
        {
            int index = (int)entityRepositoryId;
            _activeStorages.EnsureCapacity(index);

            if (_activeStorages[index] == false)
            {
                throw new System.Exception("Assosiated With Entity Repository Id Components Repository Should Exist");
            }

            return _storages[index];
        }

        /*V3*/ // [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void IEcsPool.ReleaseStorage(EntityRepositoryId entityRepositoryId)
        {
            int index = (int)entityRepositoryId;
            _activeStorages.EnsureCapacity(index);

            if (_activeStorages[index] == false) return;

            _activeStorages.Reset(index);

            _storages[index].Clear();
        }

        public class CollectionPool
        {
            private Stack<SafeGrowingArray<T>> _collections;

            public CollectionPool()
            {
                _collections = new Stack<SafeGrowingArray<T>>();
            }

            /*V3*/ // [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public SafeGrowingArray<T> Allocate()
            {
                if (_collections.Count == 0) 
                {
                    return new SafeGrowingArray<T>(4096); // _ CONFIG
                }

                return _collections.Pop();
            }

            /*V3*/ // [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Free(SafeGrowingArray<T> collection)
            {
                _collections.Push(collection);
            }
        }
    }
}
