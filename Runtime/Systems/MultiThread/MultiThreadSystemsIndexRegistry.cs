using System.Collections.Generic;
using System.Threading;
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

    public unsafe static class MultiThreadSystemsIndexRegistry
    {
        public interface IResettableDescriptor
        {
            void ResetLocal();
        }

        private static object _lock = new();
        
        private static UnsafeArray* _sizes = null;
        private static int _systemIndex;

        private static List<IResettableDescriptor> _descriptors;

        public static int ComponentsCount => _systemIndex;

        static MultiThreadSystemsIndexRegistry()
        {
            Reset();
        }

        [Inline(256)]
        public static short GetSystemIndex<TSystem>(IResettableDescriptor descriptor)
        where TSystem : unmanaged, IChunkSystem
        {
            short index = (short)Interlocked.Increment(ref _systemIndex);
            if (RUNTIME_REFERENCES.MultiThreadSystemsCount <= index)
            {
                BExceptionThrower.OutOfRange($"Trying to add more systems, than supported ({RUNTIME_REFERENCES.MultiThreadSystemsCount})");
            }

            lock (_lock)
            {
                _descriptors.Add(descriptor);
            }
            
            UnsafeArray.Set(_sizes, index, sizeof(TSystem));
            return index;
        }

        [Inline(256)]
        public static int Sizeof(short contextIndex)
        {
            return UnsafeArray.Get<int>(_sizes, contextIndex);
        }

        [Inline(256)]
        public static void Reset()
        {
            _systemIndex = -1;
            _lock = new();

            if (_sizes != null)
            {
                UnsafeArray.Free(_sizes);
            }

            if (_descriptors != null)
            {
                foreach (var descriptor in _descriptors)
                {
                    descriptor.ResetLocal();
                }
            }

            _sizes = UnsafeArray.Allocate<int>(RUNTIME_REFERENCES.MultiThreadSystemsCount);

            _descriptors = new List<IResettableDescriptor>();
        }
    }
}
