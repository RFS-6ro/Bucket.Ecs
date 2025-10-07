using System;
using System.Collections.Generic;
using UnsafeCollections.Collections.Native;
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

    public unsafe class DynamicComponentsStorage : IDisposable
    {
        private readonly EcsWorld _world;
        private readonly int _capacity;

        private readonly DynamicComponentStorageBase[] _storages;
        private UnsafeArray* _storagesToEntitiesBitSet; // BitSetPtrWrapper
        private BitSetPtrWrapper* _storagesToEntitiesBitSetRaw;
        private UnsafeArray* _sizes; // short
        private short* _sizesRaw;
        private BitSet* _matchedEntitiesCache;

        private UnsafeList* _requiredStoragesCache;
        private UnsafeList* _forbiddenStoragesCache;

        public DynamicComponentsStorage(EcsWorld world, int capacity)
        {
            _world = world;
            _capacity = capacity;

            _storages = new DynamicComponentStorageBase[RUNTIME_REFERENCES.ComponentsCount];
            _storagesToEntitiesBitSet = UnsafeArray.Allocate<BitSetPtrWrapper>(RUNTIME_REFERENCES.ComponentsCount);
            _storagesToEntitiesBitSetRaw = UnsafeArray.GetPtr<BitSetPtrWrapper>(_storagesToEntitiesBitSet, 0);
            _sizes = UnsafeArray.Allocate<short>(RUNTIME_REFERENCES.ComponentsCount);
            _sizesRaw = UnsafeArray.GetPtr<short>(_sizes, 0);

            _matchedEntitiesCache = BitSet.Allocate(_capacity);

            _requiredStoragesCache = UnsafeList.Allocate<short>(8);
            _forbiddenStoragesCache = UnsafeList.Allocate<short>(8);
        }

        [Inline(256)]
        public bool GetEntitiesMatch(BitSet* matchedEntities, short entitiesCount, BitSet* filterIncludeBits, BitSet* filterExcludeBits)
        {
            if (entitiesCount == 0) return false;
            /*
            

                OPTIMISATIONs to consider

                4. when this is working - modify UnsafeBitSetUtils to also return int firstSetIndex and int laseSetIndex

                5. create a custom (copy+paste from existing) BitSet enumerator with 2 extra values to skip first N and don't skip when index is more, than M.
                6. And then, UnsafeBitSetUtils.And should also get these firstSet and laseSet to improve iteration speed

            */
            
            // 1. Pre compute storages
            UnsafeList.Clear(_requiredStoragesCache);
            UnsafeList.Clear(_forbiddenStoragesCache);

            // TODO: change to iterate over sorted?
            for (short storageIndex = 0; storageIndex < RUNTIME_REFERENCES.ComponentsCount; storageIndex++)
            {
                if (BitSet.IsSet(filterIncludeBits, storageIndex))
                {
                    BitSet* bitSet = (*(_storagesToEntitiesBitSetRaw + storageIndex)).GetInternal();
                    // Should be included but does not even exist or empty
                    if (bitSet == null || BitSet.AnySet(bitSet) == false) return false;

                    UnsafeList.Add<short>(_requiredStoragesCache, storageIndex);
                }
                else if (BitSet.IsSet(filterExcludeBits, storageIndex)) UnsafeList.Add<short>(_forbiddenStoragesCache, storageIndex);
            }

            if (UnsafeList.GetCount(_requiredStoragesCache) > 0)
            {
                // Get all matched entities from first component bitset
                int storageIndex = UnsafeList.Get<short>(_requiredStoragesCache, 0);
                BitSet* bitSet = (*(_storagesToEntitiesBitSetRaw + storageIndex)).GetInternal();
                bool anySet = BitSet.CopyFrom(_matchedEntitiesCache, bitSet);

                if (anySet == false) return false;
            }
            else
            {
                BitSet.SetAll(_matchedEntitiesCache, true, entitiesCount);
            }

            // Check required storages
            for (int r = 1; r < UnsafeList.GetCount(_requiredStoragesCache); r++)
            {
                int storageIndex = UnsafeList.Get<short>(_requiredStoragesCache, r);
                BitSet* bitSet = (*(_storagesToEntitiesBitSetRaw + storageIndex)).GetInternal();
                bool anySet = BitSet.And(_matchedEntitiesCache, bitSet);
                if (anySet == false) return false;
            }

            // Check forbidden storages
            for (int f = 0; f < UnsafeList.GetCount(_forbiddenStoragesCache); f++)
            {
                int storageIndex = UnsafeList.Get<short>(_forbiddenStoragesCache, f);
                BitSet* bitSet = (*(_storagesToEntitiesBitSetRaw + storageIndex)).GetInternal();
                if (bitSet != null)
                {
                    bool anySet = BitSet.AndNot(_matchedEntitiesCache, bitSet);
                    if (anySet == false) return false;
                }
            }

            if (BitSet.GetFirstClearBit(_matchedEntitiesCache, out int index) && index > entitiesCount)
            {
                return true;
            }

            BitSet.CopyAnySize(matchedEntities, _matchedEntitiesCache);

            return false;
        }

        [Inline(256)]
        public bool Has<TComponent>(short entityIndex)
            where TComponent : struct, IEcsComponentBase
        {
            BAssert.IndexInRange(entityIndex, _capacity);
            
            short index = EcsComponentDescriptor<TComponent>.TypeIndex;
            BitSet* bitSet = (*(_storagesToEntitiesBitSetRaw + index)).GetInternal();
            if (bitSet == null) return false;
            return BitSet.IsSet(bitSet, entityIndex);
        }

        [Inline(256)]
        public ref TComponent Get<TComponent>(short entityIndex)
            where TComponent : struct, IEcsComponent
        {
            BAssert.IndexInRange(entityIndex, _capacity);
            
            short index = EcsComponentDescriptor<TComponent>.TypeIndex;
            BitSet* bitSet = (*(_storagesToEntitiesBitSetRaw + index)).GetInternal();
            if (bitSet == null || BitSet.IsSet(bitSet, entityIndex) == false)
            {
                BExceptionThrower.EntityHasNoComponent("Entity has no component");
            }

            bool storageExists = _storages[index] != null;
            if (storageExists == false)
            {
                _storages[index] = new ManagedDynamicComponentStorage<TComponent>(_capacity);
                *(_sizesRaw + index) = 0;
            }

            if (_storages[index] is not ManagedDynamicComponentStorage<TComponent> storage)
            {
                throw new System.InvalidCastException("Invalid storage type for component. Expected ManagedDynamicComponentStorage");
            }

            return ref storage.GetRef(entityIndex);
        }

        [Inline(256)]
        public ref TComponent GetShared<TComponent>(short entityIndex)
            where TComponent : struct, IEcsSharedComponent
        {
            BAssert.IndexInRange(entityIndex, _capacity);
            
            short index = EcsComponentDescriptor<TComponent>.TypeIndex;
            BitSet* bitSet = (*(_storagesToEntitiesBitSetRaw + index)).GetInternal();
            if (bitSet == null || BitSet.IsSet(bitSet, entityIndex) == false)
            {
                BExceptionThrower.EntityHasNoComponent("Entity has no component");
            }

            bool storageExists = _storages[index] != null;
            if (storageExists == false)
            {
                _storages[index] = new SharedDynamicComponentStorage<TComponent>(_world);
                *(_sizesRaw + index) = 0;
            }

            if (_storages[index] is not SharedDynamicComponentStorage<TComponent> storage)
            {
                throw new System.InvalidCastException("Invalid storage type for component. Expected ManagedDynamicComponentStorage");
            }

            return ref storage.GetRef(entityIndex);
        }

        [Inline(256)]
        public void AddRaw(int componentIndex, short entityIndex)
        {
            BAssert.IndexInRange(entityIndex, _capacity);

            BitSet* bitSet = (*(_storagesToEntitiesBitSetRaw + componentIndex)).GetInternal();

            bool storageExists = bitSet != null;
            if (storageExists == false)
            {
                bitSet = BitSet.Allocate(_capacity);
                *(_storagesToEntitiesBitSetRaw + componentIndex) = new BitSetPtrWrapper(bitSet);
                *(_sizesRaw + componentIndex) = 0;
            }

            BitSet.Set(bitSet, entityIndex);
            *(_sizesRaw + componentIndex) = (short)(*(_sizesRaw + componentIndex) + 1);
        }

        [Inline(256)]
        public void Add<TComponent>(short entityIndex)
            where TComponent : struct, IEcsComponent
        {
            BAssert.IndexInRange(entityIndex, _capacity);

            short index = EcsComponentDescriptor<TComponent>.TypeIndex;
            BitSet* bitSet = (*(_storagesToEntitiesBitSetRaw + index)).GetInternal();
            if (bitSet != null && BitSet.IsSet(bitSet, entityIndex))
            {
                BExceptionThrower.ComponentAlreadyAttached("Entity already has component");
            }

            bool storageExists = bitSet != null;
            if (storageExists == false)
            {
                bitSet = BitSet.Allocate(_capacity);
                *(_storagesToEntitiesBitSetRaw + index) = new BitSetPtrWrapper(bitSet);
                _storages[index] = new ManagedDynamicComponentStorage<TComponent>(_capacity);
                *(_sizesRaw + index) = 0;
            }

            if (_storages[index] is not ManagedDynamicComponentStorage<TComponent> storage)
            {
                throw new System.InvalidCastException("Invalid storage type for component. Expected ManagedDynamicComponentStorage");
            }

            BitSet.Set(bitSet, entityIndex);
            *(_sizesRaw + index) = (short)(*(_sizesRaw + index) + 1);
        }

        [Inline(256)]
        public void AddTag<TComponent>(short entityIndex)
            where TComponent : struct, IEcsTagComponent
        {
            BAssert.IndexInRange(entityIndex, _capacity);

            short index = EcsComponentDescriptor<TComponent>.TypeIndex;
            BitSet* bitSet = (*(_storagesToEntitiesBitSetRaw + index)).GetInternal();
            if (bitSet != null && BitSet.IsSet(bitSet, entityIndex))
            {
                BExceptionThrower.ComponentAlreadyAttached("Entity already has component");
            }

            bool storageExists = bitSet != null;
            if (storageExists == false)
            {
                bitSet = BitSet.Allocate(_capacity);
                *(_storagesToEntitiesBitSetRaw + index) = new BitSetPtrWrapper(bitSet);
                *(_sizesRaw + index) = 0;
            }

            BitSet.Set(bitSet, entityIndex);
            *(_sizesRaw + index) = (short)(*(_sizesRaw + index) + 1);
        }

        [Inline(256)]
        public void AddShared<TComponent>(short entityIndex)
            where TComponent : struct, IEcsSharedComponent
        {
            BAssert.IndexInRange(entityIndex, _capacity);
            
            short index = EcsComponentDescriptor<TComponent>.TypeIndex;
            BitSet* bitSet = (*(_storagesToEntitiesBitSetRaw + index)).GetInternal();
            if (bitSet != null && BitSet.IsSet(bitSet, entityIndex))
            {
                BExceptionThrower.ComponentAlreadyAttached("Entity already has component");
            }

            bool storageExists = bitSet != null;
            if (storageExists == false)
            {
                bitSet = BitSet.Allocate(_capacity);
                *(_storagesToEntitiesBitSetRaw + index) = new BitSetPtrWrapper(bitSet);
                _storages[index] = new SharedDynamicComponentStorage<TComponent>(_world);
                *(_sizesRaw + index) = 0;
            }

            if (_storages[index] is not SharedDynamicComponentStorage<TComponent> storage)
            {
                throw new System.InvalidCastException("Invalid storage type for component. Expected SharedDynamicComponentStorage");
            }
            
            BitSet.Set(bitSet, entityIndex);
            *(_sizesRaw + index) = (short)(*(_sizesRaw + index) + 1);
        }

        [Inline(256)]
        public void Add<TComponent>(TComponent component, short entityIndex)
            where TComponent : struct, IEcsComponent
        {
            Add<TComponent>(entityIndex);
            Get<TComponent>(entityIndex) = component;
        }

        [Inline(256)]
        public void AddShared<TComponent>(TComponent component, short entityIndex)
            where TComponent : struct, IEcsSharedComponent
        {
            AddShared<TComponent>(entityIndex);
            GetShared<TComponent>(entityIndex) = component;
        }

        [Inline(256)]
        public void Del<TComponent>(short entityIndex)
            where TComponent : struct, IEcsComponentBase
        {
            BAssert.IndexInRange(entityIndex, _capacity);
            
            short index = EcsComponentDescriptor<TComponent>.TypeIndex;
            BitSet* bitSet = (*(_storagesToEntitiesBitSetRaw + index)).GetInternal();
            if (bitSet == null || BitSet.IsSet(bitSet, entityIndex) == false)
            {
                BExceptionThrower.EntityHasNoComponent("Entity has no component");
            }

            BitSet.Clear(bitSet, entityIndex);
            *(_sizesRaw + index) = (short)(*(_sizesRaw + index) - 1);
        }

        [Inline(256)]
        public void DelAll(short entityIndex)
        {
            for (int componentIndex = 0; componentIndex < RUNTIME_REFERENCES.ComponentsCount; componentIndex++)
            {
                BitSet* bitSet = (*(_storagesToEntitiesBitSetRaw + componentIndex)).GetInternal();
                if (bitSet == null) continue;
                BitSet.Clear(bitSet, entityIndex);
            }
        }

        [Inline(256)]
        public void CopyTo(short oldEntityIndex, short newEntityIndex, DynamicComponentsStorage other)
        {
            for (int componentIndex = 0; componentIndex < RUNTIME_REFERENCES.ComponentsCount; componentIndex++)
            {
                // Get bit sets
                BitSet* bitSet = (*(_storagesToEntitiesBitSetRaw + componentIndex)).GetInternal();
                if (bitSet == null || BitSet.IsSet(bitSet, oldEntityIndex) == false) continue;
                
                BitSet* otherBitSet = (*(other._storagesToEntitiesBitSetRaw + componentIndex)).GetInternal();
                if (otherBitSet == null)
                {
                    otherBitSet = BitSet.Allocate(other._capacity);
                    *(other._storagesToEntitiesBitSetRaw + componentIndex) = new BitSetPtrWrapper(otherBitSet);
                }

                // Update masks
                BitSet.Clear(bitSet, oldEntityIndex);
                BitSet.Set(otherBitSet, newEntityIndex);

                // Update sizes
                *(_sizesRaw + componentIndex) = (short)(*(_sizesRaw + componentIndex) - 1);
                *(other._sizesRaw + componentIndex) = (short)(*(other._sizesRaw + componentIndex) + 1);

                // Update components data
                var oldStorage = _storages[componentIndex];
                if (oldStorage == null) continue;

                var newStorage = other._storages[componentIndex];
                if (newStorage == null)
                {
                    newStorage = other._storages[componentIndex] = oldStorage.CreateNew();
                }

                newStorage.CopyFrom(oldEntityIndex, newEntityIndex, oldStorage);
            }
        }

        [Inline(256)]
        public void CombineWith(short index, DynamicComponentsStorage other, short count)
        {
            // TODO: I added support for multiple entries copy in DynamicComponentStorageBase, but I don't want to optimize it right now.
            for (short i = 0; i < count; i++)
            {
                other.CopyTo(i, (short)(index + i), this);
            }
        }

        [Inline(256)]
        public void Dispose()
        {
            if (_storagesToEntitiesBitSet != null)
            {
                int length = UnsafeArray.GetLength(_storagesToEntitiesBitSet);
                for (int i = 0; i < length; i++)
                {
                    (*(_storagesToEntitiesBitSetRaw + i)).Dispose();
                }
                UnsafeArray.Free(_storagesToEntitiesBitSet);
                _storagesToEntitiesBitSet = null;
                _storagesToEntitiesBitSetRaw = null;
            }
            if (_sizes != null)
            {
                UnsafeArray.Free(_sizes);
                _sizes = null;
                _sizesRaw = null;
            }
        }
    }
}
