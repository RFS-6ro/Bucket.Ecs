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

    public static class MainThreadSystemsIndexRegistry
    {
        private static int _systemIndex;

        public static int SystemsCount => _systemIndex;

        [Inline(256)]
        public static int GetSystemIndex<T>() 
            where T : SystemBase
        {
            return Interlocked.Increment(ref _systemIndex);
        }
    }
}
