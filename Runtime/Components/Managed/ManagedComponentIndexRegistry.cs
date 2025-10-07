using System.Collections.Generic;
using System.Threading;

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
    
    public static class ManagedComponentIndexRegistry
    {
        public interface IResettableDescriptor
        {
            void ResetLocal();
        }

        private static object _lock = new();
        
        private static int _componentIndex;

        private static List<IResettableDescriptor> _descriptors;

        public static int ComponentsCount => _componentIndex;

        static ManagedComponentIndexRegistry()
        {
            Reset();
        }

        [Inline(256)]
        public static int GetComponentIndex<TComponent>(IResettableDescriptor descriptor)
            where TComponent : struct, IEcsComponentBase
        {
            int index = Interlocked.Increment(ref _componentIndex);
            if (RUNTIME_REFERENCES.ComponentsCount <= index)
            {
                BExceptionThrower.OutOfRange($"Trying to add more components, than supported ({RUNTIME_REFERENCES.ComponentsCount})");
            }

            lock (_lock)
            {
                _descriptors.Add(descriptor);
            }
            
            return index;
        }

        [Inline(256)]
        public static void Reset()
        {
            _componentIndex = 0;
            _lock = new();

            if (_descriptors != null)
            {
                foreach (var descriptor in _descriptors)
                {
                    descriptor.ResetLocal();
                }
            }

            _descriptors = new List<IResettableDescriptor>();
        }
    }
}
