using System;
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

    internal unsafe struct EntityMigrationData : IDisposable
    {
        [AllowUnsafePtr] private UnsafeList* _storage;

        [AllowUnsafePtr] private BitSet* _addedComponents;
        [AllowUnsafePtr] private BitSet* _removedComponents;

        private BSpinLock _lock;

        public bool IsCreated { [Inline(256)] get => _storage != null; }

        public void Create()
        {
            _lock.Lock();
            try
            {
                if (_lock.isCreated) return;

                _storage = UnsafeList.Allocate<byte>(128); // TODO: default entity migration storage capacity from config
                _addedComponents = BitSet.Allocate(RUNTIME_REFERENCES.ComponentsCount);
                _removedComponents = BitSet.Allocate(RUNTIME_REFERENCES.ComponentsCount);

                _lock = new BSpinLock() { isCreated = true };
            }
            finally
            {
                _lock.Unlock();
            }
        }

        [WriteAccess]
        [Inline(256)]
        public void Add<TComponent>()
            where TComponent : unmanaged, IEcsUnmanagedComponent
        {
            _lock.Lock();
            try
            {
                short componentIndex = EcsUnmanagedComponentDescriptor<TComponent>.TypeIndex;
                AddNoLock(componentIndex);
            }
            finally
            {
                _lock.Unlock();
            }
        }

        [WriteAccess]
        [Inline(256)]
        public void Add<TComponent>(TComponent component)
            where TComponent : unmanaged, IEcsUnmanagedComponent
        {
            _lock.Lock();
            try
            {
                short componentIndex = EcsUnmanagedComponentDescriptor<TComponent>.TypeIndex;
                AddNoLock(componentIndex);

                // Add component index
                int memoryOffset = UnsafeList.GetCount(_storage);
                for (int i = 0; i < sizeof(short); i++)
                {
                    UnsafeList.Add<byte>(_storage, 0);
                }
                byte* componentIndexPtr = UnsafeList.GetPtr<byte>(_storage, memoryOffset);
                *(short*)componentIndexPtr = componentIndex;

                // Reserve memory for component
                memoryOffset = UnsafeList.GetCount(_storage);
                for (int i = 0; i < sizeof(TComponent); i++)
                {
                    UnsafeList.Add<byte>(_storage, 0);
                }
                byte* componentPtr = UnsafeList.GetPtr<byte>(_storage, memoryOffset);
                *(TComponent*)componentPtr = component;
            }
            catch (Exception e)
            {
                BLogger.Error(e.Message);
            }
            finally
            {
                _lock.Unlock();
            }
        }

        [WriteAccess]
        [Inline(256)]
        private void AddNoLock(short componentIndex)
        {
            BitSet.Set(_addedComponents, componentIndex);
            BitSet.Clear(_removedComponents, componentIndex);
        }

        [WriteAccess]
        [Inline(256)]
        public void Del<TComponent>()
            where TComponent : unmanaged, IEcsUnmanagedComponent
        {
            _lock.Lock();
            try
            {
                short componentIndex = EcsUnmanagedComponentDescriptor<TComponent>.TypeIndex;
                BitSet.Clear(_addedComponents, componentIndex);
                BitSet.Set(_removedComponents, componentIndex);
            }
            finally
            {
                _lock.Unlock();
            }
        }

        [WriteAccess]
        [Inline(256)]
        public void ApplyComponentMask(BitSet* set)
        {
            BitSet.Or(set, _addedComponents);
            BitSet.AndNot(set, _removedComponents);
        }

        [WriteAccess]
        [Inline(256)]
        public void ApplyComponentsValues(short entityIndex, ref UnmanagedComponentsStorage storage)
        {
            int componentsMemoryLength = UnsafeList.GetCount(_storage);
            for (int memoryOffset = 0; memoryOffset < componentsMemoryLength; )
            {
                byte* componentIndexPtr = UnsafeList.GetPtr<byte>(_storage, memoryOffset);
                short componentIndex = *(short*)componentIndexPtr;
                memoryOffset += sizeof(short);

                int componentSize = UnmanagedComponentIndexRegistry.Sizeof(componentIndex);
                byte* componentPtr = UnsafeList.GetPtr<byte>(_storage, memoryOffset);
                storage.WriteRaw(entityIndex, componentIndex, componentPtr, componentSize);
                memoryOffset += componentSize;
            }
        }

        [WriteAccess]
        [Inline(256)]
        public void Dispose()
        {
            if (_storage != null)
            {
                UnsafeList.Free(_storage);
                _storage = null;
            }
            if (_addedComponents != null)
            {
                BitSet.Free(_addedComponents);
                _addedComponents = null;
            }
            if (_removedComponents != null)
            {
                BitSet.Free(_removedComponents);
                _removedComponents = null;
            }
        }
    }
}
