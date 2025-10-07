using System;

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
    
    public sealed partial class EcsSystems : IDisposable
    {
        private readonly EcsWorld _world;
        private ISystemScheduler _systemScheduler;
        
        private bool _isInitialized;
        
        public EcsSystems(EcsWorld world, ISystemScheduler systemScheduler = null, IThreadPool threadPool = null)
        {
            _world = world;
            _systemScheduler = systemScheduler ?? new SystemScheduler(world, threadPool ?? new ThreadPool());
            PreInit();
        }

        [Inline(256)]
        private void PreInit()
        {
            InitScope();
            InitSystemGroups();
        }

        [Inline(256)]
        public void Init()
        {
            PostInit();
            InitMainThreadSystems();
            _isInitialized = true;
        }

        [Inline(256)]
        private void PostInit()
        {
            DisposeScope();
        }

        [Inline(256)]
        private void ReleaseUnmanagedResources()
        {
            // TODO release unmanaged resources here
        }

        [Inline(256)]
        private void ReleaseManagedResources()
        {
            // TODO release managed resources here
            DisposeSystemGroups();
        }

        [Inline(256)]
        public void Dispose()
        {
            ReleaseManagedResources();
            ReleaseUnmanagedResources();
            GC.SuppressFinalize(this);
        }

        [Inline(256)]
        ~EcsSystems()
        {
            ReleaseUnmanagedResources();
            BExceptionThrower.ObjectNotDisposed("EcsSystems was not disposed");
        }
    }
}
