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

    public unsafe sealed partial class EcsWorld
    {
        private UnsafeList* _systemContexts;
        private UnsafeList* _systemContextsIndexes;
        private short _count;

        [Inline(256)]
        private unsafe void InitSystems()
        {
            _systemContexts = UnsafeList.Allocate<byte>(16 * RUNTIME_REFERENCES.MultiThreadSystemsCount); // TODO: size from config
            _systemContextsIndexes = UnsafeList.Allocate<int>(RUNTIME_REFERENCES.MultiThreadSystemsCount); 
        }

        [Inline(256)]
        public void SetSystemContext<TContext>(TContext context)
            where TContext : unmanaged, IMultiThreadSystemContext
        {
            short contextTypeIndex = EcsMultiThreadSystemContextDescriptor<TContext>.TypeIndex;
            int indexInMemory = GetOrAddSystemContext(contextTypeIndex);
            byte* componentPtr = UnsafeList.GetPtr<byte>(_systemContexts, indexInMemory);
            *(TContext*)componentPtr = context;
        }

        [Inline(256)]
        public ref TContext GetSystemContext<TContext>()
            where TContext : unmanaged, IMultiThreadSystemContext
        {
            short contextTypeIndex = EcsMultiThreadSystemContextDescriptor<TContext>.TypeIndex;
            int indexInMemory = GetOrAddSystemContext(contextTypeIndex);
            return ref *(TContext*)UnsafeList.GetPtr<byte>(_systemContexts, indexInMemory);
        }

        [Inline(256)]
        public byte* GetSystemContext(short contextTypeIndex)
        {
            int indexInMemory = GetOrAddSystemContext(contextTypeIndex);
            return UnsafeList.GetPtr<byte>(_systemContexts, indexInMemory);
        }

        private int GetOrAddSystemContext(short contextTypeIndex)
        {
            if (_count <= contextTypeIndex)
            {
                for (int i = _count; i <= contextTypeIndex; i++)
                {
                    UnsafeList.Add(_systemContextsIndexes, -1);
                }

                _count = contextTypeIndex;
            }

            ref int indexInMemory = ref UnsafeList.GetRef<int>(_systemContextsIndexes, contextTypeIndex);
            if (indexInMemory == -1)
            {
                indexInMemory = UnsafeList.GetCount(_systemContexts);
                // UnsafeList.Set<int>(_systemContextsIndexes, contextTypeIndex, indexInMemory);
            }

            int size = MultiThreadSystemContextIndexRegistry.Sizeof(contextTypeIndex);
            for (int i = 0; i < size; i++)
            {
                UnsafeList.Add<byte>(_systemContexts, 0);
            }
            
            return indexInMemory;
        }
        
        [Inline(256)]
        private unsafe void ReleaseSystems()
        {
            if (_systemContexts != null)
            {
                UnsafeList.Free(_systemContexts);
                _systemContexts = null;
            }
            if (_systemContextsIndexes != null)
            {
                UnsafeList.Free(_systemContextsIndexes);
                _systemContextsIndexes = null;
            }
        }
    }
}