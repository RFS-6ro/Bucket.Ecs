using System;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleToAttribute("Bv3.Tests")]
[assembly: InternalsVisibleToAttribute("IntegrationTests")]
[assembly: InternalsVisibleToAttribute("Benchmarks")]
namespace Bucket.Ecs.v3
{
#if UNITY
    using AllowUnsafePtr = Unity.Collections.LowLevel.Unsafe.NativeDisableUnsafePtrRestrictionAttribute;
    using WriteAccess = Unity.Collections.LowLevel.Unsafe.WriteAccessRequiredAttribute;
    // using Il2CppSetOption = ;
#else
    using AllowUnsafePtr = UnityAttribute;
    using WriteAccess = UnityAttribute;
    using Il2CppSetOption = UnityAttribute;
#endif
    using Inline = System.Runtime.CompilerServices.MethodImplAttribute;

    // TODO: add attributes
    // [Il2CppSetOption(Option.NullChecks, false)]
    // [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    // [Il2CppSetOption(Option.DivideByZeroChecks, false)]
    public sealed partial class EcsWorld : IDisposable
    {
        public static void Reset()
        {
            ManagedComponentIndexRegistry.Reset();
            UnmanagedComponentIndexRegistry.Reset();
            MultiThreadSystemsIndexRegistry.Reset();
            MultiThreadSystemContextIndexRegistry.Reset();
        }

        private readonly IEntityIdFactory _entityIdFactory;
        private readonly BucketAllocator _allocator;
        private readonly UnsafePoolAllocator _chunksDataAllocator;
        
        public EcsWorld(IEntityIdFactory entityIdFactory = null)
        {
            Reset();

            _entityIdFactory = entityIdFactory ?? new EntityIdFactory();
            _allocator = new BucketAllocator();
            _chunksDataAllocator = new UnsafePoolAllocator();
            InitWorldInternal();
        }

        [Inline(256)]
        private void InitWorldInternal()
        {
            RegisterPredefinedCommands();
            InitFilters();
            InitComponents();
            InitArchetypes();
            InitMigrationSupport();
            InitSystems();
        }

        [Inline(256)]
        public void OnUpdate()
        {
            _allocator.OnUpdate();
        }

        [Inline(256)]
        public void Dispose()
        {
            ReleaseManagedResources();
            ReleaseUnmanagedResources();
            GC.SuppressFinalize(this);
        }

        [Inline(256)]
        private void ReleaseManagedResources()
        {
            // TODO: release managed resources here
            ReleaseComponents();
        }

        [Inline(256)]
        private void ReleaseUnmanagedResources()
        {
            // TODO: release unmanaged resources here
            _chunksDataAllocator.Dispose();
            _allocator.Dispose();
            ReleaseFilters();
            ReleaseArchetypes();
            ReleaseMigrationSupport();
            ReleaseSystems();
        }

        [Inline(256)]
        ~EcsWorld()
        {
            ReleaseUnmanagedResources();
            BExceptionThrower.ObjectNotDisposed("EcsWorld was not disposed");
        }
    }
}
