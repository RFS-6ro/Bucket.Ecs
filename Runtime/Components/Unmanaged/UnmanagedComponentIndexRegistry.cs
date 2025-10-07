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
    
    public unsafe static class UnmanagedComponentIndexRegistry
    {
        public interface IResettableDescriptor
        {
            void ResetLocal();
        }

        private static object _lock = new();
        
        private static UnsafeArray* _sizes = null;
        private static int _componentIndex;

        private static List<IResettableDescriptor> _descriptors;

        public static int ComponentsCount => _componentIndex;

        static UnmanagedComponentIndexRegistry()
        {
            Reset();
        }

        [Inline(256)]
        public static short GetComponentIndex<TComponent>(IResettableDescriptor descriptor)
            where TComponent : unmanaged, IEcsUnmanagedComponent
        {
            short index = (short)Interlocked.Increment(ref _componentIndex);
            if (RUNTIME_REFERENCES.UnmanagedComponentsCount <= index)
            {
                BExceptionThrower.OutOfRange($"Trying to add more components, than supported ({RUNTIME_REFERENCES.UnmanagedComponentsCount})");
            }

            lock (_lock)
            {
                _descriptors.Add(descriptor);
            }
            
            UnsafeArray.Set(_sizes, index, sizeof(TComponent));
            return index;
        }

        [Inline(256)]
        public static int Sizeof(short componentIndex)
        {
            return UnsafeArray.Get<int>(_sizes, componentIndex);
        }

        [Inline(256)]
        public static void Reset()
        {
            _componentIndex = -1;
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

            _sizes = UnsafeArray.Allocate<int>(RUNTIME_REFERENCES.UnmanagedComponentsCount);

            _descriptors = new List<IResettableDescriptor>();
        }
    }
}
