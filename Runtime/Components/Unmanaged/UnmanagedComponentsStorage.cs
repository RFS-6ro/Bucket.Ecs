using System;
using UnsafeCollections;
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

    public unsafe struct UnmanagedComponentsStorage : IDisposable
    {
        [AllowUnsafePtr] private UnsafeArray* _storage;
        [AllowUnsafePtr] private byte* _storageRaw;
        [AllowUnsafePtr] private int* _indexToOffsetMapRaw;
        private readonly int _size;
        private int _componentsSummarizedSize;

        public bool IsCreated { [Inline(256)] get => _storage != null; }
        
        public UnmanagedComponentsStorage(ArchetypeId id, ChunkIndex chunkIndex, UnsafePoolAllocator chunksDataAllocator, int size, int componentsSummarizedSize, UnsafeArray* indexToOffsetMap)
        {
            _size = size;
            _componentsSummarizedSize = componentsSummarizedSize;
            _indexToOffsetMapRaw = UnsafeArray.GetPtr<int>(indexToOffsetMap, 0);
            if (_componentsSummarizedSize != 0)
            {
                _storage = chunksDataAllocator.GetByteArray(id, chunkIndex, size * componentsSummarizedSize).GetInternal();
                _storageRaw = UnsafeArray.GetPtr<byte>(_storage, 0);
            }
            else
            {
                _storage = null;
                _storageRaw = null;
            }
        }

        [Inline(256)]
        public bool Has<T>(short entityIndex, short componentIndex)
            where T : unmanaged, IEcsUnmanagedComponent
        {
            BAssert.IndexInRange(entityIndex, _size);
            BAssert.IsNotEmpty(_componentsSummarizedSize);
            BAssert.IndexInRange(componentIndex, RUNTIME_REFERENCES.UnmanagedComponentsCount);

            int componentOffset = *(_indexToOffsetMapRaw + componentIndex);

            return componentOffset >= 0;
        }

        [WriteAccess]
        [Inline(256)]
        public void WriteRaw(short entityIndex, short componentIndex, byte* componentValueRaw, int componentSize)
        {
            BAssert.IndexInRange(entityIndex, _size);
            BAssert.IsNotEmpty(_componentsSummarizedSize);
            BAssert.IndexInRange(componentIndex, RUNTIME_REFERENCES.UnmanagedComponentsCount);

            int componentOffset = *(_indexToOffsetMapRaw + componentIndex);
            BAssert.True(componentOffset >= 0);

            int offset = entityIndex * _componentsSummarizedSize + componentOffset;
            byte* storageRaw = _storageRaw + offset;//UnsafeArray.GetPtr<byte>(_storage, offset);

            Memory.ArrayCopy<byte>(componentValueRaw, 0, storageRaw, 0, componentSize);
        }

        [WriteAccess]
        [Inline(256)]
        public void Write<T>(short entityIndex, short componentIndex, in T value)
            where T : unmanaged, IEcsUnmanagedComponent
        {
            BAssert.IndexInRange(entityIndex, _size);
            BAssert.IsNotEmpty(_componentsSummarizedSize);
            BAssert.IndexInRange(componentIndex, RUNTIME_REFERENCES.UnmanagedComponentsCount);

            int componentOffset = *(_indexToOffsetMapRaw + componentIndex);
            BAssert.True(componentOffset >= 0);

            int offset = entityIndex * _componentsSummarizedSize + componentOffset;
            byte* storageRaw = _storageRaw + offset;//UnsafeArray.GetPtr<byte>(_storage, offset);
            *(T*)storageRaw = value;
        }

        [Inline(256)]
        public T Read<T>(short entityIndex, short componentIndex)
            where T : unmanaged, IEcsUnmanagedComponent
        {
            BAssert.IndexInRange(entityIndex, _size);
            BAssert.IsNotEmpty(_componentsSummarizedSize);
            BAssert.IndexInRange(componentIndex, RUNTIME_REFERENCES.UnmanagedComponentsCount);

            int componentOffset = *(_indexToOffsetMapRaw + componentIndex);
            BAssert.True(componentOffset >= 0);

            int offset = entityIndex * _componentsSummarizedSize + componentOffset;
            byte* storageRaw = _storageRaw + offset;//UnsafeArray.GetPtr<byte>(_storage, offset);
            return *(T*)storageRaw;
        }

        [WriteAccess]
        [Inline(256)]
        public ref T ReadRef<T>(short entityIndex, short componentIndex)
            where T : unmanaged, IEcsUnmanagedComponent
        {
            BAssert.IndexInRange(entityIndex, _size);
            BAssert.IsNotEmpty(_componentsSummarizedSize);
            BAssert.IndexInRange(componentIndex, RUNTIME_REFERENCES.UnmanagedComponentsCount);

            int componentOffset = *(_indexToOffsetMapRaw + componentIndex);
            BAssert.True(componentOffset >= 0);

            int offset = entityIndex * _componentsSummarizedSize + componentOffset;
            byte* storageRaw = _storageRaw + offset;//UnsafeArray.GetPtr<byte>(_storage, offset);
            return ref *(T*)storageRaw;
        }

        [Inline(256)]
        public void MigrateAllComponents(short sourceIndex, ref UnmanagedComponentsStorage otherStorage, short destinationIndex)
        {
            BAssert.IndexInRange(sourceIndex, _size);
            BAssert.IsNotEmpty(_componentsSummarizedSize);

            BAssert.IndexInRange(destinationIndex, otherStorage._size);
            BAssert.IsNotEmpty(otherStorage._componentsSummarizedSize);

            for (short componentIndex = 0; componentIndex < RUNTIME_REFERENCES.UnmanagedComponentsCount; componentIndex++)
            {
                int componentSize = UnmanagedComponentIndexRegistry.Sizeof(componentIndex);
                int sourceComponentOffset = *(_indexToOffsetMapRaw + componentIndex);
                int destinationComponentOffset = *(otherStorage._indexToOffsetMapRaw + componentIndex);

                if (sourceComponentOffset < 0 || destinationComponentOffset < 0) continue;

                int sourceOffset = sourceIndex * _componentsSummarizedSize + sourceComponentOffset;
                // byte* storageRaw = UnsafeArray.GetPtr<byte>(_storage, 0);

                int destinationOffset = destinationIndex * otherStorage._componentsSummarizedSize + destinationComponentOffset;
                // byte* otherStorageRaw = UnsafeArray.GetPtr<byte>(otherStorage._storage, 0);

                Memory.ArrayCopy<byte>(_storageRaw, sourceOffset, otherStorage._storageRaw, destinationOffset, componentSize);
            }
        }

        [Inline(256)]
        public void CombineWith(short index, UnmanagedComponentsStorage unmanagedComponentsStorage, short count)
        {
            Memory.ArrayCopy<byte>(unmanagedComponentsStorage._storageRaw, 0, _storageRaw, index, count);
        }

        [Inline(256)]
        public void Dispose()
        {
            if (IsCreated)
            {
                _storage = null;
                _storageRaw = null;
            }

            _indexToOffsetMapRaw = null;
        }
    }
}
