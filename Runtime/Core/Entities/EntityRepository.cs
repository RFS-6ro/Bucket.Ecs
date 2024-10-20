// ----------------------------------------------------------------------------
// The Proprietary or MIT License
// Copyright (c) 20224-2024 RFS_6ro <rfs6ro@gmail.com>
// ----------------------------------------------------------------------------

using BucketEcs.Collections;
using System.Collections.Generic;
using System.Threading;

namespace BucketEcs
{
    using Entities = RecycleArray<Entity>;

    public enum Entity : ulong
    {
        invalid = ~0UL // 18446744073709551615
    }

    public enum ChunkIndex : int
    {
        invalid = -1
    }

    public enum EntityIndex : ushort { }

    public enum EntityRepositoryId : int { }

    public class EntitiesCollectionPool
    {
        public EntitiesCollectionPool()
        {
            _collections = new Stack<Entities>();
        }
 
        private Stack<Entities> _collections;

        /*V3*/ // [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Entities Allocate()
        {
            if (_collections.Count == 0) 
            {
                return new Entities(4096); // _ CONFIG
            }

            return _collections.Pop();
        }

        /*V3*/ // [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Free(Entities collection)
        {
            if (collection == null) return;

            collection.RecycleAll();
            _collections.Push(collection);
        }
    }

    public class EntityRepository
    {
        public const int CHUNK_LENGTH = 4096; // _ CONFIG

        public readonly struct ComponentCache
        {
            public ComponentCache(IEcsPool pool, IComponentStorage storage)
            {
                this.pool = pool;
                this.storage = storage;
            }
 
            public readonly IEcsPool pool;
            public readonly IComponentStorage storage;
        }

        public struct Chunk
        {
            public Chunk(ChunkIndex index, Entities collection)
            {
                this.index = index;
                this.collection = collection;
            }

            public readonly ChunkIndex index;
            public Entities collection;

            public bool HasValues() => collection != null;
            public bool IsFull() => collection.Count == CHUNK_LENGTH;

            public EntityIndex AddEntityToCollection(Entity entity)
            {
                int newCollectionIndex = collection.Allocate();
                collection[newCollectionIndex] = entity;

                return (EntityIndex)newCollectionIndex;
            }

            public void RemoveEntityFromCollection(EntityIndex entityIndex)
            {
                collection.Recycle((int)entityIndex);
            }
            
            public void Clear()
            {
                collection = null;
            }
        }

        private readonly EcsWorld _world;
        private readonly EntityRepositoryId _id;
        private readonly EntitiesCollectionPool _collectionPool;

        private readonly RecycleArray<ComponentCache> _componentStorages;
        private readonly List<ChunkIndex> _chunksWithSpaces;
        private readonly RecycleArray<Chunk> _chunks;

        private ComponentBitMask _bitMask;
        private bool _recycled;

        private long _allEntitiesCount = 0;

        public EntityRepositoryId Id => _id;
        public ComponentBitMask BitMask => _bitMask;
        public ulong AllEntitiesCount => unchecked((ulong)_allEntitiesCount);

        public EntityRepository
        (
            EcsWorld world,
            EntityRepositoryId id,
            EntitiesCollectionPool collectionPool
        )
        {
            _id = id;
            _world = world;

            _collectionPool = collectionPool;
            _chunks = new RecycleArray<Chunk>(100); // _ CONFIG
            _chunksWithSpaces = new List<ChunkIndex>(100); // _ CONFIG
            _componentStorages = new RecycleArray<ComponentCache>(20); // _ CONFIG
        }

        #region Add
        /*V3*/ // [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public (ChunkIndex newEntityContainer, EntityIndex newEntityIndex) AddEntity(Entity entity)
        {
            bool allChunksAreFull = _chunksWithSpaces.Count == 0;
            if (allChunksAreFull)
            {
                ChunkIndex chunkIndex = (ChunkIndex)_chunks.Allocate();
                ref Chunk chunk = ref _chunks[(int)chunkIndex];
                chunk = new Chunk(chunkIndex, _collectionPool.Allocate());
                
                ProcessAddNewNodeComponentsStorageCallback(chunkIndex);
                EntityIndex entityIndex = chunk.AddEntityToCollection(entity);
                Interlocked.Increment(ref _allEntitiesCount);
                ProcessAddEntityStorageCallback(chunkIndex, entityIndex);

                _chunksWithSpaces.Add(chunkIndex);

                return (chunkIndex, entityIndex);
            }
            else
            {
                ChunkIndex chunkIndex = _chunksWithSpaces[0];

                ref Chunk chunk = ref _chunks[(int)chunkIndex];
                EntityIndex entityIndex = chunk.AddEntityToCollection(entity);
                Interlocked.Increment(ref _allEntitiesCount);
                ProcessAddEntityStorageCallback(chunkIndex, entityIndex);
                
                if (chunk.IsFull())
                {
                    _chunksWithSpaces.Remove(chunkIndex);
                }

                return (chunkIndex, entityIndex);
            }

            throw new System.NotImplementedException();
        }
        #endregion

