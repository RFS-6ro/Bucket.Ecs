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

    public class SharedDynamicComponentStorage<TComponent> : DynamicComponentStorageBase
        where TComponent : struct, IEcsSharedComponent
    {
        private readonly EcsWorld _world;

        private readonly SharedComponentReference<TComponent> _sharedComponentReference;

        public SharedDynamicComponentStorage(EcsWorld world)
        {
            _world = world;
            _sharedComponentReference = _world.GetOrCreateSharedComponent<TComponent>();
        }
        
        [Inline(256)]
        public ref TComponent GetRef(short entityIndex)
        {
            return ref _sharedComponentReference.GetRef();   
        }

        [Inline(256)]
        public override DynamicComponentStorageBase CreateNew()
        {
            return new SharedDynamicComponentStorage<TComponent>(_world);
        }
    }

    public class SharedComponentReference<TComponent>
        where TComponent : struct, IEcsSharedComponent
    {
        private TComponent _component;

        [Inline(256)]
        public ref TComponent GetRef()
        {
            return ref _component;
        }
    }
}