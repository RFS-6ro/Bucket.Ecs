// ----------------------------------------------------------------------------
// The Proprietary or MIT License
// Copyright (c) 20224-2024 RFS_6ro <rfs6ro@gmail.com>
// ----------------------------------------------------------------------------

using BucketEcs.Collections;
using System.Collections.Generic;

namespace BucketEcs
{
    public interface IEcsComponent { }
    public interface IEcsTagComponent : IEcsComponent { }

    public interface IComponentStorage
    {
        bool HasComponentsInContainer(ChunkIndex containerIndex);
        void AddNewContainerCallback(ChunkIndex newContainerIndex);
        void AddComponentData(ChunkIndex containerIndex, EntityIndex addIndex);
        void RemoveComponentData(ChunkIndex containerIndex, EntityIndex removeAtIndex);
        void RebalanceMergeCallback(ChunkIndex rightChild, ChunkIndex leftChild, ChunkIndex mergeParent);
        void RecycleContainerCallback(ChunkIndex recycleContainerIndex);
        void RebalanceSplitCallback(ChunkIndex splitParent, ChunkIndex rightChild, ChunkIndex leftChild);
        void CopyComponentData((EntityIndex index, ChunkIndex container) from, (EntityIndex index, ChunkIndex container) to);
        void CopyComponentDataTo(IComponentStorage copyToStorage, (ChunkIndex container, EntityIndex index) from, (ChunkIndex container, EntityIndex index) to);
    }

    public class ComponentStorage<T> : IComponentStorage
        where T : struct, IEcsComponent
    {
        public ComponentStorage(EcsPool<T>.CollectionPool collectionPool, EntityRepositoryId entityRepositoryId, ComponentId id)
        {
            _id = id;
            _entityRepositoryId = entityRepositoryId;
            _collectionPool = collectionPool;
            _storage = new SafeGrowingArray<ComponentsContainer>(100); // _ CONFIG
        }

        private ComponentId _id;
        private EntityRepositoryId _entityRepositoryId;
        private EcsPool<T>.CollectionPool _collectionPool;
        private SafeGrowingArray<ComponentsContainer> _storage;

        public ComponentId Id => _id;
        public EntityRepositoryId EntityRepositoryId => _entityRepositoryId;

        public ref ComponentsContainer GetContainer(ChunkIndex containerIndex)
        {
            return ref _storage[(int)containerIndex];
        }

        public bool HasComponentsInContainer(ChunkIndex containerIndex)
        {
            return _storage[(int)containerIndex].components != null;
        }

        public void AddNewContainerCallback(ChunkIndex newContainerIndex)
        {
            var containerCollection = _storage[(int)newContainerIndex].components;

            if (containerCollection != null) return;

            _storage[(int)newContainerIndex].components = _collectionPool.Allocate();
        }

        public void AddComponentData(ChunkIndex containerIndex, EntityIndex addIndex)
        {
            var containerCollection = _storage[(int)containerIndex].components;

            if (containerCollection == null)
            {
                _storage[(int)containerIndex].components = _collectionPool.Allocate();
            }

            _storage[(int)containerIndex].components[(ushort)addIndex] = default;
        }

        public void RemoveComponentData(ChunkIndex containerIndex, EntityIndex removeAtIndex)
        {
            var containerCollection = _storage[(int)containerIndex].components;

            if (containerCollection == null) return;

            _storage[(int)containerIndex].components[(ushort)removeAtIndex] = default;
        }

        public void RebalanceMergeCallback(ChunkIndex rightChild, ChunkIndex leftChild, ChunkIndex mergeParent)
        {
            var leftChildCollection = _storage[(int)leftChild].components;
            var mergeParentCollection = _storage[(int)mergeParent].components;

            if (mergeParentCollection != null)
            {
                _collectionPool.Free(mergeParentCollection);
                _storage[(int)mergeParent].components = null;
            }

            if (leftChildCollection == null)
            {
                _storage[(int)mergeParent].components = _collectionPool.Allocate();
            }
            else
            {
                _storage[(int)mergeParent].components = leftChildCollection;
            }

            _storage[(int)leftChild].components = null;
        }

        public void RecycleContainerCallback(ChunkIndex recycleContainerIndex)
        {
            var containerCollection = _storage[(int)recycleContainerIndex].components;

            if (containerCollection != null)
            {
                _collectionPool.Free(containerCollection);
                _storage[(int)recycleContainerIndex].components = null;
            }
        }

        public void RebalanceSplitCallback(ChunkIndex splitParent, ChunkIndex rightChild, ChunkIndex leftChild)
        {
            var leftChildCollection = _storage[(int)leftChild].components;
            var rightChildCollection = _storage[(int)rightChild].components;
            var splitParentCollection = _storage[(int)splitParent].components;

            if (leftChildCollection == null)
            {
                _storage[(int)leftChild].components = _collectionPool.Allocate();
            }

            if (rightChildCollection == null)
            {
                _storage[(int)rightChild].components = _collectionPool.Allocate();
            }

            if (splitParentCollection != null)
            {
                _collectionPool.Free(splitParentCollection);
                _storage[(int)splitParent].components = null;
            }
        }

        public void CopyComponentData((EntityIndex index, ChunkIndex container) from, (EntityIndex index, ChunkIndex container) to)
        {
            var fromCollection = _storage[(int)from.container].components;
            var toCollection = _storage[(int)to.container].components;

            if (fromCollection == null) return;
            
            if (toCollection == null)
            {
                _storage[(int)to.container].components = _collectionPool.Allocate();
            }

            _storage[(int)to.container].components[(ushort)to.index] = _storage[(int)from.container].components[(ushort)from.index];
        }

        public void CopyComponentDataTo
        (
            IComponentStorage copyToStorage, 
            (ChunkIndex container, EntityIndex index) from, 
            (ChunkIndex container, EntityIndex index) to
        )
        {
            ComponentStorage<T> other = (ComponentStorage<T>)copyToStorage;
            
            var fromCollection = _storage[(int)from.container].components;
            var toCollection = other._storage[(int)to.container].components;

            if (fromCollection == null) return;
            
            if (toCollection == null)
            {
                other._storage[(int)to.container].components = _collectionPool.Allocate();
            }

            other._storage[(int)to.container].components[(ushort)to.index] = _storage[(int)from.container].components[(ushort)from.index];
        }

        public void Clear()
        {
            // This method is not implemented because there should be no storages by the time of execution.
            // However - if user wants to destroy the world - we should ensure that the memory is released
            //TODO: CLEAR ALL USED STORAGES IF ANY. 
        }

        public struct ComponentsContainer
        {
            public SafeGrowingArray<T> components; // TODO: REPLACE SafeGrowingArray WITH SOME MORE COMPLEX CONTAINER, THAT WE CAN ACCESS AS SPAN OR AS MEMORY
        }
    }
}
