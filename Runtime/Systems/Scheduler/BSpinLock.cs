namespace Bucket.Ecs.v3
{
#if UNITY
    using AllowUnsafePtr = Unity.Collections.LowLevel.Unsafe.NativeDisableUnsafePtrRestrictionAttribute;
    using WriteAccess = Unity.Collections.LowLevel.Unsafe.WriteAccessRequiredAttribute;
#else
#endif
    using Inline = System.Runtime.CompilerServices.MethodImplAttribute;
    using System.Threading;

    public unsafe struct BSpinLock
    {
        private int _state; // 0 = free, 1 = locked

        public bool isCreated;

        [Inline(256)]
        public void Lock()
        {
            if (isCreated == false) return;
            
            for(;;)
            {
                // Optimistically assume the lock is free on the first try.
                if (Interlocked.CompareExchange(ref _state, 1, 0) == 0) return;

                // Wait for lock to be released without generate cache misses.
                while (Volatile.Read(ref _state) == 1) { }
            }
        }

        [Inline(256)]
        public void Unlock()
        {
            Volatile.Write(ref _state, 0);
        }
    }
}