        #region Remove
        /*V3*/ // [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RemoveEntity(ChunkIndex chunkIndex, EntityIndex entityIndex)
        {
            if (chunkIndex == ChunkIndex.invalid) return;

            ref Chunk chunk = ref _chunks[(int)chunkIndex];

            if (chunk.IsFull())
            {
                _chunksWithSpaces.Add(chunkIndex);
            }

            chunk.RemoveEntityFromCollection(entityIndex);
            ProcessRemoveEntityStorageCallback(chunkIndex, entityIndex);
            Interlocked.Decrement(ref _allEntitiesCount);

            if (chunk.HasValues() == false)
            {
                _chunksWithSpaces.Remove(chunkIndex);
                
                _collectionPool.Free(chunk.collection);
                _chunks.Recycle((int)chunkIndex, true);
                ProcessRecycleNodeComponentsStorageCallback(chunkIndex);
            }

            if (AllEntitiesCount == 0UL) _world.TryRecycleEntityRepository(this);
        }

        /*V3*/ // [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void MigrateEntity
        (
            EntityRepository copyTo, 
            (ChunkIndex entityContainer, EntityIndex entityIndex) from, 
            (ChunkIndex entityContainer, EntityIndex entityIndex) to
        )
        {
            ProcessCopyDataToAnotherComponentsStorageCallback(copyTo, from, to);
        }
        #endregion

        #region Get
        /*V3*/ // [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Entity GetEntity(ChunkIndex chunkIndex, EntityIndex entityIndex)
        {
            if (chunkIndex == ChunkIndex.invalid) return Entity.invalid;

            ref Chunk chunk = ref _chunks[(int)chunkIndex];

            return chunk.collection[(ushort)entityIndex];
        }
        #endregion

        #region Recycle
        /*V3*/ // [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void SetBitMask(ComponentBitMask bitMask)
        {
            _bitMask = bitMask;
            CacheRepositoryComponents();
        }

        /*V3*/ // [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CacheRepositoryComponents()
        {
            _componentStorages.RecycleAll();
            foreach (var bit in _bitMask.IncludeIterator)
            {
                IEcsPool pool = _world.GetEcsPoolById(bit);
                IComponentStorage storage = pool.GetStorageRaw(_id);

                int storageIndex = _componentStorages.Allocate();
                _componentStorages[storageIndex] = new ComponentCache(pool, storage);
            }
        }

        /*V3*/ // [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void SetRecycled()
        {
            _recycled = true;
        }

        /*V3*/ // [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool IsRecycled()
        {
            return _recycled;
        }

        /*V3*/ // [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void SetInUse()
        {
            _recycled = false; // TODO: throw exceptions if accessed when recycled
        }

        /*V3*/ // [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear()
        {
            _bitMask = null;

            //TODO: Ensure that all the memory used for component storages is also released
            foreach (ref var node in _chunks)
            {
                _collectionPool.Free(node.collection);
                node.Clear();
            }
        }
        #endregion

        #region Storage Callbacks
        /*V3*/ // [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ProcessAddNewNodeComponentsStorageCallback(ChunkIndex newNodeIndex)
        {
            foreach (ref var componentCache in _componentStorages)
            {
                componentCache.storage.AddNewContainerCallback(newNodeIndex);
            }
        }
        
        /*V3*/ // [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ProcessRecycleNodeComponentsStorageCallback(ChunkIndex recycleNodeIndex)
        {
            foreach (ref var componentCache in _componentStorages)
            {
                componentCache.storage.RecycleContainerCallback(recycleNodeIndex);
            }
        }
        
        /*V3*/ // [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ProcessAddEntityStorageCallback(ChunkIndex nodeIndex, EntityIndex addIndex)
        {
            foreach (ref var componentCache in _componentStorages)
            {
                componentCache.storage.AddComponentData(nodeIndex, addIndex);
            }
        }
        
        /*V3*/ // [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ProcessRemoveEntityStorageCallback(ChunkIndex nodeIndex, EntityIndex removeAtIndex)
        {
            //here we don't need to reset any data, because it would be just overrided later by other entities.
            //TODO: however - we may want to add an analogue "IEcsReset" interface for components.
        }
        
        /*V3*/ // [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ProcessCopyDataToAnotherComponentsStorageCallback
        (
            EntityRepository copyTo, 
            (ChunkIndex container, EntityIndex index) from, 
            (ChunkIndex container, EntityIndex index) to
        )
        {
            foreach (ref var componentCache in _componentStorages)
            {
                if (componentCache.pool.HasStorage(copyTo.Id) == false) continue;

                var copyToStorage = componentCache.pool.GetStorageRaw(copyTo.Id);
                componentCache.storage.CopyComponentDataTo(copyToStorage, from, to);
            }
        }
        #endregion

        public RecycleArray<Chunk>.Enumerator GetEnumerator() => _chunks.GetEnumerator();
    }
}
